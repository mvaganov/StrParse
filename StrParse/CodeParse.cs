using System;
using System.Collections.Generic;
using System.Text;
using System.Reflection;

namespace StrParse {
	public struct Token {
		public int index, length; // 64 bits
		public object meta; // 64 bits
		public Token(object meta, int i, int len) {
			this.meta = meta; index = i; length = len;
		}
		//public string ToString(string str) { return str.Substring(index, length); }
		public static Token None = new Token(null, 0,0);
		public int Begin => index;
		public int End => index + length;
		public override string ToString() {
			if (meta == null) throw new NullReferenceException();
			switch (meta) {
			case string s: return s.Substring(index, length);
			case StringSubstitution ss: return ss.str;
			case Delim d: return d.text;
			case ParseContext.Entry pce: return pce.fulltext.Substring(index, length);
			}
			throw new DecoderFallbackException();
		}
	}
	public struct ParseResult {
		/// <summary>
		/// how much text was resolved (no longer needs to be parsed)
		/// </summary>
		public int lengthParsed;
		/// <summary>
		/// what to replace this delimiter (and all characters until newIndex)
		/// </summary>
		public string replacementString;
		/// <summary>
		/// null unless there was an error processing this delimeter
		/// </summary>
		public string error;
		public ParseResult(int length, string str, string err = null) { lengthParsed = length; replacementString = str; error = err; }
	}
	public struct StringSubstitution { public string orig, str; public StringSubstitution(string o, string s) { orig = o; str = s; } }
	public class DelimCtx : Delim {
		public ParseContext Context => foundContext != null ? foundContext 
			: ParseContext.allContexts.TryGetValue(contextName, out foundContext) ? foundContext : null;
		private ParseContext foundContext = null;
		public string contextName;
		public bool isStart, isEnd;
		public DelimCtx(string delim, string name=null, string desc=null, string ctx=null, bool s=false, bool e=false) 
			: base(delim, name, desc) {
			contextName = ctx; isStart = s; isEnd = e;
		}
	}
	public class Delim : IComparable<Delim> {
		public string text, name, description;
		public Func<string, int, ParseResult> parseRule = null;
		public Delim(string delim, string name=null, string desc=null, Func<string, int, ParseResult> parseRule = null) {
			this.text = delim;this.name = name;description = desc;this.parseRule = parseRule;
		}
		public bool IsAt(string str, int index) {
			if (index + text.Length > str.Length) { return false; }
			for(int i = 0; i < text.Length; ++i) {
				if (text[i] != str[index + i]) return false;
			}
			return true;
		}
		public override string ToString() { return text; }
		public static implicit operator Delim(string s) => new Delim(s);

		public static Delim[] _string_delimiter = new Delim[] { new DelimCtx("\"", ctx:"string",s:true,e:true), };
		public static Delim[] _char_delimiter = new Delim[] { new DelimCtx("\'", ctx:"char",s:true,e:true), };
		public static Delim[] _char_escape_sequence = new Delim[] { new Delim("\\", parseRule: StringEscape) };
		public static Delim[] _expression_delimiter = new Delim[] { new DelimCtx("(", ctx:"()",s:true), new DelimCtx(")", ctx:"()",e:true) };
		public static Delim[] _code_body_delimiter = new Delim[] { new DelimCtx("{", ctx:"{}",s:true), new DelimCtx("}", ctx:"{}",e:true) };
		public static Delim[] _square_brace_delimiter = new Delim[] { new DelimCtx("[", ctx:"[]",s:true), new DelimCtx("]", ctx:"[]",e:true) };
		public static Delim[] _triangle_brace_delimiter = new Delim[] { new DelimCtx("<", ctx:"<>",s:true), new DelimCtx(">", ctx:"<>",e:true) };
		public static Delim[] _ternary_operator_delimiter = new Delim[] { "?", ":", "??" };
		public static Delim[] _instruction_finished_delimiter = new Delim[] { ";" };
		public static Delim[] _membership_operator = new Delim[] { new Delim(".","member"), new Delim("->","pointee"), new Delim("::","scope resolution"), new Delim("?.","null conditional") };
		public static Delim[] _prefix_unary_operator = new Delim[] { "++", "--", "!", "-", "~" };
		public static Delim[] _postfix_unary_operator = new Delim[] { "++", "--" };
		public static Delim[] _binary_operator = new Delim[] { "&", "|", "<<", ">>", "^" };
		public static Delim[] _binary_logic_operatpor = new Delim[] { "==","!=","<",">","<=",">=", "&&", "||" };
		public static Delim[] _assignment_operator = new Delim[] { "+=", "-=", "*=", "/=", "%=", "|=", "&=", "<<=", ">>=", "??=", "=" };
		public static Delim[] _lambda_operator = new Delim[] { "=>" };
		public static Delim[] _math_operator = new Delim[] { "+", "-", "*", "/", "%" };
		public static Delim[] _block_comment_delimiter = new Delim[] { new DelimCtx("/*",ctx:"/**/",s:true), new DelimCtx("*/",ctx:"/**/",e:true) };
		public static Delim[] _line_comment_delimiter = new Delim[] { new DelimCtx("//",ctx:"//",s:true) };
		public static Delim[] _XML_line_comment_delimiter = new Delim[] { new DelimCtx("///",ctx:"///",s:true) };
		public static Delim[] _end_of_line_comment = new Delim[] { new DelimCtx("\n",ctx:"//",e:true) };
		public static Delim[] _end_of_XML_line_comment = new Delim[] { new DelimCtx("\n",ctx:"///",e:true) };
		public static Delim[] _line_comment_continuation = new Delim[] { new Delim("\\", parseRule: CommentEscape) };
		public static Delim[] _DelimitersNone = new Delim[] { };
		public static char[] WhitespaceDefault = new char[] { ' ', '\t', '\n', '\r' };
		public static char[] WhitespaceNone = new char[] { };

