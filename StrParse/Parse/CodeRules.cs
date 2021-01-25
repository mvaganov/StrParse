using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace NonStandard.Data.Parse {
	class CodeRules {

		public static Context
			String, Char, Number, Hexadecimal, Expression, SquareBrace, GenericArgs, CodeBody,
			Sum, Difference, Product, Quotient, Modulus, Power, LogicalAnd, LogicalOr,
			Assignment, Equal, LessThan, GreaterThan, LessThanOrEqual, GreaterThanOrEqual,
			NotEqual, XmlCommentLine, CommentLine, CommentBlock, Default;

		public static Delim[] _string_delimiter = new Delim[] { new DelimCtx("\"", ctx: "string", s: true, e: true), };
		public static Delim[] _char_delimiter = new Delim[] { new DelimCtx("\'", ctx: "char", s: true, e: true), };
		public static Delim[] _char_escape_sequence = new Delim[] { new Delim("\\", parseRule: UnescapeString) };
		public static Delim[] _expression_delimiter = new Delim[] { new DelimCtx("(", ctx: "()", s: true), new DelimCtx(")", ctx: "()", e: true) };
		public static Delim[] _code_body_delimiter = new Delim[] { new DelimCtx("{", ctx: "{}", s: true), new DelimCtx("}", ctx: "{}", e: true) };
		public static Delim[] _square_brace_delimiter = new Delim[] { new DelimCtx("[", ctx: "[]", s: true), new DelimCtx("]", ctx: "[]", e: true) };
		public static Delim[] _triangle_brace_delimiter = new Delim[] { new DelimCtx("<", ctx: "<>", s: true), new DelimCtx(">", ctx: "<>", e: true) };
		public static Delim[] _ternary_operator_delimiter = new Delim[] { "?", ":", "??" };
		public static Delim[] _instruction_finished_delimiter = new Delim[] { ";" };
		public static Delim[] _list_item_delimiter = new Delim[] { "," };
		public static Delim[] _membership_operator = new Delim[] { new Delim(".", "member"), new Delim("->", "pointee"), new Delim("::", "scope resolution"), new Delim("?.", "null conditional") };
		public static Delim[] _prefix_unary_operator = new Delim[] { "++", "--", "!", "-", "~" };
		public static Delim[] _postfix_unary_operator = new Delim[] { "++", "--" };
		public static Delim[] _binary_operator = new Delim[] { "&", "|", "<<", ">>", "^" };
		// https://en.wikipedia.org/wiki/Order_of_operations#:~:text=In%20mathematics%20and%20computer%20programming,evaluate%20a%20given%20mathematical%20expression.
		public static Delim[] _binary_logic_operatpor = new DelimOp[] {
			new DelimOp("==",syntax:CodeRules.opinit_equ,resolve:CodeRules.op_equ, order:70),
			new DelimOp("!=",syntax:CodeRules.opinit_neq,resolve:CodeRules.op_neq, order:71),
			new DelimOp("<", syntax:CodeRules.opinit_lt_,resolve:CodeRules.op_lt_, order:60),
			new DelimOp(">", syntax:CodeRules.opinit_gt_,resolve:CodeRules.op_gt_, order:61),
			new DelimOp("<=",syntax:CodeRules.opinit_lte,resolve:CodeRules.op_lte, order:62),
			new DelimOp(">=",syntax:CodeRules.opinit_gte,resolve:CodeRules.op_gte, order:63),
			new DelimOp("&&",syntax:CodeRules.opinit_and,resolve:CodeRules.op_and, order:110),
			new DelimOp("||",syntax:CodeRules.opinit_or_,resolve:CodeRules.op_or_, order:120)
		};
		public static Delim[] _assignment_operator = new Delim[] { "+=", "-=", "*=", "/=", "%=", "|=", "&=", "<<=", ">>=", "??=", "=" };
		public static Delim[] _lambda_operator = new Delim[] { "=>" };
		public static Delim[] _math_operator = new DelimOp[] {
			new DelimOp("+", syntax:CodeRules.opinit_add,resolve:CodeRules.op_add, order:40),
			new DelimOp("-", syntax:CodeRules.opinit_dif,resolve:CodeRules.op_dif, order:41),
			new DelimOp("*", syntax:CodeRules.opinit_mul,resolve:CodeRules.op_mul, order:30),
			new DelimOp("/", syntax:CodeRules.opinit_div,resolve:CodeRules.op_div, order:31),
			new DelimOp("%", syntax:CodeRules.opinit_mod,resolve:CodeRules.op_mod, order:32),
			new DelimOp("^^",syntax:CodeRules.opinit_pow,resolve:CodeRules.op_pow, order:20),
		};
		public static Delim[] _hex_number_prefix = new Delim[] { new DelimCtx("0x", ctx: "0x", parseRule: HexadecimalParse) };
		public static Delim[] _number = new Delim[] {
			new DelimCtx("-",ctx:"number",parseRule:NumericParse,addReq:IsNextCharacterBase10NumericOrDecimal),
			new DelimCtx(".",ctx:"number",parseRule:NumericParse,addReq:IsNextCharacterBase10Numeric),
			new DelimCtx("0",ctx:"number",parseRule:NumericParse),
			new DelimCtx("1",ctx:"number",parseRule:NumericParse),
			new DelimCtx("2",ctx:"number",parseRule:NumericParse),
			new DelimCtx("3",ctx:"number",parseRule:NumericParse),
			new DelimCtx("4",ctx:"number",parseRule:NumericParse),
			new DelimCtx("5",ctx:"number",parseRule:NumericParse),
			new DelimCtx("6",ctx:"number",parseRule:NumericParse),
			new DelimCtx("7",ctx:"number",parseRule:NumericParse),
			new DelimCtx("8",ctx:"number",parseRule:NumericParse),
			new DelimCtx("9",ctx:"number",parseRule:NumericParse) };
		public static Delim[] _block_comment_delimiter = new Delim[] { new DelimCtx("/*", ctx: "/**/", s: true), new DelimCtx("*/", ctx: "/**/", e: true) };
		public static Delim[] _line_comment_delimiter = new Delim[] { new DelimCtx("//", ctx: "//", s: true) };
		public static Delim[] _XML_line_comment_delimiter = new Delim[] { new DelimCtx("///", ctx: "///", s: true) };
		public static Delim[] _end_of_line_comment = new Delim[] { new DelimCtx("\n", ctx: "//", e: true, printable: false), new DelimCtx("\r", ctx: "//", e: true,printable:false) };
		public static Delim[] _erroneous_end_of_string = new Delim[] { new DelimCtx("\n", ctx: "string", e: true, printable: false), new DelimCtx("\r", ctx: "string", e: true, printable: false) };
		public static Delim[] _end_of_XML_line_comment = new Delim[] { new DelimCtx("\n", ctx: "///", e: true, printable: false), new DelimCtx("\r", ctx: "///", e: true, printable: false) };
		public static Delim[] _line_comment_continuation = new Delim[] { new Delim("\\", parseRule: CommentEscape) };
		public static Delim[] _data_keyword = new Delim[] { "null", "true", "false", "bool", "int", "short", "string", "long", "byte",
			"float", "double", "uint", "ushort", "sbyte", "char", "if", "else", "void", "var", "new", "as", };
		public static Delim[] _data_c_sharp_keyword = new Delim[] {
			"abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked", "class",
			"const", "continue", "decimal", "default", "delegate", "do", "double", "else", "enum", "event",
			"explicit", "extern", "false", "finally", "fixed", "float", "for", "foreach", "goto", "if",
			"implicit", "in", "int", "interface", "internal", "is", "lock", "long", "namespace", "new", "null",
			"object", "operator", "out", "override", "params", "private", "protected", "public", "readonly",
			"ref", "return", "sbyte", "sealed", "short", "sizeof", "stackalloc", "static", "string", "struct",
			"switch", "this", "throw", "true", "try", "typeof", "uint", "ulong", "unchecked", "unsafe",
			"ushort", "using", "virtual", "void", "volatile", "while"
		};
		public static Delim[] None = new Delim[] { };
		public static char[] WhitespaceDefault = new char[] { ' ', '\t', '\n', '\r' };
		public static char[] WhitespaceNone = new char[] { };

		public static Delim[] CharLiteralDelimiters = CombineDelims(_char_escape_sequence, _char_delimiter);
		public static Delim[] StringLiteralDelimiters = CombineDelims(_char_escape_sequence, _string_delimiter, _erroneous_end_of_string);
		public static Delim[] StandardDelimiters = CombineDelims(_string_delimiter, _char_delimiter,
			_expression_delimiter, _code_body_delimiter, _square_brace_delimiter, _ternary_operator_delimiter,
			_instruction_finished_delimiter, _list_item_delimiter, _membership_operator, _prefix_unary_operator,
			_binary_operator, _binary_logic_operatpor, _assignment_operator, _lambda_operator, _math_operator,
			_block_comment_delimiter, _line_comment_delimiter, _number);
		public static Delim[] LineCommentDelimiters = CombineDelims(_line_comment_continuation, _end_of_line_comment);
		public static Delim[] XmlCommentDelimiters = CombineDelims(_line_comment_continuation,
			_end_of_XML_line_comment);
		public static Delim[] CommentBlockDelimiters = CombineDelims(_block_comment_delimiter);

		static CodeRules() {
			Default = new Context("default");
			String = new Context("string");
			Char = new Context("char");
			Number = new Context("number");
			Hexadecimal = new Context("0x");
			Expression = new Context("()");
			SquareBrace = new Context("[]");
			GenericArgs = new Context("<>");
			CodeBody = new Context("{}");
			Sum = new Context("sum", CodeRules.None);
			Difference = new Context("difference", CodeRules.None);
			Product = new Context("product", CodeRules.None);
			Quotient = new Context("quotient", CodeRules.None);
			Modulus = new Context("modulus", CodeRules.None);
			Power = new Context("power", CodeRules.None);
			LogicalAnd = new Context("logical and", CodeRules.None);
			LogicalOr = new Context("logical or", CodeRules.None);
			Assignment = new Context("assignment", CodeRules.None);
			Equal = new Context("equal", CodeRules.None);
			LessThan = new Context("less than", CodeRules.None);
			GreaterThan = new Context("greater than", CodeRules.None);
			LessThanOrEqual = new Context("less than or equal", CodeRules.None);
			GreaterThanOrEqual = new Context("greater than or equal", CodeRules.None);
			NotEqual = new Context("not equal", CodeRules.None);
			XmlCommentLine = new Context("///");
			CommentLine = new Context("//");
			CommentBlock = new Context("/**/");

			XmlCommentLine.delimiters = CodeRules.XmlCommentDelimiters;
			CommentLine.delimiters = CodeRules.LineCommentDelimiters;
			CommentBlock.delimiters = CodeRules.CommentBlockDelimiters;
			CommentLine.whitespace = CodeRules.WhitespaceNone;
			String.whitespace = CodeRules.WhitespaceNone;
			String.delimiters = CodeRules.StringLiteralDelimiters;
			//Char.whitespace = CodeRules.WhitespaceNone;
			//Char.delimiters = CodeRules.StringLiteralDelimiters;
			Number.whitespace = CodeRules.WhitespaceNone;

			Type t = typeof(CodeRules);
			MemberInfo[] mInfo = t.GetMembers();
			for (int i = 0; i < mInfo.Length; ++i) {
				MemberInfo mi = mInfo[i];
				FieldInfo fi = mi as FieldInfo;
				if (fi != null && mi.Name.StartsWith("_") && fi.FieldType == typeof(Delim[]) && fi.IsStatic) {
					Delim[] delims = fi.GetValue(null) as Delim[];
					GiveDesc(delims, fi.Name.Substring(1).Replace('_', ' '));
				}
			}

			//StringBuilder sb = new StringBuilder();
			//for (int i = 0; i < CodeRules.StandardDelimiters.Length; ++i) {
			//	sb.Append(CodeRules.StandardDelimiters[i].text + " " + CodeRules.StandardDelimiters[i].description).Append("\n");
			//}
			//Show.Log(sb);
		}
		public static bool IsNextCharacterBase10NumericOrDecimal(string str, int index) {
			if (index < -1 || index + 1 >= str.Length) return false;
			char c = str[index + 1]; if (c == '.') return true;
			int i = NumericValue(c); return (i >= 0 && i <= 9);
		}
		public static bool IsNextCharacterBase10Numeric(string str, int index) {
			if (index < -1 || index + 1 >= str.Length) return false;
			int i = NumericValue(str[index + 1]); return (i >= 0 && i <= 9);
		}
		public static ParseResult HexadecimalParse(string str, int index) {
			return NumberParse(str, index + 2, 16, false);
		}
		public static ParseResult NumericParse(string str, int index) {
			return NumberParse(str, index, 10, true);
		}
		public static ParseResult IntegerParse(string str, int index) {
			return NumberParse(str, index, 10, false);
		}
		public static int NumericValue(char c) {
			if (c >= '0' && c <= '9') return c - '0';
			if (c >= 'A' && c <= 'Z') return (c - 'A') + 10;
			if (c >= 'a' && c <= 'z') return (c - 'a') + 10;
			return -1;
		}
		public static bool IsValidNumber(char c, int numberBase) {
			int h = NumericValue(c);
			return h >= 0 && h < numberBase;
		}
		public static int CountNumericCharactersAt(string str, int index, int numberBase, bool includeNegativeSign, bool includeDecimal) {
			int numDigits = 0;
			bool foundDecimal = false;
			while (index + numDigits < str.Length) {
				char c = str[index + numDigits];
				bool stillGood = false;
				if (IsValidNumber(c, numberBase)) {
					stillGood = true;
				} else {
					if (includeNegativeSign && numDigits == 0 && c == '-') {
						stillGood = true;
					} else if (includeDecimal && c == '.' && !foundDecimal) {
						foundDecimal = true;
						stillGood = true;
					}
				}
				if (stillGood) { numDigits++; } else break;
			}
			return numDigits;
		}
		public static ParseResult NumberParse(string str, int index, int numberBase, bool includeDecimal) {
			return NumberParse(str, index, CountNumericCharactersAt(str, index, numberBase, true, true), numberBase, includeDecimal);
		}
		public static ParseResult NumberParse(string str, int index, int characterCount, int numberBase, bool includeDecimal) {
			ParseResult pr = new ParseResult(0, null);
			long sum = 0;
			char c = str[index];
			bool isNegative = c == '-';
			if (isNegative) { ++index; }
			bool isDecimal = c == '.';
			int numDigits;
			if (!isDecimal) {
				numDigits = CountNumericCharactersAt(str, index, numberBase, false, false);
				int b = 1, onesPlace = index + numDigits - 1;
				for (int i = 0; i < numDigits; ++i) {
					sum += NumericValue(str[onesPlace - i]) * b;
					b *= numberBase;
				}
				if (isNegative) sum *= -1;
				pr.replacementValue = (sum < int.MaxValue) ? (int)sum : sum;
				index += numDigits;
			}
			++index;
			double fraction = 0;
			if (includeDecimal && index < str.Length && str[index - 1] == '.') {
				numDigits = CountNumericCharactersAt(str, index, numberBase, false, false);
				if (numDigits == 0) { pr.SetError("decimal point with no subsequent digits", 0, index); }
				long b = numberBase;
				for (int i = 0; i < numDigits; ++i) {
					fraction += NumericValue(str[index + i]) / (double)b;
					b *= numberBase;
				}
				if (isNegative) fraction *= -1;
				pr.replacementValue = (sum + fraction);
			}
			pr.lengthParsed = characterCount;
			return pr;
		}
		public static ParseResult CommentEscape(string str, int index) { return UnescapeString(str, index); }

		public static ParseResult UnescapeString(string str, int index) {
			ParseResult r = new ParseResult(0, null); // by default, nothing happened
			if (str.Length <= index) { return r.SetError("invalid arguments"); }
			if (str[index] != '\\') { return r.SetError("expected escape sequence starting with '\\'"); }
			if (str.Length <= index + 1) { return r.SetError("unable to parse escape sequence at end of string", 0, 1); }
			char c = str[index + 1];
			switch (c) {
			case '\n': return new ParseResult(index + 2, "");
			case '\r':
				if (str.Length <= index + 2 || str[index + 2] != '\n') {
					return new ParseResult(index, "", "expected windows line ending", 0, 2);
				}
				return new ParseResult(index + 3, "");
			case 'a': return new ParseResult(2, "\a");
			case 'b': return new ParseResult(2, "\b");
			case 'e': return new ParseResult(2, ((char)27).ToString());
			case 'f': return new ParseResult(2, "\f");
			case 'r': return new ParseResult(2, "\r");
			case 'n': return new ParseResult(2, "\n");
			case 't': return new ParseResult(2, "\t");
			case 'v': return new ParseResult(2, "\v");
			case '\\': return new ParseResult(2, "\\");
			case '\'': return new ParseResult(2, "\'");
			case '\"': return new ParseResult(2, "\"");
			case '?': return new ParseResult(2, "?");
			case 'x': return NumberParse(str, index + 2, 2, 16, false).AddToLength(2).ForceCharSubstitute();
			case 'u': return NumberParse(str, index + 2, 4, 16, false).AddToLength(2).ForceCharSubstitute();
			case 'U': return NumberParse(str, index + 2, 8, 16, false).AddToLength(2).ForceCharSubstitute();
			case '0':
			case '1':
			case '2':
			case '3':
			case '4':
			case '5':
			case '6':
			case '7': {
				int digitCount = 1;
				do {
					if (str.Length <= index + digitCount + 1) break;
					c = str[index + digitCount + 1];
					if (c < '0' || c > '7') break;
					++digitCount;
				} while (digitCount < 3);
				return NumberParse(str, index + 1, digitCount, 8, false).AddToLength(1);
			}
			}
			return r.SetError("unknown escape sequence", 0, 1);
		}

		private static void GiveDesc(Delim[] delims, string desc) {
			for (int i = 0; i < delims.Length; ++i) { if (delims[i].description == null) { delims[i].description = desc; } }
		}
		public static Delim[] CombineDelims(params Delim[][] delimGroups) {
			List<Delim> delims = new List<Delim>();
			for (int i = 0; i < delimGroups.Length; ++i) { delims.AddRange(delimGroups[i]); }
			delims.Sort();
			return delims.ToArray();
		}

		public static Context.Entry opinit_Binary(List<Token> tokens, Tokenizer tok, int index, string contextName) {
			Token t = tokens[index];
			Context.Entry e = tokens[index].GetAsContextEntry();
			if (e != null) {
				if (e.context.name != contextName) { throw new Exception(tok.AddError(t,
					"expected context: "+contextName+", found "+e.context.name).ToString()); }
				return e;
			}
			if (index - 1 < 0) { tok.AddError(t, "missing left operand"); return null; }
			if (index + 1 >= tokens.Count) { tok.AddError(t, "missing right operand"); return null; }
			Context foundContext; Context.allContexts.TryGetValue(contextName, out foundContext);
			if (foundContext == null) { throw new Exception(tok.AddError(t, "context '" + contextName + "' does not exist").ToString()); }
			Context.Entry parent = null; int pIndex;
			for (pIndex = 0; pIndex < tokens.Count; ++pIndex) {
				e = tokens[pIndex].GetAsContextEntry();
				if(e != null && e.tokens == tokens) { parent = e; break; }
			}
			if (pIndex == index) { throw new Exception(tok.AddError(t,"parent context recursion").ToString()); }
			e = foundContext.GetEntry(tokens, index - 1, t.meta, parent);
			e.tokenCount = 3;
			t.meta = e;
			tokens[index] = t;
			tok.ExtractContextAsSubTokenList(e);
			return e;
		}
		public static Context.Entry opinit_add(Tokenizer tok, List<Token> tokens, int index) { return opinit_Binary(tokens, tok, index, "sum"); }
		public static Context.Entry opinit_dif(Tokenizer tok, List<Token> tokens, int index) { return opinit_Binary(tokens, tok, index, "difference"); }
		public static Context.Entry opinit_mul(Tokenizer tok, List<Token> tokens, int index) { return opinit_Binary(tokens, tok, index, "product"); }
		public static Context.Entry opinit_div(Tokenizer tok, List<Token> tokens, int index) { return opinit_Binary(tokens, tok, index, "quotient"); }
		public static Context.Entry opinit_mod(Tokenizer tok, List<Token> tokens, int index) { return opinit_Binary(tokens, tok, index, "modulus"); }
		public static Context.Entry opinit_pow(Tokenizer tok, List<Token> tokens, int index) { return opinit_Binary(tokens, tok, index, "power"); }
		public static Context.Entry opinit_and(Tokenizer tok, List<Token> tokens, int index) { return opinit_Binary(tokens, tok, index, "logical and"); }
		public static Context.Entry opinit_or_(Tokenizer tok, List<Token> tokens, int index) { return opinit_Binary(tokens, tok, index, "logical or"); }
		public static Context.Entry opinit_asn(Tokenizer tok, List<Token> tokens, int index) { return opinit_Binary(tokens, tok, index, "assign"); }
		public static Context.Entry opinit_equ(Tokenizer tok, List<Token> tokens, int index) { return opinit_Binary(tokens, tok, index, "equal"); }
		public static Context.Entry opinit_neq(Tokenizer tok, List<Token> tokens, int index) { return opinit_Binary(tokens, tok, index, "not equal"); }
		public static Context.Entry opinit_lt_(Tokenizer tok, List<Token> tokens, int index) { return opinit_Binary(tokens, tok, index, "less than"); }
		public static Context.Entry opinit_gt_(Tokenizer tok, List<Token> tokens, int index) { return opinit_Binary(tokens, tok, index, "greater than"); }
		public static Context.Entry opinit_lte(Tokenizer tok, List<Token> tokens, int index) { return opinit_Binary(tokens, tok, index, "less than or equal"); }
		public static Context.Entry opinit_gte(Tokenizer tok, List<Token> tokens, int index) { return opinit_Binary(tokens, tok, index, "greater than or equal"); }

		public static void op_GetArg(Tokenizer tok, Token token, object scope, out object value, out Type type) {
			value = token.Resolve(tok, scope);
			type = (value != null) ? value.GetType() : null;
			if (scope == null || type == null) { return; } // no scope, or no data, easy. we're done.
			string memberName = value as string;
			if(memberName == null) { return; } // data not a string (and therefore can't be a reference to scope), also easy. done.
			Context.Entry e = token.GetAsContextEntry();
			if(e != null && e.IsText()) { return; } // data is a string, but also explicitly meant to be a string, done.
			// otherwise, we search for the data within the given context
			Type scopeType = scope.GetType();
			KeyValuePair<Type, Type> dType = scopeType.GetIDictionaryType();
			if(dType.Key != null) {
				IDictionary dict = scope as IDictionary;
				if (dict.Contains(value)) { value = dict[value]; } else { value = null; }
				type = (value != null) ? value.GetType() : null;
				return;
			}
			if (!memberName.StartsWith(Parser.Wildcard) && !memberName.EndsWith(Parser.Wildcard)) {
				FieldInfo field = scopeType.GetField(memberName);
				if (field != null) {
					value = field.GetValue(scope);
					type = (value != null) ? value.GetType() : null;
					return;
				}
				PropertyInfo prop = scopeType.GetProperty(memberName);
				if(prop != null) {
					value = prop.GetValue(scope);
					type = (value != null) ? value.GetType() : null;
					return;
				}
			} else {
				FieldInfo[] fields = scopeType.GetFields();
				string[] names = Array.ConvertAll(fields, f => f.ToString());
				int index = Parser.FindIndexWithWildcard(names, memberName, false);
				if(index >= 0) {
					value = fields[index].GetValue(scope);
					type = (value != null) ? value.GetType() : null;
					return;
				}
				PropertyInfo[] props = scopeType.GetProperties();
				names = Array.ConvertAll(props, p => p.ToString());
				index = Parser.FindIndexWithWildcard(names, memberName, false);
				if (index >= 0) {
					value = props[index].GetValue(scope);
					type = (value != null) ? value.GetType() : null;
					return;
				}
			}
		}
		public static void op_BinaryArgs(Tokenizer tok, Context.Entry e, object scope, out object left, out object right, out Type lType, out Type rType) {
			op_GetArg(tok, e.tokens[0], scope, out left, out lType);
			op_GetArg(tok, e.tokens[2], scope, out right, out rType);
		}
		public static object op_asn(Tokenizer tok, Context.Entry e, object scope) { return "="; }
		public static object op_mul(Tokenizer tok, Context.Entry e, object scope) {
			op_BinaryArgs(tok, e, scope, out object left, out object right, out Type lType, out Type rType);
			do {
				bool lString = lType == typeof(string);
				bool rString = rType == typeof(string);
				// if one of them is a string, there is some string multiplication logic to do!
				if (lString != rString) {
					string meaningfulString;
					double meaningfulNumber;
					if (lString) {
						if(!CodeConvert.IsConvertable(rType)) { break; }
						meaningfulString = left.ToString();
						CodeConvert.TryConvert(ref right, typeof(double));
						meaningfulNumber = (double)right;
					} else {
						if (!CodeConvert.IsConvertable(lType)) { break; }
						meaningfulString = right.ToString();
						CodeConvert.TryConvert(ref left, typeof(double));
						meaningfulNumber = (double)left;
					}
					StringBuilder sb = new StringBuilder();
					for (int i = 0; i < meaningfulNumber; ++i) {
						sb.Append(meaningfulString);
					}
					meaningfulNumber -= (int)meaningfulNumber;
					int count = (int)(meaningfulString.Length * meaningfulNumber);
					if (count > 0) {
						sb.Append(meaningfulString.Substring(0, count));
					}
					return sb.ToString();
				}
				if (CodeConvert.IsConvertable(lType) && CodeConvert.IsConvertable(rType)) {
					CodeConvert.TryConvert(ref left, typeof(double));
					CodeConvert.TryConvert(ref right, typeof(double));
					return ((double)left) * ((double)right);
				}
			} while (false);
			tok.AddError(e.tokens[1], "unable to multiply " + lType + " and " + rType);
			return e;
		}
		public static object op_add(Tokenizer tok, Context.Entry e, object scope) {
			op_BinaryArgs(tok, e, scope, out object left, out object right, out Type lType, out Type rType);
			if (lType == typeof(string) || rType == typeof(string)) { return left.ToString() + right.ToString(); }
			if (CodeConvert.IsConvertable(lType) && CodeConvert.IsConvertable(rType)) {
				CodeConvert.TryConvert(ref left, typeof(double));
				CodeConvert.TryConvert(ref right, typeof(double));
				return ((double)left) + ((double)right);
			}
			tok.AddError(e.tokens[1], "unable to add " + lType + " and " + rType + " : " + left + " + " + right);
			return e;
		}
		public static object op_dif(Tokenizer tok, Context.Entry e, object scope) {
			op_BinaryArgs(tok, e, scope, out object left, out object right, out Type lType, out Type rType);
			do {
				if (lType == typeof(string) || rType == typeof(string)) { break; }
				if (CodeConvert.IsConvertable(lType) && CodeConvert.IsConvertable(rType)) {
					CodeConvert.TryConvert(ref left, typeof(double));
					CodeConvert.TryConvert(ref right, typeof(double));
					return ((double)left) - ((double)right);
				}
			} while (false);
			tok.AddError(e.tokens[1], "unable to subtract " + lType + " and " + rType + " : " + left + " - " + right);
			return e;
		}
		public static object op_div(Tokenizer tok, Context.Entry e, object scope) {
			op_BinaryArgs(tok, e, scope, out object left, out object right, out Type lType, out Type rType);
			do {
				if (lType == typeof(string) || rType == typeof(string)) { break; }
				if (CodeConvert.IsConvertable(lType) && CodeConvert.IsConvertable(rType)) {
					CodeConvert.TryConvert(ref left, typeof(double));
					CodeConvert.TryConvert(ref right, typeof(double));
					return ((double)left) / ((double)right);
				}
			} while (false);
			tok.AddError(e.tokens[1], "unable to divide " + lType + " and " + rType + " : " + left + " / " + right);
			return e;
		}
		public static object op_mod(Tokenizer tok, Context.Entry e, object scope) {
			op_BinaryArgs(tok, e, scope, out object left, out object right, out Type lType, out Type rType);
			do {
				// TODO implement Python-like string token replacement
				if (lType == typeof(string) || rType == typeof(string)) { break; }
				if (CodeConvert.IsConvertable(lType) && CodeConvert.IsConvertable(rType)) {
					CodeConvert.TryConvert(ref left, typeof(double));
					CodeConvert.TryConvert(ref right, typeof(double));
					return ((double)left) % ((double)right);
				}
			} while (false);
			tok.AddError(e.tokens[1], "unable to modulo " + lType + " and " + rType + " : " + left + " % " + right);
			return e;
		}
		public static object op_pow(Tokenizer tok, Context.Entry e, object scope) {
			op_BinaryArgs(tok, e, scope, out object left, out object right, out Type lType, out Type rType);
			do {
				if (lType == typeof(string) || rType == typeof(string)) { break; }
				if (CodeConvert.IsConvertable(lType) && CodeConvert.IsConvertable(rType)) {
					CodeConvert.TryConvert(ref left, typeof(double));
					CodeConvert.TryConvert(ref right, typeof(double));
					return Math.Pow((double)left, (double)right);
				}
			} while (false);
			tok.AddError(e.tokens[1], "unable to exponent " + lType + " and " + rType + " : " + left + " ^^ " + right);
			return e;
		}
		public static bool op_reduceToBoolean(object obj, Type type) {
			if (obj == null) return false;
			if (type == typeof(string)) return ((string)obj).Length != 0;
			if(!CodeConvert.TryConvert(ref obj, typeof(double))) { return true; }
			double d = (double)obj;
			return d != 0;
		}
		public static object op_and(Tokenizer tok, Context.Entry e, object scope) {
			op_BinaryArgs(tok, e, scope, out object left, out object right, out Type lType, out Type rType);
			return op_reduceToBoolean(left, lType) && op_reduceToBoolean(right, rType);
		}
		public static object op_or_(Tokenizer tok, Context.Entry e, object scope) {
			op_BinaryArgs(tok, e, scope, out object left, out object right, out Type lType, out Type rType);
			return op_reduceToBoolean(left, lType) || op_reduceToBoolean(right, rType);
		}
		// spaceship operator
		public static bool op_Compare(Tokenizer tok, Context.Entry e, object scope, out int compareValue) {
			op_BinaryArgs(tok, e, scope, out object left, out object right, out Type lType, out Type rType);
			if (lType == rType) { return lType.TryCompare(left, right, out compareValue); }
			compareValue = 0;
			tok.AddError(e.tokens[1], "can't ("+lType+")" + left + " "+ e.tokens[1] + " " + right + "("+rType+")");
			return false;
		}
		public static object op_equ(Tokenizer tok, Context.Entry e, object scope) {
			if (op_Compare(tok, e, scope, out int compareValue)) { return compareValue == 0; }
			return e;
		}
		public static object op_neq(Tokenizer tok, Context.Entry e, object scope) {
			if (op_Compare(tok, e, scope, out int compareValue)) { return compareValue != 0; }
			return e;
		}
		public static object op_lt_(Tokenizer tok, Context.Entry e, object scope) {
			if (op_Compare(tok, e, scope, out int compareValue)) { return compareValue < 0; }
			return e;
		}
		public static object op_gt_(Tokenizer tok, Context.Entry e, object scope) {
			if (op_Compare(tok, e, scope, out int compareValue)) { return compareValue > 0; }
			return e;
		}
		public static object op_lte(Tokenizer tok, Context.Entry e, object scope) {
			if(op_Compare(tok, e, scope, out int compareValue)) { return compareValue <= 0; }
			return e;
		}
		public static object op_gte(Tokenizer tok, Context.Entry e, object scope) {
			if (op_Compare(tok, e, scope, out int compareValue)) { return compareValue >= 0; }
			return e;
		}
	}
}
