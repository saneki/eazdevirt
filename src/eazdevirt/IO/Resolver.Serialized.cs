using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace eazdevirt.IO
{
	public partial class Resolver : ResourceReader
	{
		/// <summary>
		/// A guess as to the first Byte (enum) of InlineOperand.
		/// </summary>
		public enum ValueType
		{
			/// <summary>
			/// The Value field holds a raw MDToken value.
			/// </summary>
			Token = 0,

			/// <summary>
			/// The Value field holds a position.
			/// </summary>
			Position = 1
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
			UnknownType7 = 4
		}

		/// <summary>
		/// Deserialized inline operand.
		/// </summary>
		/// <remarks>Class54</remarks>
		public class InlineOperand
		{
			/// <summary>
			/// Determines how the Value field is interpreted.
			/// </summary>
			public ValueType ValueType { get; private set; }

			/// <summary>
			/// Either a raw metadata token from the parent module, or a position in
			/// the embedded resource file.
			/// </summary>
			public Int32 Value { get; private set; }

			/// <summary>
			/// Deserialized data associated with this operand.
			/// </summary>
			public InlineOperandData Data { get; private set; }

			/// <summary>
			/// Whether or not this operand contains a token.
			/// </summary>
			public Boolean IsToken { get { return this.ValueType == ValueType.Token; } }

			/// <summary>
			/// Whether or not this operand contains a position.
			/// </summary>
			public Boolean IsPosition { get { return !this.IsToken; } }

			/// <summary>
			/// Get the operand's token, throwing an exception if none.
			/// </summary>
			public Int32 Token
			{
				get
				{
					if (this.IsToken)
						return this.Value;
					else
						throw new Exception("InlineOperand has no token (only position)");
				}
			}

			/// <summary>
			/// Get the operand's position, throwing an exception if none.
			/// </summary>
			public Int32 Position
			{
				get
				{
					if (this.IsPosition)
						return this.Value;
					else
						throw new Exception("InlineOperand has no position (only token)");
				}
			}

			/// <summary>
			/// Whether or not this operand has deserialized data associated with it.
			/// </summary>
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
				this.ValueType = (ValueType)reader.ReadByte();

				if (this.ValueType == 0)
					this.Value = reader.ReadInt32();
				else
					this.Data = InlineOperandData.Read(reader);
			}

			public static InlineOperand ReadInternal(BinaryReader reader)
			{
				InlineOperand u = new InlineOperand();
				u.ValueType = ValueType.Position;
				u.Value = reader.ReadInt32();
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

		/// <summary>
		/// Operand data.
		/// </summary>
		/// <remarks>Class41</remarks>
		public abstract class InlineOperandData
		{
			/// <summary>
			/// Describes the type of operand data.
			/// </summary>
			public abstract InlineOperandType Type { get; }

			protected InlineOperandData() { }

			public InlineOperandData(BinaryReader reader)
			{
				this.Deserialize(reader);
			}

			protected abstract void Deserialize(BinaryReader reader);

			/// <summary>
			/// Read some inline operand data from a BinaryReader.
			/// </summary>
			/// <param name="reader">BinaryReader</param>
			/// <returns>InlineOperandData</returns>
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
					case InlineOperandType.UnknownType7:
						return new UnknownType7(reader);
					default:
						throw new ArgumentOutOfRangeException();
				}
			}
		}

		/// <summary>
		/// Type-related operand data.
		/// </summary>
		/// <remarks>Class46</remarks>
		public class TypeData : InlineOperandData
		{
			public Int32 SomeIndex { get; private set; } // int_1, index into type_2? (GetGenericArguments())
			public Int32 SomeIndex2 { get; private set; } // int_0, index into type_5? (DeclaringType.GetGenericArguments())
			public Boolean Unknown3 { get; private set; } // bool_0
			public String Name { get; private set; } // string_0
			public Boolean HasGenericTypes { get; private set; } // bool_1
			public InlineOperand[] GenericTypes { get; private set; } // class54_0

			public override InlineOperandType Type
			{
				get { return InlineOperandType.Type; }
			}

			public String TypeNameWithoutNamespace
			{
				get
				{
					if (this.TypeName.Contains("."))
						return this.TypeName.Split('.').Last();
					else
						return String.Empty;
				}
			}

			public String Namespace
			{
				get
				{
					if (this.TypeName.Contains("."))
					{
						return String.Join(".",
							this.TypeName.Split('.').Reverse().Skip(1).Reverse().ToArray());
					}
					else
						return this.TypeName;
				}
			}

			public String TypeName
			{
				get
				{
					if (this.Name.Contains(", "))
						return this.Name.Split(',')[0];
					else return this.Name;
				}
			}

			public String AssemblyFullName
			{
				get
				{
					return this.Name.Substring(
						this.TypeName.Length + 2,
						this.Name.Length - (this.TypeName.Length + 2)
					);
				}
			}

			public String AssemblyName
			{
				get { return AssemblyFullName.Split(',')[0]; }
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
				this.HasGenericTypes = reader.ReadBoolean();
				this.GenericTypes = InlineOperand.ReadArrayInternal(reader);
			}
		}

		/// <summary>
		/// Field-related operand data.
		/// </summary>
		/// <remarks>Class42</remarks>
		public class FieldData : InlineOperandData
		{
			public InlineOperand FieldType { get; private set; }
			public String Name { get; private set; }
			public Boolean Flags { get; private set; }

			public override InlineOperandType Type
			{
				get { return InlineOperandType.Field; }
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

			public FieldData(BinaryReader reader)
				: base(reader)
			{
			}

			protected override void Deserialize(BinaryReader reader)
			{
				this.FieldType = InlineOperand.ReadInternal(reader);
				this.Name = reader.ReadString();
				this.Flags = reader.ReadBoolean();
			}
		}

		/// <summary>
		/// Method-related operand data.
		/// </summary>
		/// <remarks>Class44</remarks>
		public class MethodData : InlineOperandData
		{
			public InlineOperand DeclaringType { get; private set; } // class54_0
			public Boolean Unknown2 { get; private set; } // bool_0
			public Boolean Flags { get; private set; } // bool_1
			public String Name { get; private set; } // string_0
			public InlineOperand ReturnType { get; private set; } // class54_3
			public InlineOperand[] Parameters { get; private set; } // class54_1
			public InlineOperand[] GenericArguments { get; private set; } // class54_2

			public Boolean HasGenericArguments
			{
				get { return this.GenericArguments.Length > 0; }
			}

			public override InlineOperandType Type
			{
				get { return InlineOperandType.Method; }
			}

			public Boolean IsStatic
			{
				get { return this.Flags; }
			}

			public Boolean IsInstance
			{
				get { return !this.Flags; }
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
				this.DeclaringType = InlineOperand.ReadInternal(reader);
				this.Unknown2 = reader.ReadBoolean();
				this.Flags = reader.ReadBoolean();
				this.Name = reader.ReadString();
				this.ReturnType = InlineOperand.ReadInternal(reader);
				this.Parameters = InlineOperand.ReadArrayInternal(reader);
				this.GenericArguments = InlineOperand.ReadArrayInternal(reader);
			}
		}

		/// <summary>
		/// String-related operand data.
		/// </summary>
		/// <remarks>Class45</remarks>
		public class StringData : InlineOperandData
		{
			/// <summary>
			/// String value.
			/// </summary>
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
				get { return InlineOperandType.UnknownType7; }
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
