using System;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using SharpFuzz.Common;

namespace Wpf.Fuzz
{
	public static class Program
	{
		[STAThread]
		public static unsafe void Main()
		{
			var trace = new byte[65536];
			var sha = SHA256.Create();

			fixed (byte* ptr = trace)
			{
				Trace.SharedMem = ptr;

				var window = new Window { Title = "Fuzzing WPF" };
				var application = new Application();
				var dispacher = application.Dispatcher;

				window.Loaded += (sender, args) =>
				{
					Task.Run(() =>
					{
						for (int i = 1; i < 100; ++i)
						{
							Array.Clear(trace, 0, trace.Length);

							dispacher.Invoke(() =>
							{
								window.Content = new TextBlock { Text = $"Iteration {i}" };
							});

							var hash = sha.ComputeHash(trace);
							var hex = BitConverter.ToString(hash).Replace("-", String.Empty);

							Console.WriteLine(hex);
						}
					});
				};

				application.Run(window);
			}
		}
	}
}
