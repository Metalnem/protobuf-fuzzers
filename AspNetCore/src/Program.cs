using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Google.Protobuf;
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

		public static unsafe void Main(string[] args)
		{
			fixed (byte* trace = new byte[65_536])
			{
				SharpFuzz.Common.Trace.SharedMem = trace;
				SharpFuzz.Common.Trace.OnBranch = (id, name) => { };

				new WebHostBuilder()
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
						Print(headers, request.Body, clientBuffer.AsSpan(0, read));

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
					Print(headers, request.Body, clientBuffer.AsSpan(0, read));
					lifetime.StopApplication();
				}
			});
		}

		private static void Print(string headers, ByteString body, ReadOnlySpan<byte> response)
		{
			Console.Write(headers);

			if (body.Length > 0)
			{
				Console.WriteLine(body.ToBase64());
				Console.WriteLine();
			}

			Console.Write(Encoding.UTF8.GetString(response));
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
			}

			return "GET";
		}

		private static string GetHeaderName(HeaderName headerName)
		{
			switch (headerName)
			{
				case HeaderName.Accept: return "Accept";
				case HeaderName.AcceptCharset: return "Accept-Charset";
				case HeaderName.AcceptEncoding: return "Accept-Encoding";
				case HeaderName.AcceptLanguage: return "Accept-Language";
				case HeaderName.AcceptPatch: return "Accept-Patch";
				case HeaderName.AcceptRanges: return "Accept-Ranges";
				case HeaderName.AccessControlAllowCredentials: return "Access-Control-Allow-Credentials";
				case HeaderName.AccessControlAllowHeaders: return "Access-Control-Allow-Headers";
				case HeaderName.AccessControlAllowMethods: return "Access-Control-Allow-Methods";
				case HeaderName.AccessControlAllowOrigin: return "Access-Control-Allow-Origin";
				case HeaderName.AccessControlExposeHeaders: return "Access-Control-Expose-Headers";
				case HeaderName.AccessControlMaxAge: return "Access-Control-Max-Age";
				case HeaderName.AccessControlRequestHeaders: return "Access-Control-Request-Headers";
				case HeaderName.AccessControlRequestMethod: return "Access-Control-Request-Method";
				case HeaderName.Age: return "Age";
				case HeaderName.Allow: return "Allow";
				case HeaderName.AltSvc: return "Alt-Svc";
				case HeaderName.Authorization: return "Authorization";
				case HeaderName.CacheControl: return "Cache-Control";
				case HeaderName.ClearSiteData: return "Clear-Site-Data";
				case HeaderName.ContentDisposition: return "Content-Disposition";
				case HeaderName.ContentEncoding: return "Content-Encoding";
				case HeaderName.ContentLanguage: return "Content-Language";
				case HeaderName.ContentLocation: return "Content-Location";
				case HeaderName.ContentRange: return "Content-Range";
				case HeaderName.ContentSecurityPolicy: return "Content-Security-Policy";
				case HeaderName.ContentSecurityPolicyReportOnly: return "Content-Security-Policy-Report-Only";
				case HeaderName.ContentType: return "Content-Type";
				case HeaderName.Cookie: return "Cookie";
				case HeaderName.Cookie2: return "Cookie2";
				case HeaderName.CrossOriginResourcePolicy: return "Cross-Origin-Resource-Policy";
				case HeaderName.Dnt: return "DNT";
				case HeaderName.Date: return "Date";
				case HeaderName.Etag: return "ETag";
				case HeaderName.EarlyData: return "Early-Data";
				case HeaderName.Expect: return "Expect";
				case HeaderName.ExpectCt: return "Expect-CT";
				case HeaderName.Expires: return "Expires";
				case HeaderName.FeaturePolicy: return "Feature-Policy";
				case HeaderName.Forwarded: return "Forwarded";
				case HeaderName.From: return "From";
				case HeaderName.IfMatch: return "If-Match";
				case HeaderName.IfModifiedSince: return "If-Modified-Since";
				case HeaderName.IfNoneMatch: return "If-None-Match";
				case HeaderName.IfRange: return "If-Range";
				case HeaderName.IfUnmodifiedSince: return "If-Unmodified-Since";
				case HeaderName.Index: return "Index";
				case HeaderName.LargeAllocation: return "Large-Allocation";
				case HeaderName.LastModified: return "Last-Modified";
				case HeaderName.Link: return "Link";
				case HeaderName.Location: return "Location";
				case HeaderName.Origin: return "Origin";
				case HeaderName.Pragma: return "Pragma";
				case HeaderName.ProxyAuthenticate: return "Proxy-Authenticate";
				case HeaderName.ProxyAuthorization: return "Proxy-Authorization";
				case HeaderName.PublicKeyPins: return "Public-Key-Pins";
				case HeaderName.PublicKeyPinsReportOnly: return "Public-Key-Pins-Report-Only";
				case HeaderName.Range: return "Range";
				case HeaderName.Referer: return "Referer";
				case HeaderName.ReferrerPolicy: return "Referrer-Policy";
				case HeaderName.RetryAfter: return "Retry-After";
				case HeaderName.SaveData: return "Save-Data";
				case HeaderName.SecWebSocketAccept: return "Sec-WebSocket-Accept";
				case HeaderName.Server: return "Server";
				case HeaderName.ServerTiming: return "Server-Timing";
				case HeaderName.SetCookie: return "Set-Cookie";
				case HeaderName.SetCookie2: return "Set-Cookie2";
				case HeaderName.SourceMap: return "SourceMap";
				case HeaderName.StrictTransportSecurity: return "Strict-Transport-Security";
				case HeaderName.Te: return "TE";
				case HeaderName.TimingAllowOrigin: return "Timing-Allow-Origin";
				case HeaderName.Tk: return "Tk";
				case HeaderName.Trailer: return "Trailer";
				case HeaderName.UpgradeInsecureRequests: return "Upgrade-Insecure-Requests";
				case HeaderName.UserAgent: return "User-Agent";
				case HeaderName.Vary: return "Vary";
				case HeaderName.Via: return "Via";
				case HeaderName.Wwwauthenticate: return "WWW-Authenticate";
				case HeaderName.Warning: return "Warning";
				case HeaderName.XcontentTypeOptions: return "X-Content-Type-Options";
				case HeaderName.XdnsprefetchControl: return "X-DNS-Prefetch-Control";
				case HeaderName.XforwardedFor: return "X-Forwarded-For";
				case HeaderName.XforwardedHost: return "X-Forwarded-Host";
				case HeaderName.XforwardedProto: return "X-Forwarded-Proto";
				case HeaderName.XframeOptions: return "X-Frame-Options";
				case HeaderName.Xxssprotection: return "X-XSS-Protection";
			}

			return null;
		}

		private static string GetHeaderValue(Header header)
		{
			if (header.Values is null)
			{
				return null;
			}

			var sb = new StringBuilder();
			var separator = String.Empty;

			foreach (var value in header.Values)
			{
				if (value.Value is null)
				{
					continue;
				}

				string s = null;

				switch (value.Value.SinglevalueOneofCase)
				{
					case SingleValue.SinglevalueOneofOneofCase.String: s = value.Value.String; break;
					case SingleValue.SinglevalueOneofOneofCase.Number: s = value.Value.Number.ToString(); break;
					case SingleValue.SinglevalueOneofOneofCase.Date: s = GetDateValue(value.Value.Date); break;
				}

				if (String.IsNullOrEmpty(s))
				{
					continue;
				}

				if (value.Quality > 0)
				{
					var quality = (double)value.Quality / uint.MaxValue;
					s = $"{s};q={quality:0.###}";
				}

				sb.Append(separator);
				sb.Append(s);

				separator = ", ";
			}

			return sb.ToString();
		}

		private static string GetDateValue(ulong date)
		{
			var maxDate = (ulong)DateTime.MaxValue.Ticks;
			var ticks = (long)(date % (maxDate + 1));

			return new DateTime(ticks).ToString("U");
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
				var name = GetHeaderName(header.Name);
				var value = GetHeaderValue(header);

				if (!String.IsNullOrEmpty(name)
					&& !String.IsNullOrEmpty(value)
					&& HttpCharacters.IndexOfInvalidTokenChar(name) == -1
					&& HttpCharacters.IndexOfInvalidFieldValueChar(value) == -1)
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
