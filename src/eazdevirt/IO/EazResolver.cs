using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using de4dot.blocks;
using dnlib.DotNet;
using dnlib.DotNet.MD;

namespace eazdevirt.IO
{
	public class EazResolver : EazResourceReader
	{
		/// <summary>
		/// Lock used for all public resolve methods.
		/// </summary>
		private Object _lock = new Object();

		public EazResolver(EazModule module)
			: base(module)
		{
		}

		/// <summary>
		/// Resolve a method.
		/// </summary>
		/// <param name="position">Position</param>
		/// <returns>Method</returns>
		public IMethod ResolveMethod(Int32 position)
		{
			lock(_lock)
			{
				return this.ResolveMethod_NoLock(position);
			}
		}

		public IMethod TryResolveMethod(Int32 position)
		{
			try
			{
				return this.ResolveMethod(position);
			}
			catch(Exception)
			{
				return null;
			}
		}

		IMethod ResolveMethod_NoLock(Int32 position)
		{
			this.Stream.Position = position;

			InlineOperand operand = new InlineOperand(this.Reader);
			if(operand.IsDirect)
			{
				MDToken token = new MDToken(operand.Token);
				if (token.Table == Table.Method)
					return this.Module.ResolveMethod(token.Rid);
				else if (token.Table == Table.MemberRef)
					return this.Module.ResolveMemberRef(token.Rid);
				else if (token.Table == Table.MethodSpec)
					return this.Module.ResolveMethodSpec(token.Rid);

				throw new Exception("Bad MDToken table");
			}
			else
			{
				MethodData data = operand.Data as MethodData;
				ITypeDefOrRef declaring = this.ResolveType_NoLock(data.DeclaringType.Token);

				if (declaring is TypeDef)
				{
					// If declaring type is a TypeDef, it is defined inside this module, so a
					// MethodDef should be attainable. However, the method may have generic parameters.

					TypeDef declaringDef = declaring as TypeDef;

					//Console.WriteLine("Method: " + data.Name);
					//Console.WriteLine("Declaring Def: " + declaringDef.FullName);

					// This signature may not be exact:
					// If `MyMethod<T>(T something): Void` is called as MyMethod<Int32>(100),
					// signature will appear as `System.Void <!!0>(System.Int32)` instead of `System.Void <!!0>(T)`
					var sig = GetMethodSig(data);

					// Try to easily find the method by name + signature
					var foundMethod = declaringDef.FindMethod(data.Name, sig);
					if (foundMethod != null)
						return foundMethod;

					// Search for the method
					var methods = declaringDef.FindMethods(data.Name);
					foreach (var method in methods)
					{
						if (method.IsStatic != data.IsStatic)
							continue;

						if (Matches(method, sig))
						{
							// This should only happen for generic inst methods
							return ToMethodSpec(method, data);
						}
					}

					throw new Exception("Blah");
				}
				else if (declaring is TypeRef)
				{
					TypeRef declaringRef = declaring as TypeRef;
					MethodSig methodSig = GetMethodSig(data);
					MemberRef memberRef = new MemberRefUser(this.Module, data.Name, methodSig, declaringRef);
					return memberRef;

					//Boolean referencesFound = this.Module.GetMemberRefs().Any((r) =>
					//{
					//	Console.WriteLine("{0} == {1}", r.DeclaringType.MDToken, declaringRef.MDToken);
					//	return r.DeclaringType.MDToken == declaringRef.MDToken;
					//});
					//
					//if (referencesFound)
					//	Console.WriteLine("References found");
					//else
					//	Console.WriteLine("References NOT found");
					//
					//Importer importer = new Importer(this.Module);
					//TypeDef declaringDef = importer.Import(declaringRef).ResolveTypeDefThrow();
					//return declaringDef.FindMethod(data.Name);
				}
				else if (declaring is TypeSpec)
				{
					// If declaring type is a TypeSpec, it should have generic types associated
					// with it. This doesn't mean the method itself will, though.

					var comparer = new SignatureEqualityComparer(SigComparerOptions.SubstituteGenericParameters);

					TypeSpec declaringSpec = declaring as TypeSpec;
					MethodSig methodSig = GetMethodSig(data);

					if (declaringSpec.TypeSig.IsGenericInstanceType
					/* && !data.Name.Equals(".ctor") */)
					{
						//Console.WriteLine("Contains generic parameter: {0}", declaringSpec);

						TypeDef declaringDef = declaringSpec.ResolveTypeDefThrow();
						var methods = declaringDef.FindMethods(data.Name);
						foreach (var method in methods)
						{
							//Console.WriteLine("Comparing: {0} | {1} == {2}",
							//	method.MethodSig, methodSig, Equals(declaringSpec, method.MethodSig, methodSig));
							if (Equals(declaringSpec, method.MethodSig, methodSig))
							{
								return new MemberRefUser(this.Module, data.Name, method.MethodSig, declaringSpec);
							}
						}
					}

					if (data.HasGenericArguments)
					{
						MemberRef memberRef = new MemberRefUser(this.Module, data.Name, methodSig, declaringSpec);
						MethodSpec methodSpec = ToMethodSpec(memberRef, data);
						return methodSpec;
					}
					else
					{
						MemberRef memberRef = new MemberRefUser(this.Module, data.Name, methodSig, declaringSpec);
						return memberRef;
					}
				}

				throw new Exception("Wat");

				//if(data.Unknown2) // if (HasGenericArguments()) ???
				//{
				//	// ???
				//	Type declaringType = this.ResolveType_Reflection_NoLock(data.DeclaringType.Token);
				//	MemberInfo[] infos = declaringType.GetMember(data.Name, data.BindingFlags);
				//
				//	foreach(var memberInfo in infos)
				//	{
				//		MethodInfo info = memberInfo as MethodInfo;
				//
				//		ParameterInfo[] paramInfos = info.GetParameters();
				//		Type[] genericArgs = info.GetGenericArguments();
				//
				//		if(paramInfos.Length != data.Parameters.Length)
				//			throw new Exception("Cannot bind method (bad parameters length)");
				//
				//		if (genericArgs.Length != data.GenericArguments.Length)
				//			throw new Exception("Cannot bind method (bad generic args length)");
				//
				//		if (!this.Compare(info.ReturnType, data.ReturnType))
				//			throw new Exception("Cannot bind method (return type mismatch)");
				//
				//		// Check param types
				//		for(Int32 i = 0; i < paramInfos.Length; i++)
				//		{
				//			if (!this.Compare(paramInfos[i].ParameterType, data.Parameters[i]))
				//				throw new Exception(String.Format(
				//					"Cannot bind method (parameter type mismatch @ {0})", i));
				//		}
				//	}
				//}
				//else
				//{
				//	// ...
				//}
			}
		}

