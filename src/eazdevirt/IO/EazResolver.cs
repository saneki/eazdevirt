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
		/// Resolve a user string.
		/// </summary>
		/// <param name="position">Position</param>
		/// <returns>String</returns>
		public String ResolveString(Int32 position)
		{
			lock (_lock)
			{
				return this.ResolveString_NoLock(position);
			}
		}

		String ResolveString_NoLock(Int32 position)
		{
			this.Stream.Position = position;

			InlineOperand operand = new InlineOperand(this.Reader);
			if (operand.IsToken)
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
		/// Resolve a method.
		/// </summary>
		/// <param name="position">Position</param>
		/// <returns>IMethod</returns>
		public IMethod ResolveMethod(Int32 position)
		{
			lock(_lock)
			{
				return this.ResolveMethod_NoLock(position);
			}
		}

		/// <summary>
		/// Try to resolve a method without throwing.
		/// </summary>
		/// <param name="position">Position</param>
		/// <returns>IMethod, or null if unable to resolve</returns>
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
			if(operand.IsToken)
			{
				MDToken token = new MDToken(operand.Token);
				if (token.Table == Table.Method)
					return this.Module.ResolveMethod(token.Rid);
				else if (token.Table == Table.MemberRef)
					return this.Module.ResolveMemberRef(token.Rid);
				else if (token.Table == Table.MethodSpec)
					return this.Module.ResolveMethodSpec(token.Rid);

				throw new Exception("[ResolveMethod_NoLock] Bad MDToken table");
			}
			else
			{
				MethodData data = operand.Data as MethodData;
				ITypeDefOrRef declaring = this.ResolveType_NoLock(data.DeclaringType.Position);

				if (declaring is TypeDef)
				{
					// If declaring type is a TypeDef, it is defined inside this module, so a
					// MethodDef should be attainable. However, the method may have generic parameters.

					TypeDef declaringDef = declaring as TypeDef;

					// This signature may not be exact:
					// If `MyMethod<T>(T something): Void` is called as MyMethod<Int32>(100),
					// signature will appear as `System.Void <!!0>(System.Int32)` instead of `System.Void <!!0>(T)`
					var sig = GetMethodSig(data);

					// Try to easily find the method by name + signature
					var foundMethod = declaringDef.FindMethod(data.Name, sig);
					if (foundMethod != null)
						return foundMethod;

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

					throw new Exception("[ResolveMethod_NoLock] Unable to resolve method from declaring TypeDef");
				}
				else if (declaring is TypeRef)
				{
					TypeRef declaringRef = declaring as TypeRef;
					MethodSig methodSig = GetMethodSig(data);
					MemberRef memberRef = new MemberRefUser(this.Module, data.Name, methodSig, declaringRef);
					return memberRef;
				}
				else if (declaring is TypeSpec)
				{
					// If declaring type is a TypeSpec, it should have generic types associated
					// with it. This doesn't mean the method itself will, though.

					var comparer = new SignatureEqualityComparer(SigComparerOptions.SubstituteGenericParameters);

					TypeSpec declaringSpec = declaring as TypeSpec;
					MethodSig methodSig = GetMethodSig(data);

					//if (declaringSpec.TypeSig.IsGenericInstanceType)
					if (declaringSpec.TypeSig.IsGenericInstanceType)
					{
						Console.WriteLine("Comparing against possible method sigs: {0}", data.Name);

						TypeDef declaringDef = declaringSpec.ResolveTypeDefThrow();
						var methods = declaringDef.FindMethods(data.Name);
						var possibleMethodSigs = CreateMethodSigs(declaringSpec, methodSig, data);

						foreach (var possibleMethodSig in possibleMethodSigs)
							Console.WriteLine("Possible method sig: {0}", possibleMethodSig.ToString());

						foreach (var possibleMethodSig in possibleMethodSigs)
						{
							//Console.WriteLine("Possible method sig: {0}", possibleMethodSig.ToString());

							MethodDef found = declaringDef.FindMethod(data.Name, possibleMethodSig);
							if (found != null)
							{
								return ToMethodSpec(found, data);
								//MemberRef memberRef = new MemberRefUser(this.Module, data.Name, found.MethodSig, declaringSpec);
								//return ToMethodSpec(memberRef, data);
							}
						}

						//foreach (var method in methods)
						//{
						//	if (Equals(declaringSpec, method.MethodSig, methodSig))
						//	{
						//		return ToMethodSpec(
						//			new MemberRefUser(this.Module, data.Name, method.MethodSig, declaringSpec),
						//			data);
						//	}
						//}
					}

					if (data.HasGenericArguments)
					{
						MemberRef memberRef = new MemberRefUser(this.Module, data.Name, methodSig, declaringSpec);
						return ToMethodSpec(memberRef, data);
					}
					else
					{
						MemberRef memberRef = new MemberRefUser(this.Module, data.Name, methodSig, declaringSpec);
						return memberRef;
					}
				}

				throw new Exception("[ResolveMethod_NoLock] Expected declaring type to be a TypeDef, TypeRef or TypeSpec");
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
			catch (Exception)
			{
				return null;
			}
		}

		IField ResolveField_NoLock(Int32 position)
		{
			this.Stream.Position = position;

			InlineOperand operand = new InlineOperand(this.Reader);
			if (operand.IsToken)
			{
				MDToken token = new MDToken(operand.Token);
				return this.Module.ResolveField(token.Rid);
			}
			else
			{
				FieldData data = operand.Data as FieldData;
				ITypeDefOrRef type = this.ResolveType_NoLock(data.FieldType.Position);
				if (type == null)
					throw new Exception("[ResolveField_NoLock] Unable to resolve type as TypeDef or TypeRef");

				TypeDef typeDef = type as TypeDef;
				if (typeDef != null)
					return typeDef.FindField(data.Name);

				typeDef = (type as TypeRef).ResolveTypeDef();
				if (typeDef != null)
					return typeDef.FindField(data.Name);

				throw new Exception("[ResolveField_NoLock] Currently unable to resolve a field from an unresolvable TypeRef");
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
			if (operand.IsToken)
			{
				MDToken token = new MDToken(operand.Token);

				if (token.Table == Table.TypeDef)
					return this.Module.ResolveTypeDef(token.Rid);
				else if (token.Table == Table.TypeRef)
					return this.Module.ResolveTypeRef(token.Rid);
				else if (token.Table == Table.TypeSpec)
					return this.Module.ResolveTypeSpec(token.Rid);

				throw new Exception("[ResolveType_NoLock] Bad MDToken table");
			}
			else
			{
				TypeData data = operand.Data as TypeData;
				String typeName = data.TypeName;

				if (data.SomeIndex != -1)
					Console.WriteLine("[{0}] SomeIndex: {1}", data.TypeName, data.SomeIndex2);
				if (data.SomeIndex2 != -1)
					Console.WriteLine("[{0}] SomeIndex2: {1}", data.TypeName, data.SomeIndex2);

				// Get the type modifiers stack
				Stack<String> modifiers = GetModifiersStack(data.TypeName, out typeName);

				// Try to find typedef
				TypeDef typeDef = this.Module.FindReflection(typeName);
				if (typeDef != null)
				{
					ITypeDefOrRef result = typeDef;
					if (data.GenericTypes.Length > 0)
						result = ApplyGenerics(typeDef, data);

					TypeSig sig = ApplyTypeModifiers(result.ToTypeSig(), modifiers);
					return sig.ToTypeDefOrRef();
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
				}

				// If all else fails, make our own typeref
				Console.WriteLine("[ResolveType_NoLock] WARNING: Creating TypeRef for: {0}", data.Name);
				AssemblyRef assemblyRef = GetAssemblyRef(data.AssemblyFullName);
				typeRef = new TypeRefUser(this.Module, data.Namespace, data.TypeNameWithoutNamespace, assemblyRef);
				if (typeRef != null)
					return typeRef;

				throw new Exception(String.Format(
					"[ResolveType_NoLock] Couldn't resolve type {0} @ {1} (0x{1:X8})", typeName, position));
			}
		}

		/// <summary>
		/// Resolve a type from a deserialized inline operand, which should
		/// have a direct token (position).
		/// </summary>
		/// <param name="operand">Inline operand</param>
		/// <returns>TypeSig</returns>
		TypeSig ResolveType(InlineOperand operand)
		{
			ITypeDefOrRef type = this.ResolveType_NoLock(operand.Position);
			return type.ToTypeSig(true);
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

		ITypeDefOrRef ApplyGenerics(ITypeDefOrRef type, IList<TypeSig> generics)
		{
			ClassOrValueTypeSig typeSig = type.ToTypeSig().ToClassOrValueTypeSig();
			GenericInstSig genericSig = new GenericInstSig(typeSig, generics);
			return new TypeSpecUser(genericSig);
		}

		/// <summary>
		/// Create a TypeSpec from a type and data with generic arguments.
		/// from deserialized data to an existing type.
		/// </summary>
		/// <param name="type">Existing type</param>
		/// <param name="data">Deserialized data with generic types</param>
		/// <returns>GenericInstSig</returns>
		ITypeDefOrRef ApplyGenerics(ITypeDefOrRef type, TypeData data)
		{
			List<TypeSig> generics = new List<TypeSig>();
			foreach (var g in data.GenericTypes)
			{
				var gtype = this.ResolveType_NoLock(g.Position);
				generics.Add(gtype.ToTypeSig());
			}

			return ApplyGenerics(type, generics);

			//List<TypeSig> types = new List<TypeSig>();
			//foreach (var g in data.GenericTypes)
			//{
			//	var gtype = this.ResolveType_NoLock(g.Position);
			//	types.Add(gtype.ToTypeSig());
			//}
			//
			//ClassOrValueTypeSig typeSig = type.ToTypeSig().ToClassOrValueTypeSig();
			//// return new GenericInstSig(typeSig, types).ToTypeDefOrRef();
			//
			//GenericInstSig genericSig = new GenericInstSig(typeSig, types);
			//TypeSpec typeSpec = new TypeSpecUser(genericSig);
			//return typeSpec;
		}

		IMethod ApplyGenerics(IMethodDefOrRef method, IList<TypeSig> generics)
		{
			return null;
		}

		/// <summary>
		/// Create a MethodSpec from a MethodDef and data with generic arguments.
		/// If data contains no generic arguments, the passed method will be returned.
		/// </summary>
		/// <param name="method">Method</param>
		/// <param name="data">Data</param>
		/// <returns>MethodSpec or untouched Method if no generic arguments</returns>
		IMethod ToMethodSpec(IMethodDefOrRef method, MethodData data)
		{
			if (!data.HasGenericArguments)
				return method;

			// Resolve all generic argument types
			List<TypeSig> types = new List<TypeSig>();
			foreach (var g in data.GenericArguments)
			{
				var gtype = this.ResolveType_NoLock(g.Position);
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
		/// Create a list of all possible combinations of types/generic types that would make
		/// sense as parameters. This is necessary because the serialized method data does not
		/// contain information about which parameters map to which generic types (indices),
		/// neither GenericVars (declaring type) or GenericMVars (method itself).
		///
		/// TODO: Factor in context generics (generics from virtualized method itself and
		/// declaring type?)
		/// </summary>
		/// <param name="parameters">Parameters (with no generic type information)</param>
		/// <param name="generics">Generics visible to the method</param>
		/// <returns>Combinations with at least one item (original parameters)</returns>
		IList<IList<TypeSig>> CreateGenericParameterCombinations(IList<TypeSig> parameters,
			IList<TypeSig> typeGenerics, IList<TypeSig> methodGenerics)
		{
			IList<IList<TypeSig>> list = new List<IList<TypeSig>>();
			list.Add(parameters);

			for (UInt16 p = 0; p < parameters.Count; p++)
			{
				var ptype = parameters[p];

				for (UInt16 g = 0; g < typeGenerics.Count; g++)
				{
					var gtype = typeGenerics[g];

					// Better comparison?
					if (ptype.FullName.Equals(gtype.FullName))
					{
						Int32 length = list.Count;
						for (Int32 i = 0; i < length; i++)
						{
							// Copy param list
							List<TypeSig> newParams = new List<TypeSig>();
							newParams.AddRange(list[i]);

							GenericVar gvar = new GenericVar(g);
							newParams[p] = gvar;

							list.Add(newParams);
						}
					}
				}

				for (UInt16 g = 0; g < methodGenerics.Count; g++)
				{
					var gtype = methodGenerics[g];

					if (ptype.FullName.Equals(gtype.FullName))
					{
						Int32 length = list.Count;
						for (Int32 i = 0; i < length; i++)
						{
							List<TypeSig> newParams = new List<TypeSig>();
							newParams.AddRange(list[i]);

							GenericMVar gmvar = new GenericMVar(g);
							newParams[p] = gmvar;

							list.Add(newParams);
						}
					}
				}
			}

			return list;
		}

		IList<MethodSig> CreateMethodSigs(ITypeDefOrRef declaringType, MethodSig sig, MethodData data)
		{
			// Setup generic types
			IList<TypeSig> typeGenerics = new List<TypeSig>(), methodGenerics = new List<TypeSig>();

			// Add all declaring spec generic types
			TypeSpec declaringSpec = declaringType as TypeSpec;
			if (declaringSpec != null)
			{
				Console.WriteLine("TYPESPEC");
				var genericInstSig = declaringSpec.TryGetGenericInstSig();
				foreach (var garg in genericInstSig.GenericArguments)
					typeGenerics.Add(garg);
			}

			// Add all method generic types
			if (data.HasGenericArguments)
			{
				foreach (var operand in data.GenericArguments)
				{
					var gtype = this.ResolveType_NoLock(operand.Position);
					methodGenerics.Add(gtype.ToTypeSig());
				}
			}

			Console.WriteLine("Type Generics");
			foreach (var t in typeGenerics)
				Console.WriteLine(" {0}", t);

			Console.WriteLine("Method Generics");
			foreach (var m in methodGenerics)
				Console.WriteLine(" {0}", m);

			// Todo: Combinations factoring in the possibility that return type might match
			// a generic type
			TypeSig returnType = ResolveType(data.ReturnType);

			TypeSig[] paramTypes = new TypeSig[data.Parameters.Length];
			for (Int32 i = 0; i < paramTypes.Length; i++)
			{
				paramTypes[i] = ResolveType(data.Parameters[i]);
			}

			UInt32 genericTypeCount = (UInt32)data.GenericArguments.Length;

			IList<MethodSig> signatures = new List<MethodSig>();
			var paramCombos = CreateGenericParameterCombinations(paramTypes, typeGenerics, methodGenerics);

			foreach (var combo in paramCombos)
			{
				var paramCombo = combo.ToArray();

				MethodSig methodSig;

				if (genericTypeCount == 0)
				{
					if (data.IsStatic)
						methodSig = MethodSig.CreateStatic(returnType, paramCombo);
					else
						methodSig = MethodSig.CreateInstance(returnType, paramCombo);
				}
				else
				{
					if (data.IsStatic)
						methodSig = MethodSig.CreateStaticGeneric(genericTypeCount, returnType, paramCombo);
					else
						methodSig = MethodSig.CreateInstanceGeneric(genericTypeCount, returnType, paramCombo);
				}

				signatures.Add(methodSig);
			}

			return signatures;
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

		/// <summary>
		/// Get a modifiers stack from a deserialized type name, and also
		/// provide the fixed name.
		/// </summary>
		/// <param name="rawName">Deserialized name</param>
		/// <param name="fixedName">Fixed name</param>
		/// <returns>Modifiers stack</returns>
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

		/// <summary>
		/// Apply type "modifiers" to some TypeSig given a modifiers stack.
		/// </summary>
		/// <param name="typeSig">TypeSig</param>
		/// <param name="modifiers">Modifiers stack</param>
		/// <returns>TypeSig</returns>
		TypeSig ApplyTypeModifiers(TypeSig typeSig, Stack<String> modifiers)
		{
			// This might not be implemented correctly
			typeSig = this.FixTypeAsArray(typeSig, modifiers);
			typeSig = this.FixTypeAsRefOrPointer(typeSig, modifiers);
			return typeSig;
		}

		/// <summary>
		/// Apply array "modifiers" to a TypeSig.
		/// </summary>
		/// <param name="typeSig">TypeSig</param>
		/// <param name="modifiers">Modifiers stack</param>
		/// <returns>TypeSig</returns>
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
			else // Might want to wrap in N SZArraySigs instead of ArraySig(type, N)?
				return new ArraySig(typeSig, rank);
		}

		/// <summary>
		/// Apply ByRef or Ptr "modifiers" to a TypeSig depending on the given modifiers
		/// stack.
		/// </summary>
		/// <param name="typeSig">TypeSig</param>
		/// <param name="modifiers">Modifiers stack</param>
		/// <returns>TypeSig</returns>
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
		/// A guess as to the first Byte (enum) of InlineOperand.
		/// </summary>
		public enum ValueType
		{
			/// <summary>
			/// The Value field holds a raw MDToken value.
			/// </summary>
			Token = 0,

			/// <summary>
			/// The Value field holds a position.
			/// </summary>
			Position = 1
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
			UnknownType7 = 4
		}

		/// <summary>
		/// Deserialized inline operand.
		/// </summary>
		/// <remarks>Class54</remarks>
		public class InlineOperand
		{
			/// <summary>
			/// Determines how the Value field is interpreted.
			/// </summary>
			public ValueType ValueType { get; private set; }

			/// <summary>
			/// Either a raw metadata token from the parent module, or a position in
			/// the embedded resource file.
			/// </summary>
			public Int32 Value { get; private set; }

			/// <summary>
			/// Deserialized data associated with this operand.
			/// </summary>
			public InlineOperandData Data { get; private set; }

			/// <summary>
			/// Whether or not this operand contains a token.
			/// </summary>
			public Boolean IsToken { get { return this.ValueType == ValueType.Token; } }

			/// <summary>
			/// Whether or not this operand contains a position.
			/// </summary>
			public Boolean IsPosition { get { return !this.IsToken; } }

			/// <summary>
			/// Get the operand's token, throwing an exception if none.
			/// </summary>
			public Int32 Token
			{
				get
				{
					if (this.IsToken)
						return this.Value;
					else
						throw new Exception("InlineOperand has no token (only position)");
				}
			}

			/// <summary>
			/// Get the operand's position, throwing an exception if none.
			/// </summary>
			public Int32 Position
			{
				get
				{
					if (this.IsPosition)
						return this.Value;
					else
						throw new Exception("InlineOperand has no position (only token)");
				}
			}

			/// <summary>
			/// Whether or not this operand has deserialized data associated with it.
			/// </summary>
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
				this.ValueType = (ValueType)reader.ReadByte();

				if (this.ValueType == 0)
					this.Value = reader.ReadInt32();
				else
					this.Data = InlineOperandData.Read(reader);
			}

			public static InlineOperand ReadInternal(BinaryReader reader)
			{
				InlineOperand u = new InlineOperand();
				u.ValueType = ValueType.Position;
				u.Value = reader.ReadInt32();
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

		/// <summary>
		/// Operand data.
		/// </summary>
		/// <remarks>Class41</remarks>
		public abstract class InlineOperandData
		{
			/// <summary>
			/// Describes the type of operand data.
			/// </summary>
			public abstract InlineOperandType Type { get; }

			protected InlineOperandData() { }

			public InlineOperandData(BinaryReader reader)
			{
				this.Deserialize(reader);
			}

			protected abstract void Deserialize(BinaryReader reader);

			/// <summary>
			/// Read some inline operand data from a BinaryReader.
			/// </summary>
			/// <param name="reader">BinaryReader</param>
			/// <returns>InlineOperandData</returns>
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
					case InlineOperandType.UnknownType7:
						return new UnknownType7(reader);
					default:
						throw new ArgumentOutOfRangeException();
				}
			}
		}

		/// <summary>
		/// Type-related operand data.
		/// </summary>
		/// <remarks>Class46</remarks>
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

		/// <summary>
		/// Field-related operand data.
		/// </summary>
		/// <remarks>Class42</remarks>
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

		/// <summary>
		/// Method-related operand data.
		/// </summary>
		/// <remarks>Class44</remarks>
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

		/// <summary>
		/// String-related operand data.
		/// </summary>
		/// <remarks>Class45</remarks>
		public class StringData : InlineOperandData
		{
			/// <summary>
			/// String value.
			/// </summary>
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
				get { return InlineOperandType.UnknownType7; }
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
