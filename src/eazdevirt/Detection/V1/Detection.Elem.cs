using System;
using System.Collections.Generic;
using System.Linq;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using eazdevirt.Reflection;
using eazdevirt.Util;

namespace eazdevirt.Detection.V1.Ext
{
	public static partial class Extensions
	{
		/// <summary>
		/// OpCode pattern seen in Ldelem, Ldelem_* helper methods.
		/// </summary>
		private static readonly Code[] Pattern_Ldelem = new Code[] {
			Code.Castclass, Code.Stloc_1, Code.Ldarg_0, Code.Ldloc_1, Code.Ldloc_0, Code.Callvirt
		};

		private static Boolean _Is_Ldelem(VirtualOpCode ins)
		{
			MethodDef method;
			IList<Instruction> pattern;
			var calls = ins.DelegateMethod.Calls().ToArray();
			return calls.Length > 0
				&& (method = calls.Last() as MethodDef) != null
				&& (pattern = method.Find(Pattern_Ldelem)) != null
				&& ((ITypeDefOrRef)pattern[0].Operand).FullName.Contains("System.Array");
		}

		private static Boolean _Is_Ldelem_T(VirtualOpCode ins, String typeName)
		{
			return ins.MatchesEntire(new Code[] {
				Code.Ldarg_0, Code.Ldtoken, Code.Call, Code.Call, Code.Ret
			}) && ((ITypeDefOrRef)ins.DelegateMethod.Body.Instructions[1].Operand)
				  .FullName.Equals(typeName)
				&& _Is_Ldelem(ins);
		}

		[Detect(Code.Ldelem)]
		public static Boolean Is_Ldelem(this VirtualOpCode ins)
		{
			return ins.MatchesEntire(new Code[] {
				Code.Ldarg_1, Code.Castclass, Code.Callvirt, Code.Stloc_0, Code.Ldarg_0, Code.Ldloc_0,
				Code.Call, Code.Stloc_1, Code.Ldarg_0, Code.Ldloc_1, Code.Call, Code.Ret
			}) && _Is_Ldelem(ins);
		}

		[Detect(Code.Ldelem_I1)]
		public static Boolean Is_Ldelem_I1(this VirtualOpCode ins)
		{
			return _Is_Ldelem_T(ins, "System.SByte");
		}

		[Detect(Code.Ldelem_I2)]
		public static Boolean Is_Ldelem_I2(this VirtualOpCode ins)
		{
			return _Is_Ldelem_T(ins, "System.Int16");
		}

		[Detect(Code.Ldelem_I4)]
		public static Boolean Is_Ldelem_I4(this VirtualOpCode ins)
		{
			return _Is_Ldelem_T(ins, "System.Int32");
		}

		[Detect(Code.Ldelem_I8)]
		public static Boolean Is_Ldelem_I8(this VirtualOpCode ins)
		{
			return _Is_Ldelem_T(ins, "System.Int64");
		}

		[Detect(Code.Ldelem_U1)]
		public static Boolean Is_Ldelem_U1(this VirtualOpCode ins)
		{
			return _Is_Ldelem_T(ins, "System.Byte");
		}

		[Detect(Code.Ldelem_U2)]
		public static Boolean Is_Ldelem_U2(this VirtualOpCode ins)
		{
			return _Is_Ldelem_T(ins, "System.UInt16");
		}

		[Detect(Code.Ldelem_U4)]
		public static Boolean Is_Ldelem_U4(this VirtualOpCode ins)
		{
			return _Is_Ldelem_T(ins, "System.UInt32");
		}

		[Detect(Code.Ldelem_R4)]
		public static Boolean Is_Ldelem_R4(this VirtualOpCode ins)
		{
			return _Is_Ldelem_T(ins, "System.Single");
		}

		[Detect(Code.Ldelem_R8)]
		public static Boolean Is_Ldelem_R8(this VirtualOpCode ins)
		{
			return _Is_Ldelem_T(ins, "System.Double");
		}

		[Detect(Code.Ldelem_Ref)]
		public static Boolean Is_Ldelem_Ref(this VirtualOpCode ins)
		{
			// Is exact same as Ldelem_I except for the field reference
			return ins.MatchesEntire(new Code[] {
				Code.Ldarg_0, Code.Ldsfld, Code.Call, Code.Ret
			}) && _Is_Ldelem(ins) && !ins.Is_Ldelem_I();
		}

		[Detect(Code.Ldelem_I)]
		public static Boolean Is_Ldelem_I(this VirtualOpCode ins)
		{
			var sub = ins.Find(new Code[] {
				Code.Ldarg_0, Code.Ldsfld, Code.Call, Code.Ret
			});
			return sub != null
				&& ((IField)sub[1].Operand).MDToken == ins.Virtualization.GetTypeField("System.IntPtr").MDToken
				&& _Is_Ldelem(ins);
		}

