using System;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using eazdevirt.Reflection;
using eazdevirt.Util;

namespace eazdevirt.Detection.V1.Ext
{
	public static partial class Extensions
	{
		public static Boolean _Is_Unsupported(VirtualOpCode ins, String name)
		{
			String exceptionString = String.Format("{0} is not supported", name);
			return ins.DelegateMethod.MatchesEntire(new Code[] {
				Code.Ldstr, Code.Newobj, Code.Throw
			}) && ((String)ins.DelegateMethod.Body.Instructions[0].Operand)
			      .StartsWith(exceptionString, StringComparison.OrdinalIgnoreCase);
		}

		[Detect(Code.Arglist)]
		public static Boolean Is_Arglist(this VirtualOpCode ins)
		{
			return _Is_Unsupported(ins, "Arglist");
		}

		[Detect(Code.Cpobj)]
		public static Boolean Is_Cpobj(this VirtualOpCode ins)
		{
			return _Is_Unsupported(ins, "Cpobj");
		}

		[Detect(Code.Mkrefany)]
		public static Boolean Is_Mkrefany(this VirtualOpCode ins)
		{
			return _Is_Unsupported(ins, "Mkrefany");
		}

		[Detect(Code.Refanytype)]
		public static Boolean Is_Refanytype(this VirtualOpCode ins)
		{
			return _Is_Unsupported(ins, "Refanytype");
		}

		[Detect(Code.Refanyval)]
		public static Boolean Is_Refanyval(this VirtualOpCode ins)
		{
			return _Is_Unsupported(ins, "Refanyval");
		}
	}
}
