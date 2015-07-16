using System;
using dnlib.DotNet.Emit;
using eazdevirt.Reflection;

namespace eazdevirt.Detection
{
	public interface IInstructionDetector
	{
		Code Identify(VirtualOpCode instruction);
		DetectAttribute IdentifyFull(VirtualOpCode instruction);
		Boolean TryIdentify(VirtualOpCode instruction, out Code code);
	}
}
