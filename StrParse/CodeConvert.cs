using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace StrParse {
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
			CodeParse.Tokens(text, tokens, rows: rows);
			if (data == null) { data = GetNew(type); }
			return TryParse(type, tokens, ref data, rows, errors);
		}

		public static T GetNew<T>(string type) { return (T)Activator.CreateInstance(Type.GetType(type)); }
		public static object GetNew(Type t) { return Activator.CreateInstance(t); }

		public static bool TryParse(Type type, IList<Token> tokens, ref object data, IList<int> rows, List<Err> errors = null) {
			FieldInfo field = null;
			PropertyInfo prop = null;
			object value = null;
			FieldInfo[] fields = type.GetFields();
			PropertyInfo[] props = type.GetProperties();
			Type typeToGet = null, elementType = null;
			Array.Sort(fields, (a, b) => a.Name.CompareTo(b.Name));
			Array.Sort(props, (a, b) => a.Name.CompareTo(b.Name));
			string[] fieldNames = Array.ConvertAll(fields, f => f.Name);
			string[] propNames = Array.ConvertAll(fields, p => p.Name);
			bool isVarPrimitiveType = false, isThisArrayType = type.IsArray;
			List<object> listData = null;
			if (isThisArrayType) {
				typeToGet = type.GetElementType();
				isVarPrimitiveType = false;
				if (typeToGet.IsArray) {
					elementType = type.GetElementType();
				} else {
					isVarPrimitiveType = IsPrimitiveType(typeToGet);
				}
				listData = new List<object>();
			} else {
				if (data == null) { data = GetNew(type); }
			}
			int tokenIndex = 0;
			Token token = tokens[tokenIndex];
			if (token.IsContextBeginning) {
				++tokenIndex;
			}
			for (; tokenIndex < tokens.Count; ++tokenIndex) {
				token = tokens[tokenIndex];
				if (token.IsContextBeginning && !token.ContextEntry.IsText) {
					if (typeToGet != null && isVarPrimitiveType) {
						if(errors != null) errors.Add(new Err(token, rows, "unexpected beginning of " + token.ContextEntry.context.name));
						return false;
					}
				}
				if (token.IsContextEnding) {
					//Console.Write("finished parsing " + token.ContextEntry.context.name);
					break;
				}
				if (!isThisArrayType) {
					if (typeToGet == null) {
						string str = null;
						Context.Entry e = token.ContextEntry;
						if (e != null) {
							if (e.IsText) {
								str = e.Text;
							}
							tokenIndex += e.tokenCount;
						} else {
							str = token.AsBasicToken;
						}
						if (str == null) continue;
						int index = Array.BinarySearch(fieldNames, str);
						if (index < 0) {
							index = Array.BinarySearch(propNames, str);
							if (index < 0) {
								if(errors != null) errors.Add(new Err(token, rows, "could not find field or property \"" + str + "\" in "+type));
								return false;
							} else {
								prop = props[index];
								typeToGet = prop.PropertyType;
							}
						} else {
							field = fields[index];
							typeToGet = field.FieldType;
						}
						value = null;
						if (typeToGet.IsArray) {
							elementType = typeToGet.GetElementType();
							isVarPrimitiveType = false;
						} else {
							isVarPrimitiveType = IsPrimitiveType(typeToGet);
						}
					} else {
						if (!TryGetValue(typeToGet, tokens, ref tokenIndex, out value, errors, rows)) {
							return false;
						}
						if (value == tokens) { continue; } // this is how TryGetValue communicates value ignore
						if (field != null) {
							field.SetValue(data, value);
						} else if (prop != null) {
							prop.SetValue(data, value);
						} else {
							throw new Exception("huh? how did we get here?");
						}
						field = null;
						prop = null;
						typeToGet = null;
					}
				} else {
					if (!TryGetValue(typeToGet, tokens, ref tokenIndex, out value, errors, rows)) {
						return false;
					}
					if (value == tokens) { continue; }
					listData.Add(value);
				}
			}
			if (isThisArrayType) {
				Array a = Array.CreateInstance(typeToGet, listData.Count);
				for (int i = 0; i < listData.Count; ++i) {
					a.SetValue(listData[i], i);
				}
				data = a;
			}
			return true;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="typeToGet"></param>
		/// <param name="tokens"></param>
		/// <param name="tokenIndex"></param>
		/// <param name="value">should have data in it. If value is pointing at tokens, the last read token is being ignored.</param>
		/// <param name="errors"></param>
		/// <param name="rows"></param>
		/// <returns></returns>
		public static bool TryGetValue(Type typeToGet, IList<Token> tokens, ref int tokenIndex, out object value, List<Err> errors, IList<int> rows) {
			value = null;
			Token token = tokens[tokenIndex];
			object meta = token.meta;
			switch (meta) {
			case Delim delim:
				switch (delim.text) {
				// skip these delimiters as though they were whitespace.
				case "=": case ":": case ",": break;
				default:
					if(errors!=null)errors.Add(new Err(token, rows, "unexpected delimiter \"" + delim.text + "\""));
					return false;
				}
				value = tokens;
				return true;
			case Context.Entry context:
				int indexAfterContext = tokenIndex + context.tokenCount;
				if (context.IsText) {
					value = context.Text;
				} else if (!TryParse(typeToGet, tokens.GetRange(tokenIndex, indexAfterContext - tokenIndex), ref value, rows, errors)) {
					return false;
				}
				tokenIndex = indexAfterContext-1; // -1 because a for-loop increments tokenIndex right outside this method
				return true;
			case string s:
				value = token.ToString(s);
				if (!TryConvert(ref value, typeToGet)) {
					if(errors!=null)errors.Add(new Err(token, rows,"unable to convert " + value + " to " + typeToGet));
					return false;
				}
				return true;
			case TokenSubstitution sub:
				value = sub.value;
				if (!TryConvert(ref value, typeToGet)) {
					if(errors!=null)errors.Add(new Err(token, rows,"unable to convert " + value + " to " + typeToGet));
					return false;
				}
				return true;
			default:
				if(errors!=null)errors.Add(new Err(token, rows,"unable to parse token with meta data " + meta));
				return false;
			}
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
			if(IsPrimitiveType(obj.GetType())) {
				if (obj is string s) {
					sb.Append("\"").Append(Escape(s)).Append("\"");
				} else {
					sb.Append(obj.ToString());
				}
			} else if (t.IsArray) {
				sb.Append("[");
				Array a = obj as Array;
				if (IsPrimitiveType(t.GetElementType())) {
					for(int i = 0; i < a.Length; ++i) {
						if (i > 0) { sb.Append(","); if (pretty) sb.Append(" "); }
						sb.Append(Stringify(a.GetValue(i),depth+1, pretty));
					}
				} else {
					for(int i = 0; i < a.Length; ++i) {
						if (i > 0) { sb.Append(","); }
						if (pretty) { sb.Append("\n" + Indent(depth + 1)); }
						sb.Append(Stringify(a.GetValue(i), depth + 1, pretty));
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
					if (parse.error != null) {
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
