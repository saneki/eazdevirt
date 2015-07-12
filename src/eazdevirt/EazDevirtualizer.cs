using System;
using System.Collections.Generic;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using eazdevirt.IO;

namespace eazdevirt
{
	public class EazDevirtualizer
	{
		/// <summary>
		/// EazModule.
		/// </summary>
		public EazModule EazModule { get; private set; }

		/// <summary>
		/// Module.
		/// </summary>
		public ModuleDefMD Module { get { return this.EazModule.Module; } }

		/// <summary>
		/// Devirtualize options flag.
		/// </summary>
		public EazDevirtualizeOptions Options { get; set; }

		public EazDevirtualizer(EazModule module)
			: this(module, EazDevirtualizeOptions.Nothing)
		{
		}

		public EazDevirtualizer(EazModule module, EazDevirtualizeOptions options)
		{
			this.EazModule = module;
			this.Options = options;
		}

		public EazDevirtualizeResults Devirtualize()
		{
			return this.Devirtualize(this.Options, null);
		}

		public EazDevirtualizeResults Devirtualize(EazDevirtualizeOptions options)
		{
			return this.Devirtualize(options, null);
		}

		public EazDevirtualizeResults Devirtualize(Action<EazDevirtualizeAttempt> attemptCallback)
		{
			return this.Devirtualize(this.Options, attemptCallback);
		}

		public EazDevirtualizeResults Devirtualize(EazDevirtualizeOptions options, Action<EazDevirtualizeAttempt> attemptCallback)
		{
			var methods = this.EazModule.FindVirtualizedMethods();

			if (methods.Length == 0)
				return new EazDevirtualizeResults();

			var attempts = new List<EazDevirtualizeAttempt>();

			foreach (var method in methods)
			{
				var reader = new EazVirtualizedMethodBodyReader(method);
				Exception exception = null;

				try
				{
					reader.Read(); // Read method
				}
				catch (Exception e)
				{
					exception = e;
				}

				EazDevirtualizeAttempt attempt;

				if (exception == null)
				{
					var body = new CilBody(
						true,
						reader.Instructions,
						new List<ExceptionHandler>(),
						reader.Locals
					);

					method.Method.FreeMethodBody();
					method.Method.Body = body;

					attempt = new EazDevirtualizeAttempt(method, body);
				}
				else
					attempt = new EazDevirtualizeAttempt(method, exception);

				// Add attempt to list and fire callback
				attempts.Add(attempt);
				if (attemptCallback != null)
					attemptCallback(attempt);
			}

			return new EazDevirtualizeResults(attempts);
		}
	}

	/// <summary>
	/// Describes a devirtualization attempt on a single virtualized method.
	/// </summary>
	public class EazDevirtualizeAttempt
	{
		/// <summary>
		/// The virtualized method associated with this attempt.
		/// </summary>
		public EazVirtualizedMethod VirtualizedMethod { get; private set; }

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
		public EazDevirtualizeAttempt(EazVirtualizedMethod vmethod, Exception exception)
		{
			this.VirtualizedMethod = vmethod;
			this.Exception = exception;
		}

		/// <summary>
		/// Constructs a successful devirtualize attempt.
		/// </summary>
		/// <param name="vmethod">Virtualized method</param>
		/// <param name="body">Devirtualized method body</param>
		public EazDevirtualizeAttempt(EazVirtualizedMethod vmethod, CilBody body)
		{
			this.VirtualizedMethod = vmethod;
			this.MethodBody = body;
		}
	}

	/// <summary>
	/// Describes the results of a devirtualization attempt.
	/// </summary>
	public class EazDevirtualizeResults
	{
		/// <summary>
		/// All attempts.
		/// </summary>
		public IList<EazDevirtualizeAttempt> Attempts { get; private set; }

		/// <summary>
		/// All virtualized methods.
		/// </summary>
		public IList<EazVirtualizedMethod> AllMethods { get; private set; }

		/// <summary>
		/// All virtualized methods which were successfully devirtualized.
		/// </summary>
		public IList<EazVirtualizedMethod> DevirtualizedMethods { get; private set; }

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
		public EazDevirtualizeResults()
			: this (new List<EazDevirtualizeAttempt>())
		{
		}

		public EazDevirtualizeResults(IList<EazDevirtualizeAttempt> attempts)
		{
			this.Attempts = attempts;
			this.Initialize();
		}

		private void Initialize()
		{
			this.AllMethods = new List<EazVirtualizedMethod>();
			this.DevirtualizedMethods = new List<EazVirtualizedMethod>();

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
	public enum EazDevirtualizeOptions
	{
		/// <summary>
		/// Nothing.
		/// </summary>
		Nothing = 0
	}
}
