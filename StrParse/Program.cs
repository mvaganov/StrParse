using System;
using System.Collections.Generic;

public struct TestData {
	public string name, text;
	public int number;
	public float[] values;
}

namespace StrParse {


	class Program {
		public static T Fill<T>(string text, T data) {
			// 
			return data;
		}

		static void Main(string[] args) {
			string filepath = 
				//"../../../Program.cs";
				"../../../testdata.txt";
			string text = System.IO.File.ReadAllText(filepath);
			//IList<string> tokens = StringParse.Tokenize(text);
			List<Token> tokens = new List<Token>();
			List<int> rows = new List<int>();
			CodeParse.Tokens(text, tokens, indexOfNewRow:rows);
			for(int i = 0; i < rows.Count; ++i) {
				Console.Write(rows[i] + " ");
			} Console.WriteLine();
			for (int i = 0; i < tokens.Count; ++i) {
				Console.ForegroundColor = ((i % 2) == 0) ? ConsoleColor.White : ConsoleColor.Green;
				Console.Write(i+"~ "+tokens[i].index+"@" + CodeParse.FilePositionOf(tokens[i],rows) + ": ");
				if(tokens[i].meta is ParseContext.Entry e) {
					if (e.IsText || e.IsComment) {
						Console.Write(e.Text);
						i = e.NextIndex(tokens, i);
					} else {
						Console.Write(tokens[i]);
						Console.ForegroundColor = ConsoleColor.DarkGray;
						Console.Write(" " + CodeParse.FilePositionOf(e.begin, rows) + " -> " + CodeParse.FilePositionOf(e.end, rows)+
							" "+e.context.name+" depth:"+e.depth);
					}
				} else {
					Console.Write(tokens[i]);
				}
				Console.WriteLine();
			}
			Console.ForegroundColor = ConsoleColor.Gray;
		}
	}
}
