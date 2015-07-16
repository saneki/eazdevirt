using System;

namespace eazdevirt
{
	public class OriginalOpcodeUnknownException : Exception
	{
		public VirtualOpCode VirtualInstruction { get; private set; }

		public OriginalOpcodeUnknownException(VirtualOpCode instruction)
		{
			this.VirtualInstruction = instruction;
		}
	}
}
