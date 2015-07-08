using System;
using System.IO;
using System.Collections.Generic;
using de4dot.blocks;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace eazdevirt.IO
{
	public class EazVirtualizedMethodBodyReader : EazResourceReader
	{
		/// <summary>
		/// Virtualized method.
		/// </summary>
		public EazVirtualizedMethod Method { get; private set; }

		/// <summary>
		/// Size of the method body in bytes.
		/// </summary>
		public Int32 CodeSize { get; private set; }

		/// <summary>
		/// Get the initial position into the embedded resource to begin reading at.
		/// </summary>
		public Int64 InitialPosition
		{
			get { return EazPosition.FromString(this.Method.PositionString, this.Method.ResourceCryptoKey); }
		}

		/// <summary>
		/// Instructions that make up the method body. Is null until read.
		/// </summary>
		public IList<Instruction> Instructions { get; private set; }

		/// <summary>
		/// Whether or not the instructions have been read and set.
		/// </summary>
		public Boolean HasInstructions { get { return this.Instructions != null; } }

		/// <summary>
		/// Whether or not all instructions in this virtual method have been successfully read.
		/// Only set after reading the last instruction.
		/// </summary>
		public Boolean FullyRead { get; private set; }

		public VirtualizedMethodInfo Info { get; private set; }

		/// <summary>
		/// Current IL offset into the method body (byte-wise).
		/// </summary>
		public UInt32 CurrentILOffset { get; private set; }

		/// <summary>
		/// Current virtual offset into the method body (byte-wise).
		/// </summary>
		public UInt32 CurrentVirtualOffset { get; private set; }

		/// <summary>
		/// The last-read virtual opcode.
		/// </summary>
		public Int32 LastVirtualOpCode { get; private set; }

		/// <summary>
		/// Current offset into the method body (instruction-wise).
		/// </summary>
		public UInt32 CurrentInstructionOffset { get; private set; }

		/// <summary>
		/// Resolver.
		/// </summary>
		public EazResolver Resolver { get; private set; }

		/// <summary>
		/// Map of IL offsets to virtual offsets.
		/// </summary>
		public Dictionary<UInt32, UInt32> VirtualOffsets { get; private set; }

		/// <summary>
		/// Construct a method body reader given a virtualized method.
		/// </summary>
		/// <param name="method">Virtualized method</param>
		public EazVirtualizedMethodBodyReader(EazVirtualizedMethod method)
			: base((method != null ? method.Module : null))
		{
			if (method == null)
				throw new ArgumentNullException();

			this.Method = method;

			this.Initialize();
		}

		private void Initialize()
		{
			/*
			var moduleDef = this.Module.Module;
			this.Resource = moduleDef.Resources.FindEmbeddedResource(this.Method.ResourceStringId);

			if (this.Resource == null)
				throw new Exception("Unable to find embedded resource");

			// Open stream + reader and seek to initial position
			this.Stream = new EazCryptoStream(this.Resource.GetResourceStream(), this.Method.ResourceCryptoKey);
			DnBinaryReader reader = new DnBinaryReader(this.Stream);
			this.Reader = reader; // BinaryReader
			this.Stream.Position = this.InitialPosition;
			*/

			this.Stream.Position = this.InitialPosition;
			this.Resolver = new EazResolver(this.Parent);
			this.VirtualOffsets = new Dictionary<UInt32, UInt32>();
		}

		public void Read()
		{
			BinaryReader reader = this.Reader;

			// VM performs this check..
			if (reader.ReadByte() != 0)
				throw new InvalidDataException();

			// Read virtualized method info
			this.Info = new VirtualizedMethodInfo(reader);

			// Read N of some unknown type
			// These are compared/used in some comparison?
			Int32 count = (Int32)reader.ReadInt16();
			UnknownType4[] unknown1 = new UnknownType4[count];
			for (Int32 i = 0; i < unknown1.Length; i++)
				unknown1[i] = new UnknownType4(reader);

			// Read instructions
			this.CodeSize = reader.ReadInt32();
			this.ReadInstructions();
		}

		/// <summary>
		/// Reads all instructions
		/// </summary>
		protected void ReadInstructions()
		{
			this.ReadInstructionsNumBytes(this.CodeSize);
		}

		/// <summary>
		/// Read all the instructions in a byte region.
		/// </summary>
		/// <param name="codeSize">Size of region in bytes</param>
		protected void ReadInstructionsNumBytes(Int32 codeSize)
		{
			// List<Instruction> instructions = new List<Instruction>();
			this.Instructions = new List<Instruction>();

			Int64 finalPosition = this.Stream.Position + codeSize;
			while(this.Stream.Position < finalPosition)
				this.Instructions.Add(this.ReadOneInstruction());

			// After fully read, branch operands can be fixed
			this.FixBranches();

			//this.Instructions = instructions;
			this.FullyRead = true;
		}

		/// <summary>
		/// Read a virtual instruction as a CIL instruction.
		/// </summary>
		/// <returns>CIL instruction</returns>
		protected Instruction ReadOneInstruction()
		{
			Int32 virtualOpcode = this.Reader.ReadInt32();
			this.LastVirtualOpCode = virtualOpcode;

			EazVirtualInstruction virtualInstruction;
			if (!this.Parent.IdentifiedOpCodes.TryGetValue(virtualOpcode, out virtualInstruction))
				//throw new Exception(String.Format("Unknown virtual opcode: {0}", virtualOpcode));
				throw new OriginalOpcodeUnknownException(virtualInstruction);

			OpCode opcode = virtualInstruction.OpCode.ToOpCode();

			this.VirtualOffsets.Add(this.CurrentILOffset, this.CurrentVirtualOffset);

			Instruction instruction = new Instruction(opcode);
			instruction.Offset = this.CurrentILOffset;
			instruction.OpCode = opcode;
			instruction.Operand = this.ReadOperand(instruction);

			if (instruction.OpCode.Code == Code.Switch)
			{
				var targets = (IList<UInt32>)instruction.Operand;
				this.CurrentILOffset += (UInt32)(instruction.OpCode.Size + 4 + 4 * targets.Count);
				this.CurrentVirtualOffset += (UInt32)(instruction.OpCode.Size + 4 + 4 * targets.Count);
			}
			else
				this.CurrentILOffset += (UInt32)instruction.GetSize();
				this.CurrentVirtualOffset += (UInt32)virtualInstruction.GetSize(instruction.Operand);
				// Doesn't apply, all virtual opcodes are size 4:
				// this.CurrentOffset += (UInt32)instruction.GetSize();

			this.CurrentInstructionOffset++;

			/*
			if(opcode.OperandType != OperandType.InlineNone
			&& opcode.OperandType != OperandType.NOT_USED_8
			&& opcode.OperandType != OperandType.InlinePhi)
			{
				Object operand = this.ReadOperand(opcode.Code, opcode.OperandType);
				instruction = new Instruction(opcode, operand);
			}
			else
				instruction = new Instruction(opcode);
			*/

			return instruction;
		}

		/// <summary>
		/// Read a virtual operand.
		/// </summary>
		/// <param name="code">CIL opcode</param>
		/// <param name="operandType">Operand type</param>
		/// <returns>Operand object, or null if unsupported operand type</returns>
		//protected Object ReadOperand(Code code, OperandType operandType)
		protected Object ReadOperand(Instruction instr)
		{
			BinaryReader reader = this.Reader;
			ModuleDefMD module = this.Module;

			// Todo: Fix some of these to factor in current offset
			switch(instr.OpCode.OperandType)
			{
				case OperandType.InlineSwitch:
					Int32 destCount = this.Reader.ReadInt32();
					Int32[] branchDests = new Int32[destCount];
					for (Int32 i = 0; i < destCount; i++)
						branchDests[i] = this.Reader.ReadInt32();
					return branchDests;
				case OperandType.ShortInlineBrTarget:
					return this.ReadShortInlineBrTarget(instr);
				case OperandType.ShortInlineI:
					if (instr.OpCode.Code == Code.Ldc_I4_S)
						return this.Reader.ReadSByte();
					else
						return this.Reader.ReadByte();
				case OperandType.InlineBrTarget: // ?
					return this.ReadInlineBrTarget(instr);
				case OperandType.InlineI:
					return this.Reader.ReadInt32();
				case OperandType.InlineI8:
					return this.Reader.ReadInt64();
				case OperandType.InlineR:
					return this.Reader.ReadDouble();
				case OperandType.ShortInlineR:
					return this.Reader.ReadSingle();
				case OperandType.InlineVar:
					// return this.Reader.ReadUInt16();
					if (IsArgOperandInstruction(instr))
						return this.GetArgument(reader.ReadUInt16());
					return this.GetLocal(reader.ReadUInt16());
				case OperandType.ShortInlineVar:
					return this.Reader.ReadByte();

				// Resolving
				case OperandType.InlineMethod:
					return this.ReadInlineMethod(instr);
				case OperandType.InlineType:
					return this.ReadInlineType(instr);
				case OperandType.InlineField:
					//return module.ResolveField(reader.ReadUInt32());
					return this.ReadInlineField(instr);
				case OperandType.InlineSig:
					// Supposed to return a MethodSig...?
					//return module.ResolveStandAloneSig(reader.ReadUInt32());
					return this.ReadInlineSig(instr);
				case OperandType.InlineTok:
					// Todo: GenericParamContext support, see ReadInlineTok of MethodBodyReader
					//return module.ResolveToken(reader.ReadInt32()) as ITokenOperand;
					return this.ReadInlineTok(instr);
				case OperandType.InlineString:
					//return module.ReadUserString(reader.ReadUInt32());
					return this.ReadInlineString(instr);
			}

			return null;
		}

		/// <summary>
		/// Translates a virtual offset to an IL offset.
		/// </summary>
		/// <param name="virtualOffset">Virtual offset used as virtual branch operand</param>
		/// <returns>Real offset, or UInt32.MaxValue if couldn't translate</returns>
		public UInt32 GetRealOffset(UInt32 virtualOffset)
		{
			foreach(var kvp in this.VirtualOffsets)
			{
				if (kvp.Value == virtualOffset)
					return kvp.Key;
			}

			return UInt32.MaxValue;
		}

		/// <summary>
		/// Fixes all branch instructions so their operands are set to an <see cref="Instruction"/>
		/// instead of a virtual offset.
		/// </summary>
		void FixBranches()
		{
			// Todo: Support switch operands
			foreach(var instr in this.Instructions)
			{
				switch(instr.OpCode.OperandType)
				{
					case OperandType.InlineBrTarget:
					case OperandType.ShortInlineBrTarget:
						UInt32 realOffset = this.GetRealOffset((UInt32)instr.Operand);
						instr.Operand = GetInstruction(realOffset);
						break;
				}
			}
		}

		/// <summary>
		/// Finds an instruction
		/// </summary>
		/// <param name="offset">Offset of instruction</param>
		/// <returns>The instruction or <c>null</c> if there's no instruction at <paramref name="offset"/>.</returns>
		/// <remarks>Copied from MethodBodyReaderBase</remarks>
		protected Instruction GetInstruction(uint offset)
		{
			// The instructions are sorted and all Offset fields are correct. Do a binary search.
			int lo = 0, hi = this.Instructions.Count - 1;
			while (lo <= hi)
			{
				int i = (lo + hi) / 2;
				var instr = this.Instructions[i];
				if (instr.Offset == offset)
					return instr;
				if (offset < instr.Offset)
					hi = i - 1;
				else
					lo = i + 1;
			}
			return null;
		}

		/// <summary>
		/// Reads a <see cref="OperandType.InlineBrTarget"/> operand
		/// </summary>
		/// <param name="instr">The current instruction</param>
		/// <returns>The operand</returns>
		/// <remarks>Copied from MethodBodyReaderBase</remarks>
		protected virtual UInt32 ReadInlineBrTarget(Instruction instr)
		{
			//return instr.Offset + (UInt32)instr.GetSize() + this.Reader.ReadUInt32();
			return (UInt32)this.Reader.ReadUInt32();
		}

		/// <summary>
		/// Reads a <see cref="OperandType.ShortInlineBrTarget"/> operand
		/// </summary>
		/// <param name="instr">The current instruction</param>
		/// <returns>The operand</returns>
		/// <remarks>Copied from MethodBodyReaderBase</remarks>
		protected virtual UInt32 ReadShortInlineBrTarget(Instruction instr)
		{
			//return instr.Offset + (UInt32)instr.GetSize() + (UInt32)this.Reader.ReadSByte();
			return (UInt32)this.Reader.ReadSByte();
		}

		protected virtual IMethod ReadInlineField(Instruction instruction)
		{
			throw new NotSupportedException();
		}

		protected virtual IMethod ReadInlineSig(Instruction instruction)
		{
			throw new NotSupportedException();
		}

		protected virtual IMethod ReadInlineTok(Instruction instruction)
		{
			throw new NotSupportedException();
		}

		protected virtual IMethod ReadInlineMethod(Instruction instruction)
		{
			return this.Resolver.ResolveMethod(this.Reader.ReadInt32());
		}

		protected virtual ITypeDefOrRef ReadInlineType(Instruction instruction)
		{
			return this.Resolver.ResolveType(this.Reader.ReadInt32());
		}

		protected virtual String ReadInlineString(Instruction instruction)
		{
			return this.Resolver.ResolveString(this.Reader.ReadInt32());
		}

		/// <summary>
		/// Translate the specified SerializedParameter to a dnlib Parameter and return it.
		/// </summary>
		/// <param name="index">Index</param>
		/// <returns>Parameter</returns>
		public Parameter GetArgument(UInt16 index)
		{
			// Todo
			return null;
		}

		/// <summary>
		/// Translate the specified SerializedLocal to a dnlib Local and return it.
		/// </summary>
		/// <param name="index">Index</param>
		/// <returns>Local</returns>
		public Local GetLocal(UInt16 index)
		{
			// Todo
			return null;
		}

		/// <summary>
		/// Returns <c>true</c> if it's one of the ldarg/starg instructions that have an operand
		/// </summary>
		/// <param name="instr">The instruction to check</param>
		/// <remarks>Copied from dnlib</remarks>
		protected static bool IsArgOperandInstruction(Instruction instr)
		{
			switch (instr.OpCode.Code)
			{
				case Code.Ldarg:
				case Code.Ldarg_S:
				case Code.Ldarga:
				case Code.Ldarga_S:
				case Code.Starg:
				case Code.Starg_S:
					return true;
				default:
					return false;
			}
		}

		/// <summary>
		/// Serialized virtualized method info.
		/// </summary>
		public class VirtualizedMethodInfo
		{
			public Boolean Unknown1 { get; set; }
			public Int32 ReturnTypeCode { get; set; }
			public Int32 Unknown3 { get; set; }
			public String Name { get; set; }
			public SerializedParameter[] Parameters { get; set; }
			public SerializedLocal[] Locals { get; set; }

			public VirtualizedMethodInfo(BinaryReader reader)
			{
				this.Deserialize(reader);
			}

			private void Deserialize(BinaryReader reader)
			{
				this.Unknown1 = reader.ReadBoolean();
				this.ReturnTypeCode = reader.ReadInt32();
				this.Unknown3 = reader.ReadInt32();
				this.Name = reader.ReadString();

				Int32 count = (Int32)reader.ReadInt16();
				this.Parameters = new SerializedParameter[count];
				for (Int32 i = 0; i < count; i++)
					this.Parameters[i] = new SerializedParameter(reader);

				count = (Int32)reader.ReadInt16();
				this.Locals = new SerializedLocal[count];
				for (Int32 i = 0; i < count; i++)
					this.Locals[i] = new SerializedLocal(reader);
			}
		}

		/// <remarks>Unsure</remarks>
		public class SerializedParameter
		{
			/// <summary>
			/// Type code.
			/// </summary>
			public Int32 TypeCode { get; set; }

			/// <summary>
			/// True if In, false if Out.
			/// </summary>
			public Boolean In { get; set; }

			public SerializedParameter(BinaryReader reader)
			{
				this.Deserialize(reader);
			}

			private void Deserialize(BinaryReader reader)
			{
				this.TypeCode = reader.ReadInt32();
				this.In = reader.ReadBoolean();
			}
		}

		/// <remarks>Unsure</remarks>
		public class SerializedLocal
		{
			public Int32 TypeCode { get; set; }

			public SerializedLocal(BinaryReader reader)
			{
				this.Deserialize(reader);
			}

			private void Deserialize(BinaryReader reader)
			{
				this.TypeCode = reader.ReadInt32();
			}
		}

		/// <remarks>Might have to do with generics?</remarks>
		public class UnknownType4
		{
			public Int32 Unknown1 { get; set; }
			public Int32 Unknown2 { get; set; }
			public UInt32 Unknown3 { get; set; }
			public UInt32 Unknown4 { get; set; }
			public UInt32 Unknown5 { get; set; }
			public UInt32 Unknown6 { get; set; }
			public UInt32 Unknown7 { get; set; }

			public UnknownType4(BinaryReader reader)
			{
				this.Deserialize(reader);
			}

			private void Deserialize(BinaryReader reader)
			{
				this.Unknown1 = reader.ReadInt32();
				this.Unknown2 = reader.ReadInt32();
				this.Unknown3 = reader.ReadUInt32();
				this.Unknown4 = reader.ReadUInt32();
				this.Unknown5 = reader.ReadUInt32();
				this.Unknown6 = reader.ReadUInt32();
				this.Unknown7 = reader.ReadUInt32();
			}
		}
	}
}