		/// <summary>
		/// Compare two method signatures, with the first having generic parameters
		/// of types from the declaring type's generic arguments.
		/// </summary>
		/// <param name="s1">First signature</param>
		/// <param name="s2">Second signature</param>
		/// <returns>true if appear equal, false if not</returns>
		/// <remarks>Make a comparer class for this later?</remarks>
		Boolean Equals(TypeSpec declaringType, MethodSig s1, MethodSig s2)
		{
			// This is necessary because the serialized MethodData doesn't contain
			// information (from what I can tell) about which parameters correspond
			// to which generic arguments.

			var comparer = new SigComparer();

			var gsig = declaringType.TryGetGenericInstSig();
			if (gsig == null)
			{
				// If declaring type isn't a generic instance, compare normally
				return comparer.Equals(s1, s2);
			}

			if (s1.Params.Count != s2.Params.Count)
				return false;

			for (Int32 i = 0; i < s1.Params.Count; i++)
			{
				var p = s1.Params[i];
				if (p.IsGenericTypeParameter)
				{
					var genericVar = p.ToGenericVar();
					var genericNumber = genericVar.GenericParam.Number;
					var genericArgument = gsig.GenericArguments[genericNumber];
					p = genericArgument;
				}

				if(!comparer.Equals(p, s2.Params[i]))
					return false;
			}

			return comparer.Equals(s1.RetType, s2.RetType);
		}

