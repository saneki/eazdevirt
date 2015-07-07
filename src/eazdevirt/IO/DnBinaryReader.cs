using System.Collections.Generic;
using System.IO;
using dnlib.IO;
using System.Text;

namespace eazdevirt.IO
{
	/// <summary>
	/// BinaryReader that satisfies IBinaryReader, to make dnlib happy.
	/// </summary>
	public class DnBinaryReader : BinaryReader, IBinaryReader
	{

		public DnBinaryReader(Stream input)
			: base(input)
		{
		}

		public DnBinaryReader(Stream input, Encoding encoding)
			: base(input, encoding)
		{
		}

		public DnBinaryReader(Stream input, Encoding encoding, bool leaveOpen)
			: base(input, encoding, leaveOpen)
		{
		}

		/// <summary>
		/// Returns the length of the stream
		/// </summary>
		public long Length
		{
			get { return this.BaseStream.Length; }
		}

		/// <summary>
		/// Gets/sets the position
		/// </summary>
		public long Position
		{
			get
			{
				return this.BaseStream.Position;
			}
			set
			{
				this.BaseStream.Position = value;
			}
		}

		/// <summary>
		/// Reads bytes until byte <paramref name="b"/> is found. <see cref="Position"/> is
		/// incremented by the number of bytes read (size of return value).
		/// </summary>
		/// <param name="b">The terminating byte</param>
		/// <returns>All the bytes (not including <paramref name="b"/>) or <c>null</c> if
		/// <paramref name="b"/> wasn't found.</returns>
		/// <remarks>Not the most efficient implementation ever, but it should do</remarks>
		public byte[] ReadBytesUntilByte(byte b)
		{
			List<byte> bytes = new List<byte>(1024);

			byte current;
			while (this.CanRead(1) && (current = this.ReadByte()) != b)
				bytes.Add(b);

			return bytes.ToArray();
		}

		/// <summary>
		/// Reads a <see cref="String"/> from the current position and increments <see cref="Position"/>
		/// by the number of bytes read.
		/// </summary>
		/// <param name="chars">Number of characters to read</param>
		/// <returns>The string</returns>
		/// <exception cref="IOException">An I/O error occurs</exception>
		/// <remarks>Assuming UTF8 (does not factor in encoding passed to constructor)</remarks>
		public string ReadString(int chars)
		{
			return Encoding.UTF8.GetString(this.ReadBytes(chars));
		}
	}
}
