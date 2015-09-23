using System;
using System.Collections.Generic;
using System.Linq;
using dnlib.DotNet;

namespace eazdevirt.Util
{
	public class NameResolver
	{
		ModuleDefMD _module;
		public Importer Importer { get; private set; }

		public NameResolver(ModuleDefMD module)
		{
			_module = module;
			this.Importer = new Importer(module, ImporterOptions.TryToUseDefs);
		}

		/// <summary>
		/// Resolve an IField from its name and a declaring TypeSpec.
		/// </summary>
		/// <param name="declaringType">Declaring TypeSpec</param>
		/// <param name="fieldName">Field name</param>
		/// <returns>IField, or null if none found</returns>
		public IField ResolveField(TypeSpec declaringType, String fieldName)
		{
			TypeDef typeDef = declaringType.ResolveTypeDef();
			if (typeDef == null)
				return null;

			FieldDef fieldDef = typeDef.FindField(fieldName);
			if (fieldDef == null)
				return null;

			MemberRef memberRef = new MemberRefUser(_module, fieldDef.Name, fieldDef.FieldSig, declaringType);
			return this.Importer.Import(memberRef);
		}

		/// <summary>
		/// Resolve an IField from its name and the resolved delcaring type.
		/// </summary>
		/// <param name="declaringType">Declaring type</param>
		/// <param name="fieldName">Field name</param>
		/// <returns>IField, or null if none found</returns>
		public IField ResolveField(ITypeDefOrRef declaringType, String fieldName)
		{
			if (declaringType is TypeSpec)
				return ResolveField(declaringType as TypeSpec, fieldName);

			TypeDef typeDef = null;
			if (declaringType is TypeDef)
				typeDef = declaringType as TypeDef;
			else if (declaringType is TypeRef)
				typeDef = (declaringType as TypeRef).ResolveTypeDef();

			if (typeDef != null)
				return this.Importer.Import(typeDef.FindField(fieldName));
			else
				return null;
		}

		/// <summary>
		/// Resolve a TypeDef or TypeRef from its name. If neither a TypeDef or TypeRef are found
		/// in the module, search its references (AssemblyRefs) and if a match is found, add a TypeRef
		/// for it to the module and return that.
		/// </summary>
		/// <param name="fullName">Name of TypeDef or TypeRef as found in the resource</param>
		/// <param name="isReflectionName">Whether or not the name is a reflection name</param>
		/// <returns>TypeDef or TypeRef, or null if none found</returns>
		public ITypeDefOrRef ResolveTypeDefOrRef(TypeName typeName)
		{
			String fullName = typeName.Name;

			// Return TypeDef if found
			TypeDef typeDef = _module.Find(fullName, false);
			if (typeDef != null)
				return typeDef;

			// Return existing TypeRef if found
			var typeRefs = _module.GetTypeRefs();
			foreach(var typeRef in typeRefs)
			{
				if (typeRef.FullName.Equals(fullName))
					return typeRef;
			}

			// Get the AssemblyRef from the type name and make our own TypeRef
			AssemblyRef asmRef = this.FindAssemblyRef(typeName);
			if(!typeName.IsNested)
				return new TypeRefUser(_module, typeName.Namespace, typeName.NameWithoutNamespace, asmRef);
			else
			{
				// Lazy...
				var parentName = typeName.ParentName.Split('.').Last();
				TypeRef resolutionRef = new TypeRefUser(_module, typeName.Namespace, parentName, asmRef);
				return new TypeRefUser(_module, "", typeName.NestedName, resolutionRef);
			}
		}

		/// <summary>
		/// Get the AssemblyRef of the module from the assembly full name, adding
		/// our own AssemblyRef if none found.
		/// </summary>
		/// <param name="fullName">TypeName containing the assembly's full name</param>
		/// <returns>AssemblyRef</returns>
		public AssemblyRef FindAssemblyRef(TypeName typeName)
		{
			return this.FindAssemblyRef(typeName.AssemblyFullName);
		}

