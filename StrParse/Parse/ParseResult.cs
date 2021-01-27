using System;

namespace NonStandard.Data.Parse {
	public struct ParseResult {
		/// <summary>
		/// how much text was resolved (no longer needs to be parsed)
		/// </summary>
		public int lengthParsed;
		/// <summary>
		/// what to replace this delimiter (and all characters until newIndex)
		/// </summary>
		public object replacementValue;
		/// <summary>
		/// null unless there was an error processing this delimeter
		/// </summary>
		public ParseError error;
		public bool IsError { get { return !string.IsNullOrEmpty(error.message); } }
		public ParseResult(int length, object value) : this(length, value, null, 0,0,0) {
		}
		public ParseResult(int length, object value, string err, int i, int r, int c) {
			lengthParsed = length; replacementValue = value; error = new ParseError(i, r, c, err);
		}
		public ParseResult AddToLength(int count) { lengthParsed += count; error.col += count; return this; }
		public ParseResult ForceCharSubstitute() { replacementValue = Convert.ToChar(replacementValue); return this; }
		public ParseResult SetError(string errorMessage) { return SetError(errorMessage, 0, 0, 0); }
		public ParseResult SetError(string errorMessage, int index, int row, int col) {
			error = new ParseError(index, row, col, errorMessage); return this;
		}
	}
}
