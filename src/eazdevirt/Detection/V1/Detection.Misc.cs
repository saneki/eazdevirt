using System;
using System.Linq;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using eazdevirt.Reflection;
using eazdevirt.Util;

namespace eazdevirt.Detection.V1.Ext
{
	public static partial class Extensions
	{
		[Detect(Code.Box)]
		public static Boolean Is_Box(this EazVirtualInstruction ins)
		{
			return ins.Matches(new Code[] {
				Code.Ldarg_1, Code.Castclass, Code.Callvirt, Code.Stloc_2, Code.Ldarg_0,
				Code.Ldloc_2, Code.Call, Code.Stloc_0, Code.Ldarg_0, Code.Call
			});
		}

		[Detect(Code.Call)]
		public static Boolean Is_Call(this EazVirtualInstruction ins)
		{
			return ins.MatchesEntire(new Code[] {
				Code.Ldarg_1, Code.Castclass, Code.Stloc_0, Code.Ldarg_0, Code.Ldloc_0,
				Code.Callvirt, Code.Call, Code.Stloc_1, Code.Ldarg_0, Code.Ldloc_1,
				Code.Ldc_I4_0, Code.Call, Code.Ret
			});
		}

		[Detect(Code.Callvirt)]
		public static Boolean Is_Callvirt(this EazVirtualInstruction ins)
		{
			MethodDef method;
			var sub = ins.Find(new Code[] {
				Code.Ldarg_1, Code.Castclass, Code.Stloc_S, Code.Ldarg_0, Code.Ldloc_S,
				Code.Callvirt, Code.Call, Code.Stloc_0, Code.Ldarg_0, Code.Ldfld, Code.Brfalse_S
			});
			return sub != null
				&& (method = sub[6].Operand as MethodDef) != null
				&& method.HasReturnType && method.ReturnType.FullName.Equals("System.Reflection.MethodBase");
		}

		/// <summary>
		/// OpCode pattern seen in the Less-Than helper method.
		/// Used in: Clt, Blt, Bge_Un (negated)
		/// </summary>
		private static readonly Code[] Pattern_Clt = new Code[] {
			Code.Ldloc_S, Code.Ldloc_S, Code.Blt_S,
			Code.Ldloc_S, Code.Call, Code.Brtrue_S, // System.Double::IsNaN(float64)
			Code.Ldloc_S, Code.Call, Code.Br_S      // System.Double::IsNaN(float64)
		};

		[Detect(Code.Clt)]
		public static Boolean Is_Clt(this EazVirtualInstruction ins)
		{
			return ins.Matches(new Code[] {
				Code.Call, Code.Brtrue_S, Code.Ldc_I4_0, Code.Br_S, Code.Ldc_I4_1,
				Code.Callvirt, Code.Ldloc_2, Code.Call, Code.Ret
			}) && ins.MatchesIndirect(Pattern_Clt);
		}

		/// <summary>
		/// OpCode pattern seen in the Greater-Than helper method.
		/// Used in: Cgt
		/// </summary>
		/// <remarks>Greater-than for Double, Int32, Int64 but not-equal for other?</remarks>
		private static readonly Code[] Pattern_Cgt = new Code[] {
			Code.Ldarg_0, Code.Castclass, Code.Callvirt, Code.Ldarg_1,
			Code.Castclass, Code.Callvirt, Code.Cgt_Un, Code.Stloc_0
		};

		[Detect(Code.Cgt)]
		/// <remarks>Unsure</remarks>
		public static Boolean Is_Cgt(this EazVirtualInstruction ins)
		{
			return ins.Matches(new Code[] {
				Code.Call, Code.Brtrue_S, Code.Ldc_I4_0, Code.Br_S, Code.Ldc_I4_1,
				Code.Callvirt, Code.Ldloc_2, Code.Call, Code.Ret
			}) && ins.MatchesIndirect(Pattern_Cgt);
		}

		[Detect(Code.Ckfinite)]
		public static Boolean Is_Ckfinite(this EazVirtualInstruction ins)
		{
			var sub = ins.Find(new Code[] {
				Code.Ldloc_0, Code.Callvirt, Code.Call, Code.Brtrue_S,
				Code.Ldloc_0, Code.Callvirt, Code.Call, Code.Brfalse_S,
				Code.Ldstr, Code.Newobj, Code.Throw
			});

			return sub != null
				&& ((IMethod)sub[2].Operand).FullName.Contains("System.Double::IsNaN")
				&& ((IMethod)sub[6].Operand).FullName.Contains("System.Double::IsInfinity");
		}

