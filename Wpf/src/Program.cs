using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Google.Protobuf;
using SharpFuzz;
using SharpFuzz.Common;

namespace Wpf.Fuzz
{
	public static class Program
	{
		private const int MapSize = 65_536;
		private const int Port = 7362;

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
			using (var client = new TcpClient())
			{
				var address = IPAddress.Parse("10.211.55.3");
				client.Connect(address, Port);

				using (var stream = client.GetStream())
				{
					FuzzLocal(stream);
				}
			}
		}

		private static unsafe void FuzzLocal(NetworkStream stream)
		{
			Fuzzer.LibFuzzer.Run(span =>
			{
				Layout.FrameworkElement proto;

				try
				{
					proto = Layout.FrameworkElement.Parser.ParseFrom(span.ToArray());
				}
				catch
				{
					return;
				}

				proto.WriteTo(stream);

				var response = Response.Parser.ParseFrom(stream);
				var sharedMem = new Span<byte>(Trace.SharedMem, MapSize);

				response.Trace.Span.CopyTo(sharedMem);
			});
		}

		private static void Remote()
		{
			var listener = new TcpListener(IPAddress.Any, Port);
			listener.Start();

			try
			{
				using (var client = listener.AcceptTcpClient())
				using (var stream = client.GetStream())
				{
					FuzzRemote(stream);
				}
			}
			finally
			{
				listener.Stop();
			}
		}

		private static unsafe void FuzzRemote(NetworkStream stream)
		{
			var sharedMem = new byte[MapSize];

			fixed (byte* ptr = sharedMem)
			{
				Trace.SharedMem = ptr;

				for (; ; )
				{
					sharedMem.AsSpan().Clear();

					var proto = Layout.FrameworkElement.Parser.ParseFrom(stream);
					var element = ProtoToElement(proto);

					var thread = new Thread(() =>
					{
						var dispatcher = Dispatcher.CurrentDispatcher;
						var context = new DispatcherSynchronizationContext(dispatcher);

						SynchronizationContext.SetSynchronizationContext(context);

						var window = new Window
						{
							Content = element,
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

					var response = new Response
					{
						Trace = ByteString.CopyFrom(sharedMem)
					};

					response.WriteTo(stream);
				}
			}
		}

		private static FrameworkElement ProtoToElement(Layout.FrameworkElement element)
		{
			switch (element.FrameworkelementOneofCase)
			{
				case Layout.FrameworkElement.FrameworkelementOneofOneofCase.TextBlock:
					return new TextBlock { Text = element.TextBlock.Text };
			}

			return null;
		}
	}
}
