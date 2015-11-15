namespace eazdevirt
{
	public enum SerializationVersion
	{
		/// <summary>
		/// The "original" serialization version from when I started
		/// researching Eazfuscator's VM.
		/// </summary>
		V1,

		/// <summary>
		/// Almost the exact same as V1, except removes two calls to
		/// ReadByte, one prior to reading EazCall data and the other
		/// prior to reading virtual method data.
		/// </summary>
		/// <remarks>
		/// Detection of this version is done by checking the CryptoStreamDef
		/// version. If it is V2 (has a special hardcoded integer used
		/// in decryption) then the serialization version should be V2.
		/// </remarks>
		V2
	}
}
