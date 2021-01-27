using System.Collections.Generic;

namespace NonStandard.Data.Parse {
	public struct ParseError {
		public int row, col;
		public string message;
		public ParseError(int r, int c, string m) { row = r; col = c; message = m; }
		public ParseError(Token token, IList<int> rows, string m) :this(token.index, rows, m) { }
		public ParseError(int index, IList<int> rows, string m) {
			FilePositionOf(index, rows, out row, out col);
			message = m;
		}
		public override string ToString() { return "@" + row + "," + col + ": " + message; }
		public static ParseError None = default(ParseError);
		public void OffsetBy(int index, IList<int> rows) {
			int r, c; FilePositionOf(index, rows, out r, out c); row += r; col += c;
		}
		public static void FilePositionOf(Token token, IList<int> indexOfNewRow, out int row, out int col) {
			FilePositionOf(token.index, indexOfNewRow, out row, out col);
		}
		public static void FilePositionOf(int index, IList<int> indexOfNewRow, out int row, out int col) {
			if(indexOfNewRow == null || indexOfNewRow.Count == 0) { row = 0; col = index; return; }
			row = indexOfNewRow.BinarySearchIndexOf(index);
			if (row < 0) { row = ~row; }
			int rowStart = row > 0 ? indexOfNewRow[row - 1] : 0;
			col = index - rowStart;
			if (row == 0) ++col;
		}
		public static string FilePositionOf(Token token, IList<int> indexOfNewRow) {
			int row, col; FilePositionOf(token, indexOfNewRow, out row, out col);
			return (row + 1) + "," + (col);
		}
	}
}
