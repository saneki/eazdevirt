using System;
using System.Collections.Generic;
using dnlib.DotNet;

namespace eazdevirt.Util
{
	public static class GenericUtils
	{
		public static IList<TypeSig> CreateGenericReturnTypePossibilities(TypeSig returnType,
			IList<TypeSig> typeGenerics, IList<TypeSig> methodGenerics)
		{
			IList<TypeSig> list = new List<TypeSig>();
			list.Add(returnType);

			if(returnType.IsGenericInstanceType)
			{
				var genericSig = returnType.ToGenericInstSig();
				var combos = GenericUtils.CreateGenericParameterCombinations(
					genericSig.GenericArguments, typeGenerics, methodGenerics);

				foreach(var combo in combos)
				{
					var sig = new GenericInstSig(genericSig.GenericType, combo);
					list.Add(sig);
				}

				return list;
			}

			for (UInt16 g = 0; g < typeGenerics.Count; g++)
			{
				var gtype = typeGenerics[g];

				if (returnType.FullName.Equals(gtype.FullName))
				{
					list.Add(new GenericVar(g));
				}
			}

			for (UInt16 g = 0; g < methodGenerics.Count; g++)
			{
				var gtype = typeGenerics[g];

				if(returnType.FullName.Equals(gtype.FullName))
				{
					list.Add(new GenericMVar(g));
				}
			}

			return list;
		}

		/// <summary>
		/// Create a list of all possible combinations of types/generic types that would make
		/// sense as parameters. This is necessary because the serialized method data does not
		/// contain information about which parameters map to which generic types (indices),
		/// neither GenericVars (declaring type) or GenericMVars (method itself).
		///
		/// TODO: Factor in context generics (generics from virtualized method itself and
		/// declaring type?)
		/// </summary>
		/// <param name="parameters">Parameters (with no generic type information)</param>
		/// <param name="generics">Generics visible to the method</param>
		/// <returns>Combinations with at least one item (original parameters)</returns>
		public static IList<IList<TypeSig>> CreateGenericParameterCombinations(IList<TypeSig> parameters,
			IList<TypeSig> typeGenerics, IList<TypeSig> methodGenerics)
		{
			IList<IList<TypeSig>> list = new List<IList<TypeSig>>();
			list.Add(parameters);

			for (UInt16 p = 0; p < parameters.Count; p++)
			{
				var ptype = parameters[p];

				for (UInt16 g = 0; g < typeGenerics.Count; g++)
				{
					var gtype = typeGenerics[g];

					// Better comparison?
					if (ptype.FullName.Equals(gtype.FullName))
					{
						Int32 length = list.Count;
						for (Int32 i = 0; i < length; i++)
						{
							// Copy param list
							List<TypeSig> newParams = new List<TypeSig>();
							newParams.AddRange(list[i]);

							GenericVar gvar = new GenericVar(g);
							newParams[p] = gvar;

							list.Add(newParams);
						}
					}
				}

				for (UInt16 g = 0; g < methodGenerics.Count; g++)
				{
					var gtype = methodGenerics[g];

					if (ptype.FullName.Equals(gtype.FullName))
					{
						Int32 length = list.Count;
						for (Int32 i = 0; i < length; i++)
						{
							List<TypeSig> newParams = new List<TypeSig>();
							newParams.AddRange(list[i]);

							GenericMVar gmvar = new GenericMVar(g);
							newParams[p] = gmvar;

							list.Add(newParams);
						}
					}
				}
			}

			return list;
		}
	}
}
