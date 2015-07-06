using System;
using System.Collections.Generic;
using de4dot.blocks;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace eazdevirt
{
	/// <summary>
	/// Extensions for detecting original instruction type (opcode).
	/// Definitely going to need a more powerful engine for this.
	/// </summary>
	public static class Extensions
	{
		/// <summary>
		/// Attempt to identify a virtual instruction with its original CIL opcode.
		/// </summary>
		/// <param name="ins">Virtual instruction</param>
		/// <exception cref="OriginalOpcodeUnknownException">Thrown if unable to identify original CIL opcode</exception>
		/// <returns>CIL opcode</returns>
		public static Code Identify(this EazVirtualInstruction ins)
		{
			if (ins.Is_Add())
				return Code.Add;
			else if (ins.Is_Add_Ovf())
				return Code.Add_Ovf;
			else if (ins.Is_Add_Ovf_Un())
				return Code.Add_Ovf_Un;
			else if (ins.Is_And())
				return Code.And;
			else if (ins.Is_Bge())
				return Code.Bge;
			else if (ins.Is_Blt())
				return Code.Blt;
			else if (ins.Is_Clt())
				return Code.Clt;
			else if (ins.Is_Div())
				return Code.Div;
			else if (ins.Is_Div_Un())
				return Code.Div_Un;
			else if (ins.Is_Shl())
				return Code.Shl;
			else if (ins.Is_Shr())
				return Code.Shr;
			else if (ins.Is_Shr_Un())
				return Code.Shr_Un;
			else if (ins.Is_Sub())
				return Code.Sub;
			else if (ins.Is_Sub_Ovf())
				return Code.Sub_Ovf;
			else if (ins.Is_Sub_Ovf_Un())
				return Code.Sub_Ovf_Un;
			else if (ins.Is_Xor())
				return Code.Xor;

			throw new OriginalOpcodeUnknownException(ins);
		}

		public static Boolean TryIdentify(this EazVirtualInstruction ins, out Code code)
		{
			try
			{
				code = ins.Identify();
				return true;
			}
			catch (OriginalOpcodeUnknownException)
			{
				code = Code.UNKNOWN2;
				return false;
			}
		}

		public static Boolean Is_And(this EazVirtualInstruction ins)
		{
			return ins.MatchesIndirect(
				new Code[] { Code.Ldloc_S, Code.Ldloc_S, Code.And, Code.Callvirt, Code.Ldloc_0, Code.Ret }
			);
		}

		public static Boolean Is_Xor(this EazVirtualInstruction ins)
		{
			return ins.MatchesIndirect(
				new Code[] { Code.Ldloc_S, Code.Ldloc_S, Code.Xor, Code.Callvirt, Code.Ldloc_0, Code.Ret }
			);
		}

		public static Boolean Is_Shl(this EazVirtualInstruction ins)
		{
			return ins.MatchesIndirect(
				new Code[] { Code.Ldloc_S, Code.Ldloc_S, Code.Ldc_I4_S, Code.And, Code.Shl, Code.Stloc_S,
				Code.Newobj, Code.Stloc_0, Code.Ldloc_0, Code.Ldloc_S, Code.Callvirt, Code.Ldloc_0, Code.Ret }
			);
		}

		/// <summary>
		/// OpCode pattern seen in the Shr_* helper method.
		/// </summary>
		private static readonly Code[] Pattern_Shr = new Code[] {
			Code.Ldc_I4_S, Code.And, Code.Shr, Code.Callvirt, Code.Ldloc_0, Code.Ret
		};

		public static Boolean Is_Shr(this EazVirtualInstruction ins)
		{
			return ins.MatchesIndirectWithBoolean(true, Pattern_Shr);
		}

		public static Boolean Is_Shr_Un(this EazVirtualInstruction ins)
		{
			return ins.MatchesIndirectWithBoolean(false, Pattern_Shr);
		}

		/// <summary>
		/// OpCode pattern seen in the Sub_* helper method.
		/// </summary>
		private static readonly Code[] Pattern_Sub = new Code[] {
			Code.Ldloc_0, Code.Ldloc_1, Code.Sub, Code.Stloc_2, Code.Newobj, Code.Stloc_3,
			Code.Ldloc_3, Code.Ldloc_2, Code.Callvirt, Code.Ldloc_3, Code.Ret
		};

		public static Boolean Is_Sub(this EazVirtualInstruction ins)
		{
			return ins.MatchesIndirectWithBoolean2(false, false, Pattern_Sub);
		}

		public static Boolean Is_Sub_Ovf(this EazVirtualInstruction ins)
		{
			return ins.MatchesIndirectWithBoolean2(true, false, Pattern_Sub);
		}

		public static Boolean Is_Sub_Ovf_Un(this EazVirtualInstruction ins)
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

		public static Boolean Is_Add(this EazVirtualInstruction ins)
		{
			return ins.MatchesIndirectWithBoolean2(false, false, Pattern_Add);
		}

		public static Boolean Is_Add_Ovf(this EazVirtualInstruction ins)
		{
			return ins.MatchesIndirectWithBoolean2(true, false, Pattern_Add);
		}

		public static Boolean Is_Add_Ovf_Un(this EazVirtualInstruction ins)
		{
			return ins.MatchesIndirectWithBoolean2(true, true, Pattern_Add);
		}

		/// <summary>
		/// OpCode pattern seen in the Less-Than helper method.
		/// Used in: Clt, Blt, Bge (negated)
		/// </summary>
		private static readonly Code[] Pattern_Clt = new Code[] {
			Code.Ldloc_S, Code.Ldloc_S, Code.Blt_S,
			Code.Ldloc_S, Code.Call, Code.Brtrue_S, // System.Double::IsNaN(float64)
			Code.Ldloc_S, Code.Call, Code.Br_S      // System.Double::IsNaN(float64)
		};

		public static Boolean Is_Clt(this EazVirtualInstruction ins)
		{
			return ins.Matches(new Code[] {
				Code.Call, Code.Brtrue_S, Code.Ldc_I4_0, Code.Br_S, Code.Ldc_I4_1,
				Code.Callvirt, Code.Ldloc_2, Code.Call, Code.Ret
			}) && ins.MatchesIndirect(Pattern_Clt);
		}

		public static Boolean Is_Bge(this EazVirtualInstruction ins)
		{
			return ins.Matches(new Code[] {
				Code.Call, Code.Brtrue_S, Code.Ldarg_1, Code.Castclass
			}) && ins.MatchesIndirect(Pattern_Clt);
		}

		public static Boolean Is_Blt(this EazVirtualInstruction ins)
		{
			return ins.Matches(new Code[] {
				Code.Call, Code.Brfalse_S, Code.Ldarg_1, Code.Castclass
			}) && ins.MatchesIndirect(Pattern_Clt);
		}

		/// <summary>
		/// OpCode pattern seen in the Div_* helper method.
		/// </summary>
		private static readonly Code[] Pattern_Div = new Code[] {
			Code.Ldloc_S, Code.Ldloc_S, Code.Div, Code.Callvirt, Code.Ldloc_0, Code.Ret
		};

		public static Boolean Is_Div(this EazVirtualInstruction ins)
		{
			return ins.MatchesIndirectWithBoolean(false, Pattern_Div);
		}

		public static Boolean Is_Div_Un(this EazVirtualInstruction ins)
		{
			return ins.MatchesIndirectWithBoolean(true, Pattern_Div);
		}
	}
}
