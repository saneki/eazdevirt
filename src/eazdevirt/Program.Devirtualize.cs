using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace eazdevirt
{
	public partial class Program
	{
		/// <summary>
		/// Print out information about whether or not a devirtualization attempt was
		/// successful, and if not why not.
		/// </summary>
		/// <param name="options">MonoOptions set from passed command-line arguments</param>
		/// <param name="module">EazModule</param>
		/// <param name="attempt">Data about the devirtualization attempt</param>
		static void PrintAttemptSuccess(MonoOptions options, EazModule module,
			DevirtualizeAttempt attempt)
		{
			var reader = attempt.Reader;

			if (attempt.Successful)
				Console.WriteLine("--> Devirtualizable");
			else if (attempt.WasInstructionUnknown)
			{
				var matches = module.VirtualInstructions
							.Where((instr) => instr.VirtualCode == reader.LastVirtualOpCode)
							.ToArray();

				if (matches.Length > 0)
				{
					VirtualOpCode v = matches[0];
					Console.WriteLine("--> Not yet devirtualizable (contains unknown virtual instruction)");
					Console.WriteLine("-----> Virtual OpCode = {0} @ [{1}] (0x{2:X8})",
						reader.LastVirtualOpCode, reader.CurrentInstructionOffset, reader.CurrentVirtualOffset);
					Console.WriteLine("-----> Delegate method: {0} (MDToken = 0x{1:X8})",
						v.DelegateMethod.FullName, v.DelegateMethod.MDToken.Raw);
				}
				else
				{
					Console.WriteLine("--> Not yet devirtualizable (contains unexpected virtual instruction @ [{0}] (0x{1:X8}))",
						reader.CurrentInstructionOffset, reader.CurrentVirtualOffset);
				}
			}
			else
				Console.WriteLine("--> Not yet devirtualizable (threw exception)");
		}

		/// <summary>
		/// Print out information about a devirtualization attempt.
		/// </summary>
		/// <param name="options">MonoOptions set from passed command-line arguments</param>
		/// <param name="module">EazModule</param>
		/// <param name="attempt">Data about the devirtualization attempt</param>
		static void PrintAttempt(MonoOptions options, EazModule module,
			DevirtualizeAttempt attempt)
		{
			var reader = attempt.Reader;
			var method = attempt.Method;
			var stub = attempt.VirtualizedMethod;
			var body = attempt.MethodBody;

			IList<Local> locals = attempt.Successful ?
				body.Variables : reader.Locals;
			IList<ExceptionHandler> handlers = attempt.Successful ?
				body.ExceptionHandlers : reader.ExceptionHandlers;
			IList<Instruction> instructions = attempt.Successful ?
				body.Instructions : reader.Instructions;

			// Message prefix
			String prefix;
			switch(options.Action)
			{
				case ProgramAction.Devirtualize:
					prefix = "Devirtualized";
					break;
				case ProgramAction.Methods:
				default:
					prefix = "Found";
					break;
			}

			Console.WriteLine("{0} {1} (MDToken = 0x{2:X8})", prefix, method.FullName, method.MDToken.Raw);

			if (options.Action == ProgramAction.Methods || options.Verbose)
			{
				Console.WriteLine("--> Position string: {0}", stub.PositionString);
				Console.WriteLine("--> Position: {0} (0x{0:X8})", stub.Position);
				Console.WriteLine("--> Resource: {0}", stub.ResourceStringId);
				Console.WriteLine("--> Crypto key: {0}", stub.ResourceCryptoKey);
				Console.WriteLine("--> Actual method size: {0} (0x{0:X8})", reader.CodeSize);

				if (options.Action == ProgramAction.Methods)
					PrintAttemptSuccess(options, module, attempt);
			}

			if (options.Action == ProgramAction.Methods || options.Verbose)
			{
				Console.WriteLine();

				// Print locals
				if (locals.Count > 0)
				{
					Int32 index = 0;
					Console.WriteLine("Locals:");
					Console.WriteLine("-------");
					foreach (var local in locals)
						Console.WriteLine("local[{0}]: {1}", index++, local.Type.FullName);
					Console.WriteLine();
				}

				// Print exception handlers
				if (handlers.Count > 0)
				{
					Int32 index = 0;
					Console.WriteLine("Exception Handlers:");
					Console.WriteLine("-------------------");
					foreach (var handler in handlers)
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
				if (instructions != null && instructions.Count > 0)
				{
					Console.WriteLine("Instructions:");
					Console.WriteLine("-------------");
					foreach (var instr in instructions)
						Console.WriteLine(instr);
					Console.WriteLine();
				}

				// Print out exception, if any
				if (!attempt.Successful && !attempt.WasInstructionUnknown)
				{
					Console.Write(attempt.Exception);
					Console.WriteLine();
					Console.WriteLine();
				}
			}

			if (!(options.Action == ProgramAction.Devirtualize && !options.Verbose))
				Console.WriteLine();
		}

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

			// Setup devirtualize options
			var opts = DevirtualizeOptions.Nothing;
			if (options.InjectAttributes)
				opts |= DevirtualizeOptions.InjectAttributes;

			Devirtualizer devirtualizer = new Devirtualizer(module, opts, options.MethodFixers, logger);

			var results = devirtualizer.Devirtualize((attempt) => {
				if (attempt.Successful)
					PrintAttempt(options, module, attempt);
			});

			if (results.Empty)
			{
				Console.WriteLine("No virtualized methods found");
				return;
			}
			else if (!options.Verbose)
				Console.WriteLine();

			Console.WriteLine("Devirtualized {0}/{1} methods",
				results.DevirtualizedCount, results.MethodCount);

			// Only save if at least one method devirtualized
			if (results.DevirtualizedCount > 0)
			{
				String outputPath = options.OutputPath ?? GetDevirtualizedModulePath(options.AssemblyPath);
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