		public static Delim[] CharLiteralDelimiters = CombinedDelimiterList(_char_escape_sequence, _char_delimiter);
		public static Delim[] StringLiteralDelimiters = CombinedDelimiterList(_char_escape_sequence, _string_delimiter);
		public static Delim[] StandardDelimiters = CombinedDelimiterList(_string_delimiter, _char_delimiter, _expression_delimiter, _code_body_delimiter, 
			_square_brace_delimiter, _ternary_operator_delimiter, _instruction_finished_delimiter, _membership_operator, _prefix_unary_operator, _binary_operator,
			_binary_logic_operatpor, _assignment_operator, _lambda_operator, _math_operator, _block_comment_delimiter, _line_comment_delimiter);
		public static Delim[] LineCommentDelimiters = CombinedDelimiterList(_line_comment_continuation, _end_of_line_comment);
		public static Delim[] XmlCommentDelimiters = CombinedDelimiterList(_line_comment_continuation, _end_of_XML_line_comment);
		public static Delim[] CommentBlockDelimiters = CombinedDelimiterList(_block_comment_delimiter);

		public static Delim[] CombinedDelimiterList(params Delim[][] delimGroups) {
			List<Delim> delims = new List<Delim>();
			for(int i = 0; i < delimGroups.Length; ++i) { delims.AddRange(delimGroups[i]); }
			delims.Sort();
			return delims.ToArray();
		}

		static Delim() {
			void GiveDesc(Delim[] delims, string desc) {
				for(int i = 0; i < delims.Length; ++i) { if(delims[i].description == null) { delims[i].description = desc; } }
			}
			Type t = typeof(Delim);
			MemberInfo[] mInfo = t.GetMembers();
			for(int i = 0; i < mInfo.Length; ++i) {
				MemberInfo mi = mInfo[i];
				if (mi.Name.StartsWith("_") && mi is FieldInfo fi && fi.FieldType == typeof(Delim[]) && fi.IsStatic) {
					Delim[] delims = fi.GetValue(null) as Delim[];
					GiveDesc(delims, fi.Name.Substring(1).Replace('_',' '));
				}
			}
		}

		public int CompareTo(Delim other) {
			if (text.Length > other.text.Length) return -1;
			if (text.Length < other.text.Length) return 1;
			return text.CompareTo(other.text);
		}

