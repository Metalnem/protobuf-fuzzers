using System;
using System.Security.Cryptography;
using System.Threading;
using System.Windows;
using SharpFuzz.Common;

namespace Wpf.Fuzz
{
	public static class Program
	{
		[STAThread]
		public static unsafe void Main(string[] args)
		{
			var trace = new byte[65536];
			var sha = SHA256.Create();

			fixed (byte* ptr = trace)
			{
				Trace.SharedMem = ptr;

				for (int i = 0; i < 100; ++i)
				{
					Array.Clear(trace, 0, trace.Length);

					var thread = new Thread(() =>
					{
						var window = new Window();
						var application = new Application();

						window.Loaded += (x, y) => window.Close();
						window.Title = "Fuzzing WPF";

						application.Run(window);
					});

					thread.Join();

					var hash = sha.ComputeHash(trace);
					var hex = BitConverter.ToString(hash).Replace("-", String.Empty);

					Console.WriteLine(hex);
				}
			}
		}
	}
}
