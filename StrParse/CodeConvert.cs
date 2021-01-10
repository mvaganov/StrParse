using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace NonStandard {
	public class CodeConvert {
		public struct Err {
			public int row, col;
			public string message;
			public Err(int r, int c, string m) { row = r; col = c; message = m; }
			public Err(Token token, IList<int> rows, string m) {
				CodeParse.FilePositionOf(token, rows, out row, out col);
				message = m;
			}
			public override string ToString() { return "@"+row+","+col+	": " + message; }
			public static Err None = default;
			public void OffsetBy(Token token, IList<int> rows) {
				CodeParse.FilePositionOf(token, rows, out int r, out int c); row += r; col += c;
			}
		}
		public static bool TryParse<T>(string text, out T data, List<Err> errors = null) {
			object value = null;
			bool result = TryParse(typeof(T), text, ref value, errors);
			data = (T)value;
			return result;
		}
		public static bool TryParse(Type type, string text, ref object data, List<Err> errors = null) {
			List<Token> tokens = new List<Token>();
			List<int> rows = new List<int>();
			CodeParse.Tokens(text, tokens, rows, errors);
			if (data == null) { data = GetNew(type); }
			return TryParse(type, tokens, ref data, rows, errors);
		}

		public static T GetNew<T>(string type) { return (T)Activator.CreateInstance(Type.GetType(type)); }
		public static object GetNew(Type t) { return Activator.CreateInstance(t); }

		public class Parser {
			/// current data being parsed
			object memberValue = null;
			/// the object being parsed into, the final result
			public object result;
			string[] fieldNames, propNames;
			FieldInfo[] fields;
			FieldInfo field = null;
			PropertyInfo[] props;
			PropertyInfo prop = null;
			/// the type that the result needs to be
			Type resultType;
			/// the type that the next value needs to be
			Type memberType = null;
			bool isVarPrimitiveType = false, isThisArrayType, isIList;
			List<object> listData = null;
			Token memberToken;
			IDictionary dict;
			IList<Token> tokens;
			IList<int> rows;
			List<Err> errors = null;
			int tokenIndex = 0;
			public void Init(Type type, IList<Token> a_tokens, object dataStructure, IList<int> rows, List<Err> errors) {
				resultType = type;
				tokens = a_tokens;
				result = dataStructure;
				this.rows = rows;
				this.errors = errors;
				fields = type.GetFields();
				props = type.GetProperties();
				memberType = null;
				Array.Sort(fields, (a, b) => a.Name.CompareTo(b.Name));
				Array.Sort(props, (a, b) => a.Name.CompareTo(b.Name));
				fieldNames = Array.ConvertAll(fields, f => f.Name);
				propNames = Array.ConvertAll(fields, p => p.Name);
				isVarPrimitiveType = false;
				memberType = GetIListType(type);
				isIList = memberType != null;
				isThisArrayType = type.IsArray || isIList;
				if (isThisArrayType) {
					if (!isIList) {
						memberType = type.GetElementType();
					}
					isVarPrimitiveType = false;
					if (memberType.IsArray) {
					} else {
						isVarPrimitiveType = IsPrimitiveType(memberType);
					}
					listData = new List<object>();
				} else {
					if (result == null) { result = GetNew(type); }
					KeyValuePair<Type, Type> kvp = GetIDictionaryType(type);
					if (kvp.Key != null) {
						dict = result as IDictionary;
					}
				}
			}

			public bool TryParse() {
				Token token = tokens[tokenIndex];
				if (token.IsContextBeginning) { ++tokenIndex; }
				for (; tokenIndex < tokens.Count; ++tokenIndex) {
					token = tokens[tokenIndex];
					if (token.IsContextBeginning && !token.ContextEntry.IsText) {
						if (memberType != null && isVarPrimitiveType) {
							if (errors != null) errors.Add(new Err(token, rows, "unexpected beginning of " + token.ContextEntry.context.name));
							return false;
						}
					}
					if (token.IsContextEnding) {
						//Console.Write("finished parsing " + token.ContextEntry.context.name);
						break;
					}
					if (!isThisArrayType) {
						if (memberType == null) {
							if (!GetMemberNameAndAssociatedType()) { return false; }
							if(memberValue == tokens) { memberValue = null; continue; }
						} else {
							if (!TryGetValue()) { return false; }
							if (memberValue == tokens) { continue; } // this is how TryGetValue communicates value ignore
							if (dict != null) {
								dict.Add(memberToken.Resolve(), memberValue);
							} else if (field != null) {
								field.SetValue(result, memberValue);
							} else if (prop != null) {
								prop.SetValue(result, memberValue);
							} else {
								throw new Exception("huh? how did we get here?");
							}
							field = null; prop = null; memberType = null;
						}
					} else {
						if (!TryGetValue()) { return false; }
						if (memberValue == tokens) { continue; }
						listData.Add(memberValue);
					}
				}
				if (isThisArrayType) {
					if (!isIList) {
						Array a = Array.CreateInstance(memberType, listData.Count);
						for (int i = 0; i < listData.Count; ++i) {
							a.SetValue(listData[i], i);
						}
						result = a;
					} else {
						this.result = GetNew(resultType);
						MethodInfo mi = resultType.GetMethod("Add", new Type[]{ memberType });
						for (int i = 0; i < listData.Count; ++i) {
							mi.Invoke(result, new object[]{ listData[i] });
						}
					}
				}
				return true;
			}

			public bool GetMemberNameAndAssociatedType() {
				memberToken = tokens[tokenIndex];
				string str = null;
				Context.Entry e = memberToken.ContextEntry;
				if (e != null) {
					if (dict == null) {
						if (e.IsText) {
							str = e.Text;
						} else {
							if (errors != null) errors.Add(new Err(memberToken, rows, "unable to parse member name for " + resultType));
						}
					}
					tokenIndex += e.tokenCount;
				} else {
					str = memberToken.AsBasicToken;
				}
				if (dict != null) { return true; }
				if (str == null) { memberValue = tokens; return true; }
				int index = Array.BinarySearch(fieldNames, str);
				if (index < 0) {
					index = Array.BinarySearch(propNames, str);
					if (index < 0) {
						if (errors != null) errors.Add(new Err(memberToken, rows, "could not find field or property \"" + str + "\" in " + resultType));
						return false;
					} else {
						prop = props[index];
						memberType = prop.PropertyType;
					}
				} else {
					field = fields[index];
					memberType = field.FieldType;
				}
				memberValue = null;
				if (memberType.IsArray) {
					isVarPrimitiveType = false;
				} else {
					isVarPrimitiveType = IsPrimitiveType(memberType);
				}
				return true;
			}

			public bool TryGetValue() {
				memberValue = null;
				Token token = tokens[tokenIndex];
				object meta = token.meta;
				switch (meta) {
				case Delim delim:
					switch (delim.text) {
					// skip these delimiters as though they were whitespace.
					case "=": case ":": case ",": break;
					default:
						if (errors != null) errors.Add(new Err(token, rows, "unexpected delimiter \"" + delim.text + "\""));
						return false;
					}
					memberValue = tokens;
					return true;
				case Context.Entry context:
					int indexAfterContext = tokenIndex + context.tokenCount;
					if (context.IsText) {
						memberValue = context.Text;
					} else if (!CodeConvert.TryParse(memberType, tokens.GetRange(tokenIndex, indexAfterContext - tokenIndex), ref memberValue, rows, errors)) {
						return false;
					}
					tokenIndex = indexAfterContext - 1; // -1 because a for-loop increments tokenIndex right outside this method
					return true;
				case string s:
					memberValue = token.ToString(s);
					if (!TryConvert(ref memberValue, memberType)) {
						if (errors != null) errors.Add(new Err(token, rows, "unable to convert " + memberValue + " to " + memberType));
						return false;
					}
					return true;
				case TokenSubstitution sub:
					memberValue = sub.value;
					if (!TryConvert(ref memberValue, memberType)) {
						if (errors != null) errors.Add(new Err(token, rows, "unable to convert " + memberValue + " to " + memberType));
						return false;
					}
					return true;
				default:
					if (errors != null) errors.Add(new Err(token, rows, "unable to parse token with meta data " + meta));
					return false;
				}
			}
		}

		public static bool TryParse(Type type, IList<Token> tokens, ref object data, IList<int> rows, List<Err> errors = null) {
			Parser p = new Parser();
			p.Init(type, tokens, data, rows, errors);
			bool result = p.TryParse();
			data = p.result;
			return result;
		}

		public static bool IsPrimitiveType(Type typeToGet) {
			switch (Type.GetTypeCode(typeToGet)) {
			case TypeCode.Boolean:
			case TypeCode.SByte:
			case TypeCode.Byte:
			case TypeCode.Char:
			case TypeCode.Int16:
			case TypeCode.UInt16:
			case TypeCode.Int32:
			case TypeCode.UInt32:
			case TypeCode.Single:
			case TypeCode.Int64:
			case TypeCode.UInt64:
			case TypeCode.Double:
			case TypeCode.String:
				return true;
			}
			return false;
		}

		public static bool TryConvert(ref object value, Type typeToGet) {
			try {
				switch (Type.GetTypeCode(typeToGet)) {
				case TypeCode.Boolean: value = Convert.ToBoolean(value); break;
				case TypeCode.SByte: value = Convert.ToSByte(value); break;
				case TypeCode.Byte: value = Convert.ToByte(value); break;
				case TypeCode.Char: value = Convert.ToChar(value); break;
				case TypeCode.Int16: value = Convert.ToInt16(value); break;
				case TypeCode.UInt16: value = Convert.ToUInt16(value); break;
				case TypeCode.Int32: value = Convert.ToInt32(value); break;
				case TypeCode.UInt32: value = Convert.ToUInt32(value); break;
				case TypeCode.Single: value = Convert.ToSingle(value); break;
				case TypeCode.Int64: value = Convert.ToInt64(value); break;
				case TypeCode.UInt64: value = Convert.ToUInt64(value); break;
				case TypeCode.Double: value = Convert.ToDouble(value); break;
				case TypeCode.String: value = Convert.ToString(value); break;
				default: return false;
				}
			} catch { return false; }
			return true;
		}

		public static Type GetICollectionType(Type type) {
			foreach (Type i in type.GetInterfaces()) {
				if (i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICollection<>)) {
					return i.GetGenericArguments()[0];
				}
			}
			return null;
		}
		public static Type GetIListType(Type type) {
			foreach (Type i in type.GetInterfaces()) {
				if (i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IList<>)) {
					return i.GetGenericArguments()[0];
				}
			}
			return null;
		}
		public static KeyValuePair<Type,Type> GetIDictionaryType(Type type) {
			foreach (Type i in type.GetInterfaces()) {
				if (i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDictionary<,>)) {
					return new KeyValuePair<Type,Type>(i.GetGenericArguments()[0], i.GetGenericArguments()[1]);
				}
			}
			return new KeyValuePair<Type, Type>(null,null);
		}

		public static string Indent(int depth) {
			StringBuilder sb = new StringBuilder();
			while (depth-- > 0) {
				sb.Append("  ");
			}
			return sb.ToString();
		}
		public static string Stringify(object obj, int depth, bool pretty = false) {
			if (obj == null) return "null";
			Type t = obj.GetType();
			StringBuilder sb = new StringBuilder();
			FieldInfo[] fi = t.GetFields();
			Type iListElement = GetIListType(t);
			if(IsPrimitiveType(obj.GetType())) {
				if (obj is string s) {
					sb.Append("\"").Append(Escape(s)).Append("\"");
				} else {
					sb.Append(obj.ToString());
				}
			} else if (t.IsArray || iListElement != null) {
				sb.Append("[");
				//Array a = obj as Array;
				IList list = obj as IList;
				if ((iListElement != null && IsPrimitiveType(iListElement)) || IsPrimitiveType(t.GetElementType())) {
					for(int i = 0; i < list.Count; ++i) {
						if (i > 0) { sb.Append(","); if (pretty) sb.Append(" "); }
						sb.Append(Stringify(list[i],depth+1, pretty));
					}
				} else {
					for(int i = 0; i < list.Count; ++i) {
						if (i > 0) { sb.Append(","); }
						if (pretty) { sb.Append("\n" + Indent(depth + 1)); }
						sb.Append(Stringify(list[i], depth + 1, pretty));
					}
					if (pretty) { sb.Append("\n" + Indent(depth)); }
				}
				sb.Append("]");
			} else if (fi.Length > 0) {
				sb.Append("{");
				for (int i = 0; i < fi.Length; ++i) {
					if (i > 0) { sb.Append(","); }
					if (pretty) { sb.Append("\n" + Indent(depth + 1)); }
					sb.Append(fi[i].Name);
					sb.Append(pretty?" : ":":");
					sb.Append(Stringify(fi[i].GetValue(obj), depth + 1, pretty));
				}
				if (pretty) { sb.Append("\n" + Indent(depth)); }
				sb.Append("}");
			}
			if(sb.Length == 0) { sb.Append(obj.ToString()); }
			return sb.ToString();
		}

		/// <summary>
		/// converts a string from it's code to it's compiled form, with processed escape sequences
		/// </summary>
		/// <param name="str"></param>
		/// <returns></returns>
		// TODO use the actual parsing mechanisms...
		public static string Unescape(string str) {
			ParseResult parse;
			StringBuilder sb = new StringBuilder();
			int stringStarted = 0;
			for (int i = 0; i < str.Length; ++i) {
				char c = str[i];
				if (c == '\\') {
					sb.Append(str.Substring(stringStarted, i - stringStarted));
					parse = Delim.UnescapeString(str, i);
					if (parse.IsError) {
						Console.ForegroundColor = ConsoleColor.Red;
						Console.WriteLine("@" + i + ": " + parse.error);
					}
					if (parse.replacementValue != null) {
						sb.Append(parse.replacementValue);
					}
					//Console.WriteLine("replacing " + str.Substring(i, parse.lengthParsed) + " with " + parse.replacementValue);
					stringStarted = i + parse.lengthParsed;
					i = stringStarted - 1;
				}
			}
			sb.Append(str.Substring(stringStarted, str.Length - stringStarted));
			return sb.ToString();
		}

		// TODO use the actual parsing mechanisms...
		public static string Escape(string str) {
			StringBuilder sb = new StringBuilder();
			for (int i = 0; i < str.Length; ++i) {
				char c = str[i];
				switch (c) {
				case '\a': sb.Append("\\a"); break;
				case '\b': sb.Append("\\b"); break;
				case '\n': sb.Append("\\n"); break;
				case '\r': sb.Append("\\r"); break;
				case '\f': sb.Append("\\f"); break;
				case '\t': sb.Append("\\t"); break;
				case '\v': sb.Append("\\v"); break;
				case '\'': sb.Append("\\\'"); break;
				case '\"': sb.Append("\\\""); break;
				case '\\': sb.Append("\\\\"); break;
				default:
					if (c < 32 || (c > 127 && c < 512)) {
						sb.Append("\\").Append(Convert.ToString((int)c, 8));
					} else if (c >= 512) {
						sb.Append("\\u").Append(((int)c).ToString("X4"));
					} else {
						sb.Append(c);
					}
					break;
				}
			}
			return sb.ToString();
		}

	}
}
