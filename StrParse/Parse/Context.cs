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
			public IList<Token> tokens;
			public int tokenStart, tokenCount = -1;
			public Delim startDelim, endDelim;
			public int depth { get { Entry p = parent; int n = 0; while (p != null) { p = p.parent; ++n; } return n; } }
			public object sourceMeta;
			public string TextRaw { 
				get {
					Entry e = this; string str;
					do {
						str = e.sourceMeta as string;
						if (str == null) { e = e.sourceMeta as Entry; }
					} while (str == null && e != null);
					return (str != null) ? str.Substring(IndexBegin, Length) : null;
				}
			}
			public string Text { get { return Unescape(); } }
			public object Resolve() { return (context.resolve != null) ? context.resolve(this) : Unescape(); }
			public bool IsText { get { return context == CodeRules.String || context == CodeRules.Char; } }
			public bool IsEnclosure { get { return context == CodeRules.Expression || context == CodeRules.CodeBody || context == CodeRules.SquareBrace; } }
			public bool IsComment { get { return context == CodeRules.CommentLine || context == CodeRules.XmlCommentLine || context == CodeRules.CommentBlock; } }
			public Token BeginToken { get { return tokens[tokenStart]; } }
			public Token EndToken { get { return tokens[tokenStart + tokenCount - 1]; } }
			public int IndexBegin { get { return BeginToken.BeginIndex; } }
			public int IndexEnd { get { return EndToken.EndIndex; } }
			public int Length { get { return IndexEnd - IndexBegin; } }
			public string Unescape() {
				if (context != CodeRules.String && context != CodeRules.Char) { return TextRaw; }
				StringBuilder sb = new StringBuilder();
				for (int i = tokenStart + 1; i < tokenStart + tokenCount - 1; ++i) {
					sb.Append(tokens[i].ToString());
				}
				return sb.ToString();
			}
			//public int IndexAfter(IList<Token> tokens, int index = 0) {
			//	if (tokenCount < 0) return tokens.Count;
			//	int endIndex = IndexEnd;
			//	while (index + 1 < tokens.Count && tokens[index + 1].index < endIndex) { ++index; }
			//	return index;
			//}
		}
		public Entry GetEntry(IList<Token> tokens, int startTokenIndex, object meta, Context.Entry parent = null) {
			Entry e = new Entry { context = this, tokens = tokens, tokenStart = startTokenIndex, sourceMeta = meta, parent = parent };
			return e;
		}
	}
}