		/// <summary>
		/// Create a generic instance signature by applying generics
		/// from deserialized data to an existing type.
		/// </summary>
		/// <param name="type">Existing type</param>
		/// <param name="data">Deserialized data with generic types</param>
		/// <returns>GenericInstSig</returns>
		ITypeDefOrRef ApplyGenerics(ITypeDefOrRef type, TypeData data)
		{
			List<TypeSig> types = new List<TypeSig>();
			foreach (var g in data.GenericTypes)
			{
				var gtype = this.ResolveType_NoLock(g.Token);
				types.Add(gtype.ToTypeSig());
			}

			ClassOrValueTypeSig typeSig = type.ToTypeSig().ToClassOrValueTypeSig();
			return new GenericInstSig(typeSig, types).ToTypeDefOrRef();
			// TypeSpec typeSpec = new TypeSpecUser(genericSig);
		}

		/// <summary>
		/// Create a MethodSpec from a MethodDef and data with generic arguments.
		/// </summary>
		/// <param name="method">Method</param>
		/// <param name="data">Data</param>
		/// <returns>MethodSpec</returns>
		MethodSpec ToMethodSpec(IMethodDefOrRef method, MethodData data)
		{
			// Resolve all generic argument types
			List<TypeSig> types = new List<TypeSig>();
			foreach (var g in data.GenericArguments)
			{
				var gtype = this.ResolveType_NoLock(g.Token);
				types.Add(gtype.ToTypeSig());
			}

			var sig = new GenericInstMethodSig(types);
			return new MethodSpecUser(method, sig);
		}

		/// <summary>
		/// Check whether or not a MethodDef matches some MethodSig.
		/// </summary>
		/// <param name="method">Method</param>
		/// <param name="signature">Method signature</param>
		/// <returns>true if matches, false if not</returns>
		/// <remarks>Could use dnlib's SigComparer for this maybe?</remarks>
		Boolean Matches(MethodDef method, MethodSig signature)
		{
			//This is imperfect and may confuse methods such as:
			// `MyMethod<T>(T, int): void` and `MyMethod<T>(int, T)`

			if (method.MethodSig.Params.Count != signature.Params.Count)
				return false;

			for (Int32 i = 0; i < method.MethodSig.Params.Count; i++)
			{
				TypeSig mp = method.MethodSig.Params[i];
				TypeSig sp = signature.Params[i];

				if (mp.IsGenericMethodParameter)
					continue;
				else if (!mp.MDToken.Equals(sp.MDToken))
					return false;
			}

			return method.MethodSig.RetType.MDToken.Equals(signature.RetType.MDToken)
				&& method.MethodSig.GenParamCount == signature.GenParamCount;
		}

		/// <summary>
		/// Convert some method data into a method signature.
		/// </summary>
		/// <param name="data">Data</param>
		/// <returns>Signature</returns>
		MethodSig GetMethodSig(MethodData data)
		{
			TypeSig returnType = ResolveType(data.ReturnType);

			TypeSig[] paramTypes = new TypeSig[data.Parameters.Length];
			for (Int32 i = 0; i < paramTypes.Length; i++)
			{
				paramTypes[i] = ResolveType(data.Parameters[i]);
			}

			UInt32 genericTypeCount = (UInt32)data.GenericArguments.Length;

			MethodSig methodSig;
			if (genericTypeCount == 0)
			{
				if (data.IsStatic)
					methodSig = MethodSig.CreateStatic(returnType, paramTypes);
				else
					methodSig = MethodSig.CreateInstance(returnType, paramTypes);
			}
			else
			{
				if (data.IsStatic)
					methodSig = MethodSig.CreateStaticGeneric(genericTypeCount, returnType, paramTypes);
				else
					methodSig = MethodSig.CreateInstanceGeneric(genericTypeCount, returnType, paramTypes);
			}

			return methodSig;
		}

		TypeSig ResolveType(InlineOperand operand)
		{
			ITypeDefOrRef type = this.ResolveType_NoLock(operand.Token);
			return type.ToTypeSig(true);
		}

		/// <summary>
		/// Resolve a user string.
		/// </summary>
		/// <param name="position">Position</param>
		/// <returns>String</returns>
		public String ResolveString(Int32 position)
		{
			lock(_lock)
			{
				return this.ResolveString_NoLock(position);
			}
		}

