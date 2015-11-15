using System;
using dnlib.DotNet;

namespace eazdevirt.Types
{
	public abstract class BaseTypeDefProvider : ITypeDefProvider
	{
		/// <summary>
		/// Underlying TypeDef.
		/// </summary>
		public TypeDef TypeDef { get; private set; }

		public BaseTypeDefProvider(TypeDef typeDef)
		{
			this.TypeDef = typeDef;
		}
	}
}
