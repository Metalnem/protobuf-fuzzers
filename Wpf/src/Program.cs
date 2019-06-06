using System;
using System.Collections.Generic;
using System.IO;
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
			var sharedMem = new byte[65536];

			fixed (byte* ptr = sharedMem)
			{
				Trace.SharedMem = ptr;
				Trace.OnBranch = (id, name) => { };

				var window = new Window { Title = "Fuzzing WPF" };
				var application = new Application();
				var dispacher = application.Dispatcher;

				window.Loaded += (sender, args) =>
				{
					Task.Run(() =>
					{
						var trace = new List<(int, string)>();

						SharpFuzz.Common.Trace.OnBranch = (id, name) =>
						{
							lock (sharedMem)
							{
								trace.Add((id, name));
							}
						};

						for (int i = 1; i < 100; ++i)
						{
							sharedMem.AsSpan().Clear();
							trace.Clear();

							dispacher.Invoke(() =>
							{
								window.Content = new TextBlock { Text = $"Iteration {i}" };
							});

							List<(int, string)> copy;

							lock (sharedMem)
							{
								copy = new List<(int, string)>(trace);
								trace.Clear();
							}

							using (var log = File.CreateText($"{i}.txt"))
							{
								foreach (var (id, name) in copy)
								{
									log.WriteLine($"{id}: {name}");
								}
							}
						}
					});
				};

				application.Run(window);
			}
		}
	}
}