		String ResolveString_NoLock(Int32 position)
		{
			this.Stream.Position = position;

			InlineOperand operand = new InlineOperand(this.Reader);
			if(operand.IsDirect)
			{
				return this.Module.ReadUserString((UInt32)operand.Token);
			}
			else
			{
				StringData data = operand.Data as StringData;
				return data.Value;
			}
		}

		/// <summary>
		/// Resolve a field.
		/// </summary>
		/// <param name="position">Position</param>
		/// <returns>Field</returns>
		public IField ResolveField(Int32 position)
		{
			lock (_lock)
			{
				return this.ResolveField_NoLock(position);
			}
		}

		public IField TryResolveField(Int32 position)
		{
			try
			{
				return this.ResolveField(position);
			}
			catch(Exception)
			{
				return null;
			}
		}

		IField ResolveField_NoLock(Int32 position)
		{
			this.Stream.Position = position;

			InlineOperand operand = new InlineOperand(this.Reader);
			if(operand.IsDirect)
			{
				MDToken token = new MDToken(operand.Token);
				return this.Module.ResolveField(token.Rid);
			}
			else
			{
				FieldData data = operand.Data as FieldData;
				ITypeDefOrRef type = this.ResolveType_NoLock(data.FieldType.Token);
				if (type == null)
					throw new Exception("Unable to resolve type as TypeDef or TypeRef");

				TypeDef typeDef = type as TypeDef;
				if (typeDef != null)
					return typeDef.FindField(data.Name);

				typeDef = (type as TypeRef).ResolveTypeDef();
				if (typeDef != null)
					return typeDef.FindField(data.Name);

				throw new Exception("Currently unable to resolve a field from an unresolvable TypeRef");
			}
		}

		/// <summary>
		/// Resolve a type.
		/// </summary>
		/// <param name="position">Position</param>
		/// <returns>Type</returns>
		public ITypeDefOrRef ResolveType(Int32 position)
		{
			lock (_lock)
			{
				return this.ResolveType_NoLock(position);
			}
		}

		public ITypeDefOrRef TryResolveType(Int32 position)
		{
			try
			{
				return this.ResolveType(position);
			}
			catch (Exception)
			{
				return null;
			}
		}

