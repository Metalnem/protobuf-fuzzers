using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using SharpFuzz.Common;

namespace Wpf.Fuzz
{
	public static class Program
	{
		public static unsafe void Main()
		{
			var sharedMem = new byte[65536];

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

						window.Loaded += (sender, args) =>
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
	}
}