		public static ParseResult CommentEscape(string str, int index) {
			return StringEscape(str, index);
		}
		public static ParseResult StringEscape(string str, int index) {
			ParseResult r = new ParseResult(0, null); // by default, nothing happened
			if(str.Length <= index) {
				r.error = "invalid arguments";
				return r;
			}
			if(str.Length <= index + 1) {
				r.error = "unable to parse escape sequence at end of string";
				return r;
			}
			char c = str[index + 1];
			int Hex(char c) {
				switch (c) {
				case '0': return 0; case '1': return 1; case '2': return 2; case '3': return 3; case '4': return 4;
				case '5': return 5; case '6': return 6; case '7': return 7; case '8': return 8; case '9': return 9;
				case 'A': return 10; case 'B': return 11; case 'C': return 12; case 'D': return 13; case 'E': return 14; case 'F': return 15;
				case 'a': return 10; case 'b': return 11; case 'c': return 12; case 'd': return 13; case 'e': return 14; case 'f': return 15;
				}
				return -1;
			}
			ParseResult ReadFunnyNumber(int index, string str, int expectedValues, int numberBase, int alreadyRead = 2) {
				int value = 0, h = 0;
				for (int i = 0; i < expectedValues; ++i) {
					if (str.Length <= index + i || (h = Hex(str[index + i])) < 0 || h >= numberBase) {
						return new ParseResult(index, "", "expected base"+numberBase+" value #"+(i+1));
					}
					value += h * (int)Math.Pow(numberBase, expectedValues-1-i);
				}
				return new ParseResult(alreadyRead+expectedValues, ((char)value).ToString());
			}
			switch (c) {
			case '\n': return new ParseResult(index + 2, "");
			case '\r':
				if (str.Length <= index + 2 || str[index + 2] != '\n') {
					return new ParseResult(index, "", "expected windows line ending");
				}
				return new ParseResult(index + 3, "");
			case 'a': return new ParseResult(2, "\a");
			case 'b': return new ParseResult(2, "\b");
			case 'e': return new ParseResult(2, ((char)27).ToString());
			case 'r': return new ParseResult(2, "\r");
			case 'f': return new ParseResult(2, "\f");
			case 'n': return new ParseResult(2, "\n");
			case 't': return new ParseResult(2, "\t");
			case 'v': return new ParseResult(2, "\v");
			case '\\': return new ParseResult(2, "\\");
			case '\'': return new ParseResult(2, "\'");
			case '\"': return new ParseResult(2, "\"");
			case '?': return new ParseResult(2, "?");
			case 'x': return ReadFunnyNumber(index + 2, str, 2, 16);
			case 'u': return ReadFunnyNumber(index + 2, str, 4, 16);
			case 'U': return ReadFunnyNumber(index + 2, str, 8, 16);
			case '0': case '1': case '2': case '3': case '4': case '5': case '6': case '7': {
				int digitCount = 1;
				do {
					if (str.Length <= index + digitCount +1) break;
					c = str[index + digitCount + 1];
					if (c < '0' || c > '7') break;
					++digitCount;
				} while (digitCount < 3);
				return ReadFunnyNumber(index + 1, str, digitCount, 8, 1);
			}
			}
			r.error = "unknown escape sequence";
			return r;
		}
	}
	public class ParseContext {
		public string name = "default";
		public char[] whitespace = Delim.WhitespaceDefault;
		public Delim[] delimiters = Delim.StandardDelimiters;

		public ParseContext(string name) {
			this.name = name;
			allContexts[name] = this;
		}

		public int IndexOfDelimeterAt(string str, int index) {
			for (int i = 0; i < delimiters.Length; ++i) {
				if (delimiters[i].IsAt(str, index)) { return i; }
			}
			return -1;
		}
		public Delim GetDelimiterAt(string str, int index) {
			int i = IndexOfDelimeterAt(str, index);
			if (i < 0) return null;
			return delimiters[i];
		}

		public static Dictionary<string, ParseContext> allContexts = new Dictionary<string, ParseContext>();

