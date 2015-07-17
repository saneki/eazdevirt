using System;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using eazdevirt.Reflection;
using eazdevirt.Util;

namespace eazdevirt.Detection.V1.Ext
{
	public static partial class Extensions
	{
		[Detect(Code.Constrained)]
		public static Boolean Is_Constrained(this VirtualOpCode ins)
		{
			return ins.DelegateMethod.MatchesEntire(
				Code.Ldarg_1, Code.Castclass, Code.Callvirt, Code.Stloc_0, Code.Ldarg_0,
				Code.Ldarg_0, Code.Ldloc_0, Code.Call, Code.Stfld, Code.Ret
			);
		}
	}
}