		ITypeDefOrRef ResolveType_NoLock(Int32 position)
		{
			this.Stream.Position = position;

			InlineOperand operand = new InlineOperand(this.Reader);
			if(operand.IsDirect)
			{
				MDToken token = new MDToken(operand.Token);

				if (token.Table == Table.TypeDef)
					return this.Module.ResolveTypeDef(token.Rid);
				else if (token.Table == Table.TypeRef)
					return this.Module.ResolveTypeRef(token.Rid);
				else if (token.Table == Table.TypeSpec)
					return this.Module.ResolveTypeSpec(token.Rid);

				throw new Exception("Bad MDToken table");
			}
			else
			{
				TypeData data = operand.Data as TypeData;
				String typeName = data.TypeName;

				if(data.SomeIndex != -1)
					Console.WriteLine("[{0}] SomeIndex: {1}", data.TypeName, data.SomeIndex2);
				if(data.SomeIndex2 != -1)
					Console.WriteLine("[{0}] SomeIndex2: {1}", data.TypeName, data.SomeIndex2);

				// Get the type modifiers stack
				Stack<String> modifiers = GetModifiersStack(data.TypeName, out typeName);

				// Hacky, add a proper fix for this (also * and &)
				//Boolean isArray = data.TypeName.EndsWith("[]");
				//if (isArray)
				//	typeName = typeName.Substring(0, typeName.Length - 2);

				// Try to find typedef
				TypeDef typeDef = this.Module.FindReflection(typeName);
				if (typeDef != null)
				{
					ITypeDefOrRef result = typeDef;
					if (data.GenericTypes.Length > 0)
						result = ApplyGenerics(typeDef, data);

					TypeSig sig = ApplyTypeModifiers(result.ToTypeSig(), modifiers);
					return sig.ToTypeDefOrRef();

					//if (isArray)
					//{
					//	var sig = new SZArraySig(typeDef.ToTypeSig());
					//	result = sig.ToTypeDefOrRef();
					//}
					//
					//return result;

					//if (data.GenericTypes.Length > 0)
					//	return ToTypeSpec(typeDef, data);
					//else return typeDef;
				}

				// Otherwise, try to find typeref
				TypeRef typeRef = null;
				typeRef = this.Module.GetTypeRefs().FirstOrDefault((t) =>
				{
					return t.ReflectionFullName.Equals(typeName);
				});
				if (typeRef != null)
				{
					ITypeDefOrRef result = typeRef;
					if (data.GenericTypes.Length > 0)
						result = ApplyGenerics(typeRef, data);

					TypeSig sig = ApplyTypeModifiers(result.ToTypeSig(), modifiers);
					return sig.ToTypeDefOrRef();

					//if (isArray)
					//{
					//	Console.WriteLine("TypeRef array type found: " + data.Name);
					//	var sig = new SZArraySig(typeRef.ToTypeSig());
					//	result = sig.ToTypeDefOrRef();
					//	Console.WriteLine("Result: " + result);
					//}
					//
					//return result;

					//if (data.GenericTypes.Length > 0)
					//	return ToTypeSpec(typeRef, data);
					//else return typeRef;
				}

				// If all else fails, make our own typeref
				Console.WriteLine("[ResolveType_NoLock] WARNING: Creating TypeRef for: {0}", data.Name);
				AssemblyRef assemblyRef = GetAssemblyRef(data.AssemblyFullName);
				typeRef = new TypeRefUser(this.Module, data.Namespace, data.TypeNameWithoutNamespace, assemblyRef);
				if (typeRef != null)
					return typeRef;

				throw new Exception(String.Format(
					"Couldn't resolve type {0} @ {1} (0x{1:X8})", typeName, position));

				// Still haven't figured out how the TypeData is used
				//TypeData data = operand.Data as TypeData;
				//
				//if(data.Unknown3)
				//{
				//	Type type = null;
				//
				//	if(data.SomeIndex == -1 && data.SomeIndex2 == -1)
				//		throw new Exception();
				//
				//	if(data.SomeIndex != -1)
				//	{
				//		// ...
				//	}
				//	else if (data.SomeIndex2 != -1)
				//	{
				//		// ...
				//	}
				//
				//	Stack<TypeModifierStruct> stack = GetTypeModifiers(data.Name);
				//	type = ApplyTypeModifiers(type, stack);
				//}
				//else
				//{
				//	Type type = Type.GetType(data.Name);
				//	if(type == null)
				//	{
				//		// ...
				//	}
				//
				//	// ...
				//}
			}
		}

		Stack<String> GetModifiersStack(String rawName, out String fixedName)
		{
			var stack = new Stack<String>();

			while(true)
			{
				if (rawName.EndsWith("*"))
					stack.Push("*");
				else if (rawName.EndsWith("&"))
					stack.Push("&");
				else break;

				rawName = rawName.Substring(0, rawName.Length - 1);
			}

			while(rawName.EndsWith("[]"))
			{
				stack.Push("[]");
				rawName = rawName.Substring(0, rawName.Length - 2);
			}

			fixedName = rawName;
			return stack;
		}

		TypeSig ApplyTypeModifiers(TypeSig typeSig, Stack<String> modifiers)
		{
			// This might not be implemented correctly
			typeSig = this.FixTypeAsArray(typeSig, modifiers);
			typeSig = this.FixTypeAsRefOrPointer(typeSig, modifiers);
			return typeSig;
		}

		TypeSig FixTypeAsArray(TypeSig typeSig, Stack<String> modifiers)
		{
			UInt32 rank = 0;

			while(modifiers.Count > 0 && modifiers.Peek().Equals("[]"))
			{
				modifiers.Pop();
				rank++;
			}

			if (rank == 0)
				return typeSig;
			else if (rank == 1)
				return new SZArraySig(typeSig);
			else
				return new ArraySig(typeSig, rank);
		}

		TypeSig FixTypeAsRefOrPointer(TypeSig typeSig, Stack<String> modifiers)
		{
			while(modifiers.Count > 0
				&& (modifiers.Peek().Equals("*") || modifiers.Peek().Equals("&")))
			{
				if(modifiers.Pop().Equals("*")) // Pointer
				{
					typeSig = new PtrSig(typeSig);
				}
				else // ByRef
				{
					typeSig = new ByRefSig(typeSig);
				}
			}

			return typeSig;
		}

