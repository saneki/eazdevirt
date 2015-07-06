using System;

namespace eazdevirt
{
	public class OriginalOpcodeUnknownException : Exception
	{
		public EazVirtualInstruction VirtualInstruction { get; private set; }

		public OriginalOpcodeUnknownException(EazVirtualInstruction instruction)
		{
			this.VirtualInstruction = instruction;
		}
	}
}
