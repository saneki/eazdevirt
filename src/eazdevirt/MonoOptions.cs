using System;
using System.Collections.Generic;
using System.IO;
using Mono.Options;

namespace eazdevirt
{
	public class MonoOptions
	{
		public ProgramAction Action = ProgramAction.None;
		public Boolean NoLogo = false;

		/// <summary>
		/// Path to input assembly.
		/// </summary>
		public String AssemblyPath = null;

		/// <summary>
		/// Path to output assembly.
		/// </summary>
		public String OutputPath = null;

		public Boolean Help = false;
		public UInt32 VerboseLevel = 0;

		/// <summary>
		/// Used for: generate.
		/// </summary>
		public String InstructionSet = null;

		public Boolean OnlyIdentified = false;

		/// <summary>
		/// Integer crypto key.
		/// </summary>
		/// <remarks>
		/// HelpText = "Integer key used for crypto"
		/// Used for: position
		/// </remarks>
		public Nullable<Int32> Key { get; set; }

		public String Destination = null;
		public Boolean OverwriteExisting = false;
		public Boolean ExtractResource = true;
		public Boolean KeepEncrypted = false;

		/// <summary>
		/// Operand "whitelist". Remove or improve later.
		/// </summary>
		/// <remarks>
		/// HelpText = "Only show instructions with specified virtual operand type"
		/// Used for: instructions.
		/// </remarks>
		public Int32 OperandTypeWhitelist = Int32.MinValue;

		/// <summary>
		/// Whether or not to print information about operand types.
		/// </summary>
		/// <remarks>
		/// Used for: instructions.
		/// HelpText = "Print information about operand types"
		/// </remarks>
		public Boolean Operands = false;

		/// <summary>
		/// Position string to translate.
		/// </summary>
		/// <remarks>
		/// Used for: position
		/// </remarks>
		public String PositionString { get; set; }

		public List<String> Extra = null;
		public OptionSet OptionSet = null;

		public Boolean Verbose
		{
			get { return this.VerboseLevel > 0; }
		}

		public Boolean VeryVerbose
		{
			get { return this.VerboseLevel > 1; }
		}

		/// <summary>
		/// Get the option descriptors text from the OptionSet. If
		/// OptionSet is null, returns null.
		/// </summary>
		public String OptionDescriptors
		{
			get
			{
				if (this.OptionSet == null)
					return null;

				// Generate help string from OptionSet
				StringWriter writer = new StringWriter();
				this.OptionSet.WriteOptionDescriptions(writer);
				return writer.ToString();
			}
		}
	}

	public enum ProgramAction
	{
		None,
		Devirtualize,
		Generate,
		GetKey,
		Instructions,
		Methods,
		Position,
		Resource
	}
}
