using System;
using System.Collections.Generic;
using System.Linq;
using dnlib.DotNet;
using System.IO;
using dnlib.DotNet.Writer;

namespace eazdevirt
{
	/// <summary>
	/// Module that contains methods virtualized by Eazfuscator.NET.
	/// </summary>
	public class EazModule
	{
		public ModuleDefMD Module { get; private set; }

		public EazVirtualization Virtualization { get; private set; }

		public IList<EazVirtualInstruction> VirtualInstructions { get; private set; }

		/// <summary>
		/// Dictionary containing all identified instruction types (opcodes).
		/// Maps virtual opcode (int) to virtual instruction containing the actual opcode.
		/// </summary>
		public Dictionary<Int32, EazVirtualInstruction> IdentifiedOpCodes;

		/// <summary>
		/// Embedded resource string identifier.
		/// </summary>
		public String ResourceStringId { get; private set; }

		/// <summary>
		/// Embedded resource crypto key.
		/// </summary>
		public Int32 ResourceCryptoKey { get; private set; }

		/// <summary>
		/// Construct an EazModule from a filepath.
		/// </summary>
		/// <param name="filepath">Filepath of assembly</param>
		public EazModule(String filepath)
			: this(ModuleDefMD.Load(filepath))
		{
		}

		/// <summary>
		/// Construct an EazModule from a loaded ModuleDefMD.
		/// </summary>
		/// <param name="module">Loaded module</param>
		public EazModule(ModuleDefMD module)
		{
			this.Module = module;
			this.Initialize();
		}

		private void Initialize()
		{
			this.Virtualization = new EazVirtualization(this);
			this.InitializeIdentifiedOpCodes();
		}

		public void Write(String filepath)
		{
			var options = new ModuleWriterOptions(this.Module);
			options.MetaDataOptions.Flags |= MetaDataFlags.PreserveAll;
			options.MetaDataOptions.Flags |= MetaDataFlags.KeepOldMaxStack;
			// options.Logger = DummyLogger.NoThrowInstance;
			this.Module.Write(filepath, options);
		}

		/// <summary>
		/// Get the resource with virtualized method data as a Stream.
		/// </summary>
		/// <param name="cryptoStream">
		/// Whether or not to return a raw stream that doesn't automatically handle crypto
		/// </param>
		/// <returns>Stream</returns>
		public Stream GetResourceStream(Boolean rawStream = false)
		{
			if (this.ResourceStringId == null)
			{
				var vmethod = this.FindFirstVirtualizedMethod();
				if (vmethod != null)
				{
					this.ResourceStringId = vmethod.ResourceStringId;
					this.ResourceCryptoKey = vmethod.ResourceCryptoKey;
				}
				else
					throw new Exception("Unable to find any virtualized methods");
			}

			var resource = this.Module.Resources.FindEmbeddedResource(this.ResourceStringId);
			if (resource == null)
				throw new Exception("Unable to find resource");

			if (!rawStream)
				return new EazCryptoStream(resource.GetResourceStream(), this.ResourceCryptoKey);
			else
				return resource.GetResourceStream();
		}

		/// <summary>
		/// Look for virtualized methods and return the first found. Useful because
		/// all virtualized methods seem to use the same manifest resource and crypto
		/// key.
		/// </summary>
		/// <returns>First virtualized method if found, null if none found</returns>
		public EazVirtualizedMethod FindFirstVirtualizedMethod()
		{
			var types = this.Module.GetTypes();
			foreach (var type in types)
			{
				EazVirtualizedMethod[] methods = this.FindVirtualizedMethods(type);
				if (methods.Length > 0)
					return methods[0];
			}

			return null;
		}

		/// <summary>
		/// Look for virtualized methods throughout the module.
		/// </summary>
		/// <returns>Found virtualized methods</returns>
		public EazVirtualizedMethod[] FindVirtualizedMethods()
		{
			List<EazVirtualizedMethod> list = new List<EazVirtualizedMethod>();

			var types = this.Module.GetTypes();
			foreach(var type in types)
			{
				EazVirtualizedMethod[] methods = this.FindVirtualizedMethods(type);
				list.AddRange(methods);
			}

			return list.ToArray();
		}

