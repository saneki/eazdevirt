using System;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using eazdevirt.Reflection;
using eazdevirt.Util;

namespace eazdevirt.Detection.V1.Ext
{
	public static partial class Extensions
	{
		[Detect(Code.And)]
		public static Boolean Is_And(this EazVirtualInstruction ins)
		{
			return ins.MatchesIndirect(
				new Code[] { Code.Ldloc_S, Code.Ldloc_S, Code.And, Code.Callvirt, Code.Ldloc_0, Code.Ret }
			);
		}

		[Detect(Code.Xor)]
		public static Boolean Is_Xor(this EazVirtualInstruction ins)
		{
			return ins.MatchesIndirect(
				new Code[] { Code.Ldloc_S, Code.Ldloc_S, Code.Xor, Code.Callvirt, Code.Ldloc_0, Code.Ret }
			);
		}

		[Detect(Code.Shl)]
		public static Boolean Is_Shl(this EazVirtualInstruction ins)
		{
			return ins.MatchesIndirect(
				new Code[] { Code.Ldloc_S, Code.Ldloc_S, Code.Ldc_I4_S, Code.And, Code.Shl, Code.Stloc_S,
				Code.Newobj, Code.Stloc_0, Code.Ldloc_0, Code.Ldloc_S, Code.Callvirt, Code.Ldloc_0, Code.Ret }
			);
		}

		/// <summary>
		/// OpCode pattern seen in the Shr_* helper method.
		/// </summary>
		private static readonly Code[] Pattern_Shr = new Code[] {
			Code.Ldc_I4_S, Code.And, Code.Shr, Code.Callvirt, Code.Ldloc_0, Code.Ret
		};

		[Detect(Code.Shr)]
		public static Boolean Is_Shr(this EazVirtualInstruction ins)
		{
			return ins.MatchesIndirectWithBoolean(true, Pattern_Shr);
		}

		[Detect(Code.Shr_Un)]
		public static Boolean Is_Shr_Un(this EazVirtualInstruction ins)
		{
			return ins.MatchesIndirectWithBoolean(false, Pattern_Shr);
		}

		[Detect(Code.Or)]
		public static Boolean Is_Or(this EazVirtualInstruction ins)
		{
			return ins.MatchesIndirect(new Code[] {
				Code.Ldloc_S, Code.Ldloc_S, Code.Or, Code.Callvirt, Code.Ldloc_0, Code.Ret
			});
		}

		[Detect(Code.Not)]
		public static Boolean Is_Not(this EazVirtualInstruction ins)
		{
			return ins.DelegateMethod.MatchesIndirect(new Code[] {
				Code.Ldloc_1, Code.Ldloc_S, Code.Not, Code.Callvirt, Code.Ldloc_1, Code.Ret
			});
		}
	}
}
