using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using CommandLine;
using CommandLine.Text;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using eazdevirt.Generator;
using eazdevirt.IO;

namespace eazdevirt
{
	public class Program
	{
		static void Main(String[] args)
		{
			var parser = CreateParser();

			var result = parser.ParseArguments
				<DSubOptions,
				 DevirtSubOptions,
				 DevirtualizeSubOptions,
				 FindMethodsSubOptions,
				 GSubOptions,
				 GenSubOptions,
				 GenerateSubOptions,
				 GetKeySubOptions,
				 ISubOptions,
				 InstructionsSubOptions,
				 MSubOptions,
				 PositionSubOptions,
				 ResourceSubOptions,
				 ResSubOptions,
				 RSubOptions>(args);

			if (!result.Errors.Any())
			{
				BaseOptions options = (BaseOptions)result.Value;

				if(!options.NoLogo)
					WriteAsciiLogo();

				if (result.Value is DevirtualizeSubOptions)
				{
					DoDevirtualize((DevirtualizeSubOptions)result.Value);
				}
				else if (result.Value is FindMethodsSubOptions)
				{
					DoFindMethods((FindMethodsSubOptions)result.Value);
				}
				else if (result.Value is GenerateSubOptions)
				{
					DoGenerate((GenerateSubOptions)result.Value);
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
				else if (result.Value is ResourceSubOptions)
				{
					DoResource((ResourceSubOptions)result.Value);
				}
			}
			else
			{
				WriteHelpText(args, parser, result);
			}
		}

		/// <summary>
		/// Create a custom command line parser.
		/// </summary>
		/// <returns>Parser</returns>
		static Parser CreateParser()
		{
			return new Parser((settings) =>
			{
				// Will handle writing help ourself
				settings.HelpWriter = null;
			});
		}

		/// <summary>
		/// Write help text to console.
		/// </summary>
		/// <param name="args">Args</param>
		/// <param name="parser">Parser</param>
		/// <param name="result">Parse result</param>
		static void WriteHelpText(String[] args, Parser parser, ParserResult<Object> result)
		{
			// Get description from assembly attribute
			Assembly assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
			var descAttr = (AssemblyDescriptionAttribute)assembly.GetCustomAttribute(typeof(AssemblyDescriptionAttribute));
			String description = descAttr.Description;

			// Append description to heading
			HeadingInfo headingInfo = HeadingInfo.Default;
			StringBuilder headingBuilder = new StringBuilder(headingInfo.ToString());
			headingBuilder.Append(" - ");
			headingBuilder.Append(description);

			HelpText helpText = HelpText.AutoBuild(result);
			helpText.AdditionalNewLineAfterOption = false;
			helpText.Heading = headingBuilder.ToString();
			Console.WriteLine(helpText);
		}

		/// <summary>
		/// Perform "devirtualize" verb.
		/// </summary>
		/// <param name="options">Options</param>
		static void DoDevirtualize(DevirtualizeSubOptions options)
		{
			EazModule module;
			if (!TryLoadModule(options.AssemblyPath, out module))
				return;

			EazDevirtualizer devirtualizer = new EazDevirtualizer(module);

			var results = devirtualizer.Devirtualize((attempt) =>
			{
				if (attempt.Successful)
				{
					var method = attempt.Method;
					var body = attempt.MethodBody;

					Console.WriteLine("Devirtualized {0} (MDToken = 0x{1:X8})",
						method.FullName, method.MDToken.Raw);

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

					// Print instructions
					Console.WriteLine("Instructions:");
					Console.WriteLine("-------------");
					foreach (var instr in body.Instructions)
						Console.WriteLine(instr);
					Console.WriteLine();
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
				module.Write(outputPath);
			}
		}

		static String GetDevirtualizedModulePath(String origPath)
		{
			String ext = Path.GetExtension(origPath);
			String noExt = Path.GetFileNameWithoutExtension(origPath);
			return String.Format("{0}-devirtualized{1}",
				Path.Combine(Path.GetDirectoryName(origPath), noExt), ext);
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
				Console.WriteLine("--> Position: {0} (0x{0:X8})", method.Position);
				Console.WriteLine("--> Resource: {0}", method.ResourceStringId);
				Console.WriteLine("--> Crypto key: {0}", method.ResourceCryptoKey);

				if(options.ExtraOutput)
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
								.Where((instr) => { return instr.VirtualOpCode == reader.LastVirtualOpCode; })
								.ToArray();

							if (matches.Length > 0)
							{
								EazVirtualInstruction v = matches[0];
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

		/// <summary>
		/// Perform "generate" verb.
		/// </summary>
		/// <param name="options">Options</param>
		static void DoGenerate(GenerateSubOptions options)
		{
			var generator = VirtualizableAssemblyGenerator.CreateConvAssembly();
			var assembly = generator.Generate();

			String filepath = options.OutputPath;
			if (filepath == null)
				filepath = "eazdevirt-test.exe";

			Console.WriteLine("Saving test assembly {0}", filepath);

			assembly.Write(filepath);
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

				// If only showing instructions with specific virtual operand types, filter
				if(options.OperandTypeWhitelist != Int32.MinValue)
				{
					vInstructions = new List<EazVirtualInstruction>(vInstructions
						.Where((instruction) => {
							return options.OperandTypeWhitelist == instruction.VirtualOperandType;
						}));
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
								Console.WriteLine("--> Operand type:    {0}", v.VirtualOperandType);
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
						var type = vInstr.VirtualOperandType;
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

		static void DoResource(ResourceSubOptions options)
		{
			EazModule module;
			if (!TryLoadModule(options.AssemblyPath, out module))
				return;

			// If no action set, set the default action (extract)
			if (!options.Extract)
				options.Extract = true;

			EazVirtualizedMethod method = module.FindFirstVirtualizedMethod();
			if(method != null)
			{
				if (options.Extract)
				{
					String outputPath = options.OutputPath;
					if (outputPath == null || outputPath.Equals(""))
						outputPath = method.ResourceStringId;

					FileMode fileMode = FileMode.CreateNew;
					if (options.OverwriteExisting)
						fileMode = FileMode.Create;

					using (Stream resourceStream = module.GetResourceStream(options.KeepEncrypted))
					{
						try
						{
							using (FileStream fileStream = new FileStream(outputPath, fileMode, FileAccess.Write))
							{
								resourceStream.CopyTo(fileStream);
							}
						}
						catch(IOException e)
						{
							Console.Write(e);
						}
					}

					Console.WriteLine("Extracted {0} resource to {1}",
						options.KeepEncrypted ? "encrypted" : "decrypted", outputPath);
				}
			}
		}

		static Boolean TryLoadModule(String path, out EazModule module)
		{
			try
			{
				module = new EazModule(path);
			}
			catch (IOException e)
			{
				// Console.WriteLine(e.Message);
				Console.Write(e);
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
