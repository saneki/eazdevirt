using System;
using System.Collections.Generic;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace eazdevirt.Generator
{
	/// <summary>
	/// Assembly generator for generating a virtualizable assembly that contains an entry point
	/// (Main method) which calls a virtualizable method with some instructions.
	/// </summary>
	public class VirtualizableAssemblyGenerator : IAssemblyGenerator
	{
		public static readonly String DefaultModuleName = "eazdevirt-test-module.exe";
		public static readonly String DefaultAssemblyName = "MyAssembly";
		public static readonly Version DefaultAssemblyVersion = new Version(1, 2, 3, 4);

		private String _assemblyName;
		private String _moduleName;
		private Version _assemblyVersion;

		public delegate IList<Instruction> MethodGenerator(ModuleDef method, TypeDef mainType);
		private Dictionary<String, MethodGenerator> _methods = new Dictionary<String, MethodGenerator>();

		/// <summary>
		/// Whether or not this generator has any virtualizable methods added.
		/// </summary>
		public Boolean HasMethod
		{
			get { return _methods.Count > 0; }
		}

		public VirtualizableAssemblyGenerator()
			: this(DefaultModuleName, DefaultAssemblyName, DefaultAssemblyVersion)
		{
		}

		/// <summary>
		/// Create a virtualizable assembly generator with some instructions to be
		/// virtualized.
		/// </summary>
		public VirtualizableAssemblyGenerator(String moduleName, String assemblyName, Version assemblyVersion)
		{
			_moduleName = moduleName;
			_assemblyName = assemblyName;
			_assemblyVersion = assemblyVersion;
		}

		public void AddConvMethod()
		{
			this.AddMethod("ConvMethod", VirtualizableAssemblyGenerator.GetConvInstructions);
		}

		public void AddIndMethod()
		{
			this.AddMethod("IndMethod", VirtualizableAssemblyGenerator.GetIndInstructions);
		}

		public void AddStaticFieldMethod()
		{
			this.AddMethod("StaticFieldMethod", VirtualizableAssemblyGenerator.GetStaticFieldInstructions);
		}

		/// <summary>
		/// Add a virtualizable method.
		/// </summary>
		/// <param name="name">Method name</param>
		/// <param name="func">Callback which generates the method body</param>
		public void AddMethod(String name, MethodGenerator func)
		{
			if (_methods.ContainsKey(name))
				_methods[name] = func;
			else _methods.Add(name, func);
		}

		/// <summary>
		/// Creates a custom ObfuscationAttribute that can be added to a method.
		/// </summary>
		/// <param name="module">Module</param>
		/// <param name="feature">Obfuscation feature name</param>
		/// <param name="exclude">true if exclude, false if include</param>
		/// <returns>CustomAttribute</returns>
		CustomAttribute CreateAttribute(ModuleDef module, String feature, Boolean exclude)
		{
			TypeSig stringSig = module.CorLibTypes.String;
			TypeSig booleanSig = module.CorLibTypes.Boolean;

			CANamedArgument[] args = new CANamedArgument[] {
				// Feature
				new CANamedArgument(
					false,
					stringSig,
					"Feature",
					new CAArgument(stringSig, feature)),

				// Exclude
				new CANamedArgument(
					false,
					booleanSig,
					"Exclude",
					new CAArgument(booleanSig, exclude))
			};

			TypeRef obfuscationRef = new TypeRefUser(
				module, "System.Reflection", "ObfuscationAttribute", module.CorLibTypes.AssemblyRef);

			MemberRef obfuscationCtor = new MemberRefUser(module, ".ctor",
						MethodSig.CreateInstance(module.CorLibTypes.Void),
						obfuscationRef);

			CustomAttribute attr = new CustomAttribute(
				obfuscationCtor,
				new CAArgument[0],
				args
			);

			return attr;
		}

		/// <summary>
		/// Create an entry point method which calls all virtualizable methods.
		/// </summary>
		/// <param name="module">Module</param>
		/// <param name="methods">Methods to call</param>
		/// <returns>Entry point method</returns>
		MethodDef CreateEntryPoint(ModuleDef module, IList<MethodDef> methods)
		{
			MethodDef entryPoint = new MethodDefUser("Main",
				MethodSig.CreateStatic(module.CorLibTypes.Int32, new SZArraySig(module.CorLibTypes.String)));

			entryPoint.Attributes = MethodAttributes.Private | MethodAttributes.Static |
				MethodAttributes.HideBySig | MethodAttributes.ReuseSlot;
			entryPoint.ImplAttributes = MethodImplAttributes.IL | MethodImplAttributes.Managed;
			entryPoint.ParamDefs.Add(new ParamDefUser("args", 1));

			entryPoint.Body = new CilBody();
			var instructions = entryPoint.Body.Instructions;

			foreach (var method in methods)
				instructions.Add(OpCodes.Call.ToInstruction(method));

			instructions.Add(OpCodes.Ldc_I4_0.ToInstruction());
			instructions.Add(OpCodes.Ret.ToInstruction());

			// Set itself as entry point
			module.EntryPoint = entryPoint;

			return entryPoint;
		}

		/// <summary>
		/// Create a main type.
		/// </summary>
		/// <param name="module">Module</param>
		/// <returns>Main type</returns>
		TypeDef CreateMainType(ModuleDef module)
		{
			TypeDef mainType = new TypeDefUser(
				this._assemblyName, "Program", module.CorLibTypes.Object.TypeDefOrRef);
			mainType.Attributes = TypeAttributes.NotPublic | TypeAttributes.AutoLayout |
				TypeAttributes.Class | TypeAttributes.AnsiClass;

			return mainType;
		}

		/// <summary>
		/// Create a virtualizable method.
		/// </summary>
		/// <param name="module">Module</param>
		/// <param name="name">Method name</param>
		/// <param name="instructions">Method body instructions</param>
		/// <returns>MethodDef</returns>
		MethodDef CreateVirtualizableMethod(ModuleDef module, String name, IList<Instruction> instructions)
		{
			MethodDef vmethod = new MethodDefUser(name,
				MethodSig.CreateStatic(module.CorLibTypes.Void));

			vmethod.Attributes = MethodAttributes.Private | MethodAttributes.Static |
				MethodAttributes.HideBySig | MethodAttributes.ReuseSlot;
			vmethod.ImplAttributes = MethodImplAttributes.IL | MethodImplAttributes.Managed;

			vmethod.CustomAttributes.Add(CreateAttribute(module, "renaming", true));
			vmethod.CustomAttributes.Add(CreateAttribute(module, "virtualization", false));

			// Add instructions to body
			vmethod.Body = new CilBody();
			foreach (var instr in instructions)
				vmethod.Body.Instructions.Add(instr);

			return vmethod;
		}

		/// <summary>
		/// Generate a test assembly.
		/// </summary>
		/// <returns>Assembly</returns>
		public AssemblyDef Generate()
		{
			var module = new ModuleDefUser(_moduleName);
			module.Kind = ModuleKind.Console;

			var assembly = new AssemblyDefUser(_assemblyName, _assemblyVersion);
			assembly.Modules.Add(module);

			var mainType = this.CreateMainType(module);
			module.Types.Add(mainType);

			var generatedMethods = new List<MethodDef>();
			foreach (var kvp in _methods)
			{
				var name = kvp.Key;
				var instructions = kvp.Value(module, mainType);
				var method = CreateVirtualizableMethod(module, name, instructions);

				mainType.Methods.Add(method);
				generatedMethods.Add(method);
			}

			var entryPoint = this.CreateEntryPoint(module, generatedMethods);
			mainType.Methods.Add(entryPoint);

			return assembly;
		}

		/// <summary>
		/// Get a sensible list of all Conv_* instructions.
		/// </summary>
		/// <returns>Instructions</returns>
		static IList<Instruction> GetConvInstructions(ModuleDef module, TypeDef mainType)
		{
			var all = new List<Instruction>();

			all.Add(OpCodes.Ldc_I4_0.ToInstruction());

			all.Add(OpCodes.Conv_I.ToInstruction());
			all.Add(OpCodes.Conv_I1.ToInstruction());
			all.Add(OpCodes.Conv_I2.ToInstruction());
			all.Add(OpCodes.Conv_I4.ToInstruction());
			all.Add(OpCodes.Conv_I8.ToInstruction());
			all.Add(OpCodes.Conv_Ovf_I.ToInstruction());
			all.Add(OpCodes.Conv_Ovf_I_Un.ToInstruction());
			all.Add(OpCodes.Conv_Ovf_I1.ToInstruction());
			all.Add(OpCodes.Conv_Ovf_I1_Un.ToInstruction());
			all.Add(OpCodes.Conv_Ovf_I2.ToInstruction());
			all.Add(OpCodes.Conv_Ovf_I2_Un.ToInstruction());
			all.Add(OpCodes.Conv_Ovf_I4.ToInstruction());
			all.Add(OpCodes.Conv_Ovf_I4_Un.ToInstruction());
			all.Add(OpCodes.Conv_Ovf_I8.ToInstruction());
			all.Add(OpCodes.Conv_Ovf_I8_Un.ToInstruction());
			all.Add(OpCodes.Conv_Ovf_U.ToInstruction());
			all.Add(OpCodes.Conv_Ovf_U_Un.ToInstruction());
			all.Add(OpCodes.Conv_Ovf_U1.ToInstruction());
			all.Add(OpCodes.Conv_Ovf_U1_Un.ToInstruction());
			all.Add(OpCodes.Conv_Ovf_U2.ToInstruction());
			all.Add(OpCodes.Conv_Ovf_U2_Un.ToInstruction());
			all.Add(OpCodes.Conv_Ovf_U4.ToInstruction());
			all.Add(OpCodes.Conv_Ovf_U4_Un.ToInstruction());
			all.Add(OpCodes.Conv_Ovf_U8.ToInstruction());
			all.Add(OpCodes.Conv_Ovf_U8_Un.ToInstruction());
			all.Add(OpCodes.Conv_R_Un.ToInstruction());
			all.Add(OpCodes.Conv_R4.ToInstruction());
			all.Add(OpCodes.Conv_R8.ToInstruction());
			all.Add(OpCodes.Conv_U.ToInstruction());
			all.Add(OpCodes.Conv_U1.ToInstruction());
			all.Add(OpCodes.Conv_U2.ToInstruction());
			all.Add(OpCodes.Conv_U4.ToInstruction());
			all.Add(OpCodes.Conv_U8.ToInstruction());

			all.Add(OpCodes.Ret.ToInstruction());

			return all;
		}

		/// <summary>
		/// Get a sensible list of all static field instructions.
		/// </summary>
		/// <returns>Instructions</returns>
		static IList<Instruction> GetStaticFieldInstructions(ModuleDef module, TypeDef mainType)
		{
			FieldDef field = new FieldDefUser(
				"StaticInteger",
				new FieldSig(module.CorLibTypes.Int32),
				FieldAttributes.Static | FieldAttributes.Public
			);

			mainType.Fields.Add(field);

			var all = new List<Instruction>();
			all.Add(OpCodes.Ldsfld.ToInstruction(field));
			all.Add(OpCodes.Stsfld.ToInstruction(field));
			all.Add(OpCodes.Ldsflda.ToInstruction(field));

			// Pop causes the virtualizer to optimize in some cases,
			// and remove previous instruction
			// all.Add(OpCodes.Pop.ToInstruction());

			all.Add(OpCodes.Ret.ToInstruction());
			return all;
		}

		/// <summary>
		/// Get a sensible list of all Ldind_*, Stind_* instructions.
		/// </summary>
		/// <param name="module">Module</param>
		/// <param name="mainType">Main type</param>
		/// <returns>Instructions</returns>
		static IList<Instruction> GetIndInstructions(ModuleDef module, TypeDef mainType)
		{
			var all = new List<Instruction>();

			// This is a bit lazy
			for (Int32 i = 0; i < 11; i++)
			{
				all.Add(OpCodes.Ldc_I4_0.ToInstruction());
			}

			all.Add(OpCodes.Ldind_I.ToInstruction());
			all.Add(OpCodes.Ldind_I1.ToInstruction());
			all.Add(OpCodes.Ldind_I2.ToInstruction());
			all.Add(OpCodes.Ldind_I4.ToInstruction());
			all.Add(OpCodes.Ldind_I8.ToInstruction());
			all.Add(OpCodes.Ldind_R4.ToInstruction());
			all.Add(OpCodes.Ldind_R8.ToInstruction());
			all.Add(OpCodes.Ldind_Ref.ToInstruction());
			all.Add(OpCodes.Ldind_U1.ToInstruction());
			all.Add(OpCodes.Ldind_U2.ToInstruction());
			all.Add(OpCodes.Ldind_U4.ToInstruction());

			for (Int32 i = 0; i < 8; i++)
			{
				all.Add(OpCodes.Ldc_I4_0.ToInstruction());
				all.Add(OpCodes.Ldc_I4_0.ToInstruction());
			}

			all.Add(OpCodes.Stind_I.ToInstruction());
			all.Add(OpCodes.Stind_I1.ToInstruction());
			all.Add(OpCodes.Stind_I2.ToInstruction());
			all.Add(OpCodes.Stind_I4.ToInstruction());
			all.Add(OpCodes.Stind_I8.ToInstruction());
			all.Add(OpCodes.Stind_R4.ToInstruction());
			all.Add(OpCodes.Stind_R8.ToInstruction());
			all.Add(OpCodes.Stind_Ref.ToInstruction());

			return all;
		}
	}
}
