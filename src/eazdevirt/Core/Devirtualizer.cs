using System;
using System.Collections.Generic;
using System.Linq;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using eazdevirt.Fixers;
using eazdevirt.IO;

namespace eazdevirt
{
	public class Devirtualizer
	{
		/// <summary>
		/// EazModule.
		/// </summary>
		public EazModule Parent { get; private set; }

		/// <summary>
		/// Module.
		/// </summary>
		public ModuleDefMD Module { get { return this.Parent.Module; } }

		/// <summary>
		/// Logger.
		/// </summary>
		public ILogger Logger { get; private set; }

		/// <summary>
		/// Attribute injector.
		/// </summary>
		public AttributeInjector Injector { get; private set; }

		/// <summary>
		/// Devirtualize options flag.
		/// </summary>
		public DevirtualizeOptions Options { get; set; }

		/// <summary>
		/// Fixers by type.
		/// </summary>
		public IList<Type> Fixers { get; private set; }

		public Devirtualizer(EazModule module)
			: this(module, DevirtualizeOptions.Nothing)
		{
		}

		public Devirtualizer(EazModule module, DevirtualizeOptions options)
			: this(module, options, null)
		{
		}

		public Devirtualizer(EazModule module, ILogger logger)
			: this(module, DevirtualizeOptions.Nothing, logger)
		{
		}

		public Devirtualizer(EazModule module, DevirtualizeOptions options, ILogger logger)
			: this(module, options, null, logger)
		{
		}

		public Devirtualizer(EazModule module, IList<Type> fixers, ILogger logger)
			: this(module, DevirtualizeOptions.Nothing, fixers, logger)
		{
		}

		public Devirtualizer(EazModule module, DevirtualizeOptions options, IList<Type> fixers, ILogger logger)
		{
			this.Parent = module;
			this.Options = options;
			this.Injector = new AttributeInjector(module);
			this.Fixers = (fixers != null ? fixers : new List<Type>());
			this.Logger = (logger != null ? logger : DummyLogger.NoThrowInstance);
		}

		public DevirtualizeResults Devirtualize()
		{
			return this.Devirtualize(this.Options, null);
		}

		public DevirtualizeResults Devirtualize(DevirtualizeOptions options)
		{
			return this.Devirtualize(options, null);
		}

		public DevirtualizeResults Devirtualize(Action<DevirtualizeAttempt> attemptCallback)
		{
			return this.Devirtualize(this.Options, attemptCallback);
		}

		public DevirtualizeResults Devirtualize(DevirtualizeOptions options, Action<DevirtualizeAttempt> attemptCallback)
		{
			var methods = this.Parent.FindMethodStubs();

			if (methods.Length == 0)
				return new DevirtualizeResults();

			var attempts = new List<DevirtualizeAttempt>();

			foreach (var method in methods)
			{
				var reader = new VirtualizedMethodBodyReader(method, this.Logger, this.Parent.Version);
				Exception exception = null, fixerException = null;

				try
				{
					reader.Read(); // Read method
				}
				catch (Exception e)
				{
					exception = e;
				}

				DevirtualizeAttempt attempt;

				if (exception == null)
				{
					var body = new CilBody(
						true,
						reader.Instructions,
						reader.ExceptionHandlers,
						reader.Locals
					);

					method.Method.FreeMethodBody();
					method.Method.Body = body;

					// Perform fixes
					try
					{
						PerformFixes(method.Method);
					}
					catch (Exception e)
					{
						fixerException = e;
					}

					if (fixerException == null)
					{
						// Inject DevirtualizedAttribute if specified
						if (options.HasFlag(DevirtualizeOptions.InjectAttributes))
							this.Injector.InjectDevirtualized(method.Method);

						attempt = new DevirtualizeAttempt(method, reader, body);
					}
					else
						attempt = new DevirtualizeAttempt(method, reader, fixerException);
				}
				else
					attempt = new DevirtualizeAttempt(method, reader, exception);

				// Add attempt to list and fire callback
				attempts.Add(attempt);
				if (attemptCallback != null)
					attemptCallback(attempt);
			}

			return new DevirtualizeResults(attempts);
		}

		void PerformFixes(MethodDef method)
		{
			var fixers = GetFixers(method);
			foreach (var fixer in fixers)
				fixer.Fix();
		}

