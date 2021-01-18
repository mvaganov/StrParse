using System.Collections.Generic;

namespace NonStandard.Data.Parse {
	public struct ParseError {
		public int row, col;
		public string message;
		public ParseError(int r, int c, string m) { row = r; col = c; message = m; }
		public ParseError(Token token, IList<int> rows, string m) {
			FilePositionOf(token, rows, out row, out col);
			message = m;
		}
		public override string ToString() { return "@" + row + "," + col + ": " + message; }
		public static ParseError None = default(ParseError);
		public void OffsetBy(Token token, IList<int> rows) {
			int r, c; FilePositionOf(token, rows, out r, out c); row += r; col += c;
		}
		public static void FilePositionOf(Token token, IList<int> indexOfNewRow, out int row, out int col) {
			if(indexOfNewRow == null || indexOfNewRow.Count == 0) { row = 0; col = token.index; return; }
			row = indexOfNewRow.BinarySearchIndexOf(token.index);
			if (row < 0) { row = ~row; }
			int rowStart = row > 0 ? indexOfNewRow[row - 1] : 0;
			col = token.index - rowStart;
			if (row == 0) ++col;
		}
		public static string FilePositionOf(Token token, IList<int> indexOfNewRow) {
			int row, col; FilePositionOf(token, indexOfNewRow, out row, out col);
			return (row + 1) + "," + (col);
		}
	}
}
