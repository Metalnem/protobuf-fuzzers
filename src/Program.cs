using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Formatting;
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
			if (args.Length == 2 && args[0] == "print")
			{
				var bytes = File.ReadAllBytes(args[1]);
				var code = CodeBuilder.Build(Function.Parser.ParseFrom(bytes));
				var syntaxTree = CSharpSyntaxTree.ParseText(code);

				Console.WriteLine(FormatError(syntaxTree));
				return;
			}

			using (var memory = new MemoryStream(10_000_000))
			{
				var debugVars = Enumerable.Range(0, 100).ToArray();
				var releaseVars = Enumerable.Range(0, 100).ToArray();

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

					var parseOptions = new CSharpParseOptions();
					var syntaxTree = CSharpSyntaxTree.ParseText(code, parseOptions);

					var debugOptions = GetCompilationOptions(OptimizationLevel.Debug);
					var releaseOptions = GetCompilationOptions(OptimizationLevel.Release);

					CompileAndRun(debugOptions, syntaxTree, debugVars);
					CompileAndRun(releaseOptions, syntaxTree, releaseVars);

					if (!debugVars.SequenceEqual(releaseVars))
					{
						throw new Exception(FormatError(syntaxTree));
					}
				});

				void CompileAndRun(CSharpCompilationOptions options, SyntaxTree syntaxTree, int[] vars)
				{
					var coreLib = MetadataReference.CreateFromFile(typeof(object).Assembly.Location);

					var compilation = CSharpCompilation.Create("Roslyn.Run.dll")
						.WithOptions(options)
						.AddReferences(coreLib)
						.AddSyntaxTrees(syntaxTree);

					memory.Seek(0, SeekOrigin.Begin);
					memory.SetLength(0);

					var result = compilation.Emit(memory);
					memory.Seek(0, SeekOrigin.Begin);

					if (result.Success)
					{
						var context = new CollectibleAssemblyLoadContext();

						try
						{
							var assembly = context.LoadFromStream(memory);
							var type = assembly.GetType("Roslyn.Run.Foo");
							var method = type.GetMethod("Bar");

							method.Invoke(null, new object[] { vars });
						}
						catch (TargetInvocationException ex) when (ex.InnerException is DivideByZeroException) { }
						catch (TargetInvocationException ex) when (ex.InnerException is OverflowException) { }
						catch (Exception ex)
						{
							throw new Exception(FormatError(syntaxTree), ex);
						}
						finally
						{
							context.Unload();
						}
					}
					else if (!result.Diagnostics.Any(diagnostic => ignore.Contains(diagnostic.Id)))
					{
						throw new Exception(FormatError(syntaxTree, result.Diagnostics));
					}
				}
			}
		}

		private static CSharpCompilationOptions GetCompilationOptions(OptimizationLevel optimizationLevel)
		{
			return new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
				.WithConcurrentBuild(false)
				.WithDeterministic(true)
				.WithOptimizationLevel(optimizationLevel);
		}

		private static string FormatError(SyntaxTree syntaxTree)
		{
			return FormatError(syntaxTree, Enumerable.Empty<Diagnostic>());
		}

		private static string FormatError(SyntaxTree syntaxTree, IEnumerable<Diagnostic> diagnostics)
		{
			StringBuilder sb = new StringBuilder();

			foreach (var diagnostic in diagnostics)
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
