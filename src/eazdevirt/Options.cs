using System;
using CommandLine;
using System.Collections.Generic;

namespace eazdevirt
{
	public class BaseOptions
	{
		[Option('L', "no-logo", DefaultValue = false, HelpText = "Don't output the ascii logo")]
		public Boolean NoLogo { get; set; }
	}

	public class BaseAssemblyOptions : BaseOptions
	{
		[Value(0, Required = true)]
		public String AssemblyPath { get; set; }
	}

	[Verb("devirtualize", HelpText = "Devirtualize")]
	public class DevirtualizeSubOptions : BaseAssemblyOptions
	{
		[Option('e', "extra-output", DefaultValue = false,
			HelpText = "Extra output")]
		public Boolean ExtraOutput { get; set; }
	}

	[Verb("devirt", HelpText = "Alias for \"devirtualize\"")]
	public class DevirtSubOptions : DevirtualizeSubOptions
	{
	}

	[Verb("d", HelpText = "Alias for \"devirtualize\"")]
	public class DSubOptions : DevirtualizeSubOptions
	{
	}

	[Verb("find-methods", HelpText = "Find virtualized methods")]
	public class FindMethodsSubOptions : BaseAssemblyOptions
	{
		[Option('e', "extra-output", DefaultValue = false,
			HelpText = "Extra output, excluding the method body")]
		public Boolean ExtraOutput { get; set; }
	}

	[Verb("m", HelpText = "Alias for \"find-methods\"")]
	public class MSubOptions : FindMethodsSubOptions
	{
	}

	[Verb("get-key", HelpText = "Extract the integer used for stream crypto")]
	public class GetKeySubOptions : BaseAssemblyOptions { }

	[Verb("instructions", HelpText = "Extract all virtual instruction information")]
	public class InstructionsSubOptions : BaseAssemblyOptions
	{
		[Option('i', "only-identified", DefaultValue = false,
			HelpText = "Only print instructions with identified original opcodes")]
		public Boolean OnlyIdentified { get; set; }

		[Option('o', "operands", DefaultValue = false,
			HelpText = "Print information about operand types")]
		public Boolean Operands { get; set; }

		// Command Line Parser Library refuses to work with IEnumerable, Arrays, etc.
		// Only allow single operand type for now
		[Option('z', "operand-type", DefaultValue = Int32.MinValue,
			HelpText = "Only show instructions with specified virtual operand type")]
		public Int32 OperandTypeWhitelist { get; set; }

		[Option('e', "extra-output", DefaultValue = false, HelpText = "Extra output")]
		public Boolean ExtraOutput { get; set; }
	}

	[Verb("i", HelpText = "Alias for \"instructions\"")]
	public class ISubOptions : InstructionsSubOptions
	{
	}

	[Verb("position", HelpText = "Get the position specified by a position string of length 10")]
	public class PositionSubOptions : BaseOptions
	{
		[Value(0, Required = true)]
		public String PositionString { get; set; }

		[Value(1)]
		public String AssemblyPath { get; set; }

		[Option('k', "key", HelpText = "Integer key used for crypto")]
		public Nullable<Int32> Key { get; set; }
	}

	[Verb("resource", HelpText = "Extract the embedded resource")]
	public class ResourceSubOptions : BaseAssemblyOptions
	{
		[Option('o', "destination")]
		public String OutputPath { get; set; }

		[Option('f', "force", DefaultValue = false, HelpText = "Overwrite existing file")]
		public Boolean OverwriteExisting { get; set; }

		[Option('x', "extract", DefaultValue = false,
			HelpText = "Extract the embedded resource to a file")]
		public Boolean Extract { get; set; }

		[Option('D', "keep-encrypted", DefaultValue = false,
			HelpText = "Don't decrypt the embedded resource when extracting")]
		public Boolean KeepEncrypted { get; set; }
	}

	[Verb("res", HelpText = "Alias for \"resource\"")]
	public class ResSubOptions : ResourceSubOptions
	{
	}

	[Verb("r", HelpText = "Alias for \"resource\"")]
	public class RSubOptions : ResourceSubOptions
	{
	}
}
