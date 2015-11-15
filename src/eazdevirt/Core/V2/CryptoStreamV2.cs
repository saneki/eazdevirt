using System;
using System.IO;

namespace eazdevirt.V2
{
	/// <summary>
	/// Updated crypto stream.
	/// </summary>
	/// <remarks>Eazfuscator started using this sometime between versions 5.0.102.18536 and 5.0.106.3450.</remarks>
	public class CryptoStreamV2 : CryptoStreamBase
	{
		/// <summary>
		/// Special integer. Similar to the key integer, except it is "hardcoded"
		/// into the crypt method instead of belonging to the stream instance.
		/// </summary>
		/// <remarks>
		/// Seems to be a constant, but as I have only tested protecting programs on
		/// one machine and with one installation of Eazfuscator, the constant may be
		/// generated via machine info or be specific to this installation.
		/// </remarks>
		public Int32 Special { get; private set; }

		/// <summary>
		/// </summary>
		/// <param name="baseStream">Base stream</param>
		/// <param name="key">Key</param>
		/// <param name="special">Special int, hardcoded in the crypt method</param>
		public CryptoStreamV2(Stream baseStream, Int32 key, Int32 special)
			: base(baseStream, key)
		{
			this.Special = special;
		}

		protected override Byte Crypt(Byte b, Int64 position)
		{
			return (Byte)((Byte)(this.Key ^ this.Special ^ (Int32)((UInt32)position)) ^ b);
		}
	}
}
