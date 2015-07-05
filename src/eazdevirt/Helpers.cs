using System;
using dnlib.DotNet;

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
	}
}
