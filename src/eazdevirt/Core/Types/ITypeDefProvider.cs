using System;
using dnlib.DotNet;

namespace eazdevirt.Types
{
	public interface ITypeDefProvider
	{
		TypeDef TypeDef { get; }
	}
}
