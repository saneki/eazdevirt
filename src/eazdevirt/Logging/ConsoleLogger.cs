using System;
using dnlib.DotNet;

namespace eazdevirt.Logging
{
	public class ConsoleLogger : ILogger
	{
		public LoggerEvent Level { get; private set; }

		public ConsoleLogger()
			: this(LoggerEvent.Info)
		{
		}

		public ConsoleLogger(LoggerEvent level)
		{
			this.Level = level;
		}

		public bool IgnoresEvent(LoggerEvent loggerEvent)
		{
			return loggerEvent > this.Level;
		}

		public void Log(object sender, LoggerEvent loggerEvent, string format, params object[] args)
		{
			if (!this.IgnoresEvent(loggerEvent))
			{
				if (sender != null)
					Console.WriteLine(String.Format(
						"[{0}] {1}", sender.GetType().FullName, String.Format(format, args)
					));
				else
					Console.WriteLine(String.Format(format, args));
			}
		}
	}
}
