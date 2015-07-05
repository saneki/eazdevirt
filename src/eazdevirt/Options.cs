using System;
using CommandLine;

namespace eazdevirt
{
	public class BaseOptions
	{
		[Value(0, Required = true)]
		public String AssemblyPath { get; set; }
	}

	[Verb("get-key", HelpText = "Extract the integer used for stream crypto")]
	public class GetKeySubOptions : BaseOptions { }

	[Verb("find-methods", HelpText = "Find virtualized methods")]
	public class FindMethodsSubOptions : BaseOptions { }

	[Verb("instructions", HelpText = "Extract all virtual instruction information")]
	public class InstructionsSubOptions : BaseOptions { }

	[Verb("position", HelpText = "Get the position specified by a position string of length 10")]
	public class PositionSubOptions
	{
		[Value(0, Required = true)]
		public String PositionString { get; set; }

		[Value(1)]
		public String AssemblyPath { get; set; }

		[Option('k', "key", HelpText = "Integer key used for crypto")]
		public Nullable<Int32> Key { get; set; }
	}
}
