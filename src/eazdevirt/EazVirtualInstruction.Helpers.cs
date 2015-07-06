using System;
using System.Linq;
using de4dot.blocks;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace eazdevirt
{
	public partial class EazVirtualInstruction
	{
		/// <summary>
		/// Try to set indentify-related info to the instruction (original opcode).
		/// </summary>
		/// <param name="ins">Virtual instruction</param>
		protected void TrySetIdentify()
		{
			Code code;
			if (this.TryIdentify(out code))
			{
				this.OpCode = code;
				this.IsIdentified = true;
			}
			else
			{
				this.IsIdentified = false;
			}
		}

		/// <summary>
		/// Check if the delegate method's body contains the given pattern.
		/// </summary>
		/// <param name="codePattern">Pattern to check for</param>
		/// <returns>true if match, false if not</returns>
		public Boolean Matches(Code[] codePattern)
		{
			this.CheckDelegateMethod();
			return (Helpers.FindOpCodePatterns(this.DelegateMethod.Body.Instructions, codePattern).Count > 0);
		}

		/// <summary>
		/// Check if a called method's body contains the given pattern (can be improved).
		/// </summary>
		/// <param name="codePattern">Pattern to search for in called method</param>
		/// <remarks>
		/// Looks like: [static] ??? method(Value, Value)
		/// </remarks>
		/// <returns>true if match, false if not</returns>
		public Boolean MatchesIndirect(Code[] codePattern)
		{
			this.CheckDelegateMethod();

			var called = DotNetUtils.GetCalledMethods(this.Module.Module, this.DelegateMethod);
			var targetMethod = called.FirstOrDefault((m) => {
				return (!m.IsStatic && m.Parameters.Count == 3
					 && m.Parameters[1].Type.FullName.Equals(m.Parameters[2].Type.FullName))
					 || (m.IsStatic && m.Parameters.Count == 2
					 && m.Parameters[0].Type.FullName.Equals(m.Parameters[1].Type.FullName));
			});

			if (targetMethod != null)
				return (Helpers.FindOpCodePatterns(targetMethod.Body.Instructions, codePattern).Count > 0);
			else return false;
		}

		/// <summary>
		/// Check if a called method's body contains the given pattern, and that the called
		/// method is given a Boolean of a specific value as the third argument.
		/// </summary>
		/// <param name="val">Boolean value to expect</param>
		/// <param name="codePattern">Pattern to search for in called method</param>
		/// <remarks>
		/// Looks like: [static] ??? method(Value, Value, Boolean)
		/// </remarks>
		/// <returns>true if match, false if not</returns>
		public Boolean MatchesIndirectWithBoolean(Boolean val, Code[] codePattern)
		{
			this.CheckDelegateMethod();

			var called = DotNetUtils.GetCalledMethods(this.Module.Module, this.DelegateMethod);
			var targetMethod = called.FirstOrDefault((m) =>
			{
				return m.Parameters.Count == 4
					&& m.Parameters[3].Type.FullName.Equals("System.Boolean");
			});

			if (targetMethod == null)
				return false;

			// Expected value of ldc.i4 operand, loading the bool value
			Int32 expected = val ? 1 : 0;

			var instrs = this.DelegateMethod.Body.Instructions;
			for (Int32 i = 0; i < instrs.Count; i++)
			{
				var instr = this.DelegateMethod.Body.Instructions[i];

				if (instr.OpCode.Code == Code.Call && instr.Operand is MethodDef
				&& ((MethodDef)instr.Operand) == targetMethod
				&& i != 0 && instrs[i - 1].IsLdcI4() && Helpers.GetLdcOperand(instrs[i - 1]) == expected)
				{
					// If we get here, we have the right method
					return (Helpers.FindOpCodePatterns(targetMethod.Body.Instructions, codePattern).Count > 0);
				}
			}

			return false;
		}

		/// <summary>
		/// Check if a called method's body contains the given pattern, and that the called
		/// method is given two Booleans of specific values as the third and fourth arguments.
		/// </summary>
		/// <param name="val1">First Boolean value to expect</param>
		/// <param name="val2">Second Boolean value to expect</param>
		/// <param name="codePattern">Pattern to search for in called method</param>
		/// <remarks>
		/// Looks like: [static] ??? method(Value, Value, Boolean, Boolean)
		/// </remarks>
		/// <returns>true if match, false if not</returns>
		public Boolean MatchesIndirectWithBoolean2(Boolean val1, Boolean val2, Code[] codePattern)
		{
			this.CheckDelegateMethod();

			var called = DotNetUtils.GetCalledMethods(this.Module.Module, this.DelegateMethod);
			var targetMethod = called.FirstOrDefault((m) =>
			{
				return m.Parameters.Count == 5
					&& m.Parameters[3].Type.FullName.Equals("System.Boolean")
					&& m.Parameters[4].Type.FullName.Equals("System.Boolean");
			});

			if (targetMethod == null)
				return false;

			// Expected values of ldc.i4 operands, loading the two bool values
			Int32 expected1 = val1 ? 1 : 0, expected2 = val2 ? 1 : 0;

			var instrs = this.DelegateMethod.Body.Instructions;
			for (Int32 i = 0; i < instrs.Count; i++)
			{
				var instr = instrs[i];

				if (instr.OpCode.Code == Code.Call && instr.Operand is MethodDef
				&& ((MethodDef)instr.Operand).MDToken == targetMethod.MDToken
				&& i > 1 && instrs[i - 1].IsLdcI4() && Helpers.GetLdcOperand(instrs[i - 1]) == expected2
				&& instrs[i - 2].IsLdcI4() && Helpers.GetLdcOperand(instrs[i - 2]) == expected1)
				{
					// If we get here, we have the right method
					return (Helpers.FindOpCodePatterns(targetMethod.Body.Instructions, codePattern).Count > 0);
				}
			}

			return false;
		}

		/// <summary>
		/// Enforce non-nullness for the delegate method, throwing an Exception if null.
		/// </summary>
		/// <exception cref="System.Exception">Thrown if DelegateMethod is null</exception>
		protected void CheckDelegateMethod()
		{
			if (this.DelegateMethod == null)
				throw new Exception("Cannot check for delegate method match, delegate method is null");
		}
	}
}