		AssemblyRef GetAssemblyRef(String fullname)
		{
			return this.Module.GetAssemblyRefs().FirstOrDefault((ar) =>
			{
				return ar.FullName.Equals(fullname);
			});
		}

		/// <summary>
		/// Type comparison method.
		/// </summary>
		/// <param name="type1">Type 1</param>
		/// <param name="type2">Type 2</param>
		/// <returns>true if seemingly equal, false if not</returns>
		static Boolean AreTypesEqual(Type type1, Type type2)
		{
			if (type1 == type2)
			{
				return true;
			}
			if (type1 == null || type2 == null)
			{
				return false;
			}
			if (type1.IsByRef)
			{
				return type2.IsByRef
					&& AreTypesEqual(type1.GetElementType(), type2.GetElementType());
			}
			if (type2.IsByRef)
			{
				return false;
			}
			if (type1.IsPointer)
			{
				return type2.IsPointer
					&& AreTypesEqual(type1.GetElementType(), type2.GetElementType());
			}
			if (type2.IsPointer)
			{
				return false;
			}
			if (type1.IsArray)
			{
				return type2.IsArray
					&& type1.GetArrayRank() == type2.GetArrayRank()
					&& AreTypesEqual(type1.GetElementType(), type2.GetElementType());
			}
			if (type2.IsArray)
			{
				return false;
			}
			if (type1.IsGenericType && !type1.IsGenericTypeDefinition)
			{
				type1 = type1.GetGenericTypeDefinition();
			}
			if (type2.IsGenericType && !type2.IsGenericTypeDefinition)
			{
				type2 = type2.GetGenericTypeDefinition();
			}
			return type1 == type2;
		}

		Type GetRealType(Type type)
		{
			if (!type.IsByRef && !type.IsArray && !type.IsPointer)
				return type;
			return GetRealType(type.GetElementType());
		}

		//Boolean Compare(Type type, InlineOperand operand)
		//{
		//	TypeData data = operand.Data as TypeData;
		//
		//	if(GetRealType(type).IsGenericParameter)
		//		return data == null || data.Unknown3;
		//
		//	Type resolvedType = this.ResolveType_Reflection_NoLock(operand.Token);
		//	return AreTypesEqual(type, resolvedType);
		//}

		/// <remarks>Mostly copied from decompiler, unsure how relevant</remarks>
		private enum TypeModifier
		{
			Ref = 0,
			Array = 1,
			Pointer = 2
		}

		/// <remarks>Mostly copied from decompiler, unsure how relevant</remarks>
		private struct TypeModifierStruct
		{
			public TypeModifier Type; // enum0_0
			public Int32 Rank; // int_0
		}

		/// <remarks>Mostly copied from decompiler, unsure how relevant</remarks>
		private static Stack<TypeModifierStruct> GetTypeModifiers(String text)
		{
			Stack<TypeModifierStruct> stack = new Stack<TypeModifierStruct>();

			while (true)
			{
				if (text.EndsWith("&", StringComparison.Ordinal))
				{
					stack.Push(new TypeModifierStruct { Type = TypeModifier.Ref });
					text = text.Substring(0, text.Length - 1);
				}
				else if (text.EndsWith("*", StringComparison.Ordinal))
				{
					stack.Push(new TypeModifierStruct { Type = TypeModifier.Pointer });
					text = text.Substring(0, text.Length - 1);
				}
				else break;
			}

			return stack;
		}

		/// <remarks>Mostly copied from decompiler, unsure how relevant</remarks>
		private static Type ApplyTypeModifiers(Type defaultType, Stack<TypeModifierStruct> modifiers)
		{
			while (modifiers.Count > 0)
			{
				TypeModifierStruct t = modifiers.Pop();
				switch (t.Type)
				{
					case TypeModifier.Ref:
						defaultType = defaultType.MakeByRefType();
						break;
					case TypeModifier.Array:
						if (t.Rank == 1)
							defaultType = defaultType.MakeArrayType();
						else
							defaultType = defaultType.MakeArrayType(t.Rank);
						break;
					case TypeModifier.Pointer:
						defaultType = defaultType.MakePointerType();
						break;
				}
			}
			return defaultType;
		}

