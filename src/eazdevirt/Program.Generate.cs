using System;
using System.Linq;
using eazdevirt.Generator;

namespace eazdevirt
{
	public partial class Program
	{
		/// <summary>
		/// Perform "generate" verb.
		/// </summary>
		/// <param name="options">Options</param>
		static void DoGenerate(GenerateSubOptions options)
		{
			var generator = new VirtualizableAssemblyGenerator();

			String instructionSet = "all";
			if (options.InstructionSet != null && options.InstructionSet.Length > 0)
				instructionSet = options.InstructionSet.ToLower();

			String[] sets;
			if (instructionSet.Contains(','))
				sets = instructionSet.Split(',');
			else sets = new String[] { instructionSet };

			Boolean all = sets.Contains("all");
			if (sets.Contains("calli") || all)
				generator.AddCalliMethod();
			if (sets.Contains("conv") || all)
				generator.AddConvMethod();
			if (sets.Contains("ind") || all)
				generator.AddIndMethod();
			if (sets.Contains("static-field") || all)
				generator.AddStaticFieldMethod();

			if (!generator.HasMethod)
			{
				Console.WriteLine("Unknown set(s): {0}", instructionSet);
				return;
			}

			var assembly = generator.Generate();

			String filepath = options.OutputPath;
			if (filepath == null)
				filepath = "eazdevirt-test.exe";

			Console.WriteLine("Saving test assembly {0}", filepath);

			assembly.Write(filepath);
		}
	}
}
