using System;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using SharpFuzz;
using Microsoft.AspNetCore.Hosting;
using System.Text;
using System.Collections.Generic;
using System.Threading;

namespace AspNetCore.Fuzz
{
	public class Program
	{
		private static readonly byte[] request = Encoding.UTF8.GetBytes("GET / HTTP/1.1\r\nHost: localhost\r\n\r\n");
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
						options.Limits.MaxConcurrentConnections = 1;
						options.Limits.RequestHeadersTimeout = TimeSpan.FromMilliseconds(100);
					})
					.UseStartup<Startup>()
					.ConfigureLogging(logging => logging.ClearProviders())
					.Build()
					.Run();
			}
		}

		private static unsafe void Fuzz()
		{
			var trace = new List<(int, string)>();

			SharpFuzz.Common.Trace.OnBranch = (id, name) =>
			{
				trace.Add((id, name));
			};

			using (var client = new TcpClient("localhost", 5000))
			using (var network = client.GetStream())
			{
				Fuzzer.Run(stream =>
				{
					network.Write(request, 0, request.Length);
					network.Read(clientBuffer, 0, clientBuffer.Length);

					Interlocked.Exchange(ref trace, new List<(int, string)>());
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
