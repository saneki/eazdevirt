using System;
using System.Collections.Generic;
using de4dot.blocks;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using eazdevirt.Util;

namespace eazdevirt
{
	public class EazVirtualization
	{
		/// <summary>
		/// The parent module.
		/// </summary>
		public EazModule Module { get; set; }

		/// <summary>
		/// Main virtualization type.
		/// </summary>
		public TypeDef VirtualizationType { get; set; }

		/// <summary>
		/// The field used to store the arguments.
		/// </summary>
		public FieldDef ArgumentsField { get; private set; }

		/// <summary>
		/// The field used to store local variables.
		/// </summary>
		public FieldDef LocalsField { get; private set; }

		/// <summary>
		/// A few Type fields are set in .cctor.
		/// </summary>
		public Dictionary<FieldDef, ITypeDefOrRef> TypeFields { get; private set; }

		public EazVirtualization(EazModule module)
		{
			if (module == null)
				throw new ArgumentNullException();

			this.Module = module;
			this.Initialize();
		}

		private void Initialize()
		{
			var vmethod = this.Module.FindFirstVirtualizedMethod();
			this.VirtualizationType = vmethod.VirtualCallMethod.DeclaringType;

			this.InitializeArgumentsField(); // Set ArgumentsField
			this.InitializeLocalsField();    // Set LocalsField
			this.InitializeTypeFields();     // Set TypeFields
		}

		/// <summary>
		/// Find and set the ArgumentsField.
		/// </summary>
		private void InitializeArgumentsField()
		{
			// Get arguments field
			MethodDef setArgumentsMethod = null;
			var methods = this.VirtualizationType.Methods;
			foreach (var method in methods)
			{
				if (!method.IsStatic && method.IsPrivate
				&& method.ReturnType.FullName.Equals("System.Object")
				&& method.Parameters.Count == 5
				&& method.Parameters[1].Type.FullName.Equals("System.Object[]")
				&& method.Parameters[2].Type.FullName.Equals("System.Type[]")
				&& method.Parameters[3].Type.FullName.Equals("System.Type[]")
				&& method.Parameters[4].Type.FullName.Equals("System.Object[]"))
				{
					setArgumentsMethod = method;
					break;
				}
			}

			if (setArgumentsMethod == null)
				throw new Exception("Unable to find the method in which the arguments field is set");

			var instructions = setArgumentsMethod.Body.Instructions;
			DotNetUtils.GetInstructions(instructions, 4, Code.Stfld.ToOpCode());

			Int32 stfldCount = 0;
			foreach (var instr in instructions)
			{
				if (instr.OpCode.Code == Code.Stfld)
					stfldCount++;

				if (stfldCount == 4)
				{
					this.ArgumentsField = (FieldDef)instr.Operand;
					break;
				}
			}

			if (this.ArgumentsField == null)
				throw new Exception("Unable to find arguments field");
		}

		private void InitializeLocalsField()
		{
			// Locals field is the same type as arguments field
			var fields = this.VirtualizationType.Fields;
			foreach(var field in fields)
			{
				if(field.FieldType.FullName.Equals(this.ArgumentsField.FieldType.FullName)
				&& field.MDToken != this.ArgumentsField.MDToken)
				{
					this.LocalsField = field;
					break;
				}
			}

			if (this.LocalsField == null)
				throw new Exception("Unable to find locals field");
		}

		private void InitializeTypeFields()
		{
			this.TypeFields = new Dictionary<FieldDef, ITypeDefOrRef>();
			MethodDef cctor = this.VirtualizationType.FindMethod(".cctor");
			if (cctor == null)
				throw new Exception("Unable to find virtualization type .cctor");

			if (!cctor.HasBody || !cctor.Body.HasInstructions)
				throw new Exception("Virtualization type .cctor has no instructions");

			var subs = cctor.FindAll(new Code[] { Code.Ldtoken, Code.Call, Code.Stsfld });
			foreach(var sub in subs)
			{
				FieldDef field = sub[2].Operand as FieldDef;
				if (field == null)
					continue;

				ITypeDefOrRef type = sub[0].Operand as ITypeDefOrRef;
				if (type == null)
					continue;

				if (this.TypeFields.ContainsKey(field))
				{
					Console.WriteLine("[InitializeTypeFields] WARNING: Overwriting ITypeDefOrRef for FieldDef");
					this.TypeFields[field] = type;
				}
				else this.TypeFields.Add(field, type);
			}
		}

		/// <summary>
		/// Get the FieldDef which maps to a type of a specific name.
		/// </summary>
		/// <param name="typeName">Name of type</param>
		/// <returns>FieldDef if successful, null if not</returns>
		public FieldDef GetTypeField(String typeName)
		{
			foreach(var kvp in this.TypeFields)
			{
				if (kvp.Value.FullName.Equals(typeName))
					return kvp.Key;
			}

			return null;
		}
	}
}
