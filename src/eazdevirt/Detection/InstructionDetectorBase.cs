using System;
using dnlib.DotNet.Emit;
using eazdevirt.Reflection;

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

		/// <summary>
		/// Identify a virtual instruction, getting the entire attribute of the detection method.
		/// </summary>
		/// <param name="instruction">Virtual instruction</param>
		/// <returns>DetectAttribute of detection method if successful</returns>
		/// <exception cref="eazdevirt.OriginalOpcodeUnknownException">
		/// Thrown if original CIL opcode is unknown.
		/// </exception>
		public abstract DetectAttribute IdentifyFull(EazVirtualInstruction instruction);

		/// <summary>
		/// Try to identify a virtual instruction, getting the entire attribute of the detection method.
		/// </summary>
		/// <param name="instruction">Virtual instruction</param>
		/// <param name="attribute">DetectAttribute of detection method if successful</param>
		/// <returns>true if successful, false if not</returns>
		public virtual Boolean TryIdentifyFull(EazVirtualInstruction instruction, out DetectAttribute attribute)
		{
			try
			{
				attribute = this.IdentifyFull(instruction);
				return true;
			}
			catch (OriginalOpcodeUnknownException)
			{
				attribute = null;
				return false;
			}
		}
	}
}
