using System;
using System.Collections.Generic;

namespace StrParse {
	class Program {
		static void Main(string[] args) {
			string filepath = "../../../Program.cs";
			string text = System.IO.File.ReadAllText(filepath);
			//IList<string> tokens = StringParse.Tokenize(text);
			List<Token> tokens = new List<Token>();
			List<int> rows = new List<int>();
			CodeParse.Tokens(text, tokens, indexOfNewRow:rows);
			for (int i = 0; i < tokens.Count; ++i) {
				Console.ForegroundColor = ((i % 2) == 0) ? ConsoleColor.White : ConsoleColor.Green;
				Console.Write("@" + CodeParse.FilePositionOf(tokens[i],rows) + ": ");
				if(tokens[i].meta is ParseContext.Entry e) {
					if(e.IsText || e.IsComment) {
						Console.Write(e.Text);
						int endIndex = e.IndexEnd;
						while (tokens[i].index < endIndex) { ++i; }
					} else {
						Console.Write(tokens[i]);
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