		[Detect(Code.Dup)]
		public static Boolean Is_Dup(this EazVirtualInstruction ins)
		{
			return ins.DelegateMethod.MatchesEntire(
				Code.Ldarg_0, Code.Call, Code.Stloc_0, Code.Ldloc_0, Code.Callvirt,
				Code.Stloc_1, Code.Ldarg_0, Code.Ldloc_0, Code.Call, Code.Ldarg_0,
				Code.Ldloc_1, Code.Call, Code.Ret
			);
		}

		[Detect(Code.Endfinally)]
		public static Boolean Is_Endfinally(this EazVirtualInstruction ins)
		{
			return ins.DelegateMethod.MatchesEntire(
				Code.Ldarg_0, Code.Call, Code.Ret
			) && ins.DelegateMethod.MatchesIndirect(
				Code.Ldarg_0, Code.Ldfld, Code.Callvirt, Code.Ldarg_0, Code.Ldloc_0,
				Code.Callvirt, Code.Call, Code.Ret
			);
		}

		/// <summary>
		/// OpCode pattern seen in the Throw, Rethrow helper methods.
		/// </summary>
		public static readonly Code[] Pattern_Throw = new Code[] {
			Code.Ldarg_0, Code.Isinst, Code.Stloc_0, Code.Ldloc_0,
			Code.Call, Code.Ldarg_0, Code.Call, Code.Ret
		};

		public static Boolean _Is_Throw(EazVirtualInstruction ins, MethodDef helper)
		{
			var matches = Helpers.FindOpCodePatterns(helper.Body.Instructions, Pattern_Throw);
			return matches.Count == 1 && matches[0].Length == Pattern_Throw.Length;
		}

		[Detect(Code.Throw)]
		public static Boolean Is_Throw(this EazVirtualInstruction ins)
		{
			return ins.MatchesEntire(new Code[] {
				Code.Ldarg_0, Code.Call, Code.Stloc_0, Code.Ldloc_0,
				Code.Callvirt, Code.Call, Code.Ret
			}) && _Is_Throw(ins, ((MethodDef)ins.DelegateMethod.Body.Instructions[5].Operand));
		}

		[Detect(Code.Rethrow)]
		public static Boolean Is_Rethrow(this EazVirtualInstruction ins)
		{
			var sub = ins.Find(new Code[] {
				Code.Newobj, Code.Throw, Code.Ldarg_0, Code.Ldarg_0, Code.Ldfld,
				Code.Callvirt, Code.Callvirt, Code.Stfld, Code.Ldarg_0, Code.Ldfld,
				Code.Call, Code.Ret
			});
			return sub != null && _Is_Throw(ins, ((MethodDef)sub[10].Operand));
		}

		[Detect(Code.Ldfld)]
		public static Boolean Is_Ldfld(this EazVirtualInstruction ins)
		{
			return ins.Matches(new Code[] {
				Code.Ldarg_0, Code.Ldloc_1, Code.Ldloc_3, Code.Callvirt, Code.Ldloc_1,
				Code.Callvirt, Code.Call, Code.Call, Code.Ret
			}) && ins.DelegateMethod.Calls().Any((called) =>
			{
				return called.FullName.Contains("System.Reflection.FieldInfo::GetValue");
			});
		}

		[Detect(Code.Ldflda)]
		public static Boolean Is_Ldflda(this EazVirtualInstruction ins)
		{
			MethodDef method;
			var sub = ins.DelegateMethod.Find(new Code[] {
				Code.Ldloc_1, Code.Ldloc_S, Code.Callvirt, Code.Ldloc_1, Code.Ldloc_2,
				Code.Callvirt, Code.Ldloc_1, Code.Call, Code.Ret
			});
			return sub != null
				&& (method = (sub[2].Operand as MethodDef)) != null
				&& method.Parameters.Count == 2
				&& method.Parameters[1].Type.FullName.Contains("System.Reflection.FieldInfo");
		}

		[Detect(Code.Ldftn)]
		public static Boolean Is_Ldftn(this EazVirtualInstruction ins)
		{
			MethodDef called = null;
			var sub = ins.DelegateMethod.Find(new Code[] {
				Code.Ldarg_0, Code.Newobj, Code.Stloc_2, Code.Ldloc_2, Code.Ldloc_1,
				Code.Callvirt, Code.Ldloc_2, Code.Call, Code.Ret
			});
			return sub != null
				&& (called = ((MethodDef)sub[5].Operand)) != null
				&& called.Parameters.Count >= 2
				&& called.Parameters[1].Type.FullName.Equals("System.Reflection.MethodBase");
		}

		[Detect(Code.Ldlen)]
		public static Boolean Is_Ldlen(this EazVirtualInstruction ins)
		{
			return ins.MatchesEntire(new Code[] {
				Code.Ldarg_0, Code.Call, Code.Callvirt, Code.Castclass, Code.Stloc_0, Code.Ldarg_0,
				Code.Newobj, Code.Stloc_1, Code.Ldloc_1, Code.Ldloc_0, Code.Callvirt, Code.Callvirt,
				Code.Ldloc_1, Code.Call, Code.Ret
			}) && ((IMethod)ins.DelegateMethod.Body.Instructions[10].Operand)
				  .FullName.Contains("System.Array::get_Length");
		}

