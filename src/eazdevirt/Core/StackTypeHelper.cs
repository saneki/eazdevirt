using System;
using System.Collections.Generic;
using System.Linq;
using dnlib;
using dnlib.DotNet;

namespace eazdevirt
{
	public static class StackTypeHelper
	{
		/// <summary>
		/// Takes a TypeDef of a "stack type" (types pushed to Eazfuscator.NET's VM stack) and tries
		/// to determine the underlying type.
		/// </summary>
		/// <param name="typeDef">Stack type</param>
		/// <returns>Underlying type (TypeSig) if determined, false if not</returns>
		public static TypeSig GetUnderlyingType(TypeDef typeDef)
		{
			if (!typeDef.HasMethods)
				return null;

			List<TypeSig> returnTypes = new List<TypeSig>();

			var methods = typeDef.Methods;
			foreach(var method in methods)
			{
				// Checks: If a getter-method (non-static, non-virtual) for a type exists,
				// and a field for that type also exists
				if (!method.IsStatic && !method.IsVirtual
				&& method.ReturnType != typeDef.Module.CorLibTypes.Void
				&& typeDef.Fields.FirstOrDefault(x =>
					{ return x.FieldType.MDToken.Raw == method.ReturnType.MDToken.Raw; }) != null)
					returnTypes.Add(method.ReturnType);
			}

			// Could be improved
			if (returnTypes.Count >= 1)
				return returnTypes[0];

			return null;
		}
	}
}
