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
		public static Boolean _Jumps(VirtualOpCode ins)
		{
			return ins.DelegateMethod.Calls().Any((called) =>
			{
				MethodDef method = called as MethodDef;
				if (method == null)
					return false;

				return method.MatchesEntire(new Code[] {
					Code.Ldarg_0, Code.Ldarg_1, Code.Newobj, Code.Stfld, Code.Ret
				}) && ((IMethod)method.Body.Instructions[2].Operand).FullName.Contains("System.Nullable");
			});
		}

		[Detect(Code.Br)]
		public static Boolean Is_Br(this VirtualOpCode ins)
		{
			MethodDef called;
			return ins.MatchesEntire(new Code[] {
				Code.Ldarg_1, Code.Castclass, Code.Callvirt, Code.Stloc_0, Code.Ldarg_0,
				Code.Ldloc_0, Code.Call, Code.Ret
			}) && (called = (MethodDef)ins.DelegateMethod.Calls().ToArray()[1]).MatchesEntire(new Code[] {
				Code.Ldarg_0, Code.Ldarg_1, Code.Newobj, Code.Stfld, Code.Ret
			}) && ((IMethod)called.Body.Instructions[2].Operand).FullName.Contains("System.Nullable");

		}

		[Detect(Code.Brfalse)]
		public static Boolean Is_Brfalse(this VirtualOpCode ins)
		{
			return ins.Matches(new Code[] {
				Code.Ldloc_0, Code.Callvirt, Code.Ldnull, Code.Ceq, Code.Stloc_1    //should work for both cleaned and uncleaned
			});
		}

		[Detect(Code.Brtrue)]
		public static Boolean Is_Brtrue(this VirtualOpCode ins)
		{
			return ins.Matches(new Code[] {
			    Code.Ldloc_0, Code.Callvirt, Code.Ldnull, Code.Cgt_Un, Code.Stloc_1 //should work for both cleaned and uncleaned
            });
			//var sub = ins.DelegateMethod.Find(new Code[] {
			//	Code.Ldloc_0, Code.Castclass, Code.Callvirt, Code.Ldsfld, Code.Call,
			//	Code.Stloc_1, Code.Br_S
			//});
			//return sub != null
			//	&& ((IMethod)sub[4].Operand).FullName.Contains("System.UIntPtr::op_Inequality");
		}

		/// <summary>
		/// OpCode pattern seen in the Beq, Bne_Un helper methods.
		/// </summary>
		private static readonly Code[] Pattern_Br_Equality_49 = new Code[] {
			Code.Ldloc_1, Code.Callvirt, Code.Call, Code.Ldarg_1, Code.Callvirt,
			Code.Call, Code.Ceq, Code.Stloc_0, Code.Ldloc_0, Code.Ret
		};

		/// <summary>
		/// Pattern_Br_Equality_49 updated for 5.0.
		/// </summary>
		private static readonly Code[] Pattern_Br_Equality_50 = new Code[] {
			Code.Ceq, Code.Stloc_0, Code.Br_S, Code.Ldarg_0, Code.Castclass, Code.Stloc_S,
			Code.Ldarg_1, Code.Castclass, Code.Stloc_S, Code.Ldloc_S, Code.Ldloc_S,
			Code.Callvirt, Code.Stloc_0, Code.Ldloc_0, Code.Ret
		};

		/// <summary>
		/// OpCode pattern seen in certain branch delegate methods.
		/// </summary>
		/// <remarks>
		/// Looks like:
		///
		/// StackType type1 = this.Pop();
		/// StackType type2 = this.Pop();
		/// if(Compare(type1, type2))
		///		this.Position = Operand;
		/// </remarks>
		private static readonly Code[] Pattern_Br_True = new Code[] {
			Code.Ldarg_0, Code.Call, Code.Stloc_0, Code.Ldarg_0, Code.Call, Code.Stloc_1,
			Code.Ldloc_1, Code.Ldloc_0, Code.Call, Code.Brfalse_S
		};

		/// <summary>
		/// OpCode pattern seen in certain branch delegate methods.
		/// </summary>
		/// <remarks>
		/// Looks like:
		///
		/// StackType type1 = this.Pop();
		/// StackType type2 = this.Pop();
		/// if(!Compare(type1, type2))
		///		this.Position = Operand;
		/// </remarks>
		private static readonly Code[] Pattern_Br_False = new Code[] {
			Code.Ldarg_0, Code.Call, Code.Stloc_0, Code.Ldarg_0, Code.Call, Code.Stloc_1,
			Code.Ldloc_1, Code.Ldloc_0, Code.Call, Code.Brtrue_S
		};

		private static Boolean _Is_Br_Equality(VirtualOpCode ins)
		{
			return ins.DelegateMethod.MatchesIndirect(Pattern_Br_Equality_49)
				|| ins.DelegateMethod.MatchesIndirect(Pattern_Br_Equality_50);
		}

		[Detect(Code.Beq)]
		public static Boolean Is_Beq(this VirtualOpCode ins)
		{
			return ins.Matches(Pattern_Br_True) && _Is_Br_Equality(ins) && _Jumps(ins);
		}

		[Detect(Code.Bne_Un)]
		public static Boolean Is_Bne_Un(this VirtualOpCode ins)
		{
			return ins.Matches(Pattern_Br_False) && _Is_Br_Equality(ins) && _Jumps(ins);
		}

		/// <summary>
		/// OpCode pattern seen in the Blt helper method.
		/// </summary>
		private static readonly Code[] Pattern_LessThan = new Code[] {
			Code.Ldarg_0, Code.Castclass, Code.Callvirt, Code.Ldarg_1, Code.Castclass,
			Code.Callvirt, Code.Clt, Code.Stloc_0, Code.Ldloc_0, Code.Ret
		};

		/// <summary>
		/// OpCode pattern seen in the Bgt helper method.
		/// </summary>
		private static readonly Code[] Pattern_GreaterThan = new Code[] {
			Code.Ldloc_S, Code.Ldloc_S, Code.Cgt, Code.Stloc_0, Code.Br_S, Code.Ldc_I4_0,
			Code.Stloc_0, Code.Ldloc_0, Code.Ret
		};

		/// <summary>
		/// OpCode pattern seen in the first Ble_Un helper method.
		/// Called Pattern_Cgt_Un because of the comparison type.
		/// </summary>
		private static readonly Code[] Pattern_Ble_Un = new Code[] {
			Code.Ldloc_S, Code.Ldloc_S, Code.Cgt, Code.Stloc_0, Code.Br_S,
			Code.Ldc_I4_0, Code.Stloc_0, Code.Ldloc_0, Code.Ret
		};

		/// <summary>
		/// OpCode pattern seen in Ble, Ble_Un delegate methods. Probably others.
		/// </summary>
		private static readonly Code[] Pattern_Ble = new Code[] {
			Code.Ldloc_1, Code.Ldloc_0, Code.Call, Code.Ldc_I4_0, Code.Ceq, Code.Stloc_2, Code.Br_S,
			Code.Ldloc_1, Code.Ldloc_0, Code.Call, Code.Ldc_I4_0, Code.Ceq, Code.Stloc_2
		};

		[Detect(Code.Blt)]
		public static Boolean Is_Blt(this VirtualOpCode ins)
		{
			return ins.Matches(Pattern_Br_True) && ins.MatchesIndirect(Pattern_LessThan);
		}

		/// <summary>
		/// OpCode pattern seen in Blt_Un helper method. Probably used in others.
		/// </summary>
		/// <remarks>Seen near the end of the method</remarks>
		private static readonly Code[] Pattern_Blt_Un = new Code[] {
			Code.Ldloc_S, Code.Ldloc_S, Code.Blt_S, Code.Ldloc_S, Code.Call, Code.Brtrue_S,
			Code.Ldloc_S, Code.Call, Code.Br_S
		};

		[Detect(Code.Blt_Un)]
		public static Boolean Is_Blt_Un(this VirtualOpCode ins)
		{
			return ins.Matches(Pattern_Br_True) && ins.MatchesIndirect(Pattern_Blt_Un);
		}

		[Detect(Code.Bgt)]
		public static Boolean Is_Bgt(this VirtualOpCode ins)
		{
			return ins.Matches(Pattern_Br_True) && ins.MatchesIndirect(Pattern_GreaterThan);
		}

		[Detect(Code.Bgt_Un)]
		public static Boolean Is_Bgt_Un(this VirtualOpCode ins)
		{
			return ins.Matches(new Code[] {
				Code.Call, Code.Brfalse_S, Code.Ldarg_1, Code.Castclass
			}) && ins.MatchesIndirect(new Code[] {
				Code.Ldloc_2, Code.Ldloc_3, Code.Bgt_S, Code.Ldloc_2, Code.Call, Code.Brtrue_S,
				Code.Ldloc_3, Code.Call, Code.Br_S
			});
		}

		[Detect(Code.Ble)]
		public static Boolean Is_Ble(this VirtualOpCode ins)
		{
			return ins.Matches(new Code[] {
				Code.Call, Code.Ldc_I4_0, Code.Ceq, Code.Stloc_2
			}) && ins.MatchesIndirect(Pattern_Ble_Un);
		}

		[Detect(Code.Ble_Un)]
		public static Boolean Is_Ble_Un(this VirtualOpCode ins)
		{
			var sub = ins.DelegateMethod.Find(Pattern_Ble);
			return sub != null && ((MethodDef)sub[2].Operand).Matches(Pattern_Ble_Un);
		}

		[Detect(Code.Bge)]
		public static Boolean Is_Bge(this VirtualOpCode ins)
		{
			return ins.Matches(new Code[] {
				Code.Call, Code.Brtrue_S, Code.Ldarg_1, Code.Castclass
			}) && ins.MatchesIndirect(new Code[] {
				Code.Ldarg_0, Code.Castclass, Code.Callvirt, Code.Ldarg_1, Code.Castclass,
				Code.Callvirt, Code.Clt, Code.Stloc_0, Code.Ldloc_0, Code.Ret
			});
		}

		[Detect(Code.Bge_Un)]
		public static Boolean Is_Bge_Un(this VirtualOpCode ins)
		{
			return ins.Matches(new Code[] {
				Code.Call, Code.Brtrue_S, Code.Ldarg_1, Code.Castclass
			}) && ins.MatchesIndirect(Pattern_Clt_Un);
		}
	}
}
