using System;
using System.Collections.Generic;
using System.Reflection;

namespace NonStandard.Data.Parse {
	public class TokenSubstitution {
		public string origSrc; public object value;
		public TokenSubstitution(string o, object v) { origSrc = o; value = v; }
	}
	public class DelimCtx : Delim {
		public Context Context {
			get {
				return foundContext != null ? foundContext :
					Context.allContexts.TryGetValue(contextName, out foundContext) ? foundContext : null;
			}
		}
		private Context foundContext = null;
		public string contextName;
		public bool isStart, isEnd;
		public DelimCtx(string delim, string name = null, string desc = null, Func<string, int, ParseResult> parseRule = null,
			string ctx = null, bool s = false, bool e = false, Func<string, int, bool> addReq = null, bool printable = true)
			: base(delim, name, desc, parseRule, addReq, printable) {
			contextName = ctx; isStart = s; isEnd = e;
		}
	}
	public class DelimOp : Delim {
		public int order;
		public Func<List<Token>, Tokenizer, int, Context.Entry> isSyntaxValid = null;
		public Func<List<Token>, Tokenizer, int, object, object> resolve = null;
		public DelimOp(string delim, string name = null, string desc = null,
			Func<string, int, ParseResult> parseRule = null,
			Func<string, int, bool> addReq = null, int order = 100,
			Func<List<Token>, Tokenizer, int, Context.Entry> syntax = null,
			Func<List<Token>, Tokenizer, int, object, object> resolve = null)
			: base(delim, name, desc, parseRule, addReq) {
			this.order = order; isSyntaxValid = syntax; this.resolve = resolve;
		}
	}
	public class Delim : IComparable<Delim> {
		public string text, name, description;
		public Func<string, int, ParseResult> parseRule = null;
		public Func<string, int, bool> extraReq = null;
		public bool printable = true;
		public Delim(string delim, string name = null, string desc = null,
			Func<string, int, ParseResult> parseRule = null,
			Func<string, int, bool> addReq = null, bool printable = true) {
			text = delim; this.name = name; description = desc; this.parseRule = parseRule; extraReq = addReq; this.printable = printable;
		}
		public bool IsAt(string str, int index) {
			if (index + text.Length > str.Length) { return false; }
			for (int i = 0; i < text.Length; ++i) {
				if (text[i] != str[index + i]) return false;
			}
			if (extraReq != null) { return extraReq.Invoke(str, index); }
			return true;
		}
		public override string ToString() { return printable?text:""; }
		public static implicit operator Delim(string s) { return new Delim(s); }

		public int CompareTo(Delim other) {
			int len = Math.Min(text.Length, other.text.Length);
			for (int i = 0; i < len; ++i) { int comp = text[i] - other.text[i]; if (comp != 0) return comp; }
			if (text.Length > other.text.Length) return -1;
			if (text.Length < other.text.Length) return 1;
			if (extraReq != null && other.extraReq == null) return -1;
			if (extraReq == null && other.extraReq != null) return 1;
			return 0;
		}
	}
}
