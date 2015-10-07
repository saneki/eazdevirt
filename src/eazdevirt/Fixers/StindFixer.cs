using System;
using System.Collections.Generic;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using eazdevirt.Util;

namespace eazdevirt.Fixers
{
	/// <summary>
	/// Fixer for stind_* CIL instructions.
	/// </summary>
	public class StindFixer : MethodFixer
	{
		private StackTypesCalculator _calc;

		/// <summary>
		/// Method instructions.
		/// </summary>
		public IList<Instruction> Instructions
		{
			get
			{
				if (this.Method.HasBody && this.Method.Body.HasInstructions)
					return this.Method.Body.Instructions;
				else
					return new List<Instruction>();
			}
		}

		/// <summary>
		/// Whether or not this method has a Stind_* instruction.
		/// </summary>
		Boolean HasStind
		{
			get
			{
				var instructions = this.Instructions;

				foreach (var instr in instructions)
				{
					if (IsStind(instr))
						return true;
				}

				return false;
			}
		}

		public StindFixer(MethodDef method)
			: base(method)
		{
			_calc = new StackTypesCalculator(method);
		}

		/// <summary>
		/// Whether or not an instruction is an Stind instruction.
		/// </summary>
		/// <param name="instr">Instruction</param>
		/// <returns>true if Stind, false if not</returns>
		Boolean IsStind(Instruction instr)
		{
			switch (instr.OpCode.Code)
			{
				case Code.Stind_I:
				case Code.Stind_I1:
				case Code.Stind_I2:
				case Code.Stind_I4:
				case Code.Stind_I8:
				case Code.Stind_R4:
				case Code.Stind_R8:
				case Code.Stind_Ref:
					return true;
				default:
					return false;
			}
		}

		/// <summary>
		/// Compare two TypeSigs.
		/// </summary>
		/// <param name="type1">TypeSig</param>
		/// <param name="type2">TypeSig</param>
		/// <returns>true if equal, false if not</returns>
		Boolean E(TypeSig type1, TypeSig type2)
		{
			return type1.FullName.Equals(type2.FullName);
		}

		void Fix(Instruction instr)
		{
			var cor = this.Method.Module.CorLibTypes;
			var state = _calc.States(instr).Item1;
			var byref = state.Pop(); // Pop ByRef (address)
			state.Pop(); // Pop value

			// Make sure it's a ByRef
			if (!(byref is ByRefSig))
				throw new Exception(String.Format("Expected ByRefSig: {0}", byref));
			// Get underlying type of ByRef
			var type = byref.Next;

			if (E(type, cor.Boolean) || E(type, cor.Byte) || E(type, cor.SByte))
				instr.OpCode = OpCodes.Stind_I1;
			else if (E(type, cor.Int16) || E(type, cor.UInt16))
				instr.OpCode = OpCodes.Stind_I2;
			else if (E(type, cor.Int32) || E(type, cor.UInt32))
				instr.OpCode = OpCodes.Stind_I4;
			else if (E(type, cor.Int64) || E(type, cor.UInt64))
				instr.OpCode = OpCodes.Stind_I8;
			else if (E(type, cor.Single))
				instr.OpCode = OpCodes.Stind_R4;
			else if (E(type, cor.Double))
				instr.OpCode = OpCodes.Stind_R8;
			else if (E(type, cor.IntPtr) || E(type, cor.UIntPtr))
				instr.OpCode = OpCodes.Stind_I;
			else
				instr.OpCode = OpCodes.Stind_Ref;
		}

		public override void Fix()
		{
			if (!this.HasStind)
				return;

			_calc.Walk();
			foreach (var instr in this.Instructions)
			{
				if (IsStind(instr))
					Fix(instr);
			}
		}
	}
}
