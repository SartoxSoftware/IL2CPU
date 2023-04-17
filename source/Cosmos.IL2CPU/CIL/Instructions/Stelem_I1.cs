using Cosmos.IL2CPU.CIL.Utils;

namespace Cosmos.IL2CPU.CIL.Instructions
{
    [OpCode( ILOpCode.Code.Stelem_I1 )]
    public class Stelem_I1 : ILOp
    {
        public Stelem_I1( XSharp.Assembler.Assembler aAsmblr )
            : base( aAsmblr )
        {
        }

        public override void Execute(Il2cpuMethodInfo aMethod, ILOpCode aOpCode )
        {
            Stelem_Ref.Assemble(Assembler, 1, aMethod, aOpCode, DebugEnabled);
        }
    }
}
