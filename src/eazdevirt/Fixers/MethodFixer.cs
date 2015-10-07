using System;
using dnlib.DotNet;

namespace eazdevirt.Fixers
{
	public abstract class MethodFixer : IMethodFixer
	{
		public MethodDef Method { get; private set; }

		public MethodFixer(MethodDef method)
		{
			this.Method = method;
		}

		public abstract void Fix();
	}
}
