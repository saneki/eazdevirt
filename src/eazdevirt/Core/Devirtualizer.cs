using System;
using System.Collections.Generic;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
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
		/// Devirtualize options flag.
		/// </summary>
		public DevirtualizeOptions Options { get; set; }

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
		{
			this.Parent = module;
			this.Options = options;
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
				var reader = new VirtualizedMethodBodyReader(method, this.Logger);
				Exception exception = null;

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

					attempt = new DevirtualizeAttempt(method, body);
				}
				else
					attempt = new DevirtualizeAttempt(method, exception);

				// Add attempt to list and fire callback
				attempts.Add(attempt);
				if (attemptCallback != null)
					attemptCallback(attempt);
			}

			return new DevirtualizeResults(attempts);
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
		/// <param name="exception">Exception that occurred while devirtualizing</param>
		public DevirtualizeAttempt(MethodStub vmethod, Exception exception)
		{
			this.VirtualizedMethod = vmethod;
			this.Exception = exception;
		}

		/// <summary>
		/// Constructs a successful devirtualize attempt.
		/// </summary>
		/// <param name="vmethod">Virtualized method</param>
		/// <param name="body">Devirtualized method body</param>
		public DevirtualizeAttempt(MethodStub vmethod, CilBody body)
		{
			this.VirtualizedMethod = vmethod;
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
		Nothing = 0
	}
}
