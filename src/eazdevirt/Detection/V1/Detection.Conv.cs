using System;
using System.Collections.Generic;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using eazdevirt.Reflection;
using eazdevirt.Util;

namespace eazdevirt.Detection.V1.Ext
{
	public static partial class Extensions
	{
		/// <summary>
		/// OpCode pattern seen at the end of Conv_* helper methods.
		/// Also seen in Conv_*_Un delegate methods.
		/// </summary>
		private static readonly Code[] Pattern_Conv_Helper_Tail = new Code[] {
			Code.Ldarg_0, Code.Newobj, Code.Stloc_3, Code.Ldloc_3, Code.Ldloc_2,
			Code.Callvirt, Code.Ldloc_3, Code.Call, Code.Ret
		};

		private static Code[] Conv_Helper_Pattern(params Code[] opcodes)
		{
			var list = new List<Code>(opcodes);
			list.AddRange(Pattern_Conv_Helper_Tail);
			return list.ToArray();
		}

		private static Boolean _Is_Conv_I(VirtualOpCode ins, Boolean ovf, IList<Code> helperPattern)
		{
			Code[] delegatePattern = (ovf ?
				new Code[] { Code.Ldarg_0, Code.Ldc_I4_1, Code.Call, Code.Ret } :
				new Code[] { Code.Ldarg_0, Code.Ldc_I4_0, Code.Call, Code.Ret });

			return ins.DelegateMethod.MatchesEntire(delegatePattern)
				&& ins.DelegateMethod.MatchesIndirect(helperPattern);
		}

		/// <summary>
		/// OpCode pattern seen in Conv_I, Conv_Ovf_I helper methods.
		/// </summary>
		private static readonly Code[] Pattern_Conv_I = Conv_Helper_Pattern(Code.Conv_I8, Code.Call);

		[Detect(Code.Conv_I)]
		public static Boolean Is_Conv_I(this VirtualOpCode ins)
		{
			return _Is_Conv_I(ins, false, Pattern_Conv_I);
		}

		[Detect(Code.Conv_Ovf_I)]
		public static Boolean Is_Conv_Ovf_I(this VirtualOpCode ins)
		{
			return _Is_Conv_I(ins, true, Pattern_Conv_I);
		}

		/// <summary>
		/// OpCode pattern seen in Conv_I1, Conv_Ovf_I1 helper methods.
		/// </summary>
		private static readonly Code[] Pattern_Conv_I1 = Conv_Helper_Pattern(Code.Conv_I1, Code.Stloc_2);

		[Detect(Code.Conv_I1)]
		public static Boolean Is_Conv_I1(this VirtualOpCode ins)
		{
			return _Is_Conv_I(ins, false, Pattern_Conv_I1);
		}

		[Detect(Code.Conv_Ovf_I1)]
		public static Boolean Is_Conv_Ovf_I1(this VirtualOpCode ins)
		{
			return _Is_Conv_I(ins, true, Pattern_Conv_I1);
		}

		/// <summary>
		/// OpCode pattern seen in Conv_I2, Conv_Ovf_I2 helper methods.
		/// </summary>
		private static readonly Code[] Pattern_Conv_I2 = Conv_Helper_Pattern(Code.Conv_I2, Code.Stloc_2);

		[Detect(Code.Conv_I2)]
		public static Boolean Is_Conv_I2(this VirtualOpCode ins)
		{
			return _Is_Conv_I(ins, false, Pattern_Conv_I2);
		}

		[Detect(Code.Conv_Ovf_I2)]
		public static Boolean Is_Conv_Ovf_I2(this VirtualOpCode ins)
		{
			return _Is_Conv_I(ins, true, Pattern_Conv_I2);
		}

		/// <summary>
		/// OpCode pattern seen in Conv_I8, Conv_Ovf_I8 helper methods.
		/// </summary>
		private static readonly Code[] Pattern_Conv_I8 = Conv_Helper_Pattern(Code.Conv_I8, Code.Stloc_2);

		[Detect(Code.Conv_I8)]
		public static Boolean Is_Conv_I8(this VirtualOpCode ins)
		{
			return _Is_Conv_I(ins, false, Pattern_Conv_I8);
		}

		[Detect(Code.Conv_Ovf_I8)]
		public static Boolean Is_Conv_Ovf_I8(this VirtualOpCode ins)
		{
			return _Is_Conv_I(ins, true, Pattern_Conv_I8);
		}

