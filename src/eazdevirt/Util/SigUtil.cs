using System;
using System.Collections.Generic;
using dnlib.DotNet;

namespace eazdevirt.Util
{
	public static class SigUtil
	{
		/// <summary>
		/// Apply a series of modifiers ("[]", "*", "&") to a base TypeSig.
		/// </summary>
		/// <param name="baseSig">Base TypeSig</param>
		/// <param name="modifiers">Modifier strings</param>
		/// <returns>TypeSig</returns>
		public static TypeSig FromBaseSig(TypeSig baseSig, Stack<String> modifiers)
		{
			String mod;
			while (modifiers.Count > 0)
			{
				mod = modifiers.Pop();
				switch (mod)
				{
					case "[]": baseSig = new SZArraySig(baseSig); break;
					case "*": baseSig = new PtrSig(baseSig); break;
					case "&": baseSig = new ByRefSig(baseSig); break;
					default:
						throw new Exception(String.Format("Unknown modifier: {0}", mod));
				}
			}
			return baseSig;
		}

		/// <summary>
		/// Get the base TypeSig.
		/// </summary>
		/// <param name="typeSig">TypeSig</param>
		/// <param name="modifiers">Modifiers to set</param>
		/// <returns>Base TypeSig</returns>
		public static TypeSig ToBaseSig(TypeSig typeSig, out Stack<String> modifiers)
		{
			modifiers = new Stack<String>();

			// While a non-leaf sig
			while (typeSig.Next != null)
			{
				if (typeSig.IsSZArray)
				{
					modifiers.Push("[]");
					typeSig = typeSig.Next;
				}
				else if (typeSig.IsPointer)
				{
					modifiers.Push("*");
					typeSig = typeSig.Next;
				}
				else if (typeSig.IsByRef)
				{
					modifiers.Push("&");
					typeSig = typeSig.Next;
				}
				//else if (typeSig.IsArray)
				//{
				//}
				else
					return null;
			}

			return typeSig;
		}
	}
}
