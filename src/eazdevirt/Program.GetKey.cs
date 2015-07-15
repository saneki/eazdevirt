using System;

namespace eazdevirt
{
	public partial class Program
	{
		/// <summary>
		/// Perform "get-key" verb.
		/// </summary>
		/// <param name="options">Options</param>
		static void DoGetKey(GetKeySubOptions options)
		{
			EazModule module;
			if (!TryLoadModule(options.AssemblyPath, out module))
				return;

			EazVirtualizedMethod method = module.FindFirstVirtualizedMethod();
			Console.WriteLine("Key: {0}", method.ResourceCryptoKey);
		}
	}
}
