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
		private static readonly byte[] chunkedHeader = Encoding.ASCII.GetBytes("Transfer-Encoding: chunked");
		private static readonly byte[] chunkedMarker = Encoding.ASCII.GetBytes("0\r\n\r\n");
		private static readonly byte[] connectionClose = Encoding.ASCII.GetBytes("Connection: close");

		private static readonly byte[] clientBuffer = new byte[10_000_000];
		private static readonly byte[] serverBuffer = new byte[10_000_000];

		private static readonly char[] invalidPathChars = new char[] { ' ', '\r', '\n', '%' };

		private static readonly HashSet<string> ignoredHeaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
		{
			"Connection", "Content-Length", "Host", "Transfer-Encoding"
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
						options.Limits.KeepAliveTimeout = TimeSpan.FromDays(10);
						options.Limits.MaxRequestBufferSize = null;
						options.Limits.MaxRequestHeaderCount = 1000;
						options.Limits.MaxRequestHeadersTotalSize = 10_000_000;
						options.Limits.MaxResponseBufferSize = null;
						options.Limits.MinRequestBodyDataRate = null;
						options.Limits.MinResponseDataRate = null;

						typeof(KestrelServerOptions).Assembly
							.GetType("Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Infrastructure.Heartbeat")
							.GetField("Interval", BindingFlags.Public | BindingFlags.Static)
							.SetValue(null, TimeSpan.FromDays(10));
					})
					.UseStartup<Startup>()
					.UseUrls("http://*:5000/")
					.ConfigureLogging(logging => logging.ClearProviders())
					.Build()
					.Run();
			}
		}

		private static void Fuzz(IApplicationLifetime lifetime)
		{
			bool libFuzzer = Environment.GetEnvironmentVariable("__LIBFUZZER_SHM_ID") is string s && Int32.TryParse(s, out _);

			TcpClient client = null;
			NetworkStream network = null;

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

				if (client is null)
				{
					client = new TcpClient("localhost", 5000);
					network = client.GetStream();
				}

				var headers = ProtoToHeaders(request);
				var length = Encoding.UTF8.GetBytes(headers, clientBuffer);

				request.Body.CopyTo(clientBuffer, length);
				network.Write(clientBuffer, 0, length + request.Body.Length);

				int read = 0;

				for (; ; )
				{
					read += network.Read(clientBuffer, read, clientBuffer.Length - read);
					var bufferSpan = clientBuffer.AsSpan(0, read);

					if (bufferSpan.IndexOf(connectionClose) > -1)
					{
						Console.WriteLine(headers);
						Console.WriteLine(Encoding.UTF8.GetString(clientBuffer, 0, read));

						client.Dispose();
						network.Dispose();

						client = null;
						network = null;

						break;
					}

					if (bufferSpan.IndexOf(chunkedHeader) == -1 || bufferSpan.EndsWith(chunkedMarker))
					{
						break;
					}
				}

				if (!libFuzzer)
				{
					Console.Write(headers);

					if (request.Body.Length > 0)
					{
						Console.WriteLine(request.Body.ToBase64());
						Console.WriteLine();
					}

					Console.Write(Encoding.UTF8.GetString(clientBuffer, 0, read));
					lifetime.StopApplication();
				}
			});
		}

		private static string GetMethod(Request request)
		{
			switch (request.Method)
			{
				case Method.Get: return "GET";
				case Method.Head: return "HEAD";
				case Method.Post: return "POST";
				case Method.Put: return "PUT";
				case Method.Delete: return "DELETE";
				case Method.Options: return "OPTIONS";
				case Method.Trace: return "TRACE";
				case Method.Patch: return "PATCH";
				default: return "GET";
			}
		}

		private static string GetPath(Request request)
		{
			const string defaultPath = "/";

			if (request.Path is null)
			{
				return defaultPath;
			}

			var path = request.Path.Trim();

			if (path.Length == 0
				|| path.StartsWith('?')
				|| path.EndsWith('?')
				|| path.IndexOfAny(invalidPathChars) >= 0)
			{
				return defaultPath;
			}

			foreach (var ch in path)
			{
				if (ch < 1 || ch > 127)
				{
					return defaultPath;
				}
			}

			if (!path.StartsWith('/'))
			{
				return $"/{path}";
			}

			return path;
		}

		private static string ProtoToHeaders(Request request)
		{
			var method = GetMethod(request);
			var path = GetPath(request);
			var host = HttpUtilities.IsHostHeaderValid(request.Host) ? request.Host : "localhost";
			var sb = new StringBuilder($"{method} {path} HTTP/1.1\r\nHost: {host}\r\n");

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

			if (request.Body.Length > 0)
			{
				sb.Append($"Content-Length: {request.Body.Length}\r\n");
			}
			else if (request.Method == Method.Post || request.Method == Method.Put)
			{
				sb.Append("Content-Length: 0\r\n");
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

				lifetime.ApplicationStarted.Register(() => Task.Run(() => Fuzz(lifetime)));
			}
		}
	}
}
