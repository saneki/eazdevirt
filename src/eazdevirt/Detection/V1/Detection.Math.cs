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
		/// OpCode pattern seen in the Sub_* helper method.
		/// </summary>
		private static readonly Code[] Pattern_Sub = new Code[] {
			Code.Ldloc_0, Code.Ldloc_1, Code.Sub, Code.Stloc_2, Code.Newobj, Code.Stloc_3,
			Code.Ldloc_3, Code.Ldloc_2, Code.Callvirt, Code.Ldloc_3, Code.Ret
		};

		[Detect(Code.Sub)]
		public static Boolean Is_Sub(this VirtualOpCode ins)
		{
			return ins.MatchesIndirectWithBoolean2(false, false, Pattern_Sub);
		}

		[Detect(Code.Sub_Ovf)]
		public static Boolean Is_Sub_Ovf(this VirtualOpCode ins)
		{
			return ins.MatchesIndirectWithBoolean2(true, false, Pattern_Sub);
		}

		[Detect(Code.Sub_Ovf_Un)]
		public static Boolean Is_Sub_Ovf_Un(this VirtualOpCode ins)
		{
			return ins.MatchesIndirectWithBoolean2(true, true, Pattern_Sub);
		}

		/// <summary>
		/// OpCode pattern seen in the Add_* helper method.
		/// </summary>
		private static readonly Code[] Pattern_Add = new Code[] {
			Code.Ldloc_0, Code.Ldloc_1, Code.Add, Code.Stloc_2, Code.Newobj, Code.Stloc_3,
			Code.Ldloc_3, Code.Ldloc_2, Code.Callvirt, Code.Ldloc_3, Code.Ret
		};

		[Detect(Code.Add)]
		public static Boolean Is_Add(this VirtualOpCode ins)
		{
			return ins.MatchesIndirectWithBoolean2(false, false, Pattern_Add);
		}

		[Detect(Code.Add_Ovf)]
		public static Boolean Is_Add_Ovf(this VirtualOpCode ins)
		{
			return ins.MatchesIndirectWithBoolean2(true, false, Pattern_Add);
		}

		[Detect(Code.Add_Ovf_Un)]
		public static Boolean Is_Add_Ovf_Un(this VirtualOpCode ins)
		{
			return ins.MatchesIndirectWithBoolean2(true, true, Pattern_Add);
		}

		/// <summary>
		/// OpCode pattern seen in the Mul_* helper method.
		/// </summary>
		private static readonly Code[] Pattern_Mul = new Code[] {
			Code.Ldloc_0, Code.Ldloc_1, Code.Mul, Code.Stloc_2, Code.Newobj, Code.Stloc_3,
			Code.Ldloc_3, Code.Ldloc_2, Code.Callvirt, Code.Ldloc_3, Code.Ret
		};

		[Detect(Code.Mul)]
		public static Boolean Is_Mul(this VirtualOpCode ins)
		{
			return ins.MatchesIndirectWithBoolean2(false, false, Pattern_Mul);
		}

		[Detect(Code.Mul_Ovf)]
		public static Boolean Is_Mul_Ovf(this VirtualOpCode ins)
		{
			return ins.MatchesIndirectWithBoolean2(true, false, Pattern_Mul);
		}

		[Detect(Code.Mul_Ovf_Un)]
		public static Boolean Is_Mul_Ovf_Un(this VirtualOpCode ins)
		{
			return ins.MatchesIndirectWithBoolean2(true, true, Pattern_Mul);
		}

		[Detect(Code.Neg)]
		public static Boolean Is_Neg(this VirtualOpCode ins)
		{
			return ins.DelegateMethod.MatchesIndirect(
				Code.Ldloc_0, Code.Ldloc_S, Code.Neg, Code.Callvirt, Code.Ldloc_0, Code.Ret
			);
		}

		/// <summary>
		/// OpCode pattern seen in the Rem_* helper method.
		/// </summary>
		private static readonly Code[] Pattern_Rem = new Code[] {
			Code.Ldloc_S, Code.Ldloc_S, Code.Rem, Code.Callvirt, Code.Ldloc_0, Code.Ret
		};

		[Detect(Code.Rem)]
		public static Boolean Is_Rem(this VirtualOpCode ins)
		{
			return ins.MatchesIndirectWithBoolean(false, Pattern_Rem);
		}

		[Detect(Code.Rem_Un)]
		public static Boolean Is_Rem_Un(this VirtualOpCode ins)
		{
			return ins.MatchesIndirectWithBoolean(true, Pattern_Rem);
		}

		/// <summary>
		/// OpCode pattern seen in the Div_* helper method.
		/// </summary>
		private static readonly Code[] Pattern_Div = new Code[] {
			Code.Ldloc_S, Code.Ldloc_S, Code.Div, Code.Callvirt, Code.Ldloc_0, Code.Ret
		};

		[Detect(Code.Div)]
		public static Boolean Is_Div(this VirtualOpCode ins)
		{
			return ins.MatchesIndirectWithBoolean(false, Pattern_Div);
		}

		[Detect(Code.Div_Un)]
		public static Boolean Is_Div_Un(this VirtualOpCode ins)
		{
			return ins.MatchesIndirectWithBoolean(true, Pattern_Div);
		}
	}
}
