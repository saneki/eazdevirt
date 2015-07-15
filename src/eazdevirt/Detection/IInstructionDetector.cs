using System;
using dnlib.DotNet.Emit;
using eazdevirt.Reflection;

namespace eazdevirt.Detection
{
	public interface IInstructionDetector
	{
		Code Identify(EazVirtualInstruction instruction);
		DetectAttribute IdentifyFull(EazVirtualInstruction instruction);
		Boolean TryIdentify(EazVirtualInstruction instruction, out Code code);
	}
}
