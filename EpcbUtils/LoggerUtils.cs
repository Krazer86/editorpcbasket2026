using System;
using System.Diagnostics;
using System.IO;

namespace EpcbUtils
{
	public static class LoggerUtils
	{
		private static readonly object SyncRoot = new object();

		public static string LogFilePath { get; set; }

		public static void LogException(Exception ex)
		{
			WriteLine(ex == null ? "Unknown exception" : ex.ToString());
		}

		public static void LogString(string str)
		{
			WriteLine(str);
		}

		private static void WriteLine(string message)
		{
			lock (SyncRoot)
			{
				try
				{
					if (string.IsNullOrWhiteSpace(LogFilePath))
					{
						Trace.WriteLine(message);
						return;
					}

					using (var logger = File.AppendText(LogFilePath))
					{
						logger.WriteLine("[{0}] {1}", DateTime.Now, message);
					}
				}
				catch (Exception loggingException)
				{
					Trace.WriteLine(loggingException);
					Trace.WriteLine(message);
				}
			}
		}

		public static void CloseLogger()
		{
		}
	}
}
