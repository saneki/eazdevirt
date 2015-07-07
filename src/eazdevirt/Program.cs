using System;
using System.Linq;
using CommandLine;
using dnlib.DotNet;
using System.IO;
using dnlib.DotNet.Emit;
using System.Collections.Generic;
using eazdevirt.IO;

namespace eazdevirt
{
	public class Program
	{
		static void Main(String[] args)
		{
			var result = CommandLine.Parser.Default.ParseArguments
				<FindMethodsSubOptions,
				 GetKeySubOptions,
				 InstructionsSubOptions,
				 PositionSubOptions>(args);

			if (!result.Errors.Any())
			{
				BaseOptions options = (BaseOptions)result.Value;

				if(!options.NoLogo)
					WriteAsciiLogo();

				if (result.Value is FindMethodsSubOptions)
				{
					DoFindMethods((FindMethodsSubOptions)result.Value);
				}
				else if (result.Value is GetKeySubOptions)
				{
					DoGetKey((GetKeySubOptions)result.Value);
				}
				else if (result.Value is InstructionsSubOptions)
				{
					DoInstructions((InstructionsSubOptions)result.Value);
				}
				else if (result.Value is PositionSubOptions)
				{
					DoPosition((PositionSubOptions)result.Value);
				}
			}
		}

		/// <summary>
		/// Perform "find-methods" verb.
		/// </summary>
		/// <param name="options">Options</param>
		static void DoFindMethods(FindMethodsSubOptions options)
		{
			EazModule module;
			if (!TryLoadModule(options.AssemblyPath, out module))
				return;

			EazVirtualizedMethod[] methods = module.FindVirtualizedMethods();

			if (methods.Length > 0) Console.WriteLine("Virtualized methods found: {0}", methods.Length);
			else Console.WriteLine("No virtualized methods found");

			foreach(var method in methods)
			{
				Console.WriteLine();
				Console.WriteLine(method.Method.FullName);
				Console.WriteLine("--> Position string: {0}", method.PositionString);
				Console.WriteLine("--> Resource: {0}", method.ResourceStringId);
				Console.WriteLine("--> Crypto key: {0}", method.ResourceCryptoKey);

				if(options.ExtraOutput)
				{
					var reader = new EazVirtualizedMethodBodyReader(method);
					Boolean threwUnknownOpcodeException = false, threwException = false;

					try
					{
						reader.Read(); // Read method
					}
					catch (OriginalOpcodeUnknownException)
					{
						// This is almost guaranteed to happen at this point
						threwUnknownOpcodeException = true;
					}
					catch (Exception)
					{
						// ...
						threwException = true;
					}

					if (reader.Info != null)
					{
						Console.WriteLine("--> Locals count: {0}", reader.Info.Locals.Length);
						Console.WriteLine("--> Actual method size: {0} (0x{0:X8})", reader.CodeSize);

						if (!threwUnknownOpcodeException && !threwException)
							Console.WriteLine("--> Potentially devirtualizable");
						else if (threwUnknownOpcodeException)
						{
							var matches = module.VirtualInstructions
								.Where((instr) => { return instr.VirtualOpCode == reader.LastVirtualOpCode; })
								.ToArray();

							if(matches.Length > 0)
							{
								EazVirtualInstruction v = matches[0];
								Console.WriteLine("--> Not yet devirtualizable (contains unknown virtual instruction)");
								Console.WriteLine("-----> Virtual OpCode = {0} @ [{1}] (0x{2:X8})",
									reader.LastVirtualOpCode, reader.CurrentInstructionOffset, reader.CurrentVirtualOffset);
								Console.WriteLine("-----> Delegate method: {0}", v.DelegateMethod.FullName);

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
							Console.WriteLine("--> Not yet devirtualizable (threw exception)");
					}
				}
			}
		}

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

		/// <summary>
		/// Perform "instructions" verb.
		/// </summary>
		/// <param name="options">Options</param>
		static void DoInstructions(InstructionsSubOptions options)
		{
			EazModule module;
			if (!TryLoadModule(options.AssemblyPath, out module))
				return;

			EazVirtualizedMethod method = module.FindFirstVirtualizedMethod();
			if(method == null)
			{
				Console.WriteLine("No methods in assembly seem to be virtualized");
				return;
			}

			// The virtual-call-method should belong to the main virtualization type
			TypeDef virtualizationType = method.VirtualCallMethod.DeclaringType;
			var vInstructions = module.VirtualInstructions;

			if (vInstructions.Count > 0)
			{
				// Get # of identified instructions
				Int32 identified = 0;
				foreach (var v in vInstructions)
					if (v.IsIdentified) identified++;

				// Get % of identified instructions as a string
				String percentIdentified;
				if (identified == 0)
					percentIdentified = "0%";
				else if (identified == vInstructions.Count)
					percentIdentified = "100%";
				else
					percentIdentified = Math.Floor(
						(((double)identified) / ((double)vInstructions.Count)) * 100d
					) + "%";

				Console.WriteLine("Virtual instruction types found: {0}", vInstructions.Count);
				Console.WriteLine("{0}/{1} instruction types identified ({2})",
					identified, vInstructions.Count, percentIdentified);

				if(!options.ExtraOutput)
					Console.WriteLine();

				// If only showing identified instructions, remove all non-identified and sort by name
				if(options.OnlyIdentified)
				{
					vInstructions = new List<EazVirtualInstruction>(vInstructions
						.Where((instruction) => { return instruction.IsIdentified; })
						.OrderBy((instruction) => { return instruction.OpCode.ToString(); }));
				}

				foreach (var v in vInstructions)
				{
					if (!options.ExtraOutput) // Simple output
					{
						if (v.IsIdentified)
							Console.WriteLine("Instruction: {0}, Method: {1}", v.OpCode, v.DelegateMethod.FullName);
						else
							Console.WriteLine("Instruction: Unknown, Method: {0}", v.DelegateMethod.FullName);
					}
					else // Not-Simple output?
					{
						Console.WriteLine();

						if (v.IsIdentified)
							Console.WriteLine("Instruction: {0}", v.OpCode);
						else
							Console.WriteLine("Instruction: Unknown");

						if (v.IsIdentified || !options.OnlyIdentified)
						{
							if (v.HasVirtualOpCode)
							{
								Console.WriteLine("--> Virtual OpCode:  {0} ({0:X8})", v.VirtualOpCode);
								Console.WriteLine("--> Operand type:    {0}", v.OperandType);
							}

							{
								Console.WriteLine("--> Delegate method: {0}", v.DelegateMethod.FullName);
							}
						}
					}
				}

				// Print operand information
				if(options.Operands)
				{
					var operandTypeDict = new Dictionary<Int32, Int32>();
					foreach (var vInstr in vInstructions)
					{
						var type = vInstr.OperandType;
						if (operandTypeDict.ContainsKey(type))
							operandTypeDict[type] = (operandTypeDict[type] + 1);
						else operandTypeDict.Add(type, 1);
					}

					Console.WriteLine();
					Console.WriteLine("Virtual operand type counts:");
					foreach(var kvp in operandTypeDict)
						Console.WriteLine("  Operand {0}: {1} occurrence(s)", kvp.Key, kvp.Value);
				}
			}
			else Console.WriteLine("No virtual instructions found?");
		}

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

		static Boolean TryLoadModule(String path, out EazModule module)
		{
			try
			{
				module = new EazModule(path);
			}
			catch (IOException e)
			{
				Console.WriteLine(e.Message);
				module = null;
				return false;
			}

			return true;
		}

		static void WritePartiallyDevirtualizedMethod(EazVirtualizedMethodBodyReader reader)
		{
			Console.WriteLine();
			foreach(var instruction in reader.Instructions)
			{
				Console.WriteLine("{0}", instruction.ToString());
			}
			Console.WriteLine();
		}

		static void WriteAsciiLogo()
		{
			String logo =
@"
                         .___          .__         __   
  ____ _____  ________ __| _/_______  _|__|_______/  |_ 
_/ __ \\__  \ \___   // __ |/ __ \  \/ /  \_  __ \   __\
\  ___/ / __ \_/    // /_/ \  ___/\   /|  ||  | \/|  |  
 \___  >____  /_____ \____ |\___  >\_/ |__||__|   |__|  
     \/     \/      \/    \/    \/                      
";
			Console.WriteLine(logo);
			Console.WriteLine();
		}
	}
}