		/// <summary>
		/// Look for virtualized methods of a specific type.
		/// </summary>
		/// <param name="type">Type to look in</param>
		/// <returns>Found virtualized methods</returns>
		public EazVirtualizedMethod[] FindVirtualizedMethods(TypeDef type)
		{
			List<EazVirtualizedMethod> list = new List<EazVirtualizedMethod>();

			var methods = type.Methods;
			foreach (var method in methods)
			{
				if (this.IsVirtualizedMethod(method))
					list.Add(new EazVirtualizedMethod(this, method));
			}

			return list.ToArray();
		}

		/// <summary>
		/// Makes an estimated guess as to whether or not the given method
		/// is a virtualized method.
		/// </summary>
		/// <param name="method">Method to inspect</param>
		/// <returns>true if virtualized, false if not</returns>
		/// <remarks>
		/// Performs two checks:
		/// First, it checks for a `ldstr` instruction that loads a length-10 string.
		/// Second, it checks for a call to a method: (Stream, String, Object[]): ???
		/// </remarks>
		public Boolean IsVirtualizedMethod(MethodDef method)
		{
			if (method == null || !method.HasBody || !method.Body.HasInstructions)
				return false;

			Boolean hasMethodCall = false, hasLdstr = false;

			var instrs = method.Body.Instructions;
			foreach(var instr in instrs)
			{
				if(instr.OpCode.Code == dnlib.DotNet.Emit.Code.Ldstr)
				{
					String operand = (String)instr.Operand;
					if (operand != null && operand.Length == 10)
						hasLdstr = true;
				}

				if (instr.OpCode.Code == dnlib.DotNet.Emit.Code.Call)
				{
					MethodDef calledMethod;
					if (instr.Operand is MethodDef && (calledMethod = ((MethodDef)instr.Operand)) != null)
					{
						ParameterList p = calledMethod.Parameters;

						TypeSig[] types = null;
						if(p.Count == 3)
						{
							types = new TypeSig[] { p[0].Type, p[1].Type, p[2].Type };
						}
						else if (p.Count == 4)
						{
							types = new TypeSig[] { p[1].Type, p[2].Type, p[3].Type };
						}

						if (types != null
						&& types[0].FullName.Equals("System.IO.Stream")
						&& types[1].FullName.Equals("System.String")
						&& types[2].FullName.Equals("System.Object[]"))
						{
							hasMethodCall = true;
							break;
						}
					}
				}
			}

			return hasLdstr && hasMethodCall;
		}

		/// <summary>
		/// Find all virtual instructions and attempt to identify them.
		/// </summary>
		private void InitializeIdentifiedOpCodes()
		{
			this.IdentifiedOpCodes = new Dictionary<Int32, EazVirtualInstruction>();

			this.VirtualInstructions = EazVirtualInstruction.FindAllInstructions(this, this.Virtualization.VirtualizationType);
			var identified = this.VirtualInstructions.Where((instruction) => { return instruction.IsIdentified; });

			Boolean warningOccurred = false;

			foreach (var instruction in identified)
			{
				Boolean containsVirtual = this.IdentifiedOpCodes.ContainsKey(instruction.VirtualOpCode);

				EazVirtualInstruction existing = this.IdentifiedOpCodes.Where((kvp, index) => {
					return kvp.Value.OpCode == instruction.OpCode;
				}).FirstOrDefault().Value;
				Boolean containsActual = (existing != null);

				if (containsVirtual)
					Console.WriteLine("WARNING: Multiple instruction types with the same virtual opcode detected ({0})",
						instruction.VirtualOpCode);

				if (containsActual)
				{
					Console.WriteLine("WARNING: Multiple virtual opcodes map to the same actual opcode ({0}, {1} => {2})",
						existing.VirtualOpCode, instruction.VirtualOpCode, instruction.OpCode.ToString());
				}

				if (!warningOccurred)
					warningOccurred = (containsVirtual || containsActual);

				this.IdentifiedOpCodes.Add(instruction.VirtualOpCode, instruction);
			}

			if (warningOccurred)
				Console.WriteLine();
		}

		/// <summary>
		/// Write params to Console for debugging purposes.
		/// </summary>
		/// <param name="method">Method</param>
		public static void WriteMethodDefParams(MethodDef method)
		{
			ParameterList p = method.Parameters;

			Console.Write("(");
			foreach (var param in p) Console.Write(param.Type.FullName + " ");
			Console.WriteLine(")");
		}
	}
}
