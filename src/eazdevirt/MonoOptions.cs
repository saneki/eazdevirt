using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Mono.Options;
using eazdevirt.Fixers;

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
		/// Whether or not to not throw when writing.
		/// </summary>
		public Boolean NoThrow = false;

		/// <summary>
		/// Whether or not to inject attributes upon successful devirtualization.
		/// </summary>
		public Boolean InjectAttributes = false;

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

		//public String Destination = null;
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

		/// <summary>
		/// List of MethodFixer types to use.
		/// </summary>
		public IList<Type> MethodFixers
		{
			get
			{
				if (_methodFixers == null)
					this.FixersString = "all";
				return _methodFixers;
			}
			private set { _methodFixers = value; }
		}
		private IList<Type> _methodFixers = null;

		/// <summary>
		/// Set MethodFixers from a string describing the fixers to use.
		/// </summary>
		public String FixersString
		{
			set
			{
				var fields = value.Split(',') // Regex.Split(value, @"\s+")
					.Select(f => { return f.Trim().ToLower(); })
					.Where(f => f.Length > 0) // Remove empty fields
					.Distinct();

				var types = Assembly.GetExecutingAssembly().GetTypes();
				types = types.Where(t => {
					return (t.GetCustomAttribute(typeof(FixerAttribute))) != null;
				}).ToArray();

				if (fields.Contains("all"))
					this.MethodFixers = types;
				else if (fields.Contains("none"))
					this.MethodFixers = new List<Type>();
				else
				{
					this.MethodFixers = types.Where(t => {
						var attr = (FixerAttribute)t.GetCustomAttribute(typeof(FixerAttribute));
						return fields.Contains(attr.Name.ToLower());
					}).ToArray();
				}
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
