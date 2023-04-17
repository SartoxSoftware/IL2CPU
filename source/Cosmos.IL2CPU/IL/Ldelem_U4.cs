namespace Cosmos.IL2CPU.IL
{
    [global::Cosmos.IL2CPU.OpCode( ILOpCode.Code.Ldelem_U4 )]
    public class Ldelem_U4 : ILOp
    {
        public Ldelem_U4( XSharp.Assembler.Assembler aAsmblr )
            : base( aAsmblr )
        {
        }

        public override void Execute(Il2cpuMethodInfo aMethod, ILOpCode aOpCode )
        {
            Ldelem_Ref.Assemble(Assembler, 4, false, aMethod, aOpCode, DebugEnabled);
        }
    }
}