		IList<IMethodFixer> GetFixers(MethodDef method)
		{
			return this.Fixers.Where(t => t.IsSubclassOf(typeof(MethodFixer)))
				.Select(t => (IMethodFixer)Activator.CreateInstance(t, method))
				.ToList();
		}
	}

	/// <summary>
	/// Describes a devirtualization attempt on a single virtualized method.
	/// </summary>
	public class DevirtualizeAttempt
	{
		/// <summary>
		/// The virtualized method associated with this attempt.
		/// </summary>
		public MethodStub VirtualizedMethod { get; private set; }

		public MethodDef Method { get { return this.VirtualizedMethod.Method; } }

		public Exception Exception { get; private set; }

		public Boolean Successful { get { return this.Exception == null; } }

		public CilBody MethodBody { get; private set; }

		public VirtualizedMethodBodyReader Reader { get; private set; }

		/// <summary>
		/// Whether or not the exception (if any) was due to an unknown instruction type.
		/// </summary>
		public Boolean WasInstructionUnknown
		{
			get
			{
				return this.Exception != null
					&& this.Exception is OriginalOpcodeUnknownException;
			}
		}

		/// <summary>
		/// Constructs a failed devirtualize attempt.
		/// </summary>
		/// <param name="vmethod">Virtualized method</param>
		/// <param name="reader">Method body reader</param>
		/// <param name="exception">Exception that occurred while devirtualizing</param>
		public DevirtualizeAttempt(MethodStub vmethod, VirtualizedMethodBodyReader reader, Exception exception)
		{
			this.VirtualizedMethod = vmethod;
			this.Reader = reader;
			this.Exception = exception;
		}

		/// <summary>
		/// Constructs a successful devirtualize attempt.
		/// </summary>
		/// <param name="vmethod">Virtualized method</param>
		/// <param name="reader">Method body reader</param>
		/// <param name="body">Devirtualized method body</param>
		public DevirtualizeAttempt(MethodStub vmethod, VirtualizedMethodBodyReader reader, CilBody body)
		{
			this.VirtualizedMethod = vmethod;
			this.Reader = reader;
			this.MethodBody = body;
		}
	}

	/// <summary>
	/// Describes the results of a devirtualization attempt.
	/// </summary>
	public class DevirtualizeResults
	{
		/// <summary>
		/// All attempts.
		/// </summary>
		public IList<DevirtualizeAttempt> Attempts { get; private set; }

		/// <summary>
		/// All virtualized methods.
		/// </summary>
		public IList<MethodStub> AllMethods { get; private set; }

		/// <summary>
		/// All virtualized methods which were successfully devirtualized.
		/// </summary>
		public IList<MethodStub> DevirtualizedMethods { get; private set; }

		/// <summary>
		/// Count of all methods.
		/// </summary>
		public Int32 MethodCount { get { return this.AllMethods.Count; } }

		/// <summary>
		/// Count of all successfully devirtualized methods.
		/// </summary>
		public Int32 DevirtualizedCount { get { return this.DevirtualizedMethods.Count; } }

		/// <summary>
		/// Whether or not the results are empty (no methods).
		/// </summary>
		public Boolean Empty { get { return this.MethodCount == 0; } }

		/// <summary>
		/// Construct empty results.
		/// </summary>
		public DevirtualizeResults()
			: this (new List<DevirtualizeAttempt>())
		{
		}

		public DevirtualizeResults(IList<DevirtualizeAttempt> attempts)
		{
			this.Attempts = attempts;
			this.Initialize();
		}

		private void Initialize()
		{
			this.AllMethods = new List<MethodStub>();
			this.DevirtualizedMethods = new List<MethodStub>();

			foreach (var attempt in this.Attempts)
			{
				this.AllMethods.Add(attempt.VirtualizedMethod);

				if (attempt.Successful)
				{
					this.DevirtualizedMethods.Add(attempt.VirtualizedMethod);
				}
			}
		}
	}

	/// <summary>
	/// Devirtualize options.
	/// </summary>
	[Flags]
	public enum DevirtualizeOptions
	{
		/// <summary>
		/// Nothing.
		/// </summary>
		Nothing = 0,

		/// <summary>
		/// Inject attributes into devirtualized methods.
		/// </summary>
		InjectAttributes = 1
	}
}
