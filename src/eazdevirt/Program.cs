using System;
using System.Linq;
using CommandLine;
using dnlib.DotNet;
using System.IO;

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
				Console.WriteLine(method.Method.FullName);
				Console.WriteLine("  Position string: {0}", method.PositionString);
				Console.WriteLine("  Resource: {0}", method.ResourceStringId);
				Console.WriteLine("  Crypto key: {0}", method.ResourceCryptoKey);
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
			var vInstructions = EazVirtualInstruction.FindAllInstructions(virtualizationType);

			if (vInstructions.Length > 0)
			{
				Console.WriteLine("Virtual instruction types found: {0}", vInstructions.Length);
				foreach (var vInstr in vInstructions)
				{
					Console.WriteLine("Instruction: {0}", vInstr.VirtualOpCode);
					Console.WriteLine("  Operand type: {0}", vInstr.OperandType);
					Console.WriteLine("  Delegate method: {0}", vInstr.DelegateMethod.FullName);
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
	}
}
