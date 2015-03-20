using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NLog;

namespace battleships
{
	public class MyLogger
	{
		private readonly string path;

		public MyLogger(string path)
		{
			this.path = path;
			if (!File.Exists(path))
				File.Create(path);
		}

		public void Add(params string[] args)
		{
			string s = args.Aggregate("", (s1, s2) => s1 + " " + s2);
			File.AppendAllLines(path, new[] {s});
		}
	}

	public class Ai
	{
		private static readonly MyLogger logger = new MyLogger("log.txt");
		private static readonly Logger log = LogManager.GetCurrentClassLogger();
		private Process process;
		private readonly string exePath;
		private readonly ProcessMonitor monitor;

		public Ai(string exePath, ProcessMonitor monitor)
		{
			this.exePath = exePath;
			this.monitor = monitor;
		}

		public string Name
		{
			get { return Path.GetFileNameWithoutExtension(exePath); }
		}

		public Vector Init(int width, int height, int[] shipSizes)
		{
			logger.Add(string.Format("Init {0} {1} {2}", width, height, string.Join(" ", shipSizes)));
			if (process == null || process.HasExited) process = RunProcess();
			(new MyLogger("log.txt")).Add(process.TotalProcessorTime.ToString());
			SendMessage("Init {0} {1} {2}", width, height, string.Join(" ", shipSizes));
			return ReceiveNextShot();
		}

		public Vector GetNextShot(Vector lastShotTarget, ShtEffct lastShot)
		{
			logger.Add(string.Format("{0} {1} {2}", lastShot, lastShotTarget.X, lastShotTarget.Y));
			SendMessage("{0} {1} {2}", lastShot, lastShotTarget.X, lastShotTarget.Y);
			return ReceiveNextShot();
		}

		private void SendMessage(string messageFormat, params object[] args)
		{
			var message = string.Format(messageFormat, args);
			process.StandardInput.WriteLine(message);
			log.Debug("SEND: " + message);
		}

		public void Dispose()
		{
			if (process == null || process.HasExited) return;
			log.Debug("CLOSE");
			process.StandardInput.Close();
			if (!process.WaitForExit(500))
				log.Info("Not terminated {0}", process.ProcessName);
			try
			{
				process.Kill();
			}
			catch
			{
				//nothing to do
			}
			process = null;
		}

		private Process RunProcess()
		{
			var startInfo = new ProcessStartInfo
			{
				FileName = exePath,
				UseShellExecute = false,
				RedirectStandardInput = true,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				CreateNoWindow = true,
				WindowStyle = ProcessWindowStyle.Hidden
			};
			var aiProcess = Process.Start(startInfo);
			monitor.Register(aiProcess);
			return aiProcess;
		}

		private Vector ReceiveNextShot()
		{
			string output;
			try
			{
				output = process.StandardOutput.ReadLine();
				log.Debug("RECEIVE " + output);
				if (output == null)
				{
					var err = process.StandardError.ReadToEnd();
					Console.WriteLine(err);
					log.Info(err);
					throw new Exception("No ai output");
				}
			}
			catch (Exception)
			{
				(new MyLogger("log.txt")).Add("ERROR");
				throw;
			}
			try
			{
				var parts = output.Split(' ').Select(int.Parse).ToList();
				(new MyLogger("log.txt")).Add(parts[0].ToString(), parts[1].ToString());
				return new Vector(parts[0], parts[1]);
			}
			catch (Exception e)
			{
				throw new Exception("Wrong ai output: " + output, e);
			}
		}
	}
}