using System;
using System.Text;

namespace NonStandard.Data.Parse {
	public struct Token : IEquatable<Token>, IComparable<Token> {
		public int index, length; // 32 bits x2
		public object meta; // 64 bits
		public Token(object meta, int i, int len) { this.meta = meta; index = i; length = len; }
		public static Token None = new Token(null, -1, -1);
		public int GetBeginIndex() { return index; }
		public int GetEndIndex() { return index + length; }
		public string ToString(string s) { return s.Substring(index, length); }
		public override string ToString() {
			Context.Entry pce = meta as Context.Entry; if (pce != null) {
				if(IsValid) return ToString(pce.TextRaw);
				string output = pce.context.name;
				if (pce.IsText()) {
					output += "(" + pce.GetText() + ")";
				}
				return output;
			}
			return Resolve().ToString();
		}
		public object Resolve() {
			if (meta == null) throw new NullReferenceException();
			if (meta is string) return ToString((string)meta);
			TokenSubstitution ss = meta as TokenSubstitution; if (ss != null) return ss.value;
			Delim d = meta as Delim; if (d != null) return d.text;
			Context.Entry pce = meta as Context.Entry; if (pce != null) return pce.Resolve();
			throw new DecoderFallbackException();
		}
		public string GetAsSmallText() {
			Context.Entry e = GetAsContextEntry();
			if (e != null) {
				if (IsContextBeginning()) { return e.beginDelim.ToString(); }
				if (IsContextEnding()) { return e.endDelim.ToString(); }
			}
			return ToString();
		}
		public string GetAsBasicToken() { if (meta is string) { return ((string)meta).Substring(index, length); } return null; }
		public Delim GetAsDelimiter() { return meta as Delim; }
		public Context.Entry GetAsContextEntry() { return meta as Context.Entry; }
		public bool IsContextBeginning() {
			Context.Entry ctx = GetAsContextEntry();
			if (ctx != null) {
				return ctx.GetBeginToken() == this;
			}
			return false;
		}
		public bool IsContextEnding() {
			Context.Entry ctx = GetAsContextEntry();
			if (ctx != null) { return ctx.GetEndToken() == this; }
			return false;
		}
		public bool IsValid { get { return index >= 0 && length >= 0; } }
		public void Invalidate() { length = -1; }
		public bool Equals(Token other) { return index == other.index && length == other.length && meta == other.meta; }
		public override bool Equals(object obj) { if (obj is Token) return Equals((Token)obj); return false; }
		public override int GetHashCode() { return meta.GetHashCode() ^ index ^ length; }
		public int CompareTo(Token other) {
			int comp = index.CompareTo(other.index);
			if (comp != 0) return comp;
			return -length.CompareTo(other.length); // bigger one should go first
		}
		public static bool operator ==(Token lhs, Token rhs) { return lhs.Equals(rhs); }
		public static bool operator !=(Token lhs, Token rhs) { return !lhs.Equals(rhs); }
	}
}
