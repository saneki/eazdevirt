using System;
using dnlib.DotNet.Emit;

namespace eazdevirt.Detection
{
	public interface IInstructionDetector
	{
		Code Identify(EazVirtualInstruction instruction);
		Boolean TryIdentify(EazVirtualInstruction instruction, out Code code);
	}
}
