using System;
using dnlib.DotNet;

namespace eazdevirt.Generator
{
	public interface IAssemblyGenerator
	{
		AssemblyDef Generate();
	}
}
