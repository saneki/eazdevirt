using System;
using System.Linq;
using dnlib.DotNet;
using eazdevirt.IO;

namespace eazdevirt
{
	public partial class Program
	{
		static void PrintLocals(VirtualizedMethodBodyReader reader)
		{
			Console.Write("--> Locals count: {0} [", reader.Info.Locals.Length);
			for(Int32 i = 0; i < reader.Locals.Count; i++)
			{
				if (i != 0) Console.Write(", ");
				Console.Write(reader.Locals[i].Type);
			}
			Console.WriteLine("]");
		}

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

			var results = devirtualizer.Devirtualize((attempt) =>
			{
				var method = attempt.VirtualizedMethod;
				var reader = attempt.Reader;

				Console.WriteLine();
				Console.WriteLine("{0} (MDToken = 0x{1:X8})", method.Method.FullName, method.Method.MDToken.Raw);
				Console.WriteLine("--> Position string: {0}", method.PositionString);
				Console.WriteLine("--> Position: {0} (0x{0:X8})", method.Position);
				Console.WriteLine("--> Resource: {0}", method.ResourceStringId);
				Console.WriteLine("--> Crypto key: {0}", method.ResourceCryptoKey);
				PrintLocals(reader);
				Console.WriteLine("--> Actual method size: {0} (0x{0:X8})", reader.CodeSize);

				if (attempt.Successful)
				{
					Console.WriteLine("--> Devirtualizable");
					WritePartiallyDevirtualizedMethod(reader);
				}
				else if(attempt.WasInstructionUnknown)
				{
					var matches = module.VirtualInstructions
								.Where((instr) => { return instr.VirtualCode == reader.LastVirtualOpCode; })
								.ToArray();

					if (matches.Length > 0)
					{
						VirtualOpCode v = matches[0];
						Console.WriteLine("--> Not yet devirtualizable (contains unknown virtual instruction)");
						Console.WriteLine("-----> Virtual OpCode = {0} @ [{1}] (0x{2:X8})",
							reader.LastVirtualOpCode, reader.CurrentInstructionOffset, reader.CurrentVirtualOffset);
						Console.WriteLine("-----> Delegate method: {0} (MDToken = 0x{1:X8})",
							v.DelegateMethod.FullName, v.DelegateMethod.MDToken.Raw);

						if (reader.CurrentInstructionOffset > 0)
							WritePartiallyDevirtualizedMethod(reader);
					}
					else
					{
						Console.WriteLine("--> Not yet devirtualizable (contains unexpected virtual instruction @ [{0}] (0x{1:X8}))",
							reader.CurrentInstructionOffset, reader.CurrentVirtualOffset);

						if (reader.CurrentInstructionOffset > 0)
							WritePartiallyDevirtualizedMethod(reader);
					}
				}
				else
				{
					Console.WriteLine("--> Not yet devirtualizable (threw exception)");
					WritePartiallyDevirtualizedMethod(reader);
					Console.Write(attempt.Exception);
					Console.WriteLine();
					Console.WriteLine();
				}
			});
		}
	}
}
