using System;
using dnlib.DotNet.Emit;

namespace eazdevirt.Detection.V1.Ext
{
	/// <summary>
	/// Extensions for detecting original instruction type (opcode).
	/// </summary>
	public static partial class Extensions
	{
		/// <summary>
		/// Attempt to identify a virtual instruction with its original CIL opcode.
		/// </summary>
		/// <param name="ins">Virtual instruction</param>
		/// <exception cref="OriginalOpcodeUnknownException">Thrown if unable to identify original CIL opcode</exception>
		/// <remarks>What this method does could probably be better done through reflection/attributes</remarks>
		/// <returns>CIL opcode</returns>
		public static Code Identify(this EazVirtualInstruction ins)
		{
			return InstructionDetectorV1.Instance.Identify(ins);
		}

		public static Boolean TryIdentify(this EazVirtualInstruction ins, out Code code)
		{
			return InstructionDetectorV1.Instance.TryIdentify(ins, out code);
		}
	}
}
