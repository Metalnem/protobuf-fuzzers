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
using Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Infrastructure;
using Microsoft.Extensions.Logging;
using SharpFuzz;

namespace AspNetCore.Fuzz
{
	public class Program
	{
		private static readonly byte[] chunkedHeader = Encoding.ASCII.GetBytes("Transfer-Encoding: chunked");
		private static readonly byte[] chunkedMarker = Encoding.ASCII.GetBytes("0\r\n\r\n");
		private static readonly byte[] connectionClose = Encoding.ASCII.GetBytes("Connection: close");

		private static readonly byte[] clientBuffer = new byte[10_000_000];
		private static readonly byte[] serverBuffer = new byte[10_000_000];

		private static readonly HashSet<string> ignoredHeaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
		{
			"Content-Length", "Host", "Transfer-Encoding"
		};

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

						options.Limits.MaxRequestBufferSize = null;
						options.Limits.MaxResponseBufferSize = null;

						options.Limits.MaxRequestHeaderCount = 1000;
						options.Limits.MaxRequestHeadersTotalSize = 10_000_000;

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
			using (var client = new TcpClient("localhost", 80))
			using (var network = client.GetStream())
			{
				Fuzzer.LibFuzzer.Run(span =>
				{
					Request request;

					try
					{
						request = Request.Parser.ParseFrom(span.ToArray());
					}
					catch
					{
						return;
					}

					var http = ProtoToHttp(request);
					var bytes = Encoding.UTF8.GetBytes(http);

					network.Write(bytes, 0, bytes.Length);
					int read = 0;

					for (; ; )
					{
						read += network.Read(clientBuffer, read, clientBuffer.Length - read);
						var bufferSpan = clientBuffer.AsSpan(0, read);

						if (bufferSpan.IndexOf(connectionClose) > -1)
						{
							Console.Error.WriteLine(http);
							throw new Exception(Encoding.UTF8.GetString(clientBuffer, 0, read));
						}

						if (bufferSpan.IndexOf(chunkedHeader) == -1 || bufferSpan.EndsWith(chunkedMarker))
						{
							break;
						}
					}
				});
			}
		}

		private static string ProtoToHttp(Request request)
		{
			var path = "/";
			var host = HttpUtilities.IsHostHeaderValid(request.Host) ? request.Host : "localhost";
			var sb = new StringBuilder($"GET {path} HTTP/1.1\r\nHost: {host}\r\n");

			foreach (var header in request.Headers)
			{
				if (header.Name is null || header.Value is null)
				{
					continue;
				}

				var name = header.Name.Trim();
				var value = header.Value.Trim();

				if (name.Length > 0
					&& value.Length > 0
					&& HttpCharacters.IndexOfInvalidTokenChar(name) == -1
					&& HttpCharacters.IndexOfInvalidFieldValueChar(value) == -1
					&& !ignoredHeaders.Contains(name))
				{
					sb.Append($"{name}: {value}\r\n");
				}
			}

			sb.Append("\r\n");

			return sb.ToString();
		}

		private class Startup
		{
			public void Configure(IApplicationBuilder app, IApplicationLifetime lifetime)
			{
				app.Run(async context =>
				{
					var headers = context.Request.GetTypedHeaders();

					var values = new object[]
					{
						headers.Accept,
						headers.AcceptCharset,
						headers.AcceptEncoding,
						headers.AcceptLanguage,
						headers.CacheControl,
						headers.ContentDisposition,
						headers.ContentLength,
						headers.ContentRange,
						headers.ContentType,
						headers.Cookie,
						headers.Date,
						headers.Expires,
						headers.Host,
						headers.IfMatch,
						headers.IfModifiedSince,
						headers.IfNoneMatch,
						headers.IfRange,
						headers.IfUnmodifiedSince,
						headers.LastModified,
						headers.Range,
						headers.Referer
					};

					await context.Request.Body.ReadAsync(serverBuffer);
					await context.Response.WriteAsync(String.Empty);
				});

				lifetime.ApplicationStarted.Register(() => Task.Run((Action)Fuzz));
			}
		}
	}
}
