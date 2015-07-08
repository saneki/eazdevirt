using System;
using System.Collections.Generic;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace eazdevirt.Util
{
	public static class Extensions
	{
		/// <summary>
		/// Check if the method's body contains the given pattern.
		/// </summary>
		/// <param name="method">Method to check</param>
		/// <param name="codePattern">Pattern to check for</param>
		/// <returns>true if match, false if not</returns>
		public static Boolean Matches(this MethodDef method, IList<Code> codePattern)
		{
			if (method == null)
				throw new ArgumentNullException();

			return (Helpers.FindOpCodePatterns(method.Body.Instructions, codePattern).Count > 0);
		}

		/// <summary>
		/// Check if the method body entirely matches the given pattern.
		/// </summary>
		/// <param name="method">Method to check</param>
		/// <param name="codePattern">Pattern to check against</param>
		/// <returns>true if match, false if not</returns>
		public static Boolean MatchesEntire(this MethodDef method, IList<Code> codePattern)
		{
			if (method == null)
				throw new ArgumentNullException();

			var instructions = Helpers.FindOpCodePatterns(method.Body.Instructions, codePattern);
			return (instructions.Count == 1 && instructions[0].Length == method.Body.Instructions.Count);
		}

		/// <summary>
		/// Find the first occurrence of an opcode pattern, returning the matching instruction sequence.
		/// </summary>
		/// <param name="method">Method to search</param>
		/// <param name="pattern">Pattern to search for</param>
		/// <returns>Matching instruction sequence, or null if none found</returns>
		public static IList<Instruction> Find(this MethodDef method, IList<Code> pattern)
		{
			if (method == null)
				throw new ArgumentNullException();

			var result = Helpers.FindOpCodePatterns(method.Body.Instructions, pattern);
			if (result.Count == 0)
				return null;
			else return result[0];
		}
	}
}
