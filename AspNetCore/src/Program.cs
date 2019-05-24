using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using SharpFuzz;
using Microsoft.AspNetCore.Hosting;
using System.Collections.Generic;
using System.Threading;
using System.Net.Http;

namespace AspNetCore.Fuzz
{
	public class Program
	{
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
				lock (trace)
				{
					trace.Add((id, name));
				}
			};

			using (var client = new HttpClient())
			{
				Fuzzer.Run(stream =>
				{
					stream.Read(clientBuffer, 0, clientBuffer.Length);

					using (var response = client.GetAsync("http://localhost:5000/").GetAwaiter().GetResult())
					{
						response.EnsureSuccessStatusCode();
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
