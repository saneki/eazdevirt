using System;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using eazdevirt.Reflection;

namespace eazdevirt.Detection.V1.Ext
{
	public static partial class Extensions
	{
		/// <summary>
		/// OpCode pattern seen in the Starg_* delegate methods.
		/// </summary>
		private static readonly Code[] Pattern_Starg = new Code[] {
			Code.Ldarg_0, Code.Ldfld, Code.Ldloc_0, Code.Callvirt, Code.Ldelem
		};

		/// <summary>
		/// OpCode pattern seen at the "tail" of the Starg_* delegate methods.
		/// </summary>
		/// <remarks>
		/// Without this tail check, multiple delegate methods were being associated
		/// with both Starg and Starg_S
		/// </remarks>
		private static readonly Code[] Pattern_Tail_Starg = new Code[] {
			Code.Callvirt, Code.Pop, Code.Ret
		};

		[Detect(Code.Starg)]
		public static Boolean Is_Starg(this VirtualOpCode ins)
		{
			var sub = ins.Find(Pattern_Starg);
			return sub != null && sub[3].Operand is MethodDef
				&& ((MethodDef)sub[3].Operand).ReturnType.FullName.Equals("System.UInt16")
				&& ins.Matches(Pattern_Tail_Starg);
		}

		[Detect(Code.Starg_S)]
		public static Boolean Is_Starg_S(this VirtualOpCode ins)
		{
			var sub = ins.Find(Pattern_Starg);
			return sub != null && sub[3].Operand is MethodDef
				&& ((MethodDef)sub[3].Operand).ReturnType.FullName.Equals("System.Byte")
				&& ins.Matches(Pattern_Tail_Starg);
		}

		/// <summary>
		/// OpCode pattern seen in the Ldarg_C delegate methods (Ldarg_0, Ldarg_1, Ldarg_2, Ldarg_3).
		/// </summary>
		private static readonly Code[] Pattern_Ldarg_C = new Code[] {
			Code.Ldarg_0, Code.Ldarg_0, Code.Ldfld, Code.Ldc_I4, Code.Ldelem, // Code.Ldc_I4 changes depending on _C
			Code.Callvirt, Code.Call, Code.Ret
		};

		private static Boolean Is_Ldarg_C(VirtualOpCode ins, Code code)
		{
			// Ldarg_C delegates will reference the arguments field in their Ldfld, which sets them apart from
			// other very similar delegates
			return ins.Matches(ins.ModifyPattern(Pattern_Ldarg_C, Code.Ldc_I4, code))
				&& ((FieldDef)ins.DelegateMethod.Body.Instructions[2].Operand).MDToken == ins.Virtualization.ArgumentsField.MDToken;
		}

		[Detect(Code.Ldarg_0)]
		public static Boolean Is_Ldarg_0(this VirtualOpCode ins)
		{
			return Is_Ldarg_C(ins, Code.Ldc_I4_0);
		}

		[Detect(Code.Ldarg_1)]
		public static Boolean Is_Ldarg_1(this VirtualOpCode ins)
		{
			return Is_Ldarg_C(ins, Code.Ldc_I4_1);
		}

		[Detect(Code.Ldarg_2)]
		public static Boolean Is_Ldarg_2(this VirtualOpCode ins)
		{
			return Is_Ldarg_C(ins, Code.Ldc_I4_2);
		}

		[Detect(Code.Ldarg_3)]
		public static Boolean Is_Ldarg_3(this VirtualOpCode ins)
		{
			return Is_Ldarg_C(ins, Code.Ldc_I4_3);
		}

		/// <summary>
		/// OpCode pattern seen in the Ldarga, Ldarga_S delegate methods.
		/// </summary>
		private static readonly Code[] Pattern_Ldarga = new Code[] {
			Code.Ldloc_1, Code.Ldarg_0, Code.Ldfld, Code.Ldloc_0, Code.Callvirt,
			Code.Ldelem, Code.Callvirt, Code.Ldloc_1, Code.Call, Code.Ret
		};

		[Detect(Code.Ldarga)]
		/// <remarks>Unsure</remarks>
		public static Boolean Is_Ldarga(this VirtualOpCode ins)
		{
			var sub = ins.Find(Pattern_Ldarga);
			return sub != null
				&& ((FieldDef)sub[2].Operand).MDToken == ins.Virtualization.ArgumentsField.MDToken
				&& ((MethodDef)sub[4].Operand).ReturnType.FullName.Equals("System.UInt16");
		}

		[Detect(Code.Ldarga_S)]
		/// <remarks>Unsure</remarks>
		public static Boolean Is_Ldarga_S(this VirtualOpCode ins)
		{
			var sub = ins.Find(Pattern_Ldarga);
			return sub != null
				&& ((FieldDef)sub[2].Operand).MDToken == ins.Virtualization.ArgumentsField.MDToken
				&& ((MethodDef)sub[4].Operand).ReturnType.FullName.Equals("System.Byte");
		}

		/// <summary>
		/// OpCode pattern seen in the Ldarg, Ldarg_S delegate methods.
		/// </summary>
		/// <remarks>There are other delegate methods that match this exact pattern.</remarks>
		private static readonly Code[] Pattern_Ldarg = new Code[] {
			Code.Ldarg_1, Code.Castclass, Code.Stloc_0, Code.Ldarg_0, Code.Ldarg_0,
			Code.Ldfld, Code.Ldloc_0, Code.Callvirt, Code.Ldelem, Code.Callvirt,
			Code.Call, Code.Ret
		};

		[Detect(Code.Ldarg)]
		public static Boolean Is_Ldarg(this VirtualOpCode ins)
		{
			return ins.MatchesEntire(Pattern_Ldarg)
				&& ((MethodDef)ins.DelegateMethod.Body.Instructions[7].Operand)
				   .ReturnType.FullName.Equals("System.UInt16")
				&& ((FieldDef)ins.DelegateMethod.Body.Instructions[5].Operand)
				   .MDToken == ins.Virtualization.ArgumentsField.MDToken;
		}

		[Detect(Code.Ldarg_S)]
		public static Boolean Is_Ldarg_S(this VirtualOpCode ins)
		{
			return ins.MatchesEntire(Pattern_Ldarg)
				&& ((MethodDef)ins.DelegateMethod.Body.Instructions[7].Operand)
				   .ReturnType.FullName.Equals("System.Byte")
				&& ((FieldDef)ins.DelegateMethod.Body.Instructions[5].Operand)
				   .MDToken == ins.Virtualization.ArgumentsField.MDToken;
		}
	}
}
