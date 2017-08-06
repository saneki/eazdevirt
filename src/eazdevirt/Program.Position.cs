using System;

namespace eazdevirt
{
	public partial class Program
	{
		/// <summary>
		/// Perform "position" verb.
		/// </summary>
		/// <param name="options">Options</param>
		static void DoPosition(MonoOptions options)
		{
			Int64 position = 0;

			if (options.AssemblyPath != null)
			{
				EazModule module;
				if (!TryLoadModule(options.AssemblyPath, out module))
					return;

				IPositionTranslator translator = module.PositionTranslator;

				MethodStub method = module.FindFirstVirtualizedMethod();
				if (method != null)
				{
					try
					{
						position = translator.ToPosition(options.PositionString, method.ResourceCryptoKey2);
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
