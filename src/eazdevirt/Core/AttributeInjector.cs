using System;
using System.Collections.Generic;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using eazdevirt.IO;
using eazdevirt.Util;

namespace eazdevirt
{
	public class AttributeInjector
	{
		/// <summary>
		/// EazModule.
		/// </summary>
		public EazModule EazModule { get; private set; }

		/// <summary>
		/// Module.
		/// </summary>
		public ModuleDefMD Module
		{
			get { return this.EazModule != null ? this.EazModule.Module : null; }
		}

		private Boolean _initialized = false;
		private TypeDef _devirtualizedAttribute = null;

		/// <summary>
		/// Construct an AttributeInjector for a specific module.
		/// </summary>
		/// <param name="module">Target module</param>
		public AttributeInjector(EazModule module)
		{
			this.EazModule = module;
		}

		void Initialize()
		{
			_devirtualizedAttribute = this.CreateDevirtualizedAttribute();
			this.Module.Types.Add(_devirtualizedAttribute);
			_initialized = true;
		}

		/// <summary>
		/// Create the DevirtualizedAttribute TypeDef, with a "default .ctor" that
		/// calls the base type's .ctor (System.Attribute).
		/// </summary>
		/// <returns>TypeDef</returns>
		TypeDef CreateDevirtualizedAttribute()
		{
			var importer = new Importer(this.Module);
			var attributeRef = this.Module.CorLibTypes.GetTypeRef("System", "Attribute");
			var attributeCtorRef = importer.Import(attributeRef.ResolveTypeDefThrow().FindMethod(".ctor"));

			var devirtualizedAttr = new TypeDefUser(
				"eazdevirt.Injected", "DevirtualizedAttribute", attributeRef);
			//devirtualizedAttr.Attributes = TypeAttributes.Public | TypeAttributes.AutoLayout
			//	| TypeAttributes.Class | TypeAttributes.AnsiClass;

			var emptyCtor = new MethodDefUser(".ctor", MethodSig.CreateInstance(this.Module.CorLibTypes.Void),
				MethodImplAttributes.IL | MethodImplAttributes.Managed,
				MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName |
				MethodAttributes.ReuseSlot | MethodAttributes.HideBySig);

			var instructions = new List<Instruction>();
			instructions.Add(OpCodes.Ldarg_0.ToInstruction());
			instructions.Add(OpCodes.Call.ToInstruction(attributeCtorRef)); // Call the constructor .ctor
			instructions.Add(OpCodes.Ret.ToInstruction());
			emptyCtor.Body = new CilBody(false, instructions, new List<ExceptionHandler>(), new List<Local>());

			devirtualizedAttr.Methods.Add(emptyCtor);

			return devirtualizedAttr;
		}

		/// <summary>
		/// Inject the DevirtualizedAttribute into a method.
		/// </summary>
		/// <param name="method">Method to inject</param>
		public void InjectDevirtualized(MethodDef method)
		{
			if (!_initialized)
				this.Initialize();

			var customAttr = new CustomAttribute(_devirtualizedAttribute.FindMethod(".ctor"));
			method.CustomAttributes.Add(customAttr);
		}
	}
}