		public static ParseContext
			Default = new ParseContext("default"),
			String = new ParseContext("string"),
			Char = new ParseContext("char"),
			Expression = new ParseContext("()"),
			SquareBrace = new ParseContext("[]"),
			GenericArgs = new ParseContext("<>"),
			XmlCommentLine = new ParseContext("///"),
			CommentLine = new ParseContext("//"),
			CommentBlock = new ParseContext("/**/"),
			CodeBody = new ParseContext("{}");
		static ParseContext() {
			XmlCommentLine.delimiters = Delim.XmlCommentDelimiters;
			CommentLine.delimiters = Delim.LineCommentDelimiters;
			CommentBlock.delimiters = Delim.CommentBlockDelimiters;
		}
		public class Entry {
			public ParseContext context = null;
			public Entry parent = null;
			public Token begin = Token.None, end = Token.None;
			public int depth;
			public string fulltext;
			public string Text => fulltext.Substring(IndexBegin, Length);
			public bool IsText => context == String || context == Char;
			public bool IsComment => context == CommentLine || context == XmlCommentLine || context == CommentBlock;
			public int IndexBegin => begin.index;
			public int IndexEnd => end.index + end.length;
			public int Length => IndexEnd - begin.index;
		}
		public Entry GetEntry(Token begin, string text, int depth, ParseContext.Entry parent = null) {
			return new Entry { context = this, begin = begin, fulltext = text, depth=depth, parent=parent };
		}
	}
	class CodeParse {
		public static int Tokens(string str, List<Token> tokens, ParseContext a_context = null, int index = 0, List<int> indexOfNewRow = null) {
			if (a_context == null) a_context = ParseContext.Default;
			int tokenBegin = -1, tokenEnd = -1;
			List<ParseContext.Entry> contextStack = new List<ParseContext.Entry>();
			ParseContext currentContext = a_context;
			while(index < str.Length) {
				char c = str[index];
				Delim delim = currentContext.GetDelimiterAt(str, index);
				if(delim != null) {
					object meta = null;
					//Token delimToken = new Token(str.Substring(index, delim.delim.Length), row+1, col+1, index, delim);
					Token delimToken = new Token(delim, index, delim.text.Length);
					if (tokenBegin >= 0 && tokenEnd < 0) {
						tokenEnd = index;
						int len = tokenEnd - tokenBegin;
						tokens.Add(new Token(str, tokenBegin, len));
						tokenBegin = tokenEnd = -1;
					}
					if (delim.parseRule != null) {
						ParseResult pr = delim.parseRule.Invoke(str, index);
						if (pr.replacementString != null) {
							delimToken.length = pr.lengthParsed;
							delimToken.meta = new StringSubstitution(str, pr.replacementString);
						}
						index += pr.lengthParsed - 1;
					} else {
						index += delim.text.Length - 1;
					}
					if (delim is DelimCtx dcx) {
						bool endProcessed = false;
						if(contextStack.Count > 0 && dcx.Context == currentContext && dcx.isEnd) {
							ParseContext.Entry endingContext = contextStack[contextStack.Count - 1];
							//Console.WriteLine("leaving " + endingContext.context.name);
							delimToken.meta = endingContext;
							endingContext.end = delimToken;
							contextStack.RemoveAt(contextStack.Count - 1);
							if (contextStack.Count > 0) {
								currentContext = contextStack[contextStack.Count - 1].context;
							} else {
								currentContext = a_context;
							}
							endProcessed = true;
						}
						if(!endProcessed && dcx.isStart) {
							ParseContext.Entry newContext = dcx.Context.GetEntry(delimToken, str, contextStack.Count+1);
							if(contextStack.Count > 0) { newContext.parent = contextStack[contextStack.Count - 1]; }
							currentContext = dcx.Context;
							delimToken.meta = newContext;
							contextStack.Add(newContext);
							//Console.WriteLine("going into " + dcx.Context.name);
						}
					}
					tokens.Add(delimToken);
				} else if (Array.IndexOf(currentContext.whitespace, c) < 0) {
					if(tokenBegin < 0) { tokenBegin = index; }
				} else {
					if(tokenEnd < 0 && tokenBegin >= 0) {
						tokenEnd = index;
						int len = tokenEnd - tokenBegin;
						//tokens.Add(new Token(str.Substring(tokenBegin, len), row+1, col+1 - len));
						tokens.Add(new Token(str, tokenBegin, len));
						tokenBegin = tokenEnd = -1;
					}
				}
				++index;
				if (indexOfNewRow != null && c == '\n') {
					indexOfNewRow.Add(index);
				}
			}
			return index;
		}
		public static void FilePositionOf(Token token, List<int> indexOfNewRow, out int row, out int col) {
			row = indexOfNewRow.BinarySearch(token.index);
			if(row < 0) { row = ~row; }
			int rowStart = row > 0 ? indexOfNewRow[row - 1] : 0;
			col = token.index - rowStart;
		}
		public static string FilePositionOf(Token token, List<int> indexOfNewRow) {
			FilePositionOf(token, indexOfNewRow, out int row, out int col);
			return (row+1) + "," + (col+1);
		}
		public static string ResolveString(string str) {
			ParseResult parse;
			StringBuilder sb = new StringBuilder();
			int stringStarted = 0;
			for (int i = 0; i < str.Length; ++i) {
				char c = str[i];
				if (c == '\\') {
					sb.Append(str.Substring(stringStarted, i - stringStarted));
					parse = Delim.StringEscape(str, i);
					if (parse.error != null) {
						Console.ForegroundColor = ConsoleColor.Red;
						Console.WriteLine("@" + i + ": " + parse.error);
					}
					if (parse.replacementString != null) {
						sb.Append(parse.replacementString);
					}
					Console.WriteLine("replacing " + str.Substring(i, parse.lengthParsed) + " with " + parse.replacementString);
					stringStarted = i + parse.lengthParsed;
					i = stringStarted - 1;
				}
			}
			sb.Append(str.Substring(stringStarted, str.Length - stringStarted));
			return sb.ToString();
		}
	}
}
