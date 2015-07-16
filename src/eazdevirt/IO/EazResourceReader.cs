using System;
using System.IO;
using dnlib.DotNet;

namespace eazdevirt.IO
{
	public class EazResourceReader
	{
		/// <summary>
		/// Parent module.
		/// </summary>
		public EazModule Parent { get; private set; }

		/// <summary>
		/// Module.
		/// </summary>
		public ModuleDefMD Module { get { return this.Parent.Module; } }

		/// <summary>
		/// Embedded resource stream.
		/// </summary>
		public CryptoStream Stream { get; private set; }

		/// <summary>
		/// Embedded Resource reader.
		/// </summary>
		public BinaryReader Reader { get; private set; }

		public EazResourceReader(EazModule module)
		{
			if (module == null)
				throw new ArgumentNullException();

			this.Parent = module;
			this.Initialize();
		}

		private void Initialize()
		{
			this.Stream = (CryptoStream)this.Parent.GetResourceStream();
			this.Reader = new BinaryReader(this.Stream);
		}
	}
}
