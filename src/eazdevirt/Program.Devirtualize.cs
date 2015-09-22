using System;
using System.IO;
using dnlib.DotNet;

namespace eazdevirt
{
	public partial class Program
	{
		/// <summary>
		/// Perform "devirtualize" verb.
		/// </summary>
		/// <param name="options">Options</param>
		static void DoDevirtualize(MonoOptions options)
		{
			ILogger logger = GetLogger(options);

			EazModule module;
			if (!TryLoadModule(options.AssemblyPath, logger, out module))
				return;

			Devirtualizer devirtualizer = new Devirtualizer(module, logger);

			var results = devirtualizer.Devirtualize((attempt) =>
			{
				if (attempt.Successful)
				{
					var method = attempt.Method;
					var body = attempt.MethodBody;

					Console.WriteLine("Devirtualized {0} (MDToken = 0x{1:X8})",
						method.FullName, method.MDToken.Raw);

					if (options.Verbose)
					{
						Console.WriteLine();

						// Print locals
						if (body.HasVariables)
						{
							Console.WriteLine("Locals:");
							Console.WriteLine("-------");
							foreach (var local in body.Variables)
								Console.WriteLine("local[{0}]: {1}", local.Index, local.Type.FullName);
							Console.WriteLine();
						}

						if (body.HasExceptionHandlers)
						{
							Int32 index = 0;
							Console.WriteLine("Exception Handlers:");
							Console.WriteLine("-------------------");
							foreach (var handler in body.ExceptionHandlers)
							{
								if (handler.CatchType != null)
									Console.WriteLine("handler[{0}]: HandlerType = {1}, CatchType = {2}",
										index++, handler.HandlerType, handler.CatchType);
								else
									Console.WriteLine("handler[{0}]: HandlerType = {1}",
										index++, handler.HandlerType);
								Console.WriteLine("--> Try:     [{0}, {1}]", handler.TryStart, handler.TryEnd);
								Console.WriteLine("--> Handler: [{0}, {1}]", handler.HandlerStart, handler.HandlerEnd);
								Console.WriteLine("--> Filter:  {0}", handler.FilterStart);
							}
							Console.WriteLine();
						}

						// Print instructions
						Console.WriteLine("Instructions:");
						Console.WriteLine("-------------");
						foreach (var instr in body.Instructions)
							Console.WriteLine(instr);
						Console.WriteLine();
					}
				}
			});

			if (results.Empty)
			{
				Console.WriteLine("No virtualized methods found");
				return;
			}

			if (results.DevirtualizedCount > 0)
				Console.WriteLine();

			Console.WriteLine("Devirtualized {0}/{1} methods",
				results.DevirtualizedCount, results.MethodCount);

			// Only save if at least one method devirtualized
			if (results.DevirtualizedCount > 0)
			{
				String outputPath = GetDevirtualizedModulePath(options.AssemblyPath);
				Console.WriteLine("Saving {0}", outputPath);
				module.Write(outputPath, options.NoThrow);
			}
		}

		static String GetDevirtualizedModulePath(String origPath)
		{
			String ext = Path.GetExtension(origPath);
			String noExt = Path.GetFileNameWithoutExtension(origPath);
			return String.Format("{0}-devirtualized{1}",
				Path.Combine(Path.GetDirectoryName(origPath), noExt), ext);
		}
	}
}
