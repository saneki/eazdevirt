using System;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using System.Collections.Generic;

namespace eazdevirt
{
	public static class Helpers
	{
		/// <summary>
		/// Get the first method that is Call-ed in a method's body. Does not factor
		/// in branching.
		/// </summary>
		/// <param name="method">Method to look in</param>
		public static MethodDef GetFirstCalledMethod(MethodDef method)
		{
			if (method == null)
				throw new ArgumentNullException();

			if (!method.HasBody || !method.Body.HasInstructions)
				return null;

			foreach (var instr in method.Body.Instructions)
			{
				if (instr.OpCode.Code == dnlib.DotNet.Emit.Code.Call)
				{
					MethodDef calledMethod;
					if (instr.Operand is MethodDef && (calledMethod = ((MethodDef)instr.Operand)) != null)
					{
						return calledMethod;
					}
				}

				// Todo: Calli, Callvirt?
			}

			return null;
		}

		/// <summary>
		/// Try to cast a `call` operand to a MethodDef. If not successful,
		/// returns null.
		/// </summary>
		/// <param name="obj">Operand to cast</param>
		/// <returns>MethodDef if successful, null if not</returns>
		public static MethodDef TryTransformCallOperand(Object operand)
		{
			if (operand is MethodDef)
				return (MethodDef)operand;
			else return null;
		}

		/// <summary>
		/// Look through some sequence of instructions for a pattern of opcodes, and return all instruction
		/// subsequences which match the given pattern.
		/// </summary>
		/// <param name="instructions">Instruction sequence to look through</param>
		/// <param name="pattern">OpCode pattern</param>
		/// <returns>All matching instruction subsequences</returns>
		public static IList<Instruction[]> FindOpCodePatterns(IList<Instruction> instructions, IList<Code> pattern)
		{
			List<Instruction[]> list = new List<Instruction[]>();

			for(Int32 i = 0; i < instructions.Count; i++)
			{
				List<Instruction> current = new List<Instruction>();

				for(Int32 j = i, k = 0; j < instructions.Count && k < pattern.Count; j++, k++)
				{
					if (instructions[j].OpCode.Code != pattern[k])
						break;
					else current.Add(instructions[j]);
				}

				if (current.Count == pattern.Count)
					list.Add(current.ToArray());
			}

			return list;
		}

		/// <summary>
		/// Get the operand value for all Ldc_* instructions.
		/// </summary>
		/// <param name="ldc">Instruction</param>
		/// <returns>Operand value</returns>
		public static Int32 GetLdcOperand(Instruction ldc)
		{
			switch (ldc.OpCode.Code)
			{
				case Code.Ldc_I4:
					return (Int32)ldc.Operand;
				case Code.Ldc_I4_S:
					return (Int32)((SByte)ldc.Operand);
				case Code.Ldc_I4_0:
					return 0;
				case Code.Ldc_I4_1:
					return 1;
				case Code.Ldc_I4_2:
					return 2;
				case Code.Ldc_I4_3:
					return 3;
				case Code.Ldc_I4_4:
					return 4;
				case Code.Ldc_I4_5:
					return 5;
				case Code.Ldc_I4_6:
					return 6;
				case Code.Ldc_I4_7:
					return 7;
				case Code.Ldc_I4_8:
					return 8;
				case Code.Ldc_I4_M1:
					return -1;
			}

			throw new Exception("Cannot get operand value of non-ldc instruction");
		}

		public static MethodDef GetRetMethod(MethodDef method)
		{
			if (method == null)
				throw new ArgumentNullException();

			MethodDef lastCalled = null;
			foreach(var instr in method.Body.Instructions)
			{
				MethodDef called = null;
				if(instr.OpCode.Code == Code.Call
				&& (called = TryTransformCallOperand(instr.Operand)) != null)
				{
					lastCalled = called;
				}
				else if (instr.OpCode.Code == Code.Ret)
					break;
			}

			return lastCalled;
		}
	}
}
