using System;
using System.IO;

namespace eazdevirt
{
	/// <summary>
	/// Crypto stream used to read resources containing information about
	/// virtualized methods. Requires an integer key.
	/// </summary>
	public class CryptoStream : Stream
	{
		/// <summary>
		/// Key used for crypto.
		/// </summary>
		public Int32 Key { get; private set; }

		/// <summary>
		/// Underlying stream.
		/// </summary>
		private Stream _stream;

		public override Boolean CanRead
		{
			get { return _stream.CanRead; }
		}

		public override Boolean CanSeek
		{
			get { return _stream.CanSeek; }
		}

		public override Boolean CanWrite
		{
			get { return _stream.CanWrite; }
		}

		public override Int64 Length
		{
			get { return _stream.Length; }
		}

		public override Int64 Position
		{
			get
			{
				return _stream.Position;
			}
			set
			{
				_stream.Position = value;
			}
		}

		public CryptoStream(Stream baseStream, Int32 key)
		{
			_stream = baseStream;
			this.Key = key;
		}

		public override void Flush()
		{
			_stream.Flush();
		}

		private Byte Crypt(Byte b, Int64 position)
		{
			Byte x = (Byte)((UInt64)this.Key | (UInt64)position);
			return (Byte)(b ^ x);
		}

		public override void Write(Byte[] buffer, Int32 offset, Int32 count)
		{
			Byte[] array = new Byte[count];
			Buffer.BlockCopy(buffer, offset, array, 0, count);
			Int64 position = this.Position;
			for (Int32 i = 0; i < count; i++)
			{
				array[i] = this.Crypt(array[i], position + (Int64)i);
			}
			_stream.Write(array, offset, count);
		}

		public override int Read(byte[] buffer, int offset, int count)
		{
			Int64 position = this.Position;
			Byte[] array = new Byte[count];
			Int32 num = _stream.Read(array, 0, count);
			for (Int32 i = 0; i < num; i++)
			{
				buffer[i + offset] = this.Crypt(array[i], position + (Int64)i);
			}
			return num;
		}

		public override Int64 Seek(Int64 offset, SeekOrigin origin)
		{
			return _stream.Seek(offset, origin);
		}

		public override void SetLength(Int64 value)
		{
			_stream.SetLength(value);
		}
	}
}
