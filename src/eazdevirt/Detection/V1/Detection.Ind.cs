using System;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using eazdevirt.Reflection;
using eazdevirt.Util;

namespace eazdevirt.Detection.V1.Ext
{
	public static partial class Extensions
	{
		[Detect(Code.Ldind_I)]
		public static Boolean Is_Ldind_I(this EazVirtualInstruction ins)
		{
			return ins.DelegateMethod.MatchesEntire(
				Code.Ldarg_0, Code.Ldsfld, Code.Call, Code.Ret
			) && ((FieldDef)ins.DelegateMethod.Body.Instructions[1].Operand)
				 .MDToken == ins.Virtualization.GetTypeField("System.IntPtr").MDToken
			&& ins.DelegateMethod.MatchesIndirect(
				Code.Call, Code.Callvirt, Code.Ldarg_1, Code.Call, Code.Call, Code.Ret
			);
		}

		[Detect(Code.Ldind_Ref)]
		public static Boolean Is_Ldind_Ref(this EazVirtualInstruction ins)
		{
			// Same as Ldind_I, but different static field. Just check if NOT Ldind_I.
			return ins.DelegateMethod.MatchesEntire(
				Code.Ldarg_0, Code.Ldsfld, Code.Call, Code.Ret
			) && ins.DelegateMethod.MatchesIndirect(
				Code.Call, Code.Callvirt, Code.Ldarg_1, Code.Call, Code.Call, Code.Ret
			) && !Is_Ldind_I(ins);
		}

		public static Boolean _Is_Ldind_IC(EazVirtualInstruction ins, String tokenTypeName)
		{
			TypeRef tokenType = null;
			return ins.DelegateMethod.MatchesEntire(
				Code.Ldarg_0, Code.Ldtoken, Code.Call, Code.Call, Code.Ret
			) && ins.DelegateMethod.MatchesIndirect(
				Code.Ldarg_0, Code.Call, Code.Stloc_0, Code.Ldarg_0, Code.Ldarg_0, Code.Ldloc_0,
				Code.Call, Code.Callvirt, Code.Ldarg_1, Code.Call, Code.Call, Code.Ret
			) && (tokenType = ins.DelegateMethod.Body.Instructions[1].Operand as TypeRef) != null
			&& tokenType.FullName.Equals(tokenTypeName);
		}

		[Detect(Code.Ldind_I1)]
		public static Boolean Is_Ldind_I1(this EazVirtualInstruction ins)
		{
			return _Is_Ldind_IC(ins, "System.SByte");
		}

		[Detect(Code.Ldind_I2)]
		public static Boolean Is_Ldind_I2(this EazVirtualInstruction ins)
		{
			return _Is_Ldind_IC(ins, "System.Int16");
		}

		[Detect(Code.Ldind_I4)]
		public static Boolean Is_Ldind_I4(this EazVirtualInstruction ins)
		{
			return _Is_Ldind_IC(ins, "System.Int32");
		}

		[Detect(Code.Ldind_I8)]
		public static Boolean Is_Ldind_I8(this EazVirtualInstruction ins)
		{
			return _Is_Ldind_IC(ins, "System.Int64");
		}

		// Conflict
		[Detect(Code.Ldind_R4)]
		public static Boolean Is_Ldind_R4(this EazVirtualInstruction ins)
		{
			return _Is_Ldind_IC(ins, "System.Single");
		}

		// Conflict
		[Detect(Code.Ldind_R8)]
		public static Boolean Is_Ldind_R8(this EazVirtualInstruction ins)
		{
			return _Is_Ldind_IC(ins, "System.Double");
		}

		[Detect(Code.Ldind_U1)]
		public static Boolean Is_Ldind_U1(this EazVirtualInstruction ins)
		{
			return _Is_Ldind_IC(ins, "System.Byte");
		}

		[Detect(Code.Ldind_U2)]
		public static Boolean Is_Ldind_U2(this EazVirtualInstruction ins)
		{
			return _Is_Ldind_IC(ins, "System.UInt16");
		}

		[Detect(Code.Ldind_U4)]
		public static Boolean Is_Ldind_U4(this EazVirtualInstruction ins)
		{
			return _Is_Ldind_IC(ins, "System.UInt32");
		}

		/// <remarks>
		/// All Stind_* delegate methods follow this pattern. They have different delegate
		/// methods, but their delegate methods all just call the same method with no params.
		///
		/// One way to attack this: Devirtualize all as Stind_I, then go back through the
		/// method afterwards, following the types on the stack. When a Stind_* instruction
		/// is reached, the value (on top of the stack) is the type being set.
		/// </remarks>
		private static Boolean _Is_Stind(EazVirtualInstruction ins)
		{
			return ins.DelegateMethod.MatchesEntire(Code.Ldarg_0, Code.Call, Code.Ret)
				&& ins.DelegateMethod.MatchesIndirect(
				Code.Ldarg_0, Code.Call, Code.Stloc_0, Code.Ldarg_0, Code.Call,
				Code.Stloc_1, Code.Ldarg_0, Code.Ldloc_1, Code.Ldloc_0, Code.Call,
				Code.Ret
			);
		}
	}
}