		/// <summary>
		/// A guess as to the first Byte (enum) of InlineOperand.
		/// </summary>
		public enum Directness
		{
			/// <summary>
			/// If direct, can just use the provided MDToken.
			/// </summary>
			Direct = 0,

			/// <summary>
			/// If indirect, requires using the data somehow.
			/// </summary>
			Indirect = 1
		}

		/// <summary>
		/// Inline operand types.
		/// </summary>
		public enum InlineOperandType
		{
			Type = 0,
			Field = 1,
			Method = 2,
			UserString = 3,
			Unknown4 = 4
		}

		// Class54
		public class InlineOperand
		{
			public Directness ResolveType { get; private set; }
			public Int32 Token { get; private set; }
			public InlineOperandData Data { get; private set; }

			public Boolean IsDirect { get { return this.ResolveType == Directness.Direct; } }
			public Boolean HasData { get { return this.Data != null; } }

			protected InlineOperand()
			{
			}

			public InlineOperand(BinaryReader reader)
			{
				this.Deserialize(reader);
			}

			private void Deserialize(BinaryReader reader)
			{
				this.ResolveType = (Directness)reader.ReadByte();

				if (this.ResolveType == 0)
					this.Token = reader.ReadInt32();
				else
					this.Data = InlineOperandData.Read(reader);
			}

			public static InlineOperand ReadInternal(BinaryReader reader)
			{
				InlineOperand u = new InlineOperand();
				u.ResolveType = Directness.Indirect;
				u.Token = reader.ReadInt32();
				return u;
			}

			public static InlineOperand[] ReadArrayInternal(BinaryReader reader)
			{
				Int32 count = reader.ReadInt16();
				InlineOperand[] arr = new InlineOperand[count];

				for (Int32 i = 0; i < arr.Length; i++)
					arr[i] = InlineOperand.ReadInternal(reader);

				return arr;
			}
		}

		// Class41
		public abstract class InlineOperandData
		{
			public abstract InlineOperandType Type { get; }
			protected InlineOperandData() { }

			public InlineOperandData(BinaryReader reader)
			{
				this.Deserialize(reader);
			}

			protected abstract void Deserialize(BinaryReader reader);

			public static InlineOperandData Read(BinaryReader reader)
			{
				switch ((InlineOperandType)reader.ReadByte())
				{
					case InlineOperandType.Type:
						return new TypeData(reader);
					case InlineOperandType.Field:
						return new FieldData(reader);
					case InlineOperandType.Method:
						return new MethodData(reader);
					case InlineOperandType.UserString:
						return new StringData(reader);
					case InlineOperandType.Unknown4:
						return new UnknownType7(reader);
					default:
						throw new ArgumentOutOfRangeException();
				}
			}
		}

		// Class46
		public class TypeData : InlineOperandData
		{
			public Int32 SomeIndex { get; private set; } // int_1, index into type_2? (GetGenericArguments())
			public Int32 SomeIndex2 { get; private set; } // int_0, index into type_5? (DeclaringType.GetGenericArguments())
			public Boolean Unknown3 { get; private set; } // bool_0
			public String Name { get; private set; } // string_0
			public Boolean HasGenericTypes { get; private set; } // bool_1
			public InlineOperand[] GenericTypes { get; private set; } // class54_0

			public override InlineOperandType Type
			{
				get { return InlineOperandType.Type; }
			}

			public String TypeNameWithoutNamespace
			{
				get
				{
					if (this.TypeName.Contains("."))
						return this.TypeName.Split('.').Last();
					else
						return String.Empty;
				}
			}

			public String Namespace
			{
				get
				{
					if (this.TypeName.Contains("."))
					{
						return String.Join(".",
							this.TypeName.Split('.').Reverse().Skip(1).Reverse().ToArray());
					}
					else
						return this.TypeName;
				}
			}

			public String TypeName
			{
				get
				{
					if (this.Name.Contains(", "))
						return this.Name.Split(',')[0];
					else return this.Name;
				}
			}

			public String AssemblyFullName
			{
				get
				{
					return this.Name.Substring(
						this.TypeName.Length + 2,
						this.Name.Length - (this.TypeName.Length + 2)
					);
				}
			}

