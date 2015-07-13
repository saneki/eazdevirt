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
		/// <summary>
		/// Instructions to add to the virtualizable method.
		/// </summary>
		public IList<Instruction> Instructions { get; private set; }

		/// <summary>
		/// Create a virtualizable assembly generator with some instructions to be
		/// virtualized.
		/// </summary>
		/// <param name="instructions">Instructions</param>
		public VirtualizableAssemblyGenerator(IList<Instruction> instructions)
		{
			this.Instructions = instructions;
		}

		/// <summary>
		/// Create a virtualizable assembly generator with all Conv_* instructions.
		/// </summary>
		/// <returns>Generator</returns>
		public static VirtualizableAssemblyGenerator CreateConvAssembly()
		{
			return new VirtualizableAssemblyGenerator(GetConvInstructions());
		}

		/// <summary>
		/// Generate a test assembly.
		/// </summary>
		/// <returns>Assembly</returns>
		public AssemblyDef Generate()
		{
			ModuleDef module = new ModuleDefUser("eazdevirt-test-module.exe");
			module.Kind = ModuleKind.Console;

			AssemblyDef assembly = new AssemblyDefUser("MyAssembly", new Version(1, 2, 3, 4));
			assembly.Modules.Add(module);

			module.Types.Add(this.CreateMainType(module));
			return assembly;
		}

		/// <summary>
		/// Create a main type with an entry point method.
		/// </summary>
		/// <param name="module">Module</param>
		/// <returns>Main type</returns>
		TypeDef CreateMainType(ModuleDef module)
		{
			TypeDef mainType = new TypeDefUser(
				"MyAssembly", "Program", module.CorLibTypes.Object.TypeDefOrRef);
			mainType.Attributes = TypeAttributes.NotPublic | TypeAttributes.AutoLayout |
				TypeAttributes.Class | TypeAttributes.AnsiClass;

			var vmethod = CreateVirtualizableMethod(module);
			mainType.Methods.Add(this.CreateEntryPoint(module, vmethod));
			mainType.Methods.Add(vmethod);

			return mainType;
		}

		/// <summary>
		/// Create an entry point method which calls the virtualizable method.
		/// </summary>
		/// <param name="module">Module</param>
		/// <param name="vmethod">Virtualizable method</param>
		/// <returns>Entry point method</returns>
		MethodDef CreateEntryPoint(ModuleDef module, IMethod vmethod)
		{
			MethodDef entryPoint = new MethodDefUser("Main",
				MethodSig.CreateStatic(module.CorLibTypes.Int32, new SZArraySig(module.CorLibTypes.String)));

			entryPoint.Attributes = MethodAttributes.Private | MethodAttributes.Static |
				MethodAttributes.HideBySig | MethodAttributes.ReuseSlot;
			entryPoint.ImplAttributes = MethodImplAttributes.IL | MethodImplAttributes.Managed;
			entryPoint.ParamDefs.Add(new ParamDefUser("args", 1));

			entryPoint.Body = new CilBody();
			var instructions = entryPoint.Body.Instructions;
			instructions.Add(OpCodes.Call.ToInstruction(vmethod));
			instructions.Add(OpCodes.Ldc_I4_0.ToInstruction());
			instructions.Add(OpCodes.Ret.ToInstruction());

			module.EntryPoint = entryPoint;

			return entryPoint;
		}

		/// <summary>
		/// Create the virtualizable method with the instructions specified in the constructor.
		/// </summary>
		/// <param name="module">Module</param>
		/// <returns>Virtualizable method</returns>
		MethodDef CreateVirtualizableMethod(ModuleDef module)
		{
			MethodDef vmethod = new MethodDefUser("DevirtualizeMe",
				MethodSig.CreateStatic(module.CorLibTypes.Void));

			vmethod.Attributes = MethodAttributes.Private | MethodAttributes.Static |
				MethodAttributes.HideBySig | MethodAttributes.ReuseSlot;
			vmethod.ImplAttributes = MethodImplAttributes.IL | MethodImplAttributes.Managed;

			vmethod.CustomAttributes.Add(CreateAttribute(module, "renaming", true));
			vmethod.CustomAttributes.Add(CreateAttribute(module, "virtualization", false));

			// Add instructions to body
			vmethod.Body = new CilBody();
			foreach (var instr in this.Instructions)
				vmethod.Body.Instructions.Add(instr);

			return vmethod;
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
		/// Get a sensible list of all Conv_* instructions.
		/// </summary>
		/// <returns>Instructions</returns>
		static IList<Instruction> GetConvInstructions()
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
	}
}
