using System;
using dnlib.DotNet;
using de4dot.blocks;
using dnlib.DotNet.Emit;

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
	}
}
