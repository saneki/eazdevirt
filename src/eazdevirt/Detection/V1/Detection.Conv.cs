using System;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using eazdevirt.Reflection;
using eazdevirt.Util;

namespace eazdevirt.Detection.V1.Ext
{
	public static partial class Extensions
	{
		/// <summary>
		/// OpCode pattern seen in Conv_I4, Conv_Ovf_I4 helper methods.
		/// </summary>
		private static readonly Code[] Pattern_Conv_I4 = new Code[] {
			Code.Ldloc_0, Code.Castclass, Code.Callvirt, Code.Conv_Ovf_I4,
			Code.Stloc_2, Code.Br_S, Code.Ldloc_0, Code.Castclass, Code.Callvirt,
			Code.Conv_I4, Code.Stloc_2
		};

		[Detect(Code.Conv_I4)]
		public static Boolean Is_Conv_I4(this EazVirtualInstruction ins)
		{
			return ins.MatchesEntire(new Code[] {
				Code.Ldarg_0, Code.Ldc_I4_0, Code.Call, Code.Ret
			}) && ins.DelegateMethod.MatchesIndirect(Pattern_Conv_I4);
		}

		[Detect(Code.Conv_Ovf_I4)]
		public static Boolean Is_Conv_Ovf_I4(this EazVirtualInstruction ins)
		{
			return ins.MatchesEntire(new Code[] {
				Code.Ldarg_0, Code.Ldc_I4_1, Code.Call, Code.Ret
			}) && ins.DelegateMethod.MatchesIndirect(Pattern_Conv_I4);
		}
	}
}
