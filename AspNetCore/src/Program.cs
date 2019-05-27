using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Http;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Logging;
using SharpFuzz;

namespace AspNetCore.Fuzz
{
	public class Program
	{
		private static readonly byte[] request = Encoding.UTF8.GetBytes("GET / HTTP/1.1\r\nHost: localhost\r\n\r\n");
		private static readonly byte[] responseEnd = new byte[] { 48, 13, 10, 13, 10 };

		private static readonly byte[] clientBuffer = new byte[10_000_000];
		private static readonly byte[] serverBuffer = new byte[10_000_000];

		public static unsafe void Main(string[] args)
		{
			fixed (byte* trace = new byte[65_536])
			{
				SharpFuzz.Common.Trace.SharedMem = trace;
				SharpFuzz.Common.Trace.OnBranch = (id, name) => { };

				WebHost.CreateDefaultBuilder(args)
					.UseKestrel(options =>
					{
						options.Limits.MinRequestBodyDataRate = null;
						options.Limits.MinResponseDataRate = null;

						typeof(KestrelServerOptions).Assembly
							.GetType("Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Infrastructure.Heartbeat")
							.GetField("Interval", BindingFlags.Public | BindingFlags.Static)
							.SetValue(null, TimeSpan.FromDays(10));
					})
					.UseStartup<Startup>()
					.ConfigureLogging(logging => logging.ClearProviders())
					.Build()
					.Run();
			}
		}

		private static void Fuzz()
		{
			var trace = new List<(int, string)>();

			SharpFuzz.Common.Trace.OnBranch = (id, name) =>
			{
				lock (trace)
				{
					trace.Add((id, name));
				}
			};

			using (var client = new TcpClient("localhost", 80))
			using (var network = client.GetStream())
			{
				Fuzzer.LibFuzzer.Run(span =>
				{
					Request message;

					try
					{
						message = Request.Parser.ParseFrom(span.ToArray());
					}
					catch
					{
						return;
					}

					network.Write(request, 0, request.Length);
					int read = 0;

					for (; ; )
					{
						read += network.Read(clientBuffer, read, clientBuffer.Length - read);

						if (clientBuffer.AsSpan(0, read).EndsWith(responseEnd))
						{
							break;
						}
					}

					List<(int, string)> copy;

					lock (trace)
					{
						copy = new List<(int, string)>(trace);
						trace.Clear();
					}
				});
			}
		}

		private class Startup
		{
			public void Configure(IApplicationBuilder app, IApplicationLifetime lifetime)
			{
				app.Run(async context =>
				{
					await context.Request.Body.ReadAsync(serverBuffer);
					await context.Response.WriteAsync(String.Empty);
				});

				lifetime.ApplicationStarted.Register(() => Task.Run((Action)Fuzz));
			}
		}
	}
}
