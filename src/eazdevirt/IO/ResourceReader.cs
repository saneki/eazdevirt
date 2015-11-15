using System;
using System.IO;
using dnlib.DotNet;

namespace eazdevirt.IO
{
	public abstract class ResourceReader
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
		public CryptoStreamBase Stream { get; private set; }

		/// <summary>
		/// Embedded Resource reader.
		/// </summary>
		public BinaryReader Reader { get; private set; }

		public ResourceReader(EazModule module)
		{
			if (module == null)
				throw new ArgumentNullException();

			this.Parent = module;
			this.Initialize();
		}

		private void Initialize()
		{
			this.Stream = (CryptoStreamBase)this.Parent.GetResourceStream();
			this.Reader = new BinaryReader(this.Stream);
		}
	}
}
