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
		/// Associated special opcode.
		/// </summary>
		public SpecialCode SpecialOpCode { get; private set; }

		/// <summary>
		/// Whether or not the method expects to be matched against
		/// more than one virtual instruction type.
		/// </summary>
		public Boolean ExpectsMultiple = false;

		/// <summary>
		/// Whether or not the associated opcode is a special opcode.
		/// </summary>
		public Boolean IsSpecial { get; private set; }

		/// <summary>
		/// Construct a Detect attribute with a CIL opcode.
		/// </summary>
		/// <param name="code">CIL opcode the method checks for</param>
		public DetectAttribute(Code code)
		{
			this.OpCode = code;
			this.IsSpecial = false;
		}

		/// <summary>
		/// Construct a Detect attribute with a special opcode.
		/// </summary>
		/// <param name="code">Special opcode the method checks for</param>
		public DetectAttribute(SpecialCode code)
		{
			this.SpecialOpCode = code;
			this.IsSpecial = true;
		}
	}
}
