using System;
using System.Collections.Generic;
using System.Linq;
using dnlib.DotNet;

namespace eazdevirt
{
	public partial class Program
	{
		/// <summary>
		/// Perform "instructions" verb.
		/// </summary>
		/// <param name="options">Options</param>
		static void DoInstructions(MonoOptions options
			/* InstructionsSubOptions options */)
		{
			EazModule module;
			if (!TryLoadModule(options.AssemblyPath, out module))
				return;

			MethodStub method = module.FindFirstVirtualizedMethod();
			if (method == null)
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

				if (!options.Verbose)
					Console.WriteLine();

				// If only showing identified instructions, remove all non-identified and sort by name
				if (options.OnlyIdentified)
				{
					vInstructions = new List<VirtualOpCode>(vInstructions
						.Where((instruction) => { return instruction.IsIdentified; })
						.OrderBy((instruction) => { return instruction.OpCode.ToString(); }));
				}

				// If only showing instructions with specific virtual operand types, filter
				if (options.OperandTypeWhitelist != Int32.MinValue)
				{
					vInstructions = new List<VirtualOpCode>(vInstructions
						.Where((instruction) =>
						{
							return options.OperandTypeWhitelist == instruction.VirtualOperandType;
						}));
				}

				foreach (var v in vInstructions)
				{
					if (!options.Verbose) // Simple output
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
							if (v.HasVirtualCode)
							{
								Console.WriteLine("--> Virtual OpCode:  {0} ({0:X8})", v.VirtualCode);
								Console.WriteLine("--> Operand type:    {0}", v.VirtualOperandType);
							}

							{
								Console.WriteLine("--> Delegate method: {0}", v.DelegateMethod.FullName);
							}
						}
					}
				}

				// Print operand information
				if (options.Operands)
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
					foreach (var kvp in operandTypeDict)
						Console.WriteLine("  Operand {0}: {1} occurrence(s)", kvp.Key, kvp.Value);
				}
			}
			else Console.WriteLine("No virtual instructions found?");
		}
	}
}
