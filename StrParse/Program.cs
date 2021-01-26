using System;
using System.Collections.Generic;
using System.Reflection;
using NonStandard;
using NonStandard.Data.Parse;

namespace NonStandard.Data {
	public enum TextAnchor { Top, Bottom, Left, Right, UpperLeft, UpperRight, LowerLeft, LowerRight }
	[Serializable] public class Dialog {
		public string name;
		public DialogOption[] options;
		public abstract class DialogOption {
			public string text;
			public TextAnchor anchorText = TextAnchor.UpperLeft;
			public Expression requires; // conditional requirement for this option
		}
		[Serializable] public class Text : DialogOption { }
		[Serializable] public class Choice : DialogOption { public string command; }
		[Serializable] public class Command : DialogOption { public string command; }
	}
	class Program {
		public struct Things { public int a, b; }
		public struct TestData {
			public string name, text;
			public int number;
			public List<float> values;
			public Things things;
		}

		static void Main(string[] args) {
			string filepath = 
				//"../../../Program.cs";
				//"../../../testdata.txt";
				"../../../dialogs.txt";
			string text = System.IO.File.ReadAllText(filepath);
			//IList<string> tokens = StringParse.Tokenize(text);
			//List<Token> tokens = new List<Token>();
			//List<int> rows = new List<int>();
			//List<ParseError> errors = new List<ParseError>();
			//Dictionary<string, float> dict;
			//CodeConvert.TryParse(text, out dict, errors);
			//Show.Log(Show.Stringify(dict, true));
			//errors.ForEach(e => Show.Log(e.ToString()));
			//Tokenizer.Tokenize(text, tokens, rows, errors);
			//bool parsed = CodeConvert.TryParse(text, out TestData testData, errors);
			Dictionary<string, float> dict = new Dictionary<string, float>() {
				["number"] = 10.0f,
				["xp"] = 1000
			};
			Tokenizer tokenizer = new Tokenizer();
			bool parsed = CodeConvert.TryParse(text, out Dialog[] testData, dict, tokenizer);
			Console.WriteLine(Show.Stringify(testData, true));
			if (tokenizer.errors.Count>0) {
				Show.Error(tokenizer.errors.Join("\n"));
				tokenizer.errors.Clear();
				Console.ReadKey();
			}
			TestData td = new TestData();
			td.number = 3;
			Dialog lastDialog = testData[testData.Length - 1];
			Dialog.DialogOption[] opt = lastDialog.options;
			Expression ex = opt[opt.Length - 1].requires;
//			Console.WriteLine(ex.DebugPrint());
			Console.WriteLine(ex.Resolve(tokenizer, dict).Join(", "));
			if (tokenizer.errors.Count > 0) {
				Show.Error(tokenizer.errors.Join("\n"));
				Console.ReadKey();
			}
			return;
			List<Token> tokens = tokenizer.tokens;
			List<int> rows = tokenizer.rows;
			for (int i = 0; i < tokens.Count; ++i) {
				Console.ForegroundColor = ((i % 2) == 0) ? ConsoleColor.White : ConsoleColor.Green;
				Console.Write(i+"~ "+tokens[i].index+"@" + ParseError.FilePositionOf(tokens[i],rows) + ": ");
				if(tokens[i].meta is Context.Entry e) {
					if (e.IsText() || e.IsComment()) {
						Console.Write(e.TextRaw);
						i += e.tokenCount-1;//e.IndexAfter(tokens, i);
					} else {
						Console.Write(tokens[i]);
						Console.ForegroundColor = ConsoleColor.DarkGray;
						Console.Write(" " + ParseError.FilePositionOf(e.GetBeginToken(), rows) + " -> " + ParseError.FilePositionOf(e.GetEndToken(), rows)+
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
