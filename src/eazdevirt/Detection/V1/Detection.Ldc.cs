using System;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using eazdevirt.Reflection;

namespace eazdevirt.Detection.V1.Ext
{
	public static partial class Extensions
	{
		/// <summary>
		/// OpCode pattern seen in the Ldc_I4_C delegate methods.
		/// </summary>
		private static readonly Code[] Pattern_Ldc_I4_C = new Code[] {
			Code.Ldarg_0, Code.Newobj, Code.Stloc_0, Code.Ldloc_0, Code.Ldc_I4, // Code.Ldc_I4 changes depending on _C
			Code.Callvirt, Code.Ldloc_0, Code.Call, Code.Ret
		};

		private static Boolean Is_Ldc_I4_C(VirtualOpCode ins, Code code)
		{
			return ins.MatchesEntire(ins.ModifyPattern(Pattern_Ldc_I4_C, Code.Ldc_I4, code));
		}

		[Detect(Code.Ldc_I4_0)]
		public static Boolean Is_Ldc_I4_0(this VirtualOpCode ins)
		{
			return Is_Ldc_I4_C(ins, Code.Ldc_I4_0);
		}

		[Detect(Code.Ldc_I4_1)]
		public static Boolean Is_Ldc_I4_1(this VirtualOpCode ins)
		{
			return Is_Ldc_I4_C(ins, Code.Ldc_I4_1);
		}

		[Detect(Code.Ldc_I4_2)]
		public static Boolean Is_Ldc_I4_2(this VirtualOpCode ins)
		{
			return Is_Ldc_I4_C(ins, Code.Ldc_I4_2);
		}

		[Detect(Code.Ldc_I4_3)]
		public static Boolean Is_Ldc_I4_3(this VirtualOpCode ins)
		{
			return Is_Ldc_I4_C(ins, Code.Ldc_I4_3);
		}

		[Detect(Code.Ldc_I4_4)]
		public static Boolean Is_Ldc_I4_4(this VirtualOpCode ins)
		{
			return Is_Ldc_I4_C(ins, Code.Ldc_I4_4);
		}

		[Detect(Code.Ldc_I4_5)]
		public static Boolean Is_Ldc_I4_5(this VirtualOpCode ins)
		{
			return Is_Ldc_I4_C(ins, Code.Ldc_I4_5);
		}

		[Detect(Code.Ldc_I4_6)]
		public static Boolean Is_Ldc_I4_6(this VirtualOpCode ins)
		{
			return Is_Ldc_I4_C(ins, Code.Ldc_I4_6);
		}

		[Detect(Code.Ldc_I4_7)]
		public static Boolean Is_Ldc_I4_7(this VirtualOpCode ins)
		{
			return Is_Ldc_I4_C(ins, Code.Ldc_I4_7);
		}

		[Detect(Code.Ldc_I4_8)]
		public static Boolean Is_Ldc_I4_8(this VirtualOpCode ins)
		{
			return Is_Ldc_I4_C(ins, Code.Ldc_I4_8);
		}

		[Detect(Code.Ldc_I4_M1)]
		public static Boolean Is_Ldc_I4_M1(this VirtualOpCode ins)
		{
			return Is_Ldc_I4_C(ins, Code.Ldc_I4_M1);
		}

		/// <summary>
		/// OpCode pattern seen in the Ldc_I4, Ldc_I4_S, Ldc_I8, Ldc_R4, Ldc_R8 delegate methods.
		/// </summary>
		public static readonly Code[] Pattern_Ldc = new Code[] {
			Code.Ldarg_0, Code.Ldarg_1, Code.Call, Code.Ret
		};

		public static Boolean _Is_Ldc(VirtualOpCode ins, OperandType expectedOperandType)
		{
			OperandType operandType;
			return ins.MatchesEntire(Pattern_Ldc)
				&& ins.TryGetOperandType(out operandType)
				&& operandType == expectedOperandType;
		}

		[Detect(Code.Ldc_I4)]
		public static Boolean Is_Ldc_I4(this VirtualOpCode ins)
		{
			return _Is_Ldc(ins, OperandType.InlineI);
		}

		[Detect(Code.Ldc_I4_S)]
		public static Boolean Is_Ldc_I4_S(this VirtualOpCode ins)
		{
			return _Is_Ldc(ins, OperandType.ShortInlineI);
		}

		[Detect(Code.Ldc_I8)]
		public static Boolean Is_Ldc_I8(this VirtualOpCode ins)
		{
			return _Is_Ldc(ins, OperandType.InlineI8);
		}

		[Detect(Code.Ldc_R4)]
		public static Boolean Is_Ldc_R4(this VirtualOpCode ins)
		{
			return _Is_Ldc(ins, OperandType.ShortInlineR);
		}

		[Detect(Code.Ldc_R8)]
		public static Boolean Is_Ldc_R8(this VirtualOpCode ins)
		{
			return _Is_Ldc(ins, OperandType.InlineR);
		}
	}
}
