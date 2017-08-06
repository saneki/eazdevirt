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
		static void DoGenerate(MonoOptions options)
		{
			var generator = new VirtualizableAssemblyGenerator();

			String instructionSet = string.IsNullOrEmpty(options.InstructionSet)
                ? "all" 
                : options.InstructionSet.ToLower();

			String[] sets = instructionSet.Contains(',') 
                ? instructionSet.Split(',') 
                : new String[] { instructionSet };

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

			String filepath = options.OutputPath ?? "eazdevirt-test.exe";

		    Console.WriteLine("Saving test assembly {0}", filepath);

			assembly.Write(filepath);
		}
	}
}
