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
		public string FilePositionOf(Token token) {
			List<Context.Entry> traversed = new List<Context.Entry>();
			while (!token.IsValid) {
				Context.Entry e = token.GetAsContextEntry();
				if (e == null || traversed.IndexOf(e) >= 0) return "???";
				traversed.Add(e);
				token = e.tokens[0];
			}
			return ParseError.FilePositionOf(token, rows);
		}
		public ParseError AddError(int index, string message) {
			ParseError e = new ParseError(index, rows, message); errors.Add(e); return e;
		}
		public ParseError AddError(Token token, string message) { return AddError(token.index, message); }
		public void AddError(ParseError error) { errors.Add(error); }
		public override string ToString() { return errors.Join(", "); }
		public void Tokenize(string str) {
			this.str = str;
			Tokenize(null, 0);
		}
		public string DebugPrint(int depth = 0, string indent = "  ") { return DebugPrint(tokens, depth, indent); }
		public static string DebugPrint(IList<Token> tokens, int depth = 0, string indent = "  ") {
			StringBuilder sb = new StringBuilder();
			for(int i = 0; i < tokens.Count; ++i) {
				Token t = tokens[i];
				Context.Entry e = t.GetAsContextEntry();
				if (e != null) {
					if (e.tokens != tokens) {
						Context.Entry prevEntry = i > 0 ? tokens[i - 1].GetAsContextEntry() : null;
						if (prevEntry != null && prevEntry.tokens != tokens) {
							sb.Append(indent);
						} else {
							sb.Append("\n").Append(Show.Indent(depth + 1, indent));
						}
						if (e.tokenStart != 0)
							Show.Error("woah");
						if (e.tokenCount != e.tokens.Count)
							Show.Error("woah!!!");
						sb.Append(DebugPrint(e.tokens, depth + 1)).
							Append("\n").Append(Show.Indent(depth, indent));
					} else {
						if (i == 0) { sb.Append(e.beginDelim); }
						else if (i == tokens.Count-1) { sb.Append(e.endDelim); }
						else { sb.Append(" ").Append(e.sourceMeta).Append(" "); }
					}
				} else {
					sb.Append("'").Append(tokens[i].GetAsSmallText()).Append("'");
				}
			}
			return sb.ToString();
		}
		public void Tokenize(Context a_context = null, int index = 0) {
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
			DebugPrint(-1);
			ApplyOperators();
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
					pr.error.OffsetBy(delimToken.index, rows);
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
				Context.Entry e = tokens[i].GetAsContextEntry();
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
			List<int[]> paths = FindTokenPaths(t => t.meta is DelimOp);
			paths.Sort((a, b) => {
				int comp;
				comp = b.Length.CompareTo(a.Length);
				if(comp != 0) { return comp; }
				Context.Entry e = null;
				Token ta = GetTokenAt(tokens, a, ref e);
				Token tb = GetTokenAt(tokens, b, ref e);
				DelimOp da = ta.meta as DelimOp;
				DelimOp db = tb.meta as DelimOp;
				comp = da.order.CompareTo(db.order);
				if (comp == 0) { comp = ta.index.CompareTo(tb.index); }
				return comp;
			});
			//Console.WriteLine(PrintTokenPaths(paths));
			for(int i = 0; i < paths.Count; ++i) {
				Context.Entry pathNode = null;
				Token t = GetTokenAt(tokens, paths[i], ref pathNode);
				DelimOp op = t.meta as DelimOp;
				Context.Entry opEntry = op.isSyntaxValid.Invoke(this, pathNode.tokens, paths[i][paths[i].Length - 1]);
				if(pathNode.tokenCount != pathNode.tokens.Count) {
					pathNode.tokenCount = pathNode.tokens.Count;
				}
			}
		}
		public string PrintTokenPaths(IList<int[]> paths) {
			return paths.Join("\n", arr => {
				Context.Entry e = null;
				Token t = GetTokenAt(tokens, arr, ref e);
				return arr.Join(", ") + ":" + t + " @" + ParseError.FilePositionOf(t, rows);
			});
		}
		Token GetTokenAt(List<Token> currentPath, IList<int> index, ref Context.Entry lastPathNode) {
			Token t = currentPath[index[0]];
			if (index.Count == 1) return t;
			index = index.GetRange(1, index.Count - 1);
			lastPathNode = t.GetAsContextEntry();
			return GetTokenAt(lastPathNode.tokens, index, ref lastPathNode);
		}
		List<int[]> FindTokenPaths(Func<Token, bool> predicate, bool justOne = false) {
			if (tokens.Count == 0) return new List<int[]>();
			List<List<Token>> path = new List<List<Token>>();
			List<int> position = new List<int>();
			List<int[]> paths = new List<int[]>();
			path.Add(tokens);
			position.Add(0);
			while(position[position.Count-1] < path[path.Count - 1].Count) {
				List<Token> currentTokens = path[path.Count - 1];
				int currentIndex = position[position.Count - 1];
				Token token = currentTokens[currentIndex];
				if (predicate(token)) { paths.Add(position.ToArray()); }
				Context.Entry e = token.GetAsContextEntry();
				bool incremented = false;
				if(e != null) {
					if (currentTokens != e.tokens) {
						position.Add(0);
						path.Add(e.tokens);
						if (justOne) break;
						currentIndex = position[position.Count - 1];
						currentTokens = path[path.Count - 1];
						incremented = true;
					}
				}
				if (!incremented) {
					do {
						position[position.Count - 1] = ++currentIndex;
						if (currentIndex >= currentTokens.Count) {
							position.RemoveAt(position.Count - 1);
							path.RemoveAt(path.Count - 1);
							if (position.Count <= 0) break;
							currentIndex = position[position.Count - 1];
							position[position.Count - 1] = ++currentIndex;
							currentTokens = path[path.Count - 1];
						}
					} while (currentIndex >= currentTokens.Count);
				}
				if (position.Count <= 0) break;
			}
			return paths;
		}
		public void ExtractContextAsSubTokenList(Context.Entry entry) {
			if(entry.tokenCount <= 0) { throw new Exception("what just happened?"); }
			int indexWhereItHappens = entry.tokenStart;
			List<Token> subTokens = entry.tokens.GetRange(entry.tokenStart, entry.tokenCount);
			int index = subTokens.FindIndex(t => t.GetAsContextEntry() == entry);
			entry.RemoveTokenRange(entry.tokenStart, entry.tokenCount - 1);
			Token entryToken = subTokens[index];
			entryToken.Invalidate();
			entry.tokens[indexWhereItHappens] = entryToken;
			entry.tokens = subTokens;
			entry.tokenStart = 0;
			entry.tokenCount = subTokens.Count;
		}
	}
}
