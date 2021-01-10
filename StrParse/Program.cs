using System;
using System.Collections.Generic;
using System.Reflection;
using NonStandard;

namespace StrParse {


	class Program {
		public struct Things {
			public int a, b;
		}
		public struct TestData {
			public string name, text;
			public int number;
			public List<float> values;
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
			List<CodeConvert.Err> errors = new List<CodeConvert.Err>();
			CodeParse.Tokens(text, tokens, rows, errors);
			bool parsed = CodeConvert.TryParse(text, out TestData testData, errors);
			Console.WriteLine(testData.name);
			Console.WriteLine(testData.number);
			Console.WriteLine(testData.text);
			if (testData.values != null) { Console.WriteLine(string.Join(", ", testData.values)); }
			Console.WriteLine(testData.things.a);
			Console.WriteLine(testData.things.b);
			Console.WriteLine(CodeConvert.Stringify(testData, 0, true));
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
						Console.Write(e.TextRaw);
						i += e.tokenCount-1;//e.IndexAfter(tokens, i);
					} else {
						Console.Write(tokens[i]);
						Console.ForegroundColor = ConsoleColor.DarkGray;
						Console.Write(" " + CodeParse.FilePositionOf(e.BeginToken, rows) + " -> " + CodeParse.FilePositionOf(e.EndToken, rows)+
							" "+e.context.name+" depth:"+e.depth);
					}
					Console.ForegroundColor = ConsoleColor.DarkGray;
					Console.Write(" " + e.tokenCount + "tokens ");
					//for (int t = 0; t < e.tokenCount; ++t) { Console.Write("'" + tokens[t + e.tokenStart] + "' "); }

				} else {
					Console.Write(tokens[i]);
				}
				Console.WriteLine();
			}
			Console.ForegroundColor = ConsoleColor.Gray;
		}
	}
}
