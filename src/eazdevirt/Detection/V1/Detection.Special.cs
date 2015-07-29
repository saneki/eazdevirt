using System;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using eazdevirt.Reflection;
using eazdevirt.Util;

namespace eazdevirt.Detection.V1.Ext
{
	public static partial class Extensions
	{
		[Detect(SpecialCode.Eaz_Call)]
		public static Boolean Is_Eaz_Call(this VirtualOpCode ins)
		{
			var sub = ins.DelegateMethod.Find(new Code[] {
				Code.Ldc_I4, Code.And, Code.Ldc_I4_0, Code.Ceq, Code.Ldc_I4_0, Code.Ceq
			});
			return sub != null
				&& ((Int32)sub[0].Operand) == -2147483648;
		}
	}
}
