﻿using XSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;

using Cosmos.Build.Common;
using IL2CPU.Debug.Symbols;

namespace Cosmos.IL2CPU
{
    // http://blogs.msdn.com/b/visualstudio/archive/2010/07/06/debugging-msbuild-script-with-visual-studio.aspx
    internal class CompilerEngine
    {
        private const string AssemblerLog = "XSharp.Assembler.log";

        public Action<string> OnLogMessage;
        public Action<string> OnLogError;
        public Action<string> OnLogWarning;
        public Action<Exception> OnLogException;
        protected static Action<string> mStaticLog = null;

        // HACK: only GCImplementationRefs depends on this, remove when possible
        public static TypeResolver TypeResolver { get; private set; }

        public static string KernelPkg { get; set; }
        
        private ICompilerEngineSettings mSettings;

        private AssemblyLoadContext _assemblyLoadContext;

        private Dictionary<MethodBase, int?> mBootEntries;
        private List<MemberInfo> mForceIncludes;

        protected void LogTime(string message)
        {
        }

        protected void LogMessage(string aMsg)
        {
            OnLogMessage?.Invoke(aMsg);
        }

        protected void LogWarning(string aMsg)
        {
            OnLogWarning?.Invoke(aMsg);
        }

        protected void LogError(string aMsg)
        {
            OnLogError?.Invoke(aMsg);
        }

        protected void LogException(Exception e)
        {
            OnLogException?.Invoke(e);
        }

        public CompilerEngine(ICompilerEngineSettings aSettings)
        {
            mSettings = aSettings;

            XS.AllowComments = mSettings.AllowComments;

            #region Assembly Path Checks

            if (!File.Exists(mSettings.TargetAssembly))
            {
                throw new FileNotFoundException("The target assembly path is invalid!", mSettings.TargetAssembly);
            }

            foreach (var xReference in mSettings.References)
            {
                if (!File.Exists(xReference))
                {
                    throw new FileNotFoundException("A reference assembly path is invalid!", xReference);
                }
            }

            foreach (var xPlugsReference in mSettings.PlugsReferences)
            {
                if (!File.Exists(xPlugsReference))
                {
                    throw new FileNotFoundException("A plugs reference assembly path is invalid!", xPlugsReference);
                }
            }

            #endregion

            _assemblyLoadContext = new IsolatedAssemblyLoadContext(
                mSettings.References.Concat(mSettings.PlugsReferences).Append(mSettings.TargetAssembly));

            TypeResolver = new TypeResolver(_assemblyLoadContext);

            EnsureCosmosPathsInitialization();
        }

        private bool EnsureCosmosPathsInitialization()
        {
            try
            {
                CosmosPaths.Initialize();
                return true;
            }
            catch (Exception e)
            {
                var builder = new StringBuilder();
                builder.Append("Error while initializing Cosmos paths");
                for (Exception scannedException = e; null != scannedException; scannedException = scannedException.InnerException)
                {
                    builder.Append(" | " + scannedException.Message);
                }
                LogError(builder.ToString());
                return false;
            }
        }

        public bool Execute()
        {
            try
            {
                LogMessage("Executing IL2CPU on assembly");
                LogTime("Engine execute started");

                // Gen2
                // Find the kernel's entry point. We are looking for a public class Kernel, with public static void Boot()
                MethodBase xKernelCtor = null;

                xKernelCtor = LoadAssemblies();
                if (xKernelCtor == null)
                {
                    return false;
                }

                var debugCom = mSettings.DebugCom;

                if (!mSettings.EnableDebug)
                {
                    // Default of 1 is in Cosmos.Targets. Need to change to use proj props.
                    debugCom = 0;
                }

                using (var xAsm = GetAppAssembler(debugCom))
                {
                    var xOutputFilenameWithoutExtension = Path.ChangeExtension(mSettings.OutputFilename, null);

                    using DebugInfo xDebugInfo = new(xOutputFilenameWithoutExtension + ".cdb", true, false);
                    xAsm.DebugInfo = xDebugInfo;
                    xAsm.DebugEnabled = mSettings.EnableDebug;
                    xAsm.StackCorruptionDetection = mSettings.EnableStackCorruptionDetection;
                    xAsm.StackCorruptionDetectionLevel = mSettings.StackCorruptionDetectionLevel;
                    xAsm.DebugMode = mSettings.DebugMode;
                    xAsm.TraceAssemblies = mSettings.TraceAssemblies;
                    xAsm.IgnoreDebugStubAttribute = mSettings.IgnoreDebugStubAttribute;
                    if (!mSettings.EnableDebug)
                    {
                        xAsm.ShouldOptimize = true;
                    }

                    bool VBEMultiboot = mSettings.CompileVBEMultiboot;
                    string VBEResolution = string.IsNullOrEmpty(mSettings.VBEResolution) ? "800x600x32" : mSettings.VBEResolution;

                    xAsm.Assembler.RemoveBootDebugOutput = mSettings.RemoveBootDebugOutput;
                    xAsm.Assembler.Initialize(VBEMultiboot, VBEResolution);

                    if (mSettings.DebugMode != DebugMode.IL)
                    {
                        xAsm.Assembler.EmitAsmLabels = false;
                    }

                    using (var xScanner = new ILScanner(xAsm, new TypeResolver(_assemblyLoadContext), LogException, LogWarning))
                    {
                        CompilerHelpers.DebugEvent += LogMessage;
                        if (mSettings.EnableLogging)
                        {
                            var xLogFile = xOutputFilenameWithoutExtension + ".log.html";
                            if (!xScanner.EnableLogging(xLogFile))
                            {
                                // file creation not possible
                                LogWarning("Could not create the file \"" + xLogFile + "\"! No log will be created!");
                            }
                        }

                        var plugsAssemblies = mSettings.PlugsReferences.Select(
                            r => _assemblyLoadContext.LoadFromAssemblyPath(r));

                        xScanner.QueueMethod(xKernelCtor.DeclaringType.BaseType.GetMethod("Start"));
                        xScanner.Execute(xKernelCtor, plugsAssemblies);

                        //AppAssemblerRingsCheck.Execute(xScanner, xKernelCtor.DeclaringType.Assembly);

                        using (StreamWriter xOut = new(File.Create(mSettings.OutputFilename), Encoding.ASCII, 128 * 1024))
                        {
                            //if (EmitDebugSymbols) {
                            xAsm.Assembler.FlushText(xOut);
                            xAsm.FinalizeDebugInfo();
                            //// for now: write debug info to console
                            //Console.WriteLine("Wrote {0} instructions and {1} datamembers", xAsm.Assembler.Instructions.Count, xAsm.Assembler.DataMembers.Count);
                            //var dict = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
                            //foreach (var instr in xAsm.Assembler.Instructions)
                            //{
                            //    var mn = instr.Mnemonic ?? "";
                            //    if (dict.ContainsKey(mn))
                            //    {
                            //        dict[mn] = dict[mn] + 1;
                            //    }
                            //    else
                            //    {
                            //        dict[mn] = 1;
                            //    }
                            //}
                            //foreach (var entry in dict)
                            //{
                            //    Console.WriteLine("{0}|{1}", entry.Key, entry.Value);
                            //}
                        }
                    }
                    // If you want to uncomment this line make sure to enable PERSISTANCE_PROFILING symbol in
                    // DebugInfo.cs file.
                    //LogMessage(string.Format("DebugInfo flatening {0} seconds, persistance : {1} seconds",
                    //    (int)xDebugInfo.FlateningDuration.TotalSeconds,
                    //    (int)xDebugInfo.PersistanceDuration.TotalSeconds));
                }
                LogTime("Engine execute finished");
                return true;
            }
            catch (Exception ex)
            {
                LogException(ex);
                return false;
            }
        }

