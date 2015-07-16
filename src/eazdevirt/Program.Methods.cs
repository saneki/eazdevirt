using System;
using System.Linq;
using dnlib.DotNet;
using eazdevirt.IO;

namespace eazdevirt
{
	public partial class Program
	{
		/// <summary>
		/// Perform "find-methods" verb.
		/// </summary>
		/// <param name="options">Options</param>
		static void DoFindMethods(FindMethodsSubOptions options)
		{
			ILogger logger = GetLogger(options);

			EazModule module;
			if (!TryLoadModule(options.AssemblyPath, logger, out module))
				return;

			MethodStub[] methods = module.FindVirtualizedMethods();

			if (methods.Length > 0) Console.WriteLine("Virtualized methods found: {0}", methods.Length);
			else Console.WriteLine("No virtualized methods found");

			foreach (var method in methods)
			{
				Console.WriteLine();
				Console.WriteLine(method.Method.FullName);
				Console.WriteLine("--> Position string: {0}", method.PositionString);
				Console.WriteLine("--> Position: {0} (0x{0:X8})", method.Position);
				Console.WriteLine("--> Resource: {0}", method.ResourceStringId);
				Console.WriteLine("--> Crypto key: {0}", method.ResourceCryptoKey);

				if (options.ExtraOutput)
				{
					var reader = new EazVirtualizedMethodBodyReader(method);
					Boolean threwUnknownOpcodeException = false, threwException = false;
					Exception exception = null;

					try
					{
						reader.Read(); // Read method
					}
					catch (OriginalOpcodeUnknownException)
					{
						// This is almost guaranteed to happen at this point
						threwUnknownOpcodeException = true;
					}
					catch (Exception e)
					{
						// ...
						threwException = true;
						exception = e;
					}

					if (reader.Info != null)
					{
						Console.WriteLine("--> Locals count: {0}", reader.Info.Locals.Length);
						Console.WriteLine("--> Actual method size: {0} (0x{0:X8})", reader.CodeSize);

						if (!threwUnknownOpcodeException && !threwException)
						{
							Console.WriteLine("--> Devirtualizable");
							WritePartiallyDevirtualizedMethod(reader);
						}
						else if (threwUnknownOpcodeException)
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
							}

						}
						else if (threwException)
						{
							Console.WriteLine("--> Not yet devirtualizable (threw exception)");
							Console.WriteLine();
							Console.Write(exception);
						}
					}
				}
			}
		}
	}
}
