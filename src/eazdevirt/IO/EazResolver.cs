using System;
using System.Collections.Generic;
using System.IO;
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
		/// Resolve a method.
		/// </summary>
		/// <param name="position">Position</param>
		/// <returns>Method</returns>
		public IMethod ResolveMethod(Int32 position)
		{
			lock(_lock)
			{
				return this.ResolveMethod_NoLock(position);
			}
		}

		IMethod ResolveMethod_NoLock(Int32 position)
		{
			this.Stream.Position = position;

			InlineOperand operand = new InlineOperand(this.Reader);
			if(operand.IsDirect)
			{
				MDToken token = new MDToken(operand.Token);
				return this.Module.ResolveMethod(token.Rid);
			}
			else
			{
				// For now just throw
				throw new Exception(String.Format(
					"Cannot yet resolve a Method without a direct token, resolved @ {0} (0x{0:X8})",
					position
				));

				// Still haven't figured out how the MethodData is used
				//MethodData data = operand.Data as MethodData;
				//
				//if(data.Unknown2)
				//{
				//	// ???
				//	this.ResolveType_NoLock(data.Unknown1.Token);
				//}
				//else
				//{
				//	// ...
				//}
			}
		}

		/// <summary>
		/// Resolve a user string.
		/// </summary>
		/// <param name="position">Position</param>
		/// <returns>String</returns>
		public String ResolveString(Int32 position)
		{
			lock(_lock)
			{
				return this.ResolveString_NoLock(position);
			}
		}

		String ResolveString_NoLock(Int32 position)
		{
			this.Stream.Position = position;

			InlineOperand operand = new InlineOperand(this.Reader);
			if(operand.IsDirect)
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
		/// Resolve a type.
		/// </summary>
		/// <param name="position">Position</param>
		/// <returns>Type</returns>
		public ITypeDefOrRef ResolveType(Int32 position)
		{
			lock(_lock)
			{
				return this.ResolveType_NoLock(position);
			}
		}

		ITypeDefOrRef ResolveType_NoLock(Int32 position)
		{
			this.Stream.Position = position;

			InlineOperand operand = new InlineOperand(this.Reader);
			if(operand.IsDirect)
			{
				MDToken token = new MDToken(operand.Token);

				if(token.Table == Table.TypeDef)
					return this.Module.ResolveTypeDef(token.Rid);
				else if (token.Table == Table.TypeRef)
					return this.Module.ResolveTypeRef(token.Rid);

				throw new Exception("Bad MDToken table");
			}
			else
			{
				// For now just throw
				throw new Exception(String.Format(
					"Cannot yet resolve a Type without a direct token, resolved @ {0} (0x{0:X8})",
					position
				));

				// Still haven't figured out how the TypeData is used
				//TypeData data = operand.Data as TypeData;
				//
				//if(data.Unknown3)
				//{
				//	Type type = null;
				//
				//	if(data.SomeIndex == -1 && data.SomeIndex2 == -1)
				//		throw new Exception();
				//
				//	if(data.SomeIndex != -1)
				//	{
				//		// ...
				//	}
				//	else if (data.SomeIndex2 != -1)
				//	{
				//		// ...
				//	}
				//
				//	Stack<TypeModifierStruct> stack = GetTypeModifiers(data.Name);
				//	type = ApplyTypeModifiers(type, stack);
				//}
				//else
				//{
				//	Type type = Type.GetType(data.Name);
				//	if(type == null)
				//	{
				//		// ...
				//	}
				//
				//	// ...
				//}
			}
		}

		/// <remarks>Mostly copied from decompiler, unsure how relevant</remarks>
		private enum TypeModifier
		{
			Ref = 0,
			Array = 1,
			Pointer = 2
		}

		/// <remarks>Mostly copied from decompiler, unsure how relevant</remarks>
		private struct TypeModifierStruct
		{
			public TypeModifier Type; // enum0_0
			public Int32 Rank; // int_0
		}

		/// <remarks>Mostly copied from decompiler, unsure how relevant</remarks>
		private static Stack<TypeModifierStruct> GetTypeModifiers(String text)
		{
			Stack<TypeModifierStruct> stack = new Stack<TypeModifierStruct>();

			while (true)
			{
				if (text.EndsWith("&", StringComparison.Ordinal))
				{
					stack.Push(new TypeModifierStruct { Type = TypeModifier.Ref });
					text = text.Substring(0, text.Length - 1);
				}
				else if (text.EndsWith("*", StringComparison.Ordinal))
				{
					stack.Push(new TypeModifierStruct { Type = TypeModifier.Pointer });
					text = text.Substring(0, text.Length - 1);
				}
				else break;
			}

			return stack;
		}

		/// <remarks>Mostly copied from decompiler, unsure how relevant</remarks>
		private static Type ApplyTypeModifiers(Type defaultType, Stack<TypeModifierStruct> modifiers)
		{
			while (modifiers.Count > 0)
			{
				TypeModifierStruct t = modifiers.Pop();
				switch (t.Type)
				{
					case TypeModifier.Ref:
						defaultType = defaultType.MakeByRefType();
						break;
					case TypeModifier.Array:
						if (t.Rank == 1)
							defaultType = defaultType.MakeArrayType();
						else
							defaultType = defaultType.MakeArrayType(t.Rank);
						break;
					case TypeModifier.Pointer:
						defaultType = defaultType.MakePointerType();
						break;
				}
			}
			return defaultType;
		}

		/// <summary>
		/// A guess as to the first Byte (enum) of InlineOperand.
		/// </summary>
		public enum Directness
		{
			/// <summary>
			/// If direct, can just use the provided MDToken.
			/// </summary>
			Direct = 0,

			/// <summary>
			/// If indirect, requires using the data somehow.
			/// </summary>
			Indirect = 1
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
			Unknown4 = 4
		}

		// Class54
		public class InlineOperand
		{
			public Directness ResolveType { get; private set; }
			public Int32 Token { get; private set; }
			public InlineOperandData Data { get; private set; }

			public Boolean IsDirect { get { return this.ResolveType == Directness.Direct; } }
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
				this.ResolveType = (Directness)reader.ReadByte();

				if (this.ResolveType == 0)
					this.Token = reader.ReadInt32();
				else
					this.Data = InlineOperandData.Read(reader);
			}

			public static InlineOperand ReadInternal(BinaryReader reader)
			{
				InlineOperand u = new InlineOperand();
				u.ResolveType = Directness.Indirect;
				u.Token = reader.ReadInt32();
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

		// Class41
		public abstract class InlineOperandData
		{
			public abstract InlineOperandType Type { get; }
			protected InlineOperandData() { }

			public InlineOperandData(BinaryReader reader)
			{
				this.Deserialize(reader);
			}

			protected abstract void Deserialize(BinaryReader reader);

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
					case InlineOperandType.Unknown4:
						return new UnknownType7(reader);
					default:
						throw new ArgumentOutOfRangeException();
				}
			}
		}

		// Class46
		public class TypeData : InlineOperandData
		{
			public Int32 SomeIndex { get; private set; } // int_1, index into type_2? (GetGenericArguments())
			public Int32 SomeIndex2 { get; private set; } // int_0, index into type_5? (DeclaringType.GetGenericArguments())
			public Boolean Unknown3 { get; private set; } // bool_0
			public String Name { get; private set; } // string_0
			public Boolean Unknown5 { get; private set; } // bool_1
			public InlineOperand[] Unknown6 { get; private set; } // class54_0

			public override InlineOperandType Type
			{
				get { return InlineOperandType.Type; }
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
				this.Unknown5 = reader.ReadBoolean();
				this.Unknown6 = InlineOperand.ReadArrayInternal(reader);
			}
		}

		// Class42
		public class FieldData : InlineOperandData
		{
			public InlineOperand Unknown1 { get; private set; }
			public String Unknown2 { get; private set; }
			public Boolean Unknown3 { get; private set; }

			public override InlineOperandType Type
			{
				get { return InlineOperandType.Field; }
			}

			public FieldData(BinaryReader reader)
				: base(reader)
			{
			}

			protected override void Deserialize(BinaryReader reader)
			{
				this.Unknown1 = InlineOperand.ReadInternal(reader);
				this.Unknown2 = reader.ReadString();
				this.Unknown3 = reader.ReadBoolean();
			}
		}

		// Class44
		public class MethodData : InlineOperandData
		{
			public InlineOperand Unknown1 { get; private set; } // class54_0
			public Boolean Unknown2 { get; private set; } // bool_0
			public Boolean Flags { get; private set; } // bool_1
			public String Name { get; private set; } // string_0
			public InlineOperand Unknown5 { get; private set; } // class54_3
			public InlineOperand[] Parameters { get; private set; } // class54_1
			public InlineOperand[] GenericArguments { get; private set; } // class54_2

			public override InlineOperandType Type
			{
				get { return InlineOperandType.Method; }
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
				this.Unknown1 = InlineOperand.ReadInternal(reader);
				this.Unknown2 = reader.ReadBoolean();
				this.Flags = reader.ReadBoolean();
				this.Name = reader.ReadString();
				this.Unknown5 = InlineOperand.ReadInternal(reader);
				this.Parameters = InlineOperand.ReadArrayInternal(reader);
				this.GenericArguments = InlineOperand.ReadArrayInternal(reader);
			}
		}

		// Class45
		public class StringData : InlineOperandData
		{
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
				get { return InlineOperandType.Unknown4; }
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