		[Detect(Code.Conv_Ovf_I_Un)]
		public static Boolean Is_Conv_Ovf_I_Un(this VirtualOpCode ins)
		{
			return ins.DelegateMethod.Matches(Conv_Helper_Pattern(Code.Conv_Ovf_I8, Code.Call));
		}

		[Detect(Code.Conv_Ovf_I1_Un)]
		public static Boolean Is_Conv_Ovf_I1_Un(this VirtualOpCode ins)
		{
			return ins.DelegateMethod.Matches(Conv_Helper_Pattern(Code.Conv_Ovf_I1, Code.Stloc_2));
		}

		[Detect(Code.Conv_Ovf_I2_Un)]
		public static Boolean Is_Conv_Ovf_I2_Un(this VirtualOpCode ins)
		{
			return ins.DelegateMethod.Matches(Conv_Helper_Pattern(Code.Conv_Ovf_I2, Code.Stloc_2));
		}

		[Detect(Code.Conv_Ovf_I4_Un)]
		public static Boolean Is_Conv_Ovf_I4_Un(this VirtualOpCode ins)
		{
			return ins.DelegateMethod.Matches(Conv_Helper_Pattern(Code.Conv_Ovf_I4, Code.Stloc_2));
		}

		[Detect(Code.Conv_Ovf_I8_Un)]
		public static Boolean Is_Conv_Ovf_I8_Un(this VirtualOpCode ins)
		{
			return ins.DelegateMethod.Matches(Conv_Helper_Pattern(Code.Conv_Ovf_I8, Code.Stloc_2));
		}

		/// <summary>
		/// OpCode pattern seen in Conv_Ovf_U helper method and Conv_Ovf_U_Un delegate method.
		/// </summary>
		private static readonly Code[] Pattern_Conv_Ovf_U = Conv_Helper_Pattern(Code.Conv_U8, Code.Call);

		[Detect(Code.Conv_U)]
		public static Boolean Is_Conv_U(this VirtualOpCode ins)
		{
			return _Is_Conv_I(ins, false, Pattern_Conv_Ovf_U);
		}

		[Detect(Code.Conv_Ovf_U)]
		public static Boolean Is_Conv_Ovf_U(this VirtualOpCode ins)
		{
			return _Is_Conv_I(ins, true, Pattern_Conv_Ovf_U);
		}

		[Detect(Code.Conv_Ovf_U_Un)]
		public static Boolean Is_Conv_Ovf_U_Un(this VirtualOpCode ins)
		{
			return ins.DelegateMethod.Matches(Pattern_Conv_Ovf_U);
		}

		[Detect(Code.Conv_U1)]
		public static Boolean Is_Conv_U1(this VirtualOpCode ins)
		{
			return _Is_Conv_I(ins, false, Conv_Helper_Pattern(Code.Conv_U1, Code.Stloc_2));
		}

		[Detect(Code.Conv_Ovf_U1)]
		public static Boolean Is_Conv_Ovf_U1(this VirtualOpCode ins)
		{
			return _Is_Conv_I(ins, true, Conv_Helper_Pattern(Code.Conv_U1, Code.Stloc_2));
		}

		[Detect(Code.Conv_Ovf_U1_Un)]
		public static Boolean Is_Conv_Ovf_U1_Un(this VirtualOpCode ins)
		{
			return ins.DelegateMethod.Matches(Conv_Helper_Pattern(Code.Conv_Ovf_U1, Code.Stloc_2));
		}

		[Detect(Code.Conv_U2)]
		public static Boolean Is_Conv_U2(this VirtualOpCode ins)
		{
			return _Is_Conv_I(ins, false, Conv_Helper_Pattern(Code.Conv_U2, Code.Stloc_2));
		}

		[Detect(Code.Conv_Ovf_U2)]
		public static Boolean Is_Conv_Ovf_U2(this VirtualOpCode ins)
		{
			return _Is_Conv_I(ins, true, Conv_Helper_Pattern(Code.Conv_U2, Code.Stloc_2));
		}

		[Detect(Code.Conv_Ovf_U2_Un)]
		public static Boolean Is_Conv_Ovf_U2_Un(this VirtualOpCode ins)
		{
			return ins.DelegateMethod.Matches(Conv_Helper_Pattern(Code.Conv_Ovf_U2, Code.Stloc_2));
		}

		[Detect(Code.Conv_U4)]
		public static Boolean Is_Conv_Ovf_U4(this VirtualOpCode ins)
		{
			return _Is_Conv_I(ins, false, Conv_Helper_Pattern(Code.Conv_U4, Code.Stloc_2));
		}

