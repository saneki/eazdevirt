using System;
using System.Collections.Generic;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace eazdevirt
{
	/// <summary>
	/// Contains information about a specific virtual instruction.
	/// </summary>
	public class EazVirtualInstruction
	{
		/// <summary>
		/// The container type that holds all instruction fields.
		/// </summary>
		public TypeDef ContainerType { get; private set; }

		/// <summary>
		/// Instruction field. These are all initialized in the .ctor of the container.
		/// </summary>
		public FieldDef InstructionField { get; private set; }

		/// <summary>
		/// The virtual opcode, set when the instruction field is constructed.
		/// </summary>
		public Int32 VirtualOpCode { get; private set; }

		/// <summary>
		/// The operand type, set when the instruction field is constructed.
		/// </summary>
		public Int32 OperandType { get; private set; }

		/// <summary>
		/// The dictionary method used by the main virtualization class, which returns a dictionary
		/// of all virtual instructions (with their respective delegates) mapped by virtual opcode.
		/// </summary>
		public MethodDef DictionaryMethod { get; private set; }

		/// <summary>
		/// The delegate method associated with this virtual instruction in the dictionary method.
		/// </summary>
		public MethodDef DelegateMethod { get; private set; }

		/// <summary>
		/// OpCode pattern seen per dictionary add in the dictionary method.
		/// </summary>
		public static readonly Code[] DictionaryAddPattern = new Code[] {
			Code.Ldloc_0,
			Code.Ldarg_0,
			Code.Ldfld,
			Code.Ldfld,
			Code.Callvirt,
			Code.Ldarg_0,
			Code.Ldfld,
			Code.Ldfld,
			Code.Ldarg_0,
			Code.Ldftn,
			Code.Newobj,
			Code.Newobj,
			Code.Callvirt
		};

		protected EazVirtualInstruction()
		{
		}

		/// <summary>
		/// Find all virtual instructions given the main virtualization type.
		/// </summary>
		/// <param name="virtualizationType">Main virtualization type (class)</param>
		/// <returns>All found virtualization instructions</returns>
		public static EazVirtualInstruction[] FindAllInstructions(TypeDef virtualizationType)
		{
			if (virtualizationType == null)
				throw new ArgumentNullException();

			// Find dictionary method
			MethodDef dictMethod = null;
			var methods = virtualizationType.Methods;
			foreach(var method in methods)
			{
				if(method.IsPrivate && !method.IsStatic
				&& method.Parameters.Count == 1
				&& method.HasReturnType
				&& method.ReturnType.FullName.StartsWith("System.Collections.Generic.Dictionary"))
				{
					dictMethod = method;
					break;
				}
			}

			if (dictMethod == null)
				throw new Exception("Unable to find dictionary method");

			// Each dictionary addition looks like this:
			//IL_000b: ldloc.0  // [0]
			//IL_000c: ldarg.0  // [0]
			//IL_000d: ldfld class Class33 Class805::class33_0 // 0x0400092c // TypeDef of this class (Class33) is important
			//IL_0012: ldfld class Class487 Class33::class487_162 // 0x040000da // FieldDef of this field (class487_162) is important
			//IL_0017: callvirt instance int32 Class487::method_1() // 0x06000ac3
			//IL_001c: ldarg.0  // [0]
			//IL_001d: ldfld class Class33 Class805::class33_0 // 0x0400092c
			//IL_0022: ldfld class Class487 Class33::class487_162 // 0x040000da
			//IL_0027: ldarg.0  // [0]
			//IL_0028: ldftn instance void Class805::method_281(class Class1) // 0x060015ca
			//IL_002e: newobj instance void Class805/Delegate9::.ctor(object, native int) // 0x060015dc
			//IL_0033: newobj instance void Class805/Class808::.ctor(class Class487, class Class805/Delegate9) // 0x060015e5
			//IL_0038: callvirt instance void class [mscorlib]System.Collections.Generic.Dictionary`2<int32, class Class805/Class808>::Add(!0, !1) // 0x0a000b63

			if (!dictMethod.HasBody || !dictMethod.Body.HasInstructions)
				throw new Exception("Dictionary method has no instructions");

			IList<Instruction[]> subsequences = Helpers.FindOpCodePatterns(dictMethod.Body.Instructions, DictionaryAddPattern);

			// Remove this check later..?
			if (subsequences.Count != 203)
				throw new Exception("Number of found subsequences (DictionaryAddPattern) != 203 (expected value)");

			List<EazVirtualInstruction> vInstructions = new List<EazVirtualInstruction>();

			TypeDef containerType = null;

			// Each series of instructions represents a virtualized instruction
			foreach(var instrs in subsequences)
			{
				EazVirtualInstruction vInstruction = new EazVirtualInstruction();

				containerType = ((FieldDef)instrs[2].Operand).FieldType.TryGetTypeDef(); // ldfld
				FieldDef instructionField = ((FieldDef)instrs[3].Operand); // ldfld
				MethodDef delegateMethod = ((MethodDef)instrs[9].Operand); // ldftn

				vInstruction.DictionaryMethod = dictMethod;
				vInstruction.ContainerType = containerType;
				vInstruction.InstructionField = instructionField;
				vInstruction.DelegateMethod = delegateMethod;

				vInstructions.Add(vInstruction);
			}

			if (containerType == null)
				throw new Exception("Container type cannot be null");

			// Get the container .ctor method
			MethodDef containerCtor = null;
			foreach(var m in containerType.FindMethods(".ctor"))
			{
				containerCtor = m;
				break;
			}

			if (containerCtor == null)
				throw new Exception("Container .ctor method cannot be found");

			// Each field construction looks like this:
			//IL_0000: ldarg.0  // [0]
			//IL_0001: ldc.i4 1550052828
			//IL_0006: ldc.i4.5
			//IL_0007: newobj instance void Class487::.ctor(int32, valuetype Enum2) // 0x06000ac1
			//IL_000c: stfld class Class487 Class33::class487_47 // 0x04000067

			if (!containerCtor.HasBody || !containerCtor.Body.HasInstructions)
				throw new Exception("Container .ctor method has no instructions");

			if (containerCtor.Body.Instructions.Count < (vInstructions.Count * 5))
				throw new Exception("Container .ctor not large enough for all virtual instructions");

			// This makes a bit of an assumption..
			for(int i = 0; i < vInstructions.Count; i++)
			{
				// Grab the subsequence
				List<Instruction> subsequence = new List<Instruction>();
				for (int j = 0; j < 5; j++)
					subsequence.Add(containerCtor.Body.Instructions[(i * 5) + j]);

				if (subsequence[0].OpCode.Code != Code.Ldarg_0)
					throw new Exception("Unexpected opcode in container .ctor subsequence");

				Int32 virtualOpCode = Helpers.GetLdcOperand(subsequence[1]);
				Int32 operandType = Helpers.GetLdcOperand(subsequence[2]);
				FieldDef instructionField = (FieldDef)subsequence[4].Operand;

				// Find virtual instruction with matching instruction field MD token to set
				foreach(var vInstr in vInstructions)
				{
					if(vInstr.InstructionField.MDToken == instructionField.MDToken)
					{
						vInstr.VirtualOpCode = virtualOpCode;
						vInstr.OperandType = operandType;
						break;
					}
				}
			}

			return vInstructions.ToArray();
		}

		/// <summary>
		/// Check if the delegate method's body contains the given pattern.
		/// </summary>
		/// <param name="codePattern">Pattern to check for</param>
		/// <returns>true if match, false if not</returns>
		public Boolean Matches(Code[] codePattern)
		{
			if(this.DelegateMethod == null)
				throw new Exception("Cannot check for delegate method match, delegate method is null");

			return (Helpers.FindOpCodePatterns(this.DelegateMethod.Body.Instructions, codePattern).Count > 0);
		}

		public Boolean MatchesIndirect(Code[] codePattern)
		{
			if (this.DelegateMethod == null)
				throw new Exception("Cannot check for delegate method match, delegate method is null");

			// Get the method right before ret
			// (unsure how well this will work with obfuscation, probably not well at all)
			MethodDef retMethod = Helpers.GetRetMethod(this.DelegateMethod);

			if (retMethod != null)
				return (Helpers.FindOpCodePatterns(retMethod.Body.Instructions, codePattern).Count > 0);
			else return false;
		}
	}
}
