using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using dnlib.DotNet;
using Mono.Options;
using eazdevirt.IO;
using eazdevirt.Logging;

namespace eazdevirt
{
	public partial class Program
	{
		static void PrintHelp(MonoOptions parsed)
		{
			Console.WriteLine(GetDescriptorString());
			Console.WriteLine();

			Console.WriteLine("usage: eazdevirt [-dgikmpr] [options] <assembly>");
			Console.WriteLine();

			String generatedHelp = parsed.OptionDescriptors;
			Console.Write(generatedHelp);
			Console.WriteLine();

			Console.WriteLine("examples:");
			Console.WriteLine("  eazdevirt -d MyAssembly.exe");
			Console.WriteLine("  eazdevirt -r --keep-encrypted MyAssembly.exe");
		}

		static String GetDescriptorString()
		{
			// Get description + version from assembly attribute
			Assembly assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();

			// Get description
			var descAttr = (AssemblyDescriptionAttribute)assembly.GetCustomAttribute(typeof(AssemblyDescriptionAttribute));
			String description = descAttr.Description;

			// Get version
			Version version = Assembly.GetExecutingAssembly().GetName().Version;

			return String.Format("eazdevirt {0} - {1}", version.ToString(), description);
		}

		/// <summary>
		/// Parse a MonoOptions from passed arguments.
		/// </summary>
		/// <param name="args">Arguments passed to program</param>
		/// <returns>MonoOptions</returns>
		static MonoOptions Parse(String[] args)
		{
			MonoOptions options = new MonoOptions();
			OptionSet optionSet = new OptionSet()
			{
				// Program action options
				{ "d|devirtualize", "attempt to devirtualize methods in a protected assembly",
					v => options.Action = ProgramAction.Devirtualize },
				{ "g|generate", "generate a test executable to be protected and analysed",
					v => options.Action = ProgramAction.Generate },
				{ "i|instructions", "print virtual opcode information extracted from a protected assembly",
					v => options.Action = ProgramAction.Instructions },
				{ "k|get-key", "extract the integer crypto key from a protected assembly",
					v => options.Action = ProgramAction.GetKey },
				{ "m|methods", "print virtualized method + method stub information extracted from a protected assembly",
					v => options.Action = ProgramAction.Methods },
				{ "p|position", "translate a position string into its Int64 representation given either an integer "
				              + "crypto key or a protected assembly",
					v => options.Action = ProgramAction.Position },
				{ "r|resource", "extract the embedded resource from a protected assembly",
					v => options.Action = ProgramAction.Resource },

				{ "N|no-throw", "don't throw when writing a module", v => options.NoThrow = true },

				// `generate` options
				{ "I=|instruction-set=", "name of \"instruction sets\" to generate",
					v => options.InstructionSet = v },

				// `instructions` options
				{ "only-identified", "only show identified opcodes",
					v => options.OnlyIdentified = true },
				{ "operands", "print info about operand types",
					v => options.Operands = true },
				{ "operand-type=", "operand type whitelist",
					(Int32 v) => options.OperandTypeWhitelist = v },

				// `position` options
				{ "K=|key=", "integer crypto key used to translate a position string",
					(Int32 v) => options.Key = v },
				{ "P=|position-string=", "position string to translate",
					v => options.PositionString = v },

				// `resource` options
				{ "o=|destination=", "destination file (type of file depends on program action)",
					v => options.OutputPath = v },
				{ "f|force", "overwrite destination file if it exists",
					v => options.OverwriteExisting = true },
				{ "x|extract", v => options.ExtractResource = true },
				{ "D|keep-encrypted", "don't decrypt the resource file when extracting",
					v => options.KeepEncrypted = true },

				// Other options
				{ "L|no-logo", "don't show the ascii logo", v => options.NoLogo = true },
				{ "h|?|help", "show help/usage info and exit", v => options.Help = true },
				{ "v|verbose", "more output", v => options.VerboseLevel++ }
			};

			options.Extra = optionSet.Parse(args);
			options.OptionSet = optionSet;

			if (options.Extra.Count > 0)
				options.AssemblyPath = options.Extra[0];

			return options;
		}

		static Int32 Main(String[] args)
		{
			var options = Parse(args);

			if (!options.NoLogo)
				WriteAsciiLogo();

			if (options.Help || options.Action == ProgramAction.None)
			{
				PrintHelp(options);
				return 0;
			}

			switch (options.Action)
			{
				case ProgramAction.Devirtualize:
					DoDevirtualize(options);
					break;
				case ProgramAction.Generate:
					DoGenerate(options);
					break;
				case ProgramAction.GetKey:
					DoGetKey(options);
					break;
				case ProgramAction.Instructions:
					DoInstructions(options);
					break;
				case ProgramAction.Methods:
					DoMethods(options);
					break;
				case ProgramAction.Position:
					DoPosition(options);
					break;
				case ProgramAction.Resource:
					DoResource(options);
					break;
			}

			return 0;
		}

		static ILogger GetLogger(MonoOptions options)
		{
			LoggerEvent e = LoggerEvent.Info;

			if (options.VerboseLevel == 1)
				e = LoggerEvent.Verbose;
			else if (options.VerboseLevel > 1)
				e = LoggerEvent.VeryVerbose;

			return new ConsoleLogger(e);
		}

		static Boolean TryLoadModule(String path, out EazModule module)
		{
			return TryLoadModule(path, null, out module);
		}

		static Boolean TryLoadModule(String path, ILogger logger, out EazModule module)
		{
			try
			{
				ModuleDefMD moduleDef = ModuleDefMD.Load(path);
				AssemblyResolver asmResolver = new AssemblyResolver();
				ModuleContext modCtx = new ModuleContext(asmResolver);
				// All resolved assemblies will also get this same modCtx
				asmResolver.DefaultModuleContext = modCtx;
				moduleDef.Context = modCtx;

				module = new EazModule(moduleDef, logger);
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

		static void WritePartiallyDevirtualizedMethod(VirtualizedMethodBodyReader reader)
		{
			if (!reader.HasInstructions)
				return;

			Console.WriteLine();
			foreach(var instruction in reader.Instructions)
				Console.WriteLine("{0}", instruction.ToString());
			Console.WriteLine();
		}

		/// <summary>
		/// ASCII logo.
		/// </summary>
		static String Logo
		{
			get
			{
				return
@"
                         .___          .__         __   
  ____ _____  ________ __| _/_______  _|__|_______/  |_ 
_/ __ \\__  \ \___   // __ |/ __ \  \/ /  \_  __ \   __\
\  ___/ / __ \_/    // /_/ \  ___/\   /|  ||  | \/|  |  
 \___  >____  /_____ \____ |\___  >\_/ |__||__|   |__|  
     \/     \/      \/    \/    \/                      
";
			}
		}

		static void WriteAsciiLogo()
		{
			Console.WriteLine(Logo);
			Console.WriteLine();
		}
	}
}