		[Detect(Code.Ldsfld)]
		public static Boolean Is_Ldsfld(this EazVirtualInstruction ins)
		{
			return ins.DelegateMethod.Matches(new Code[] {
				Code.Ldarg_0, Code.Ldloc_1, Code.Ldnull, Code.Callvirt, Code.Ldloc_1,
				Code.Callvirt, Code.Call, Code.Call, Code.Ret
			}) && ins.DelegateMethod.Calls().Any((called) =>
			{
				return called.FullName.Contains("System.Reflection.FieldInfo::GetValue");
			});
		}

		[Detect(Code.Ldsflda)]
		public static Boolean Is_Ldsflda(this EazVirtualInstruction ins)
		{
			MethodDef method;
			var sub = ins.DelegateMethod.Find(new Code[] {
				Code.Ldarg_0, Code.Newobj, Code.Stloc_2, Code.Ldloc_2, Code.Ldloc_1,
				Code.Callvirt, Code.Ldloc_2, Code.Call, Code.Ret
			});
			return sub != null
				&& (method = (sub[5].Operand as MethodDef)) != null
				&& method.Parameters.Count == 2
				&& method.Parameters[1].Type.FullName.Contains("System.Reflection.FieldInfo");
		}

		[Detect(Code.Ldobj)]
		public static Boolean Is_Ldobj(this EazVirtualInstruction ins)
		{
			return ins.DelegateMethod.MatchesEntire(new Code[] {
				Code.Ldarg_1, Code.Castclass, Code.Callvirt, Code.Stloc_0, Code.Ldarg_0,
				Code.Ldloc_0, Code.Call, Code.Stloc_1, Code.Ldarg_0, Code.Ldloc_1,
				Code.Call, Code.Ret
			}) && ins.DelegateMethod.MatchesIndirect(new Code[] {
				Code.Ldarg_0, Code.Call, Code.Stloc_0, Code.Ldarg_0, Code.Ldarg_0,
				Code.Ldloc_0, Code.Call, Code.Callvirt, Code.Ldarg_1, Code.Call,
				Code.Call, Code.Ret
			}) && ((MethodDef)ins.DelegateMethod.Body.Instructions[2].Operand)
				  .ReturnType.FullName.Equals("System.Int32")
			&& ((MethodDef)ins.DelegateMethod.Body.Instructions[6].Operand)
				  .ReturnType.FullName.Equals("System.Type");
		}

		[Detect(Code.Ldstr)]
		public static Boolean Is_Ldstr(this EazVirtualInstruction ins)
		{
			return ins.MatchesEntire(new Code[] {
				Code.Ldarg_1, Code.Castclass, Code.Callvirt, Code.Stloc_0, Code.Ldarg_0,
				Code.Ldloc_0, Code.Call, Code.Stloc_1, Code.Ldarg_0, Code.Newobj,
				Code.Stloc_2, Code.Ldloc_2, Code.Ldloc_1, Code.Callvirt, Code.Ldloc_2,
				Code.Call, Code.Ret
			}) && ((MethodDef)ins.DelegateMethod.Body.Instructions[6].Operand)
				  .ReturnType.FullName.Equals("System.String");
		}

		[Detect(Code.Ldnull)]
		public static Boolean Is_Ldnull(this EazVirtualInstruction ins)
		{
			return ins.MatchesEntire(new Code[] {
				Code.Ldarg_0, Code.Newobj, Code.Call, Code.Ret
			});
		}

		[Detect(Code.Ldtoken)]
		public static Boolean Is_Ldtoken(this EazVirtualInstruction ins)
		{
			// Checks delegate method tail
			// Could also check: System.Reflection.FieldInfo::get_Type/Field/MethodHandle(),
			// there are 1 of each of these calls
			return ins.DelegateMethod.Matches(
				Code.Ldarg_0, Code.Newobj, Code.Stloc_3, Code.Ldloc_3, Code.Ldloc_1,
				Code.Callvirt, Code.Ldloc_3, Code.Call, Code.Ret
			);
		}

		[Detect(Code.Ldvirtftn)]
		public static Boolean Is_Ldvirtftn(this EazVirtualInstruction ins)
		{
			MethodDef called = null;
			var sub = ins.DelegateMethod.Find(new Code[] {
				Code.Ldarg_0, Code.Newobj, Code.Stloc_S, Code.Ldloc_S, Code.Ldloc_3,
				Code.Callvirt, Code.Ldloc_S, Code.Call, Code.Ret
			});
			return sub != null
				&& (called = ((MethodDef)sub[5].Operand)) != null
				&& called.Parameters.Count >= 2
				&& called.Parameters[1].Type.FullName.Equals("System.Reflection.MethodBase");
		}