        private AppAssembler GetAppAssembler(byte debugCom)
        {
            var assemblerLogFile = Path.Combine(Path.GetDirectoryName(mSettings.OutputFilename), AssemblerLog);
            Directory.CreateDirectory(Path.GetDirectoryName(assemblerLogFile));
            StreamWriter mLog = new(File.OpenWrite(assemblerLogFile));
            return new AppAssembler(new CosmosAssembler(debugCom), mLog, Path.GetDirectoryName(assemblerLogFile));
        }

        #region Gen2

        /// <summary>Load every refernced assemblies that have an associated FullPath property and seek for
        /// the kernel default constructor.</summary>
        /// <returns>The kernel default constructor or a null reference if either none or several such
        /// constructor could be found.</returns>
        private MethodBase LoadAssemblies()
        {
            // Try to load explicit path references.
            // These are the references of our boot project. We dont actually ever load the boot
            // project asm. Instead the references will contain plugs, and the kernel. We load
            // them then find the entry point in the kernel.
            //
            // Plugs and refs in this list will be loaded absolute (or as proj refs) only. Asm resolution
            // will not be tried on them, but will on ASMs they reference.

            string xKernelBaseName = "Cosmos.System.Kernel";
            LogMessage("Kernel Base: " + xKernelBaseName);

            Type xKernelType = null;

            LogMessage($"Checking target assembly: {mSettings.TargetAssembly}");

            if (!File.Exists(mSettings.TargetAssembly))
            {
                throw new FileNotFoundException("Target assembly not found!", mSettings.TargetAssembly);
            }

            var xAssembly = _assemblyLoadContext.LoadFromAssemblyPath(mSettings.TargetAssembly);

            CompilerHelpers.Debug($"Looking for kernel in {xAssembly}");

            foreach (var xType in xAssembly.ExportedTypes)
            {
                if (!xType.IsGenericTypeDefinition && !xType.IsAbstract)
                {
                    CompilerHelpers.Debug($"Checking type {xType.FullName}");

                    // We used to resolve with this:
                    //   if (xType.IsSubclassOf(typeof(Cosmos.System.Kernel))) {
                    // But this caused a single dependency on Cosmos.System which is bad.
                    // We could use an attribute, or maybe an interface would be better in this limited case. Interface
                    // will force user to implement what is needed if replacing our core. But in the end this is a "not needed" feature
                    // and would only complicate things.
                    // So for now at least, we look by name so we dont have a dependency since the method returns a MethodBase and not a Kernel instance anyway.
                    if (xType.BaseType.FullName == xKernelBaseName)
                    {
                        if (xKernelType != null)
                        {
                            LogError($"Two kernels found: {xType.FullName} and {xKernelType.FullName}");
                            return null;
                        }
                        xKernelType = xType;
                    }
                }
            }

            if (xKernelType == null)
            {
                LogError("No kernel found.");
                return null;
            }
            var xCtor = xKernelType.GetConstructor(Type.EmptyTypes);
            if (xCtor == null)
            {
                LogError("Kernel has no public parameterless constructor.");
                return null;
            }
            return xCtor;
        }

        #endregion
    }
}