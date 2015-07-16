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
		/// Method stub of virtualized method.
		/// </summary>
		public MethodStub Method { get; private set; }

		/// <summary>
		/// Size of the method body in bytes.
		/// </summary>
		public Int32 CodeSize { get; private set; }

		/// <summary>
		/// Get the initial position into the embedded resource to begin reading at.
		/// </summary>
		public Int64 InitialPosition
		{
			get { return Position.FromString(this.Method.PositionString, this.Method.ResourceCryptoKey); }
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
		/// Exception handlers. Not set until the method is read.
		/// </summary>
		public IList<ExceptionHandler> ExceptionHandlers { get; private set; }

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
		/// Logger.
		/// </summary>
		public ILogger Logger { get; private set; }

		/// <summary>
		/// Serialized exception handlers read from the embedded resource. After the method body is
		/// read, these will be translated to dnlib ExceptionHandlers.
		/// </summary>
		private IList<SerializedExceptionHandler> _exceptionHandlers;

		/// <summary>
		/// Construct a method body reader given a method stub.
		/// </summary>
		/// <param name="method">Method stub</param>
		public EazVirtualizedMethodBodyReader(MethodStub method)
			: this(method, null)
		{
		}

		/// <summary>
		/// Construct a method body reader given a method stub.
		/// </summary>
		/// <param name="method">Method stub</param>
		/// <param name="logger">Logger</param>
		public EazVirtualizedMethodBodyReader(MethodStub method, ILogger logger)
			: base((method != null ? method.Parent : null))
		{
			if (method == null)
				throw new ArgumentNullException();

			this.Method = method;
			this.Logger = (logger != null ? logger : DummyLogger.NoThrowInstance);

			this.Initialize();
		}

		/// <summary>
		/// Set the ExceptionHandlers property to a list of dnlib ExceptionHandlers.
		/// </summary>
		protected void FixExceptionHandlers()
		{
			this.ExceptionHandlers = this.GetExceptionHandlers();
		}

		/// <summary>
		/// Convert all SerializedExceptionHandlers to dnlib ExceptionHandlers.
		/// </summary>
		/// <returns>List of ExceptionHandlers</returns>
		IList<ExceptionHandler> GetExceptionHandlers()
		{
			IList<ExceptionHandler> handlers = new List<ExceptionHandler>();
			for (Int32 i = 0; i < _exceptionHandlers.Count; i++)
				handlers.Add(this.GetExceptionHandler(i));
			return handlers;
		}

		/// <summary>
		/// Convert the SerializedExceptionHandler at some index to a dnlib ExceptionHandler
		/// and return it.
		/// </summary>
		/// <param name="index">Index</param>
		/// <returns>ExceptionHandler</returns>
		ExceptionHandler GetExceptionHandler(Int32 index)
		{
			var deserialized = _exceptionHandlers[index];

			ExceptionHandler handler = new ExceptionHandler(deserialized.HandlerType);
			if (deserialized.HasCatchType)
				handler.CatchType = this.Resolver.ResolveType(deserialized.VirtualCatchType);

			handler.TryStart = GetInstruction(this.GetRealOffset(deserialized.VirtualTryStart));
			handler.TryEnd = GetInstruction(this.GetRealOffset(deserialized.VirtualTryEnd));
			handler.HandlerStart = GetInstruction(this.GetRealOffset(deserialized.VirtualHandlerStart));

			// VirtualHandlerEnd actually points to virtual instruction before the actual end
			handler.HandlerEnd = GetInstruction(this.GetRealOffset(deserialized.VirtualHandlerEnd));
			handler.HandlerEnd = GetInstructionAfter(handler.HandlerEnd);

			handler.FilterStart = GetInstruction(this.GetRealOffset(deserialized.VirtualFilterStart));

			return handler;
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
			this.Resolver = new EazResolver(this.Parent, this.Logger);
			this.VirtualOffsets = new Dictionary<UInt32, UInt32>();
			this.ExceptionHandlers = new ExceptionHandler[0];
		}

		public void Read()
		{
			BinaryReader reader = this.Reader;

			// VM performs this check..
			if (reader.ReadByte() != 0)
				throw new InvalidDataException();

			// Read virtualized method info
			this.Info = new VirtualizedMethodInfo(reader);

			// Read exception handlers
			Int32 count = (Int32)reader.ReadInt16();
			_exceptionHandlers = new SerializedExceptionHandler[count];
			for (Int32 i = 0; i < _exceptionHandlers.Count; i++)
				_exceptionHandlers[i] = new SerializedExceptionHandler(reader);

			//if (count > 0)
			//{
			//	Console.WriteLine("Exception Handlers ({0}):", count);
			//	for (Int32 i = 0; i < _exceptionHandlers.Count; i++)
			//	{
			//		var handler = _exceptionHandlers[i];
			//		Console.WriteLine(" Exception handler {0}:", i);
			//		Console.WriteLine("  Handler Type:  {0}", handler.VirtualHandlerType);
			//
			//		if(handler.HasCatchType)
			//			Console.WriteLine("  Catch Type:    {0}", this.Resolver.ResolveType(handler.VirtualCatchType).FullName);
			//		else
			//			Console.WriteLine("  Catch Type:    {0}", handler.VirtualCatchType);
			//
			//		Console.WriteLine("  Try start:     {0}", handler.VirtualTryStart);
			//		Console.WriteLine("  Try end:       {0}", handler.VirtualTryEnd);
			//		Console.WriteLine("  Handler start: {0}", handler.VirtualHandlerStart);
			//		Console.WriteLine("  Handler end:   {0}", handler.VirtualHandlerEnd);
			//		Console.WriteLine("  Filter start:  {0}", handler.VirtualFilterStart);
			//	}
			//}

			// Set locals and parameters
			this.SetLocalsAndParameters();

			// Read instructions
			this.CodeSize = reader.ReadInt32();
			this.ReadInstructions();
		}

		protected void SetLocalsAndParameters()
		{
			ITypeDefOrRef type;

			foreach(var local in this.Info.Locals)
			{
				type = this.Resolver.ResolveType(local.TypeCode);

				if(type != null)
					this.Locals.Add(new Local(type.ToTypeSig(true)));
				else
					this.Logger.Verbose(this, "[SetLocalsAndParameters] WARNING: Unable to resolve local type");
			}

			for(Int32 i = 0; i < this.Info.Parameters.Length; i++)
			{
				var parameter = this.Info.Parameters[i];
				type = this.Resolver.ResolveType(parameter.TypeCode);

				if(type != null)
					this.Parameters.Add(new Parameter(i, type.ToTypeSig(true)));
				else
					this.Logger.Verbose(this, "[SetLocalsAndParameters] WARNING: Unable to resolve parameter type");
			}
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

			// Also set real exception handlers
			this.FixExceptionHandlers();

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

			VirtualOpCode virtualInstruction;
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
				this.CurrentILOffset += (UInt32)(instruction.OpCode.Size + 4 + (4 * targets.Count));
				this.CurrentVirtualOffset += (UInt32)(4 + 4 + (4 * targets.Count));
			}
			else
			{
				this.CurrentILOffset += (UInt32)instruction.GetSize();
				this.CurrentVirtualOffset += (UInt32)virtualInstruction.GetSize(instruction.Operand);
				// Doesn't apply, all virtual opcodes are size 4:
				// this.CurrentOffset += (UInt32)instruction.GetSize();
			}

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
					return this.ReadInlineSwitch(instr);
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
					return this.ReadInlineVar(instr);
				case OperandType.ShortInlineVar:
					return this.ReadShortInlineVar(instr);

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
			foreach(var instr in this.Instructions)
			{
				switch(instr.OpCode.OperandType)
				{
					case OperandType.InlineBrTarget:
					case OperandType.ShortInlineBrTarget:
						UInt32 realOffset = this.GetRealOffset((UInt32)instr.Operand);
						instr.Operand = this.GetInstruction(realOffset);
						break;

					case OperandType.InlineSwitch:
						Int32[] virtualOffsets = instr.Operand as Int32[];
						Instruction[] destinations = new Instruction[virtualOffsets.Length];
						for (Int32 i = 0; i < virtualOffsets.Length; i++)
							destinations[i] = this.GetInstruction(this.GetRealOffset((UInt32)virtualOffsets[i]));
						instr.Operand = destinations;
						break;
				}
			}
		}

		/// <summary>
		/// Get the instruction after the specified instruction in the list.
		/// </summary>
		/// <param name="instruction">Specified instruction</param>
		/// <returns>
		/// Instruction afterwards, or null if specified instruction not found
		/// or specified instruction last in the list
		/// </returns>
		protected Instruction GetInstructionAfter(Instruction instruction)
		{
			for (Int32 i = 0; i < this.Instructions.Count; i++)
			{
				if (this.Instructions[i] == instruction
					&& i < (this.Instructions.Count - 2))
					return this.Instructions[i + 1];
			}

			return null;
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

		protected virtual IVariable ReadInlineVar(Instruction instr)
		{
			if (IsArgOperandInstruction(instr))
				return this.GetArgument(this.Reader.ReadUInt16());
			return this.GetLocal(this.Reader.ReadUInt16());
		}

		protected virtual IVariable ReadShortInlineVar(Instruction instr)
		{
			if (IsArgOperandInstruction(instr))
				return this.GetArgument(this.Reader.ReadByte());
			return this.GetLocal(this.Reader.ReadByte());
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

		protected virtual UInt32[] ReadInlineSwitch(Instruction instr)
		{
			UInt32 destCount = this.Reader.ReadUInt32();
			UInt32[] branchDests = new UInt32[destCount];
			for (UInt32 i = 0; i < destCount; i++)
				branchDests[i] = this.Reader.ReadUInt32();
			return branchDests;
		}

		protected virtual IField ReadInlineField(Instruction instruction)
		{
			return this.Resolver.ResolveField(this.Reader.ReadInt32());
		}

		protected virtual MethodSig ReadInlineSig(Instruction instruction)
		{
			throw new NotSupportedException();
		}

		protected virtual ITokenOperand ReadInlineTok(Instruction instruction)
		{
			return this.Resolver.ResolveToken(this.Reader.ReadInt32());
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

		public IList<Parameter> Parameters = new List<Parameter>();

		/// <summary>
		/// Translate the specified SerializedParameter to a dnlib Parameter and return it.
		/// </summary>
		/// <param name="index">Index</param>
		/// <returns>Parameter</returns>
		public Parameter GetArgument(UInt16 index)
		{
			if (index < this.Parameters.Count)
				return this.Parameters[index];
			else return null;
		}

		public IList<Local> Locals = new List<Local>();
		//public LocalList Locals = new LocalList();

		/// <summary>
		/// Translate the specified SerializedLocal to a dnlib Local and return it.
		/// </summary>
		/// <param name="index">Index</param>
		/// <returns>Local</returns>
		public Local GetLocal(UInt16 index)
		{
			if (index < this.Locals.Count)
				return this.Locals[index];
			else return null;
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
		public class SerializedExceptionHandler
		{
			public Int32 VirtualHandlerType { get; set; }
			public Int32 VirtualCatchType { get; set; }
			public UInt32 VirtualTryStart { get; set; }
			public UInt32 VirtualTryLength { get; set; }
			public UInt32 VirtualHandlerStart { get; set; }
			public UInt32 VirtualHandlerLength { get; set; }
			public UInt32 VirtualFilterStart { get; set; }

			/// <summary>
			/// The calculated TryEnd virtual offset.
			/// </summary>
			public UInt32 VirtualTryEnd
			{
				get { return this.VirtualTryStart + this.VirtualTryLength; }
			}

			/// <summary>
			/// The calculated HandlerEnd virtual offset.
			/// </summary>
			public UInt32 VirtualHandlerEnd
			{
				get { return this.VirtualHandlerStart + this.VirtualHandlerLength; }
			}

			/// <summary>
			/// The dnlib HandlerType of this SerializedExceptionHandler.
			/// </summary>
			public ExceptionHandlerType HandlerType
			{
				get
				{
					switch(VirtualHandlerType)
					{
						case 0: return ExceptionHandlerType.Catch;
						case 2: return ExceptionHandlerType.Finally;
						default: throw new NotSupportedException();
					}
				}
			}

			/// <summary>
			/// Whether or not the virtual catch type (position) is non-negative, should
			/// only be true if the HandlerType is Catch.
			/// </summary>
			public Boolean HasCatchType
			{
				get { return this.VirtualCatchType >= 0; }
			}

			public SerializedExceptionHandler(BinaryReader reader)
			{
				this.Deserialize(reader);
			}

			private void Deserialize(BinaryReader reader)
			{
				this.VirtualHandlerType = reader.ReadInt32();
				this.VirtualCatchType = reader.ReadInt32();
				this.VirtualTryStart = reader.ReadUInt32();
				this.VirtualTryLength = reader.ReadUInt32();
				this.VirtualHandlerStart = reader.ReadUInt32();
				this.VirtualHandlerLength = reader.ReadUInt32();
				this.VirtualFilterStart = reader.ReadUInt32();
			}
		}
	}
}