		[Detect(Code.Leave)]
		public static Boolean Is_Leave(this EazVirtualInstruction ins)
		{
			return ins.DelegateMethod.MatchesEntire(
				Code.Ldarg_1, Code.Castclass, Code.Callvirt, Code.Stloc_0, Code.Ldarg_0,
				Code.Ldnull, Code.Ldloc_0, Code.Call, Code.Ret
			);
		}

		[Detect(Code.Newarr)]
		public static Boolean Is_Newarr(this EazVirtualInstruction ins)
		{
			var sub = ins.DelegateMethod.Find(
				Code.Ldloc_S, Code.Ldloc_1, Code.Call, Code.Stloc_S
			);
			return sub != null
				&& ((IMethod)sub[2].Operand).FullName.Contains("System.Array::CreateInstance");
		}

		[Detect(Code.Newobj)]
		public static Boolean Is_Newobj(this EazVirtualInstruction ins)
		{
			return ins.Matches(new Code[] {
				Code.Ldarg_0, Code.Ldloc_2, Code.Ldnull, Code.Ldloc_3, Code.Ldc_I4_0,
				Code.Call, Code.Stloc_S, Code.Leave_S
			});
		}

		[Detect(Code.Nop, ExpectsMultiple = true)]
		public static Boolean Is_Nop(this EazVirtualInstruction ins)
		{
			// Three virtual opcodes match this. One of them makes sense to be Nop,
			// unsure what the other two are (maybe Endfault, Endfilter).
			OperandType operandType;
			return ins.DelegateMethod.MatchesEntire(Code.Ret)
				&& ins.TryGetOperandType(out operandType)
				&& operandType == OperandType.InlineNone;
		}

		[Detect(Code.Pop)]
		public static Boolean Is_Pop(this EazVirtualInstruction ins)
		{
			MethodDef method = null;
			return ins.MatchesEntire(new Code[] {
				Code.Ldarg_0, Code.Call, Code.Pop, Code.Ret
			}) && (method = ins.DelegateMethod.Body.Instructions[1].Operand as MethodDef) != null
			   && method.MatchesEntire(new Code[] {
				   Code.Ldarg_0, Code.Ldfld, Code.Callvirt, Code.Ret
			   });
		}

		[Detect(Code.Ret)]
		public static Boolean Is_Ret(this EazVirtualInstruction ins)
		{
			return ins.MatchesEntire(new Code[] {
				Code.Ldarg_0, Code.Call, Code.Ret
			}) && ((MethodDef)ins.DelegateMethod.Body.Instructions[1].Operand).MatchesEntire(new Code[] {
				Code.Ldarg_0, Code.Ldc_I4_1, Code.Stfld, Code.Ret
			});
		}

		[Detect(Code.Stfld)]
		public static Boolean Is_Stfld(this EazVirtualInstruction ins)
		{
			return ins.Matches(new Code[] {
				Code.Ldarg_0, Code.Ldloc_1, Code.Ldloc_0, Code.Ldnull, Code.Call,
				Code.Call, Code.Ret
			}) && ins.DelegateMethod.Calls().Any((called) =>
			{
				return called.FullName.Contains("System.Reflection.FieldInfo::SetValue");
			});
		}

		[Detect(Code.Stsfld)]
		public static Boolean Is_Stsfld(this EazVirtualInstruction ins)
		{
			return ins.Matches(new Code[] {
				Code.Ldloc_1, Code.Ldnull, Code.Ldloc_3, Code.Callvirt, Code.Callvirt,
				Code.Ret
			}) && ins.DelegateMethod.Calls().Any((called) =>
			{
				return called.FullName.Contains("System.Reflection.FieldInfo::SetValue");
			});
		}

		[Detect(Code.Switch)]
		public static Boolean Is_Switch(this EazVirtualInstruction ins)
		{
			return ins.DelegateMethod.Matches(
				Code.Blt_S, Code.Ret, Code.Ldloc_3, Code.Ldloc_2, Code.Conv_U, Code.Ldelem,
				Code.Callvirt, Code.Stloc_S, Code.Ldarg_0, Code.Ldloc_S, Code.Call, Code.Ret
			);
		}

		[Detect(Code.Unbox)]
		public static Boolean Is_Unbox(this EazVirtualInstruction ins)
		{
			OperandType operandType;
			return ins.DelegateMethod.MatchesEntire(Code.Ret)
				&& ins.TryGetOperandType(out operandType)
				&& operandType == OperandType.InlineType;
		}
	}
}
