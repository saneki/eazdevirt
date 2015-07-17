using System;

namespace eazdevirt
{
	public interface IPositionTranslator
	{
		/// <summary>
		/// Given the crypto key, convert a position string into a position.
		/// </summary>
		/// <param name="s">Position string</param>
		/// <param name="cryptoKey">Crypto key</param>
		/// <returns>Position</returns>
		Int64 ToPosition(String s, Int32 cryptoKey);
	}
}
