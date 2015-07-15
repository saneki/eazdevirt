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
		/// Whether or not the method expects to be matched against
		/// more than one virtual instruction type.
		/// </summary>
		public Boolean ExpectsMultiple = false;

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