		/// <summary>
		/// Get the AssemblyRef of the module from the assembly full name, adding
		/// our own AssemblyRef if none found.
		/// </summary>
		/// <param name="fullName">Assembly full name</param>
		/// <returns>AssemblyRef</returns>
		public AssemblyRef FindAssemblyRef(String fullName)
		{
			// Try to find AssemblyRef via full name
			var assemblyRef = _module.GetAssemblyRefs().FirstOrDefault((ar) =>
			{
				return ar.FullName.Equals(fullName);
			});

			if (assemblyRef != null)
				return assemblyRef;

			// If unable to find, add our own AssemblyRef from the full name
			return new AssemblyRefUser(new System.Reflection.AssemblyName(fullName));
		}
	}

	/// <summary>
	/// Convenience class for interpreting the type names found in the
	/// encrypted virtualization resources file.
	/// </summary>
	public class TypeName
	{
		/// <summary>
		/// Full name as given in constructor.
		/// </summary>
		public String FullName { get; private set; }

		public TypeName(String fullName)
		{
			// Eazfuscator.NET uses '+' to indicate a nested type name follows, while
			// dnlib uses '/'
			this.FullName = fullName.Replace('+', '/');
		}

		/// <summary>
		/// Full assembly name.
		/// </summary>
		public String AssemblyFullName
		{
			get
			{
				return this.FullName.Substring(
					this.Name.Length + 2,
					this.FullName.Length - (this.Name.Length + 2)
				);
			}
		}

		/// <summary>
		/// Assembly name.
		/// </summary>
		public String AssemblyName
		{
			get { return AssemblyFullName.Split(',')[0]; }
		}

		/// <summary>
		/// Type name without namespace.
		/// </summary>
		public String NameWithoutNamespace
		{
			get
			{
				//if (this.Name.Contains('/'))
				//	return this.Name.Split('/').Last();

				if (this.Name.Contains('.'))
					return this.Name.Split('.').Last();
				else
					return String.Empty;
			}
		}

		/// <summary>
		/// Namespace.
		/// </summary>
		public String Namespace
		{
			get
			{
				if (this.Name.Contains('.'))
				{
					return String.Join(".",
						this.Name.Split('.').Reverse().Skip(1).Reverse().ToArray());
				}
				else
					return this.Name;
			}
		}

		/// <summary>
		/// Type name without assembly info.
		/// </summary>
		public String Name
		{
			get
			{
				if (this.FullName.Contains(", "))
				{
					// return this.FullName.Split(',')[0];
					String fixedName, typeName = this.FullName.Split(',')[0];
					this.GetModifiersStack(typeName, out fixedName);
					return fixedName;
				}
				else return this.FullName;
			}
		}

		public Stack<String> Modifiers
		{
			get
			{
				if (this.FullName.Contains(", "))
				{
					String fixedName, typeName = this.FullName.Split(',')[0];
					return this.GetModifiersStack(typeName, out fixedName);
				}
				else
					return null;
			}
		}

		/// <summary>
		/// Whether or not this name indicates the type is nested.
		/// </summary>
		public Boolean IsNested
		{
			get
			{
				return this.Name.Contains('/');
			}
		}

		/// <summary>
		/// The parent type name if nested. If not nested, null.
		/// </summary>
		public String ParentName
		{
			get
			{
				// Return name without last "+TypeName"
				if (this.IsNested)
					return String.Join("/",
						this.Name.Split('/').Reverse().Skip(1).Reverse().ToArray());
				else
					return null;
			}
		}

		/// <summary>
		/// The nested child type name if nested. If not nested, null.
		/// </summary>
		public String NestedName
		{
			get
			{
				if (this.IsNested)
					return this.Name.Split('/').Last();
				else
					return null;
			}
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

			while (true)
			{
				if (rawName.EndsWith("[]"))
					stack.Push("[]");
				else if (rawName.EndsWith("*"))
					stack.Push("*");
				else if (rawName.EndsWith("&"))
					stack.Push("&");
				else break;

				rawName = rawName.Substring(0, rawName.Length - stack.Peek().Length);
			}

			fixedName = rawName;
			return stack;
		}
	}
}
