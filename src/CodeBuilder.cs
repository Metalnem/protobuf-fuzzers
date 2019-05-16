using System;
using System.Text;

namespace Roslyn.Fuzz
{
	internal sealed class CodeBuilder
	{
		private readonly StringBuilder code = new StringBuilder();
		private CodeBuilder() { }

		public static string Build(Function function)
		{
			var builder = new CodeBuilder();
			builder.Emit(function);

			return
$@"namespace Roslyn.Run
{{
	public static class Foo
	{{
		public static void Bar(int[] a) {{ {builder.code.ToString()} }}
	}}
}}";
		}

		private void Emit(VarRef value) => code.Append($"a[{Math.Abs(value.Varnum) % 100}]");
		private void Emit(Lvalue value) => Emit(ThrowIfNull(value.Varref));
		private void Emit(Const value) => code.Append($"({value.Val})");

		private void Emit(BinaryOp value)
		{
			code.Append('(');
			Emit(ThrowIfNull(value.Left));

			switch (value.Op)
			{
				case BinaryOp.Types.Op.Plus: code.Append('+'); break;
				case BinaryOp.Types.Op.Minus: code.Append('-'); break;
				case BinaryOp.Types.Op.Mul: code.Append('*'); break;
				case BinaryOp.Types.Op.Div: code.Append('/'); break;
				case BinaryOp.Types.Op.Mod: code.Append('%'); break;
				case BinaryOp.Types.Op.Xor: code.Append('^'); break;
				case BinaryOp.Types.Op.And: code.Append('&'); break;
				case BinaryOp.Types.Op.Or: code.Append('|'); break;
			}

			Emit(ThrowIfNull(value.Right));
			code.Append(')');
		}

		private void Emit(Rvalue value)
		{
			switch (value.RvalueOneofCase)
			{
				case Rvalue.RvalueOneofOneofCase.None: throw new ArgumentNullException();
				case Rvalue.RvalueOneofOneofCase.Varref: Emit(ThrowIfNull(value.Varref)); break;
				case Rvalue.RvalueOneofOneofCase.Cons: Emit(ThrowIfNull(value.Cons)); break;
				case Rvalue.RvalueOneofOneofCase.Binop: Emit(ThrowIfNull(value.Binop)); break;
			}
		}

		private void Emit(AssignmentStatement value)
		{
			Emit(ThrowIfNull(value.Lvalue));
			code.Append('=');
			Emit(ThrowIfNull(value.Rvalue));
			code.Append(';');
		}

		private void Emit(IfElse value)
		{
			code.Append("if((");
			Emit(ThrowIfNull(value.Cond));
			code.Append(")>0){");
			Emit(ThrowIfNull(value.IfBody));
			code.Append("}else{");
			Emit(ThrowIfNull(value.ElseBody));
			code.Append("}");
		}

		private void Emit(Statement value)
		{
			switch (value.StmtOneofCase)
			{
				case Statement.StmtOneofOneofCase.None: throw new ArgumentNullException();
				case Statement.StmtOneofOneofCase.Assignment: Emit(ThrowIfNull(value.Assignment)); break;
				case Statement.StmtOneofOneofCase.Ifelse: Emit(ThrowIfNull(value.Ifelse)); break;
			}
		}

		private void Emit(StatementSeq value)
		{
			foreach (var statement in value.Statements)
			{
				Emit(ThrowIfNull(statement));
			}
		}

		private void Emit(Function value) => Emit(ThrowIfNull(value.Statements));

		private static T ThrowIfNull<T>(T value) where T : class
		{
			return value ?? throw new ArgumentNullException();
		}
	}
}
