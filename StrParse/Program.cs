using System;
using System.Collections.Generic;
using System.Reflection;

namespace StrParse {


	class Program {
		public struct Things {
			public int a, b;
		}
		public struct TestData {
			public string name, text;
			public int number;
			public float[] values;
			public Things things;
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

			List<CodeConvert.Err> errors = new List<CodeConvert.Err>();
			bool parsed = CodeConvert.TryParse(text, out TestData testData, errors);
			Console.WriteLine(testData.name);
			Console.WriteLine(testData.number);
			Console.WriteLine(testData.text);
			if (testData.values != null) { Console.WriteLine(string.Join(", ", testData.values)); }
			Console.WriteLine(testData.things.a);
			Console.WriteLine(testData.things.b);
			if (!parsed) {
				for(int i = 0; i < errors.Count; ++i) {
					Console.WriteLine(errors[i]);
				}
				Console.ReadKey();
			}

			for(int i = 0; i < rows.Count; ++i) {
				Console.Write(rows[i] + " ");
			} Console.WriteLine();
			for (int i = 0; i < tokens.Count; ++i) {
				Console.ForegroundColor = ((i % 2) == 0) ? ConsoleColor.White : ConsoleColor.Green;
				Console.Write(i+"~ "+tokens[i].index+"@" + CodeParse.FilePositionOf(tokens[i],rows) + ": ");
				if(tokens[i].meta is Context.Entry e) {
					if (e.IsText || e.IsComment) {
						Console.Write(e.Text);
						i = e.IndexAfter(tokens, i);
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
