using System;

namespace eazdevirt.Fixers
{
	public class FixerAttribute : Attribute
	{
		/// <summary>
		/// Name of the fixer as it is specified by program arguments.
		/// </summary>
		public String Name { get; private set; }

		public FixerAttribute(String name)
		{
			this.Name = name;
		}
	}
}
