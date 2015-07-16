using System;
using dnlib.DotNet;

namespace eazdevirt
{
	/// <summary>
	/// Represents a method stub of a method virtualized by Eazfuscator.NET.
	/// </summary>
	public class MethodStub
	{
		/// <summary>
		/// Parent.
		/// </summary>
		public EazModule Parent { get; private set; }

		/// <summary>
		/// Module.
		/// </summary>
		public ModuleDefMD Module { get { return this.Parent.Module; } }

		/// <summary>
		/// Underlying method.
		/// </summary>
		public MethodDef Method { get; private set; }

		/// <summary>
		/// The method that creates the raw resource stream. The method body
		/// contains a `ldstr` instruction with the resource name.
		/// </summary>
		public MethodDef CreateStreamMethod { get; private set; }

		/// <summary>
		/// The method that does the work.
		/// </summary>
		public MethodDef VirtualCallMethod { get; private set; }

		/// <summary>
		/// Instruction index at which the VirtualCallMethod was found.
		/// </summary>
		public Int32 VirtualCallIndex { get; private set; }

		/// <summary>
		/// The string used to specify the file position to read at.
		/// </summary>
		public String PositionString { get; private set; }

		public Int64 Position { get; private set; }

		/// <summary>
		/// String identifier of the embedded resource which contains encrypted
		/// virtualized method info.
		/// </summary>
		public String ResourceStringId { get; private set; }

		/// <summary>
		/// Crypto key integer used to decrypt the embedded resource.
		/// </summary>
		public Int32 ResourceCryptoKey { get; private set; }

		/// <summary>
		/// Construct a MethodStub from an existing method.
		/// </summary>
		/// <param name="module">Parent module</param>
		/// <param name="method">Stub method</param>
		public MethodStub(EazModule module, MethodDef method)
		{
			this.Parent = module;
			this.Method = method;
			this.Initialize();
		}

		/// <summary>
		/// Try to find all helpful information about the method stub and
		/// virtualized method.
		/// </summary>
		private void Initialize()
		{
			if (!this.Method.HasBody || !this.Method.Body.HasInstructions)
				return;

			var instrs = this.Method.Body.Instructions;
			this.VirtualCallIndex = -1;

			// Get info on virtual call method
			for(int i = 0; i < instrs.Count; i++)
			{
				var instr = instrs[i];

				MethodDef method = null;
				if(instr.OpCode.Code == dnlib.DotNet.Emit.Code.Call
				&& (method = Helpers.TryTransformCallOperand(instr.Operand)) != null
				&& IsVirtualCallMethod(method))
				{
					this.VirtualCallMethod = method;
					this.VirtualCallIndex = i;
					break;
				}
			}

			if (this.VirtualCallMethod == null)
				throw new Exception("Couldn't get VirtualCallMethod");

			// Get the position string
			int expectedLdstrIndex = (this.VirtualCallIndex - 2);
			if (expectedLdstrIndex >= 0
			&& instrs[expectedLdstrIndex].OpCode.Code == dnlib.DotNet.Emit.Code.Ldstr)
			{
				this.PositionString = (String)instrs[expectedLdstrIndex].Operand;
			}
			else throw new Exception("Couldn't get PositionString");

			// Get the create stream method
			for (int i = (this.VirtualCallIndex - 3); i >= 0; i--)
			{
				var instr = instrs[i];
				MethodDef method = null;

				if (instr.OpCode.Code == dnlib.DotNet.Emit.Code.Call
				&& (method = Helpers.TryTransformCallOperand(instr.Operand)) != null
				&& method.ReturnType.FullName.Equals("System.IO.Stream"))
				{
					this.CreateStreamMethod = method;
				}
			}

			if (this.CreateStreamMethod == null)
				throw new Exception("Couldn't get CreateStreamMethod");

			// Get the resource string id
			this.ResourceStringId = FindResourceStringId(this.CreateStreamMethod);

			if (this.ResourceStringId == null)
				throw new Exception("Couldn't get ResourceStringId");

			// Get the crypto key
			this.ResourceCryptoKey = FindResourceCryptoKey(this.VirtualCallMethod);

			// Set position from position string + crypto key
			this.Position = eazdevirt.Position.FromString(this.PositionString, this.ResourceCryptoKey);
		}

		/// <summary>
		/// Find the crypto key for the resource associated with the given method
		/// used in virtualized methods.
		/// </summary>
		/// <param name="method">
		/// Method used in virtualized methods, following the pattern: (Stream, String, Object[]): Object
		/// </param>
		/// <returns>Crypto key</returns>
		public static Int32 FindResourceCryptoKey(MethodDef method)
		{
			MethodDef origMethod = method;

			for (int i = 0; i < 5 && method != null; i++)
			{
				if (method.ReturnType.FullName.Equals("System.Int32"))
					break;

				method = Helpers.GetFirstCalledMethod(method);
			}

			if (method == null)
				throw new Exception("Rabbit-Hole strategy of finding the resource crypto key failed");

			var instructions = method.Body.Instructions;
			var count = method.Body.Instructions.Count;

			if (!method.HasBody || !method.Body.HasInstructions || count < 2
			|| !method.ReturnType.FullName.Equals("System.Int32"))
				throw new Exception(String.Format(
					"Found method, but seems insufficient (token={0:X8})", method.MDToken.Raw
				));

			if (instructions[count - 2].OpCode.Code == dnlib.DotNet.Emit.Code.Ldc_I4
			&& instructions[count - 1].OpCode.Code == dnlib.DotNet.Emit.Code.Ret)
			{
				return (Int32)instructions[count - 2].Operand;
			}

			throw new Exception(String.Format(
				"Found bad method? (token={0:X8})", origMethod.MDToken.Raw
			));
		}

		/// <summary>
		/// Find the resource string Id, given the method that contains it.
		/// </summary>
		/// <param name="method">Method</param>
		/// <returns>Resource string Id if successful, or null if not successful</returns>
		/// <remarks>This extraction is very simple and just gets the operand of the first `ldstr`</remarks>
		public static String FindResourceStringId(MethodDef method)
		{
			if (method == null)
				throw new ArgumentNullException();

			if (!method.HasBody || !method.Body.HasInstructions)
				return null;

			foreach(var instr in method.Body.Instructions)
			{
				if (instr.OpCode.Code == dnlib.DotNet.Emit.Code.Ldstr)
					return (String)instr.Operand;
			}

			return null;
		}

		/// <summary>
		/// Whether or not a method appears to be the stream creation method.
		/// </summary>
		/// <param name="method">Method to check</param>
		/// <returns>true if stream creation method, false if not</returns>
		public static Boolean IsCreateStreamMethod(MethodDef method)
		{
			if (method == null)
				throw new ArgumentNullException();

			return method.Parameters.Count == 0
				&& method.ReturnType.FullName.Equals("System.IO.Stream");
		}

		/// <summary>
		/// Whether or not a method appears to be the virtual call method.
		/// </summary>
		/// <param name="method">Method to check</param>
		/// <returns>true if virtual call method, false if not</returns>
		public static Boolean IsVirtualCallMethod(MethodDef method)
		{
			if (method == null)
				throw new ArgumentNullException();

			ParameterList p = method.Parameters;

			TypeSig[] types = null;
			if (p.Count == 3)
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
				return true;
			}

			return false;
		}
	}
}
