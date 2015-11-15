using System;
using System.IO;

namespace eazdevirt.V1
{
	/// <summary>
	/// Crypto stream used to read resources containing information about
	/// virtualized methods. Requires an integer key.
	/// </summary>
	public class CryptoStreamV1 : CryptoStreamBase
	{
		/// <summary>
		/// </summary>
		/// <param name="baseStream">Base stream</param>
		/// <param name="key">Key</param>
		public CryptoStreamV1(Stream baseStream, Int32 key)
			: base(baseStream, key)
		{
		}

		protected override Byte Crypt(Byte b, Int64 position)
		{
			return (Byte)((Byte)((UInt64)this.Key | (UInt64)position) ^ b);
		}
	}
}
