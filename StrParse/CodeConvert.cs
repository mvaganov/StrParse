using System;
using System.Collections.Generic;
using System.Reflection;

namespace StrParse {
	class CodeConvert {
		public struct Err {
			public int row, col;
			public string message;
			public Err(int r, int c, string m) { row = r; col = c; message = m; }
			public Err(Token token, IList<int> rows, string m) {
				CodeParse.FilePositionOf(token, rows, out row, out col);
				message = m;
			}
			public override string ToString() { return "@"+row+","+col+	": " + message; }
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
			CodeParse.Tokens(text, tokens, indexOfNewRow: rows);
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
						string str = token.AsBasicToken;
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
		/// <param name="i"></param>
		/// <param name="value">should have data in it. If value is pointing at tokens, the last read token is being ignored.</param>
		/// <param name="errors"></param>
		/// <param name="rows"></param>
		/// <returns></returns>
		public static bool TryGetValue(Type typeToGet, IList<Token> tokens, ref int i, out object value, List<Err> errors, IList<int> rows) {
			value = null;
			object meta = tokens[i].meta;
			switch (meta) {
			case Delim delim:
				switch (delim.text) {
				// skip these delimiters as though they were whitespace.
				case "=": case ":": case ",": break;
				default:
					if(errors!=null)errors.Add(new Err(tokens[i], rows, "unexpected delimiter \"" + delim.text + "\""));
					return false;
				}
				value = tokens;
				return true;
			case Context.Entry context:
				int indexAfterContext = context.IndexAfter(tokens, i);
				if (context.IsText) {
					string text = context.Text;
					value = CodeParse.ResolveString(text.Substring(1, text.Length - 2));
				} else if (!TryParse(typeToGet, tokens.GetRange(i, indexAfterContext - i), ref value, rows, errors)) {
					return false;
				}
				i = indexAfterContext;
				return true;
			case string s:
				value = tokens[i].ToString(s);
				if (!TryConvert(ref value, typeToGet)) {
					if(errors!=null)errors.Add(new Err(tokens[i], rows,"unable to convert " + value + " to " + typeToGet));
					return false;
				}
				return true;
			case TokenSubstitution sub:
				value = sub.value;
				if (!TryConvert(ref value, typeToGet)) {
					if(errors!=null)errors.Add(new Err(tokens[i], rows,"unable to convert " + value + " to " + typeToGet));
					return false;
				}
				return true;
			default:
				if(errors!=null)errors.Add(new Err(tokens[i], rows,"unable to parse token with meta data " + meta));
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
	}
}
