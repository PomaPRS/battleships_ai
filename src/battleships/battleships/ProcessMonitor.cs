using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using NLog;

namespace battleships
{
	public class ProcessMonitor
	{
		private static readonly Logger log = LogManager.GetCurrentClassLogger();
		private readonly object locker = new object();
		private readonly long memoryLimit;
		private readonly List<Process> processes = new List<Process>();
		private readonly TimeSpan timeLimit;

		public ProcessMonitor(TimeSpan timeLimit, long memoryLimit)
		{
			this.timeLimit = timeLimit;
			this.memoryLimit = memoryLimit;
			CreateMonitoringThread().Start();
			TotalProcessesTime = new TimeSpan(0);
		}

		public TimeSpan TotalProcessesTime { get; private set; }

		public bool Active
		{
			get
			{
				lock (processes)
				{
					return processes.Any();
				}
			}
		}

		private Thread CreateMonitoringThread()
		{
			return new Thread(() =>
			{
				while (true)
				{
					lock (processes)
					{
						foreach (var process in processes.ToList()) Inspect(process);
					}
					Thread.Sleep(500);
				}
				// ReSharper disable once FunctionNeverReturns
			})
			{
				IsBackground = true,
				Name = "Process monitoring"
			};
		}

		public void Register(Process process)
		{
			lock (locker)
			{
				processes.Add(process);
			}
		}

		private void Inspect(Process process)
		{
			if (process.HasExited)
			{
				TotalProcessesTime += process.ExitTime - process.StartTime;
				processes.Remove(process);
			}
			else
			{
				CheckParameter(process.TotalProcessorTime, timeLimit, process, "TimeLimit");
				CheckParameter(process.PeakWorkingSet64, memoryLimit, process, "MemoryLimit");
			}
		}

		private void CheckParameter<T>(T param, T limit, Process process, string message) where T : IComparable<T>
		{
			if (param.CompareTo(limit) <= 0) return;
			log.Error(message + " {0}: {1}", process.ProcessName, param);
			process.Kill();
			processes.Remove(process);
		}
	}
}