		[Detect(Code.Conv_Ovf_U4)]
		public static Boolean Is_Conv_U4(this VirtualOpCode ins)
		{
			return _Is_Conv_I(ins, true, Conv_Helper_Pattern(Code.Conv_U4, Code.Stloc_2));
		}

		[Detect(Code.Conv_Ovf_U4_Un)]
		public static Boolean Is_Conv_Ovf_U4_Un(this VirtualOpCode ins)
		{
			return ins.DelegateMethod.Matches(Conv_Helper_Pattern(Code.Conv_Ovf_U4, Code.Stloc_2));
		}

		[Detect(Code.Conv_U8)]
		public static Boolean Is_Conv_U8(this VirtualOpCode ins)
		{
			return _Is_Conv_I(ins, false, Conv_Helper_Pattern(Code.Conv_U8, Code.Stloc_2));
		}

		[Detect(Code.Conv_Ovf_U8)]
		public static Boolean Is_Conv_Ovf_U8(this VirtualOpCode ins)
		{
			return _Is_Conv_I(ins, true, Conv_Helper_Pattern(Code.Conv_U8, Code.Stloc_2));
		}

		[Detect(Code.Conv_Ovf_U8_Un)]
		public static Boolean Is_Conv_Ovf_U8_Un(this VirtualOpCode ins)
		{
			return ins.DelegateMethod.Matches(Conv_Helper_Pattern(Code.Conv_Ovf_U8, Code.Stloc_2));
		}

		[Detect(Code.Conv_R_Un)]
		public static Boolean Is_Conv_R_Un(this VirtualOpCode ins)
		{
			return ins.DelegateMethod.Matches(Conv_Helper_Pattern(Code.Conv_R_Un, Code.Conv_R8, Code.Stloc_2));
		}

		/// <summary>
		/// OpCode pattern seen at the end of Conv_R4 delegate method.
		/// </summary>
		private static readonly Code[] Pattern_Conv_R4_Helper_Tail = new Code[] {
			Code.Ldarg_0, Code.Newobj, Code.Stloc_3, Code.Ldloc_3, Code.Ldloc_2,
			Code.Conv_R8, Code.Callvirt, Code.Ldloc_3, Code.Call, Code.Ret
		};

		private static Code[] Conv_R4_Helper_Pattern(params Code[] opcodes)
		{
			var list = new List<Code>(opcodes);
			list.AddRange(Pattern_Conv_R4_Helper_Tail);
			return list.ToArray();
		}

		[Detect(Code.Conv_R4)]
		public static Boolean Is_Conv_R4(this VirtualOpCode ins)
		{
			return ins.DelegateMethod.Matches(Conv_R4_Helper_Pattern(Code.Conv_R4, Code.Stloc_2));
		}

		[Detect(Code.Conv_R8)]
		public static Boolean Is_Conv_R8(this VirtualOpCode ins)
		{
			return ins.DelegateMethod.Matches(Conv_Helper_Pattern(
				Code.Conv_R8, Code.Stloc_2, Code.Br_S, Code.Ldloc_0, Code.Castclass,
				Code.Callvirt, Code.Stloc_2
			));
		}

		/// <summary>
		/// OpCode pattern seen in Conv_I4, Conv_Ovf_I4 helper methods.
		/// </summary>
		private static readonly Code[] Pattern_Conv_I4 = new Code[] {
			Code.Ldloc_0, Code.Castclass, Code.Callvirt, Code.Conv_Ovf_I4,
			Code.Stloc_2, Code.Br_S, Code.Ldloc_0, Code.Castclass, Code.Callvirt,
			Code.Conv_I4, Code.Stloc_2
		};

		[Detect(Code.Conv_I4)]
		public static Boolean Is_Conv_I4(this VirtualOpCode ins)
		{
			return ins.MatchesEntire(new Code[] {
				Code.Ldarg_0, Code.Ldc_I4_0, Code.Call, Code.Ret
			}) && ins.DelegateMethod.MatchesIndirect(Pattern_Conv_I4);
		}

		[Detect(Code.Conv_Ovf_I4)]
		public static Boolean Is_Conv_Ovf_I4(this VirtualOpCode ins)
		{
			return ins.MatchesEntire(new Code[] {
				Code.Ldarg_0, Code.Ldc_I4_1, Code.Call, Code.Ret
			}) && ins.DelegateMethod.MatchesIndirect(Pattern_Conv_I4);
		}
	}
}
