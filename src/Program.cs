using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Formatting;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Formatting;
using SharpFuzz;

namespace Roslyn.Fuzz
{
	public class Program
	{
		private static readonly HashSet<string> ignore = new HashSet<string>
		{
			"CS0020", // Division by constant zero
			"CS0220", // The operation overflows at compile time in checked mode
		};

		public static unsafe void Main(string[] args)
		{
			using (var memory = new MemoryStream(10_000_000))
			{
				CSharpCompilationOptions options;
				PortableExecutableReference coreLib;

				fixed (byte* sharedMem = new byte[65_536])
				{
					SharpFuzz.Common.Trace.SharedMem = sharedMem;

					options = new CSharpCompilationOptions(OutputKind.ConsoleApplication)
						.WithConcurrentBuild(false)
						.WithDeterministic(true);

					coreLib = MetadataReference.CreateFromFile(typeof(object).Assembly.Location);
				}

				Fuzzer.LibFuzzer.Run(span =>
				{
					string code = null;

					try
					{
						code = CodeBuilder.Build(Function.Parser.ParseFrom(span.ToArray()));
					}
					catch
					{
						return;
					}

					var tree = CSharpSyntaxTree.ParseText(code);

					var compilation = CSharpCompilation.Create("Roslyn.Fuzz.dll")
						.WithOptions(options)
						.AddReferences(coreLib)
						.AddSyntaxTrees(tree);

					memory.Seek(0, SeekOrigin.Begin);
					var result = compilation.Emit(memory);

					if (!result.Success && !result.Diagnostics.Any(diagnostic => ignore.Contains(diagnostic.Id)))
					{
						throw new Exception(FormatError(tree, result));
					}
				});
			}
		}

		private static string FormatError(SyntaxTree syntaxTree, EmitResult emitResult)
		{
			StringBuilder sb = new StringBuilder();

			foreach (var diagnostic in emitResult.Diagnostics)
			{
				sb.AppendLine(diagnostic.ToString());
			}

			var workspace = new AdhocWorkspace();

			var options = workspace.Options
				.WithChangedOption(CSharpFormattingOptions.WrappingKeepStatementsOnSingleLine, false)
				.WithChangedOption(CSharpFormattingOptions.WrappingPreserveSingleLine, false);

			var formattedTree = Formatter.Format(syntaxTree.GetRoot(), workspace, options);
			sb.Append(formattedTree.ToString());

			return sb.ToString();
		}
	}
}
