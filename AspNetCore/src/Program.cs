using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Reflection;
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

			using (var client = new HttpClient())
			{
				Fuzzer.Run(stream =>
				{
					int size = stream.Read(clientBuffer, 0, clientBuffer.Length);
					Request message;

					try
					{
						message = Request.Parser.ParseFrom(clientBuffer, 0, size);
					}
					catch
					{
						return;
					}

					var request = Convert(message);

					using (var response = client.SendAsync(request).GetAwaiter().GetResult())
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

		private static HttpMethod Convert(Method method)
		{
			switch (method)
			{
				case Method.Get: return HttpMethod.Get;
				case Method.Head: return HttpMethod.Head;
				case Method.Post: return HttpMethod.Post;
				case Method.Put: return HttpMethod.Put;
				case Method.Delete: return HttpMethod.Delete;
				case Method.Options: return HttpMethod.Options;
				case Method.Trace: return HttpMethod.Trace;
				case Method.Patch: return HttpMethod.Patch;
				default: return HttpMethod.Get;
			}
		}

		private static HttpRequestMessage Convert(Request message)
		{
			var request = new HttpRequestMessage(
				Convert(message.Method),
				"http://localhost:5000/"
			);

			foreach (var header in message.Headers)
			{
				request.Headers.Add(header.Name, header.Value);
			}

			request.Content = new ByteArrayContent(message.Body.ToByteArray());

			return request;
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
