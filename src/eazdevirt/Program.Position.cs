using System;

namespace eazdevirt
{
	public partial class Program
	{
		/// <summary>
		/// Perform "position" verb.
		/// </summary>
		/// <param name="options">Options</param>
		static void DoPosition(PositionSubOptions options)
		{
			Int64 position = 0;

			if (options.Key.HasValue)
			{
				// This doesn't work yet: Command line parser can't parse Nullable?
				position = EazPosition.FromString(options.PositionString, options.Key.Value);
			}
			else if (options.AssemblyPath != null)
			{
				EazModule module;
				if (!TryLoadModule(options.AssemblyPath, out module))
					return;

				EazVirtualizedMethod method = module.FindFirstVirtualizedMethod();
				if (method != null)
				{
					try
					{
						position = EazPosition.FromString(options.PositionString, method.ResourceCryptoKey);
					}
					catch (FormatException e)
					{
						Console.WriteLine(e.Message);
						return;
					}
				}
				else
				{
					Console.WriteLine("No virtualized methods found in specified assembly");
					return;
				}
			}
			else
			{
				Console.WriteLine("Provide either the crypto key or assembly from which to extract the crypto key");
				return;
			}

			Console.WriteLine("{0} => {1:X8}", options.PositionString, position);
		}
	}
}
