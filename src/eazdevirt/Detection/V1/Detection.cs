using System;
using dnlib.DotNet.Emit;
using eazdevirt.Reflection;

namespace eazdevirt.Detection.V1.Ext
{
	/// <summary>
	/// Extensions for detecting original instruction type (opcode).
	/// </summary>
	public static partial class Extensions
	{
		/// <summary>
		/// Identify a virtual instruction.
		/// </summary>
		/// <param name="instruction">Virtual instruction</param>
		/// <returns>Detected CIL opcode</returns>
		/// <exception cref="eazdevirt.OriginalOpcodeUnknownException">
		/// Thrown if original CIL opcode is unknown.
		/// </exception>
		public static Code Identify(this VirtualOpCode ins)
		{
			return InstructionDetectorV1.Instance.Identify(ins);
		}

		public static Boolean TryIdentify(this VirtualOpCode ins, out Code code)
		{
			return InstructionDetectorV1.Instance.TryIdentify(ins, out code);
		}

		/// <summary>
		/// Identify a virtual instruction, getting the entire attribute of the detection method.
		/// </summary>
		/// <param name="instruction">Virtual instruction</param>
		/// <returns>DetectAttribute of detection method if successful</returns>
		/// <exception cref="eazdevirt.OriginalOpcodeUnknownException">
		/// Thrown if original CIL opcode is unknown.
		/// </exception>
		public static DetectAttribute IdentifyFull(this VirtualOpCode ins)
		{
			return InstructionDetectorV1.Instance.IdentifyFull(ins);
		}

		public static Boolean TryIdentifyFull(this VirtualOpCode ins, out DetectAttribute attribute)
		{
			return InstructionDetectorV1.Instance.TryIdentifyFull(ins, out attribute);
		}
	}
}
