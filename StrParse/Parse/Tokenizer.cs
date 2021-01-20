using System;
using System.Collections.Generic;
using System.Text;

namespace NonStandard.Data.Parse {
	public class Tokenizer {
		public string str;
		public List<Token> tokens = new List<Token>(); // actually a tree. each token can point to more token lists
		public List<ParseError> errors = new List<ParseError>();
		public List<int> rows = new List<int>();
		public Tokenizer() { }
		public void FilePositionOf(Token token, out int row, out int col) {
			ParseError.FilePositionOf(token, rows, out row, out col);
		}
		public ParseError AddError(Token token, string message) { 
			ParseError e = new ParseError(token, rows, message); errors.Add(e); return e;
		}
		public void AddError(ParseError error) { errors.Add(error); }
		public override string ToString() { return errors.Join(", "); }
		public int Tokenize(string str) {
			this.str = str;
			return Tokenize(null, 0);
		}
		public string DebugPrint(int depth = 0) { return DebugPrint(tokens, depth); }
		public static string DebugPrint(IList<Token> tokens, int depth = 0) {
			StringBuilder sb = new StringBuilder();
			string indent = "  ";
			for(int i = 0; i < tokens.Count; ++i) {
				Token t = tokens[i];
				Context.Entry e = t.AsContextEntry;
				if (e != null) {
					if (e.tokens != tokens) {
						Context.Entry prevEntry = i > 0 ? tokens[i - 1].AsContextEntry : null;
						if (prevEntry != null && prevEntry.tokens != tokens) {
							sb.Append(indent);
						} else {
							sb.Append("\n").Append(Show.Indent(depth + 1, indent));
						}
						sb.Append(DebugPrint(e.tokens, depth + 1)).
							Append("\n").Append(Show.Indent(depth, indent));
					} else {
						if (i == 0) { sb.Append(e.beginDelim); }
						else if (i == tokens.Count-1) { sb.Append(e.endDelim); }
						else { sb.Append("[").Append(tokens[i]).Append("]"); }
					}
				} else {
					sb.Append("[").Append(tokens[i]).Append("]");
				}
			}
			return sb.ToString();
		}
		public int Tokenize(Context a_context = null, int index = 0) {
			if (a_context == null) a_context = CodeRules.Default;
			int tokenBegin = -1;
			List<Context.Entry> contextStack = new List<Context.Entry>();
			Context currentContext = a_context;
			while (index < str.Length) {
				char c = str[index];
				Delim delim = currentContext.GetDelimiterAt(str, index);
				if (delim != null) {
					FinishToken(index, ref tokenBegin);
					HandleDelimiter(delim, ref index, contextStack, ref currentContext, a_context);
				} else if (Array.IndexOf(currentContext.whitespace, c) < 0) {
					if (tokenBegin < 0) { tokenBegin = index; } // handle non-whitespace
				} else {
					FinishToken(index, ref tokenBegin); // handle whitespace
				}
				if (rows != null && c == '\n') { rows.Add(index); }
				++index;
			}
			FinishToken(index, ref tokenBegin); // add the last token that was still being processed
			FinalTokenCleanup();
			//ApplyOperators();
			return index;
		}

