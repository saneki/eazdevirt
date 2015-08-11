namespace eazdevirt
{
	/// <summary>
	/// All special codes are assigned pretend values.
	/// </summary>
	public enum SpecialCode : uint
	{
		/// <summary>
		/// Special opcode, used when calling a virtualized method from within
		/// another virtualized method.
		/// </summary>
		Eaz_Call = 0x80000000
	}
}
