using System;
using System.Collections.Generic;
using System.Text;

namespace NonStandard.Data.Parse {
	public class Context {
		public static Dictionary<string, Context> allContexts = new Dictionary<string, Context>();
		public string name = "default";
		public char[] whitespace = CodeRules.WhitespaceDefault;
		public Delim[] delimiters = CodeRules.StandardDelimiters;
		public Func<Entry, object> resolve;
		/// <summary>
		/// data used to make delimiter searching very fast
		/// </summary>
		private char minDelim = char.MaxValue, maxDelim = char.MinValue; private int[] textLookup;
		public Context(string name, Delim[] defaultDelimiters = null, char[] defaultWhitespace = null) {
			this.name = name;
			allContexts[name] = this;
			if (defaultDelimiters == null) {
				delimiters = CodeRules.StandardDelimiters;
			}
			if(defaultWhitespace == null) {
				whitespace = CodeRules.WhitespaceDefault;
			}
		}
		/// <summary>
		/// set the delimiters of this Context, also calculating a simple lookup table
		/// </summary>
		/// <param name="delims"></param>
		public void SetDelimiters(Delim[] delims) {
			if(delims == null || delims.Length == 0) {
				minDelim = maxDelim = (char)0;
				textLookup = new int[] { -1 };
			}
			char c, last = delims[0].text[0];
			for (int i = 0; i < delims.Length; ++i) {
				c = delims[i].text[0];
				if (c < last) { Array.Sort(delims); SetDelimiters(delims); return; }
				if (c < minDelim) minDelim = c;
				if (c > maxDelim) maxDelim = c;
			}
			textLookup = new int[maxDelim + 1 - minDelim];
			for (int i = 0; i < textLookup.Length; ++i) { textLookup[i] = -1; }
			for (int i = 0; i < delims.Length; ++i) {
				c = delims[i].text[0];
				int lookupIndex = c - minDelim; // where in the delimiters list this character can be found
				if (textLookup[lookupIndex] < 0) { textLookup[lookupIndex] = i; }
			}
		}
		public int IndexOfDelimeterAt(string str, int index) {
			if (minDelim > maxDelim) { SetDelimiters(delimiters); }
			char c = str[index];
			if (c < minDelim || c > maxDelim) return -1;
			int i = textLookup[c - minDelim];
			if (i < 0) return -1;
			while (i < delimiters.Length) {
				if (delimiters[i].text[0] != c) break;
				if (delimiters[i].IsAt(str, index)) return i;
				++i;
			}
			return -1;
		}
		public Delim GetDelimiterAt(string str, int index) {
			int i = IndexOfDelimeterAt(str, index);
			if (i < 0) return null;
			return delimiters[i];
		}
		public class Entry {
			public Context context = null;
			public Entry parent = null;
			public List<Token> tokens;
			public int tokenStart, tokenCount = -1;
			public Delim beginDelim, endDelim;
			public int depth { get { Entry p = parent; int n = 0; while (p != null) { p = p.parent; ++n; } return n; } }
			public object sourceMeta;
			public readonly static Entry None = new Entry();
			public static string PrintAll(List<Token> tokens) {
				StringBuilder sb = new StringBuilder();
				List<List<Token>> stack = new List<List<Token>>();
				PrintAll(tokens, sb, stack);
				return sb.ToString();
			}
			protected static void PrintAll(List<Token> tokens, StringBuilder sb, List<List<Token>> stack) {
				int recurse = stack.IndexOf(tokens);
				stack.Add(tokens);
				if (recurse >= 0) { sb.Append("/* recurse " + stack.Count + " */"); return; }
				for(int i = 0; i < tokens.Count; ++i) {
					Token t = tokens[i];
					Entry e = t.GetAsContextEntry();
					if (e != null && !t.IsValid) {
						PrintAll(e.tokens, sb, stack);
					} else {
						sb.Append(t);
					}
				}
			}
			public string TextRaw { 
				get {
					Entry e = this; string str;
					Delim d;
					do {
						str = e.sourceMeta as string;
						if (str == null) { d = e.sourceMeta as Delim; if (d != null) { str = d.text; } }
						if (str == null) { e = e.sourceMeta as Entry; }
					} while (str == null && e != null);
					return (str != null) ? str : null;
				}
			}
			public string GetText() { return Unescape(); }
			public object Resolve(object a_context) {
				DelimOp op = sourceMeta as DelimOp;
				if(op != null) { return op.resolve.Invoke(this, a_context); }
				return (context.resolve != null) ? context.resolve(this) : Unescape();
			}
			public bool IsText() { return context == CodeRules.String || context == CodeRules.Char; }
			public bool IsEnclosure { get { return context == CodeRules.Expression || context == CodeRules.CodeBody || context == CodeRules.SquareBrace; } }
			public bool IsComment() { return context == CodeRules.CommentLine || context == CodeRules.XmlCommentLine || context == CodeRules.CommentBlock; }
			public Token GetBeginToken() { return tokens[tokenStart]; }
			public Token GetEndToken() { return tokens[tokenStart + tokenCount - 1]; }
			public int GetIndexBegin() { return GetBeginToken().GetBeginIndex(); }
			public int GetIndexEnd() { return GetEndToken().GetEndIndex(); }
			public bool IsBegin(Token t) { return t == GetBeginToken(); }
			public bool IsEnd(Token t) { return t == GetEndToken(); }
			public bool IsBeginOrEnd(Token t) { return t == GetBeginToken() || t == GetEndToken(); }
			public int Length { get { return GetIndexEnd() - GetIndexBegin(); } }
			public string Unescape() {
				if (context != CodeRules.String && context != CodeRules.Char) { return TextRaw.Substring(GetIndexBegin(), Length); }
				StringBuilder sb = new StringBuilder();
				for (int i = tokenStart + 1; i < tokenStart + tokenCount - 1; ++i) {
					sb.Append(tokens[i].ToString());
				}
				return sb.ToString();
			}
			public void RemoveTokenRange(int index, int count) {
				if (count <= 0) return;
				List<Token> tok = tokens as List<Token>;
				if (tok != null) { tok.RemoveRange(index, count); }
				else {
					Token[] tArr = new Token[tokens.Count-count];
					int end = index + count, length = tokens.Count - end;
					for (int i = 0; i < index; ++i) { tArr[i] = tokens[i]; }
					for (int i = 0; i < length; ++i) { tArr[index + i] = tokens[end + i]; }
					tokens = new List<Token>(tArr);
				}
				tokenCount -= count;
			}
		}
		public Entry GetEntry(List<Token> tokens, int startTokenIndex, object meta, Context.Entry parent = null) {
			Entry e = new Entry { context = this, tokens = tokens, tokenStart = startTokenIndex, sourceMeta = meta, parent = parent };
			return e;
		}
	}
}