		private bool FinishToken(int index, ref int tokenBegin) {
			if (tokenBegin >= 0) {
				int len = index - tokenBegin;
				if (len > 0) { tokens.Add(new Token(str, tokenBegin, len)); }
				tokenBegin = -1;
				return true;
			}
			return false;
		}
		private void HandleDelimiter(Delim delim, ref int index,  List<Context.Entry> contextStack, 
			ref Context currentContext, Context defaultContext) {
			Token delimToken = new Token(delim, index, delim.text.Length);
			if (delim.parseRule != null) {
				ParseResult pr = delim.parseRule.Invoke(str, index);
				if (pr.IsError && errors != null) {
					pr.error.OffsetBy(delimToken, rows);
					errors.Add(pr.error);
				}
				if (pr.replacementValue != null) {
					delimToken.length = pr.lengthParsed;
					delimToken.meta = new TokenSubstitution(str, pr.replacementValue);
				}
				index += pr.lengthParsed - 1;
			} else {
				index += delim.text.Length - 1;
			}
			DelimCtx dcx = delim as DelimCtx;
			Context.Entry endedContext = null;
			if (dcx != null) {
				if (contextStack.Count > 0 && dcx.Context == currentContext && dcx.isEnd) {
					endedContext = contextStack[contextStack.Count - 1];
					endedContext.endDelim = dcx;
					delimToken.meta = endedContext;
					endedContext.tokenCount = (tokens.Count - endedContext.tokenStart) + 1;
					contextStack.RemoveAt(contextStack.Count - 1);
					if (contextStack.Count > 0) {
						currentContext = contextStack[contextStack.Count - 1].context;
					} else {
						currentContext = defaultContext;
					}
				}
				if (endedContext == null && dcx.isStart) {
					Context.Entry parentCntx = (contextStack.Count > 0) ? contextStack[contextStack.Count - 1] : null;
					Context.Entry newContext = dcx.Context.GetEntry(tokens, tokens.Count, str, parentCntx);
					newContext.beginDelim = dcx;
					currentContext = dcx.Context;
					delimToken.meta = newContext;
					contextStack.Add(newContext);
				}
			}
			tokens.Add(delimToken);
			if (endedContext != null) { ExtractContextAsSubTokenList(endedContext); }
		}
		private void FinalTokenCleanup() {
			for (int i = 0; i < tokens.Count; ++i) {
				// any unfinished contexts must end. the last place they could end is the end of this string
				Context.Entry e = tokens[i].AsContextEntry;
				if (e != null && e.tokenCount < 0) {
					e.tokenCount = tokens.Count - e.tokenStart;
					ExtractContextAsSubTokenList(e);
					if (e.context != CodeRules.CommentLine) { // this is an error, unless it's a comment
						errors.Add(new ParseError(tokens[i], rows, "missing closing token"));
					}
				}
			}
		}
		public void ApplyOperators() {
			if (tokens.Count == 0) return;
			List<IList<Token>> path = new List<IList<Token>>();
			List<int> position = new List<int>();
			List<int[]> foundOperators = new List<int[]>();

			path.Add(tokens);
			position.Add(0);
			while(position[position.Count-1] < path[path.Count - 1].Count) {
				IList<Token> currentTokens = path[path.Count - 1];
				int currentIndex = position[position.Count - 1];
				Token token = currentTokens[currentIndex];
				Console.Write(token.AsSmallText+"@"+token.index+" ");
				Context.Entry e = token.AsContextEntry;
				bool incremented = false;
				if(e != null) {
					if (currentTokens != e.tokens) {
						position.Add(0);
						path.Add(e.tokens);
						currentIndex = position[position.Count - 1];
						currentTokens = path[path.Count - 1];
						incremented = true;
					} else {
						Console.Write(".");
					}
				} else {
					DelimOp op = token.meta as DelimOp;
					if (op != null) {
						foundOperators.Add(position.ToArray());
					}
				}
				if (!incremented) {
					do {
						++currentIndex;
						if (currentIndex >= currentTokens.Count) {
							position.RemoveAt(position.Count - 1);
							path.RemoveAt(path.Count - 1);
							if (position.Count <= 0) break;
							currentIndex = position[position.Count - 1] + 1;
							currentTokens = path[path.Count - 1];
						}
					} while (currentIndex >= currentTokens.Count);
				}
				if (position.Count <= 0) break;
			}
			Console.WriteLine(foundOperators.Join("\n", arr => arr.Join(", "))); ;
		}
		public void ExtractContextAsSubTokenList(Context.Entry entry) {
			if(entry.tokenCount <= 0) { throw new Exception("what just happened?"); }
			int indexWhereItHappens = entry.tokenStart;
			IList<Token> subTokens = entry.tokens.GetRange(entry.tokenStart, entry.tokenCount);
			int index = subTokens.FindIndex(t => t.AsContextEntry == entry);
			entry.RemoveTokenRange(entry.tokenStart, entry.tokenCount - 1);
			entry.tokens[indexWhereItHappens] = subTokens[index];
			entry.tokens = subTokens;
			entry.tokenStart = 0;
		}
	}
}
