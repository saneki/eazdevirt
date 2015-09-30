using System;
using dnlib.DotNet;

namespace eazdevirt
{
	public partial class Program
	{
		/// <summary>
		/// Perform "methods" verb.
		/// </summary>
		/// <param name="options">Options</param>
		static void DoMethods(MonoOptions options)
		{
			ILogger logger = GetLogger(options);

			EazModule module;
			if (!TryLoadModule(options.AssemblyPath, logger, out module))
				return;

			Devirtualizer devirtualizer = new Devirtualizer(module, logger);

			var results = devirtualizer.Devirtualize((attempt) => {
				PrintAttempt(options, module, attempt);
			});

			if (results.Empty)
			{
				Console.WriteLine("No virtualized methods found");
				return;
			}

			Console.WriteLine("{0}/{1} method stubs devirtualizable",
				results.DevirtualizedCount, results.MethodCount);
		}
	}
}
