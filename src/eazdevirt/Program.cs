using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using CommandLine;
using CommandLine.Text;
using dnlib.DotNet;
using eazdevirt.IO;
using eazdevirt.Logging;

namespace eazdevirt
{
	public partial class Program
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

		static ILogger GetLogger(BaseOptions options)
		{
			LoggerEvent e = LoggerEvent.Info;

			if (options.VeryVerbose)
				e = LoggerEvent.VeryVerbose;
			else if (options.Verbose)
				e = LoggerEvent.Verbose;

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
				module = new EazModule(path, logger);
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
