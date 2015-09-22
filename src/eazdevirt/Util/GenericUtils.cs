using System;
using System.Collections.Generic;
using System.Linq;
using dnlib.DotNet;

namespace eazdevirt.Util
{
	public static class GenericUtils
	{
		public static IList<TypeSig> PossibleTypeSigs(TypeSig returnType,
			IList<TypeSig> typeGenerics, IList<TypeSig> methodGenerics)
		{
			IList<TypeSig> list = new List<TypeSig>();

			// Ignore [], &, * when comparing against generic types
			// Otherwise, String[] Blah<String>(...) won't consider that the
			// return type might be T[].
			Stack<String> modifiers;
			TypeSig returnTypeBase = SigUtil.ToBaseSig(returnType, out modifiers);
			if (returnTypeBase == null)
				throw new Exception(String.Format("Given TypeSig is not a TypeDefOrRefSig: {0}", returnType));

			// Generic instance type
			if (returnTypeBase.IsGenericInstanceType)
			{
				var genericSig = returnTypeBase.ToGenericInstSig();
				var combos = GenericUtils.CreateGenericParameterCombinations(
					genericSig.GenericArguments, typeGenerics, methodGenerics);

				foreach (var combo in combos)
					list.Add(new GenericInstSig(genericSig.GenericType, combo));

				return list;
			}
			else // Non-generic-instance type
			{
				list.Add(returnType);

				for (UInt16 g = 0; g < typeGenerics.Count; g++)
				{
					var gtype = typeGenerics[g];
					if (returnTypeBase.FullName.Equals(gtype.FullName))
						list.Add(SigUtil.FromBaseSig(new GenericVar(g), modifiers));
				}

				for (UInt16 g = 0; g < methodGenerics.Count; g++)
				{
					var gtype = methodGenerics[g];
					if (returnTypeBase.FullName.Equals(gtype.FullName))
						list.Add(SigUtil.FromBaseSig(new GenericMVar(g), modifiers));
				}

				return list;
			}
		}

		/// <summary>
		/// Get all combinations of some collections of lists.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="list">Collection of lists to get all combinations from</param>
		/// <returns>All combinations</returns>
		public static IList<IList<T>> AllCombinations<T>(IEnumerable<IList<T>> list)
		{
			return _AllCombinations<T>(list).ToArray();
		}

		/// <summary>
		/// Get all combinations of some collections of lists.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="list">Collection of lists to get all combinations from</param>
		/// <param name="_selected"></param>
		/// <returns>All combinations</returns>
		/// <remarks>Credits to Fung: https://stackoverflow.com/a/17642220 </remarks>
		static IEnumerable<IList<T>> _AllCombinations<T>(IEnumerable<IList<T>> list, IEnumerable<T> _selected = null)
		{
			if(_selected == null)
				_selected = new T[0];

			if (list.Any())
			{
				var remainingLists = list.Skip(1);
				foreach (var item in list.First().Where(x => !_selected.Contains(x)))
					foreach (var combo in _AllCombinations<T>(remainingLists, _selected.Concat(new T[] { item })))
						yield return combo;
			}
			else
			{
				yield return _selected.ToList();
			}
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
		/// <param name="typeGenerics">Generic variables of the type</param>
		/// <param name="methodGenerics">Generic variables of the method</param>
		/// <returns>Combinations with at least one item (original parameters)</returns>
		public static IList<IList<TypeSig>> CreateGenericParameterCombinations(IList<TypeSig> parameters,
			IList<TypeSig> typeGenerics, IList<TypeSig> methodGenerics)
		{
			IList<IList<TypeSig>> list = new List<IList<TypeSig>>();
			list.Add(parameters);

			var paramCombos = parameters.Select(p => PossibleTypeSigs(p, typeGenerics, methodGenerics));
			var allCombos = AllCombinations<TypeSig>(paramCombos);

			return allCombos;
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
		public static IList<IList<TypeSig>> CreateGenericParameterCombinations_(IList<TypeSig> parameters,
			IList<TypeSig> typeGenerics, IList<TypeSig> methodGenerics)
		{
			IList<IList<TypeSig>> list = new List<IList<TypeSig>>();
			list.Add(parameters);

			for (UInt16 p = 0; p < parameters.Count; p++)
			{
				TypeSig paramtype = parameters[p];
				IList<TypeSig> ptypes = new TypeSig[] { paramtype };

				// Might be something like: DoSomething(IList<!0> someList)
				if (paramtype.IsGenericInstanceType)
					ptypes = PossibleTypeSigs(paramtype, typeGenerics, methodGenerics);

				for (UInt16 g = 0; g < typeGenerics.Count; g++)
				{
					var gtype = typeGenerics[g];

					foreach (var ptype in ptypes)
					{
						// Better comparison?
						//if (ptype.FullName.Equals(gtype.FullName))
						//{
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
						//}
					}
				}

				for (UInt16 g = 0; g < methodGenerics.Count; g++)
				{
					var gtype = methodGenerics[g];

					foreach (var ptype in ptypes)
					{
						//if (ptype.FullName.Equals(gtype.FullName))
						//{
							Int32 length = list.Count;
							for (Int32 i = 0; i < length; i++)
							{
								List<TypeSig> newParams = new List<TypeSig>();
								newParams.AddRange(list[i]);

								GenericMVar gmvar = new GenericMVar(g);
								newParams[p] = gmvar;

								list.Add(newParams);
							}
						//}
					}
				}
			}

			return list;
		}
	}
}
