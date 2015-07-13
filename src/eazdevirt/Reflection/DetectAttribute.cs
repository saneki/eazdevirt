using System;
using dnlib.DotNet.Emit;

namespace eazdevirt.Reflection
{
	/// <summary>
	/// Attribute to identify an instruction detection method.
	/// </summary>
	public class DetectAttribute : Attribute
	{
		/// <summary>
		/// Associated opcode.
		/// </summary>
		public Code OpCode { get; private set; }

		/// <summary>
		/// Construct a Detect attribute.
		/// </summary>
		/// <param name="code">CIL opcode the method checks for</param>
		public DetectAttribute(Code code)
		{
			this.OpCode = code;
		}
	}
}
