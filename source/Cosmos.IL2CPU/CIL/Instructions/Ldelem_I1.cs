using Cosmos.IL2CPU.CIL.Utils;

namespace Cosmos.IL2CPU.CIL.Instructions
{
    [OpCode( ILOpCode.Code.Ldelem_I1 )]
    public class Ldelem_I1 : ILOp
    {
        public Ldelem_I1( XSharp.Assembler.Assembler aAsmblr )
            : base( aAsmblr )
        {
        }

        public override void Execute(Il2cpuMethodInfo aMethod, ILOpCode aOpCode )
        {
            Ldelem_Ref.Assemble(Assembler, 1, true, aMethod, aOpCode, DebugEnabled);
        }
    }
}
