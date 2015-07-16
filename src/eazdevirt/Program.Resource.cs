using System;
using System.IO;

namespace eazdevirt
{
	public partial class Program
	{
		/// <summary>
		/// Perform "resource" verb.
		/// </summary>
		/// <param name="options">Options</param>
		static void DoResource(ResourceSubOptions options)
		{
			EazModule module;
			if (!TryLoadModule(options.AssemblyPath, out module))
				return;

			// If no action set, set the default action (extract)
			if (!options.Extract)
				options.Extract = true;

			MethodStub method = module.FindFirstVirtualizedMethod();
			if (method != null)
			{
				if (options.Extract)
				{
					String outputPath = options.OutputPath;
					if (outputPath == null || outputPath.Equals(""))
						outputPath = method.ResourceStringId;

					FileMode fileMode = FileMode.CreateNew;
					if (options.OverwriteExisting)
						fileMode = FileMode.Create;

					using (Stream resourceStream = module.GetResourceStream(options.KeepEncrypted))
					{
						try
						{
							using (FileStream fileStream = new FileStream(outputPath, fileMode, FileAccess.Write))
							{
								resourceStream.CopyTo(fileStream);
							}
						}
						catch (IOException e)
						{
							Console.Write(e);
						}
					}

					Console.WriteLine("Extracted {0} resource to {1}",
						options.KeepEncrypted ? "encrypted" : "decrypted", outputPath);
				}
			}
		}
	}
}
