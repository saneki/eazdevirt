using dnlib.DotNet.Emit;
using System;
using System.Collections.Generic;

namespace eazdevirt
{
	/// <summary>
	/// Extensions for detecting original instruction type (opcode).
	/// Definitely going to need a more powerful engine for this.
	/// </summary>
	public static class Extensions
	{
		public static Boolean Is_And(this EazVirtualInstruction ins)
		{
			return ins.MatchesIndirect(
				new Code[] { Code.Ldloc_S, Code.Ldloc_S, Code.And, Code.Callvirt, Code.Ldloc_0, Code.Ret }
			);
		}

		public static Boolean Is_Xor(this EazVirtualInstruction ins)
		{
			return ins.MatchesIndirect(
				new Code[] { Code.Ldloc_S, Code.Ldloc_S, Code.Xor, Code.Callvirt, Code.Ldloc_0, Code.Ret }
			);
		}

		public static Boolean Is_Shl(this EazVirtualInstruction ins)
		{
			return ins.MatchesIndirect(
				new Code[] { Code.Ldloc_S, Code.Ldloc_S, Code.Ldc_I4_S, Code.And, Code.Shl, Code.Stloc_S,
				Code.Newobj, Code.Stloc_0, Code.Ldloc_0, Code.Ldloc_S, Code.Callvirt, Code.Ldloc_0, Code.Ret }
			);
		}
	}
}
