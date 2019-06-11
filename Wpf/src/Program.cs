using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
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

				for (int i = 1; i < 30; ++i)
				{
					var trace = new List<(int, string)>();

					SharpFuzz.Common.Trace.OnBranch = (id, name) =>
					{
						lock (sharedMem)
						{
							trace.Add((id, name));
						}
					};

					sharedMem.AsSpan().Clear();
					trace.Clear();

					var thread = new Thread(() =>
					{
						var dispatcher = Dispatcher.CurrentDispatcher;
						var context = new DispatcherSynchronizationContext(dispatcher);

						SynchronizationContext.SetSynchronizationContext(context);

						Window window = new Window { Content = new TextBlock { Text = $"Iteration {i:00}" } };
						window.Loaded += (sender, args) => dispatcher.BeginInvokeShutdown(DispatcherPriority.ApplicationIdle);

						window.Show();
						Dispatcher.Run();
					});

					thread.SetApartmentState(ApartmentState.STA);
					thread.IsBackground = true;

					thread.Start();
					thread.Join();

					List<(int, string)> copy;

					lock (sharedMem)
					{
						copy = new List<(int, string)>(trace);
						trace.Clear();
					}

					using (var log = File.CreateText($"{i:00}.txt"))
					{
						foreach (var (id, name) in copy)
						{
							log.WriteLine($"{id}: {name}");
						}
					}
				}
			}
		}
	}
}
