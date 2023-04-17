using Cosmos.IL2CPU.CIL.Utils;

namespace Cosmos.IL2CPU.CIL.Instructions
{
	[OpCode(ILOpCode.Code.Conv_Ovf_I_Un)]
	public class Conv_Ovf_I_Un: ILOp
	{
		public Conv_Ovf_I_Un(XSharp.Assembler.Assembler aAsmblr)
			:base(aAsmblr)
		{
		}

		public override void Execute(Il2cpuMethodInfo aMethod, ILOpCode aOpCode)
		{
			var xSource = aOpCode.StackPopTypes[0];
			Conv_I4.DoExecute(SizeOfType(xSource), TypeIsFloat(xSource), false, true, Assembler, aMethod, aOpCode);

		}
	}
}
