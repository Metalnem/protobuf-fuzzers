using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using SharpFuzz;
using SharpFuzz.Common;

namespace Wpf.Fuzz
{
	public static class Program
	{
		private const int MapSize = 65_536;

		public static void Main(string[] args)
		{
			switch (args[0])
			{
				case "--local": Local(); break;
				case "--remote": Remote(); break;
				default: throw new ArgumentException("Unknown command line flag.");
			}
		}

		private static void Local()
		{
			Fuzzer.LibFuzzer.Run(span =>
			{
				Layout.FrameworkElement element;

				try
				{
					element = Layout.FrameworkElement.Parser.ParseFrom(span.ToArray());
				}
				catch
				{
					return;
				}
			});
		}

		private static unsafe void Remote()
		{
			var sharedMem = new byte[MapSize];

			fixed (byte* ptr = sharedMem)
			{
				Trace.SharedMem = ptr;

				for (int i = 1; i <= 30; ++i)
				{
					sharedMem.AsSpan().Clear();

					var thread = new Thread(() =>
					{
						var dispatcher = Dispatcher.CurrentDispatcher;
						var context = new DispatcherSynchronizationContext(dispatcher);

						SynchronizationContext.SetSynchronizationContext(context);

						var window = new Window
						{
							Content = new TextBlock { Text = $"Iteration {i:00}" },
							IsHitTestVisible = false,
							ShowInTaskbar = false,
							WindowStyle = WindowStyle.None,
							WindowStartupLocation = WindowStartupLocation.CenterScreen
						};

						window.Loaded += (sender, _) =>
						{
							dispatcher.BeginInvokeShutdown(DispatcherPriority.ApplicationIdle);
						};

						window.Show();
						Dispatcher.Run();
					});

					thread.SetApartmentState(ApartmentState.STA);
					thread.IsBackground = true;

					thread.Start();
					thread.Join();
				}
			}
		}

		private static FrameworkElement ProtoToElement(Layout.FrameworkElement element)
		{
			throw new NotImplementedException();
		}
	}
}
