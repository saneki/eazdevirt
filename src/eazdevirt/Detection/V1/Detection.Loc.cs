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
		/// OpCode pattern seen in the Ldloc_C delegate methods.
		/// </summary>
		private static readonly Code[] Pattern_Ldloc_C = new Code[] {
			Code.Ldarg_0, Code.Ldarg_0, Code.Ldfld, Code.Ldc_I4, Code.Ldelem, // Code.Ldc_I4 changes depending on _C
			Code.Callvirt, Code.Call, Code.Ret
		};

		private static Boolean Is_Ldloc_C(VirtualOpCode ins, Code code)
		{
			return ins.MatchesEntire(ins.ModifyPattern(Pattern_Ldloc_C, Code.Ldc_I4, code))
				&& ((FieldDef)ins.DelegateMethod.Body.Instructions[2].Operand).MDToken == ins.Virtualization.LocalsField.MDToken;
		}

		[Detect(Code.Ldloc_0)]
		public static Boolean Is_Ldloc_0(this VirtualOpCode ins)
		{
			return Is_Ldloc_C(ins, Code.Ldc_I4_0);
		}

		[Detect(Code.Ldloc_1)]
		public static Boolean Is_Ldloc_1(this VirtualOpCode ins)
		{
			return Is_Ldloc_C(ins, Code.Ldc_I4_1);
		}

		[Detect(Code.Ldloc_2)]
		public static Boolean Is_Ldloc_2(this VirtualOpCode ins)
		{
			return Is_Ldloc_C(ins, Code.Ldc_I4_2);
		}

		[Detect(Code.Ldloc_3)]
		public static Boolean Is_Ldloc_3(this VirtualOpCode ins)
		{
			return Is_Ldloc_C(ins, Code.Ldc_I4_3);
		}

		/// <summary>
		/// OpCode pattern seen in the Ldloc, Ldloc_S delegate methods.
		/// </summary>
		private static readonly Code[] Pattern_Ldloc = new Code[] {
			Code.Ldarg_1, Code.Castclass, Code.Stloc_0, Code.Ldarg_0, Code.Ldarg_0,
			Code.Ldfld, Code.Ldloc_0, Code.Callvirt, Code.Ldelem, Code.Callvirt,
			Code.Call, Code.Ret
		};

		[Detect(Code.Ldloc)]
		public static Boolean Is_Ldloc(this VirtualOpCode ins)
		{
			return ins.MatchesEntire(Pattern_Ldloc)
				&& ((MethodDef)ins.DelegateMethod.Body.Instructions[7].Operand)
				   .ReturnType.FullName.Equals("System.UInt16")
				&& ((FieldDef)ins.DelegateMethod.Body.Instructions[5].Operand)
				   .MDToken == ins.Virtualization.LocalsField.MDToken;
		}

		[Detect(Code.Ldloc_S)]
		public static Boolean Is_Ldloc_S(this VirtualOpCode ins)
		{
			return ins.MatchesEntire(Pattern_Ldloc)
				&& ((MethodDef)ins.DelegateMethod.Body.Instructions[7].Operand)
				   .ReturnType.FullName.Equals("System.Byte")
				&& ((FieldDef)ins.DelegateMethod.Body.Instructions[5].Operand)
				   .MDToken == ins.Virtualization.LocalsField.MDToken;
		}

		[Detect(Code.Ldloca_S)]
		public static Boolean Is_Ldloca_S(this VirtualOpCode ins)
		{
			return ins.DelegateMethod.MatchesEntire(
				Code.Ldarg_1, Code.Castclass, Code.Stloc_0, Code.Ldarg_0, Code.Newobj,
				Code.Stloc_1, Code.Ldloc_1, Code.Ldloc_0, Code.Callvirt, Code.Callvirt,
				Code.Ldloc_1, Code.Call, Code.Ret
			);
		}

		/// <summary>
		/// OpCode pattern seen in the Stloc_C delegate methods.
		/// </summary>
		private static readonly Code[] Pattern_Stloc_C = new Code[] {
			Code.Ldarg_0, Code.Ldc_I4, Code.Call, Code.Ret // Code.Ldc_I4 changes depending on _C
		};

		/// <summary>
		/// OpCode pattern seen in the Stloc_C helper method.
		/// </summary>
		/// <remarks>Found at the head of the method body</remarks>
		private static readonly Code[] Pattern_Helper_Stloc_C = new Code[] {
			Code.Ldarg_0, Code.Call, Code.Stloc_0, Code.Ldloc_0, Code.Isinst,
		};

		private static Boolean Is_Stloc_C(VirtualOpCode ins, Code code)
		{
			return ins.MatchesEntire(ins.ModifyPattern(Pattern_Stloc_C, Code.Ldc_I4, code))
				&& Helpers.FindOpCodePatterns( // Check called method against Pattern_Helper_Stloc_C
					 ((MethodDef)ins.DelegateMethod.Body.Instructions[2].Operand).Body.Instructions,
					 Pattern_Helper_Stloc_C
				   ).Count > 0;
		}

		[Detect(Code.Stloc_0)]
		public static Boolean Is_Stloc_0(this VirtualOpCode ins)
		{
			return Is_Stloc_C(ins, Code.Ldc_I4_0);
		}

		[Detect(Code.Stloc_1)]
		public static Boolean Is_Stloc_1(this VirtualOpCode ins)
		{
			return Is_Stloc_C(ins, Code.Ldc_I4_1);
		}

		[Detect(Code.Stloc_2)]
		public static Boolean Is_Stloc_2(this VirtualOpCode ins)
		{
			return Is_Stloc_C(ins, Code.Ldc_I4_2);
		}

		[Detect(Code.Stloc_3)]
		public static Boolean Is_Stloc_3(this VirtualOpCode ins)
		{
			return Is_Stloc_C(ins, Code.Ldc_I4_3);
		}

		/// <summary>
		/// OpCode pattern seen in the Stloc, Stloc_S delegate methods.
		/// </summary>
		private static readonly Code[] Pattern_Stloc = new Code[] {
			Code.Ldarg_1, Code.Castclass, Code.Stloc_0, Code.Ldarg_0, Code.Ldloc_0,
			Code.Callvirt, Code.Call, Code.Ret
		};

		private static Boolean _Is_Stloc(VirtualOpCode ins, String indexTypeName)
		{
			return ins.MatchesEntire(Pattern_Stloc)
				&& ((MethodDef)ins.DelegateMethod.Body.Instructions[5].Operand)
				   .ReturnType.FullName.Equals(indexTypeName)
				&& Helpers.FindOpCodePatterns( // Check called method against Pattern_Helper_Stloc_C
					 ((MethodDef)ins.DelegateMethod.Body.Instructions[6].Operand).Body.Instructions,
					 Pattern_Helper_Stloc_C
				   ).Count > 0;
		}

		[Detect(Code.Stloc)]
		public static Boolean Is_Stloc(this VirtualOpCode ins)
		{
			return _Is_Stloc(ins, "System.UInt16");
		}

		[Detect(Code.Stloc_S)]
		public static Boolean Is_Stloc_S(this VirtualOpCode ins)
		{
			return _Is_Stloc(ins, "System.Byte");
		}
	}
}