			public String AssemblyName
			{
				get { return AssemblyFullName.Split(',')[0]; }
			}

			public TypeData(BinaryReader reader)
				: base(reader)
			{
			}

			protected override void Deserialize(BinaryReader reader)
			{
				this.SomeIndex = reader.ReadInt32();
				this.SomeIndex2 = reader.ReadInt32();
				this.Unknown3 = reader.ReadBoolean();
				this.Name = reader.ReadString();
				this.HasGenericTypes = reader.ReadBoolean();
				this.GenericTypes = InlineOperand.ReadArrayInternal(reader);
			}
		}

		// Class42
		public class FieldData : InlineOperandData
		{
			public InlineOperand FieldType { get; private set; }
			public String Name { get; private set; }
			public Boolean Flags { get; private set; }

			public override InlineOperandType Type
			{
				get { return InlineOperandType.Field; }
			}

			public BindingFlags BindingFlags
			{
				get
				{
					BindingFlags bindingFlags = BindingFlags.Public | BindingFlags.NonPublic;
					if (this.Flags)
						bindingFlags |= BindingFlags.Static;
					else
						bindingFlags |= BindingFlags.Instance;
					return bindingFlags;
				}
			}

			public FieldData(BinaryReader reader)
				: base(reader)
			{
			}

			protected override void Deserialize(BinaryReader reader)
			{
				this.FieldType = InlineOperand.ReadInternal(reader);
				this.Name = reader.ReadString();
				this.Flags = reader.ReadBoolean();
			}
		}

		// Class44
		public class MethodData : InlineOperandData
		{
			public InlineOperand DeclaringType { get; private set; } // class54_0
			public Boolean Unknown2 { get; private set; } // bool_0
			public Boolean Flags { get; private set; } // bool_1
			public String Name { get; private set; } // string_0
			public InlineOperand ReturnType { get; private set; } // class54_3
			public InlineOperand[] Parameters { get; private set; } // class54_1
			public InlineOperand[] GenericArguments { get; private set; } // class54_2

			public Boolean HasGenericArguments
			{
				get { return this.GenericArguments.Length > 0; }
			}

			public override InlineOperandType Type
			{
				get { return InlineOperandType.Method; }
			}

			public Boolean IsStatic
			{
				get { return this.Flags; }
			}

			public Boolean IsInstance
			{
				get { return !this.Flags; }
			}

			public BindingFlags BindingFlags
			{
				get
				{
					BindingFlags bindingFlags = BindingFlags.Public | BindingFlags.NonPublic;
					if (this.Flags)
						bindingFlags |= BindingFlags.Static;
					else
						bindingFlags |= BindingFlags.Instance;
					return bindingFlags;
				}
			}

			public MethodData(BinaryReader reader)
				: base(reader)
			{
			}

			protected override void Deserialize(BinaryReader reader)
			{
				this.DeclaringType = InlineOperand.ReadInternal(reader);
				this.Unknown2 = reader.ReadBoolean();
				this.Flags = reader.ReadBoolean();
				this.Name = reader.ReadString();
				this.ReturnType = InlineOperand.ReadInternal(reader);
				this.Parameters = InlineOperand.ReadArrayInternal(reader);
				this.GenericArguments = InlineOperand.ReadArrayInternal(reader);
			}
		}

		// Class45
		public class StringData : InlineOperandData
		{
			public String Value { get; private set; }

			public override InlineOperandType Type
			{
				get { return InlineOperandType.UserString; }
			}

			public StringData(BinaryReader reader)
				: base(reader)
			{
			}

			protected override void Deserialize(BinaryReader reader)
			{
				this.Value = reader.ReadString();
			}
		}

		// Class43
		public class UnknownType7 : InlineOperandData
		{
			public Int32 Unknown1 { get; private set; }
			public Int32 Unknown2 { get; private set; }

			public override InlineOperandType Type
			{
				get { return InlineOperandType.Unknown4; }
			}

			public UnknownType7(BinaryReader reader)
				: base(reader)
			{
			}

			protected override void Deserialize(BinaryReader reader)
			{
				this.Unknown1 = reader.ReadInt32();
				this.Unknown2 = reader.ReadInt32();
			}
		}
	}
}