		[Detect(Code.Ldelema)]
		public static Boolean Is_Ldelema(this VirtualOpCode ins)
		{
			// Note: Another way to detect may be by looking at Newobj TypeDef, as
			// it seems specific to the Ldelema instruction type
			// (has 3 fields: Array, long, Type)
			return ins.Matches(new Code[] {
				Code.Ldarg_0, Code.Newobj, Code.Stloc_0,
				Code.Ldloc_0, Code.Ldloc_S, Code.Callvirt,
				Code.Ldloc_0, Code.Ldloc_2, Code.Callvirt,
				Code.Ldloc_0, Code.Ldloc_3, Code.Callvirt,
				Code.Ldloc_0, Code.Call, Code.Ret
			});
		}

		/// <summary>
		/// OpCode pattern seen in Stelem, Stelem_* helper methods.
		/// </summary>
		private static readonly Code[] Pattern_Stelem = new Code[] {
			Code.Castclass, Code.Stloc_3, Code.Ldarg_0, Code.Ldarg_1, Code.Ldloc_1,
			Code.Ldloc_2, Code.Ldloc_3, Code.Call, Code.Ret
		};

		[Detect(Code.Stelem)]
		public static Boolean Is_Stelem(this VirtualOpCode ins)
		{
			return ins.DelegateMethod.MatchesEntire(
				Code.Ldarg_1, Code.Castclass, Code.Callvirt, Code.Stloc_0, Code.Ldarg_0,
				Code.Ldloc_0, Code.Call, Code.Stloc_1, Code.Ldarg_0, Code.Ldloc_1,
				Code.Call, Code.Ret
			) && ins.DelegateMethod.MatchesIndirect(Pattern_Stelem);
		}

		[Detect(Code.Stelem_I)]
		public static Boolean Is_Stelem_I(this VirtualOpCode ins)
		{
			return ins.DelegateMethod.MatchesEntire(
				Code.Ldarg_0, Code.Ldsfld, Code.Call, Code.Ret
			) && ins.DelegateMethod.MatchesIndirect(Pattern_Stelem)
			&& ((FieldDef)ins.DelegateMethod.Body.Instructions[1].Operand)
			   .MDToken == ins.Virtualization.GetTypeField("System.IntPtr").MDToken;
		}

		[Detect(Code.Stelem_Ref)]
		public static Boolean Is_Stelem_Ref(this VirtualOpCode ins)
		{
			return ins.DelegateMethod.MatchesEntire(
				Code.Ldarg_0, Code.Ldsfld, Code.Call, Code.Ret
			) && ins.DelegateMethod.MatchesIndirect(Pattern_Stelem)
			&& !Is_Stelem_I(ins);
		}

		private static Boolean _Is_Stelem_IC(VirtualOpCode ins, String typeName)
		{
			ITypeDefOrRef type = null;
			var sub = ins.DelegateMethod.Find(
				Code.Ldarg_0, Code.Ldtoken, Code.Call, Code.Ldloc_0, Code.Ldloc_1,
				Code.Ldloc_2, Code.Call, Code.Ret
			);
			return sub != null
				&& (type = sub[1].Operand as ITypeDefOrRef) != null
				&& type.FullName.Equals(typeName);
		}

		[Detect(Code.Stelem_I1)]
		public static Boolean Is_Stelem_I1(this VirtualOpCode ins)
		{
			return _Is_Stelem_IC(ins, "System.SByte");
		}

		[Detect(Code.Stelem_I2)]
		public static Boolean Is_Stelem_I2(this VirtualOpCode ins)
		{
			return _Is_Stelem_IC(ins, "System.Int16");
		}

		[Detect(Code.Stelem_I4)]
		public static Boolean Is_Stelem_I4(this VirtualOpCode ins)
		{
			return _Is_Stelem_IC(ins, "System.Int32");
		}

		[Detect(Code.Stelem_I8)]
		public static Boolean Is_Stelem_I8(this VirtualOpCode ins)
		{
			return _Is_Stelem_IC(ins, "System.Int64");
		}

		private static Boolean _Is_Stelem_RC(VirtualOpCode ins, String typeName)
		{
			ITypeDefOrRef type = null;
			var body = ins.DelegateMethod.Body.Instructions;
			return ins.DelegateMethod.MatchesEntire(
				Code.Ldarg_0, Code.Ldtoken, Code.Call, Code.Call, Code.Ret
			) && ins.DelegateMethod.MatchesIndirect(Pattern_Stelem)
			&& (type = body[1].Operand as ITypeDefOrRef) != null
			&& type.FullName.Equals(typeName);
		}

		[Detect(Code.Stelem_R4)]
		public static Boolean Is_Stelem_R4(this VirtualOpCode ins)
		{
			return _Is_Stelem_RC(ins, "System.Single");
		}

		[Detect(Code.Stelem_R8)]
		public static Boolean Is_Stelem_R8(this VirtualOpCode ins)
		{
			return _Is_Stelem_RC(ins, "System.Double");
		}
	}
}
