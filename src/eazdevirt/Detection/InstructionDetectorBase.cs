using System;
using dnlib.DotNet.Emit;

namespace eazdevirt.Detection
{
	public abstract class InstructionDetectorBase : IInstructionDetector
	{
		/// <summary>
		/// Identify a virtual instruction.
		/// </summary>
		/// <param name="instruction">Virtual instruction</param>
		/// <returns>Detected CIL opcode</returns>
		/// <exception cref="eazdevirt.OriginalOpcodeUnknownException">
		/// Thrown if original CIL opcode is unknown.
		/// </exception>
		public abstract Code Identify(EazVirtualInstruction instruction);

		/// <summary>
		/// Try to identify a virtual instruction.
		/// </summary>
		/// <param name="instruction">Virtual instruction</param>
		/// <param name="code">Detected CIL opcode if successful</param>
		/// <returns>true if successful, false if not</returns>
		public virtual Boolean TryIdentify(EazVirtualInstruction instruction, out Code code)
		{
			try
			{
				code = this.Identify(instruction);
				return true;
			}
			catch (OriginalOpcodeUnknownException)
			{
				code = Code.UNKNOWN2;
				return false;
			}
		}
	}
}
