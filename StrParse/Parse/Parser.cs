using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace NonStandard.Data.Parse {
	public class Parser {
		/// current data being parsed
		object memberValue = null;
		/// the object being parsed into, the final result
		public object result;
		/// the type that the result needs to be
		Type resultType;
		/// the type that the next value needs to be
		Type memberType = null;
		// parse state
		public class ParseState { public int tokenIndex = 0; public IList<Token> tokens; }
		List<ParseState> state = new List<ParseState>();
		IList<int> rows;
		List<ParseError> errors = null;
		bool isVarPrimitiveType = false, isList; // can these be designed out?
		Token memberToken; // for objects and dictionaries
 		// for parsing an object
		string[] fieldNames, propNames;
		FieldInfo[] fields;
		FieldInfo field = null;
		PropertyInfo[] props;
		PropertyInfo prop = null;
		// for parsing a list
		List<object> listData = null;
		// for parsing a dictionary
		KeyValuePair<Type, Type> dictionaryTypes;
		private MethodInfo dictionaryAdd = null;

		public ParseState Current { get { return state[state.Count - 1]; } }
		public void AddParseState(IList<Token> tokenList, int index = 0) {
			state.Add(new ParseState { tokens = tokenList, tokenIndex = index });
		}

		// TODO MoveNext(), which should handle context switching, and skip comments as well (see FindInternalType)
		// TODO IsDone()
		// TODO create an enumerator that does the traversal.

		public void PopParseState() { if (state.Count > 0) { state.RemoveAt(state.Count - 1); } }
		public void SetResultType(Type type) {
			resultType = type;
			fields = type.GetFields();
			props = type.GetProperties();
			Array.Sort(fields, (a, b) => a.Name.CompareTo(b.Name));
			Array.Sort(props, (a, b) => a.Name.CompareTo(b.Name));
			fieldNames = Array.ConvertAll(fields, f => f.Name);
			propNames = Array.ConvertAll(props, p => p.Name);
		}
		public void Init(Type type, IList<Token> a_tokens, object dataStructure, IList<int> rows, List<ParseError> errors) {
			resultType = type;
			AddParseState(a_tokens);//tokens = a_tokens;
			result = dataStructure;
			this.rows = rows;
			this.errors = errors;
			SetResultType(type);
			memberType = null;
			isVarPrimitiveType = false;
			memberType = type.GetIListType();
			isList = memberType != null;
			memberToken.Invalidate();
			if (isList) {
				isVarPrimitiveType = false;
				if (memberType.IsArray) {
				} else {
					isVarPrimitiveType = CodeConvert.IsConvertable(memberType);
				}
				listData = new List<object>();
			} else {
				try {
					if (result == null && !resultType.IsAbstract) { result = type.GetNewInstance(); }
				} catch (Exception e) {
					throw new Exception("failed to create " + type + " at " +
						ParseError.FilePositionOf(Current.tokens[0], rows) + "\n" + e.ToString());
				}
				dictionaryTypes = type.GetIDictionaryType();
				if (dictionaryTypes.Value != null) {
					memberType = dictionaryTypes.Value;
					dictionaryAdd = resultType.GetMethod("Add", new Type[] { dictionaryTypes.Key, dictionaryTypes.Value });
				}
			}
		}

		private Type FindInternalType() {
			if (Current.tokenIndex >= Current.tokens.Count) return null;
			Token token; Context.Entry e;

			token = Current.tokens[Current.tokenIndex];
			e = token.AsContextEntry;
			while (e != null && e.IsComment) {
				++Current.tokenIndex;
				token = Current.tokens[Current.tokenIndex];
				e = token.AsContextEntry;
			}

			if (e != null && token.IsContextBeginning) {
				++Current.tokenIndex;
				AddParseState(e.tokens);
				++Current.tokenIndex;

				token = Current.tokens[Current.tokenIndex];
				e = token.AsContextEntry;
				while (e != null && e.IsComment) {
					++Current.tokenIndex;
					token = Current.tokens[Current.tokenIndex];
					e = token.AsContextEntry;
				}
			}
			Delim d = token.AsDelimiter;
			if (d != null) {
				if (d.text == "=" || d.text == ":") {
					++Current.tokenIndex;
					memberType = typeof(string);
					if (!TryGetValue()) { return null; }
					memberType = null;
					++Current.tokenIndex;
					string typeName = memberValue.ToString();
					Type t = Type.GetType(typeName);
					if (t == null) {
						Type[] childTypes = resultType.GetSubClasses();
						string[] typeNames = Array.ConvertAll(childTypes, ty => ty.ToString());
						string nameSearch = !typeName.StartsWith(Parser.defaultWildcard) ? Parser.defaultWildcard + typeName : typeName;
						int index = FindIndexWithWildcard(typeNames, nameSearch, false);
						if (index >= 0) { t = childTypes[index]; }
					}
					if (result == null || result.GetType() != t) {
						if (t != null) {
							SetResultType(t);
							result = resultType.GetNewInstance();
						} else {
							if (errors != null) errors.Add(new ParseError(token, rows, "unknown type " + typeName));
						}
					}
					return t;
				} else {
					if (errors != null) errors.Add(new ParseError(token, rows, "unexpected beginning token " + d.text));
				}
			}
			return null;
		}

		public bool TryParse() {
			FindInternalType(); // first, check if this has a more correct internal type
			int counter = 0;
			while(state.Count > 0 && Current.tokenIndex < Current.tokens.Count) {
				if(counter++ > 100) { throw new Exception("infinite loop?"); }
				Token token = Current.tokens[Current.tokenIndex];
				Context.Entry e = token.AsContextEntry;
				// skip comments
				if (e != null && (e.context == CodeRules.CommentLine || e.context == CodeRules.CommentBlock || e.context == CodeRules.XmlCommentLine)) {
					//Show.Log("skipping comment "+(e.tokens == Current.tokens));
					if (e.tokens == Current.tokens) {
						Current.tokenIndex += e.tokenCount;
					} else {
						++Current.tokenIndex;
					}
					while (state.Count > 0 && Current.tokenIndex >= Current.tokens.Count) { // TODO while(Current.IsDone) {
						PopParseState();
						if (state.Count > 0) { ++Current.tokenIndex; } // TODO MoveNext()
					}
					continue;
				}
				// flag errors for complicated text when a primitive is expected TODO expressions.
				if (token.IsContextBeginning && !token.AsContextEntry.IsText) {
					if (memberType != null && isVarPrimitiveType) {
						if (errors != null) errors.Add(new ParseError(token, rows, "unexpected beginning of " + token.AsContextEntry.context.name));
						return false;
					}
				}
				if (token.IsContextEnding) {
					Show.Log("breaking at "+Current.tokenIndex);
					break;
				} // assume any unexpected context ending belongs to this context
				if (!isList) {
					if(result == null) {
						if (errors != null) {
							errors.Add(new ParseError(token, rows, resultType + " needs more specific, eg: "+ resultType.GetSubClasses().Join(", ")));
						}
						return false;
					}
					// how to parse non-lists: find what member is being assigned, get the value to assign, assign it.
					if (!memberToken.IsValid) {
						//Show.Log("trying to get memberToken @" + Current.tokenIndex);
						if (!GetMemberNameAndAssociatedType()) {
							//Show.Log("fail membername");
							return false; }
						if (memberValue == state) {
							//Show.Log("skipping token during memberName search");
							memberValue = null;
							++Current.tokenIndex;
							continue; }
						Show.Log("memberToken: "+memberToken+" @"+Current.tokenIndex);
					} else {
						Show.Log("trying to get memberValue @" + Current.tokenIndex);
						if (!TryGetValue()) {
							//Show.Log("fail value");
							return false;
						}
						if (memberValue == state) {
							//Show.Log("skipping token during memberValue search");
							++Current.tokenIndex;
							continue; } // this is how TryGetValue communicates value ignore
						Show.Log("memberValue: "+memberValue+" @"+Current.tokenIndex);
						if (dictionaryAdd != null) {
							object key = memberToken.Resolve();
							if (!memberType.IsAssignableFrom(memberValue.GetType())) {
								if (errors != null) errors.Add(new ParseError(token, rows, "unable to convert element \"" + key + "\" value ("+memberValue.GetType()+") \"" + memberValue + "\" to type " + memberType));
							} else {
								dictionaryAdd.Invoke(result, new object[] { key, memberValue });
							}
						} else if (field != null) {
							field.SetValue(result, memberValue);
						} else if (prop != null) {
							prop.SetValue(result, memberValue, null);
						} else {
							throw new Exception("huh? how did we get here?");
						}
						field = null; prop = null; memberType = dictionaryTypes.Value; memberToken.Invalidate();
					}
				} else {
					if (!TryGetValue()) { return false; }
					if (memberValue == state) {
						++Current.tokenIndex;
						continue; }
					listData.Add(memberValue);
				}
				++Current.tokenIndex;
				while(state.Count > 0 && Current.tokenIndex >= Current.tokens.Count) { // TODO while(Current.IsDone) {
					PopParseState();
					if (state.Count > 0) { ++Current.tokenIndex; } // TODO MoveNext()
				}
			}
			if (isList) {
				if (resultType.IsArray) {
					Array a = Array.CreateInstance(memberType, listData.Count);
					for (int i = 0; i < listData.Count; ++i) { a.SetValue(listData[i], i); }
					result = a;
				} else {
					result = resultType.GetNewInstance();
					IList ilist = result as IList;
					for (int i = 0; i < listData.Count; ++i) { ilist.Add(listData[i]); }
				}
			}
			return true;
		}

		public bool GetMemberNameAndAssociatedType() {
			memberToken = Current.tokens[Current.tokenIndex];
			string str = null;
			Context.Entry e = memberToken.AsContextEntry;
			if (e != null) {
				if (dictionaryAdd == null) {
					if (e.IsText) {
						str = e.Text;
					} else {
						if (errors != null) errors.Add(new ParseError(memberToken, rows, "unable to parse member ("+e.context.name+") name " + e.BeginToken + " for " + resultType));
					}
				} else {
					str = "dictionary member value will be resolved later";
				}
				if (e.tokens == Current.tokens) {
					Current.tokenIndex += e.tokenCount - 1;
				}
			} else {
				str = memberToken.AsBasicToken;
			}
			if (str == null) { memberToken.index = -1; memberValue = state; return true; }
			if (dictionaryAdd != null) { return true; } // dictionary has no field to find
			int index = FindIndexWithWildcard(fieldNames, str, true);
			if (index < 0) {
				index = FindIndexWithWildcard(propNames, str, true);
				if (index < 0) {
					if (errors != null) {
						StringBuilder sb = new StringBuilder();
						sb.Append("\nvalid possibilities include: ");
						for (int i = 0; i < fieldNames.Length; ++i) {
							if (i > 0) sb.Append(", ");
							sb.Append(fieldNames[i]);
						}
						for (int i = 0; i < propNames.Length; ++i) {
							if (i > 0 || fieldNames.Length > 0) sb.Append(", ");
							sb.Append(propNames[i]);
						}
						errors.Add(new ParseError(memberToken, rows, "could not find field or property \"" + str + "\" in " + result.GetType() + sb));
					}
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
				isVarPrimitiveType = CodeConvert.IsConvertable(memberType);
			}
			return true;
		}
		public bool TryGetValue() {
			memberValue = null;
			Token token = Current.tokens[Current.tokenIndex];
			object meta = token.meta;
			Delim delim = meta as Delim;
			if (delim != null) {
				switch (delim.text) {
				// skip these delimiters as though they were whitespace.
				case "=": case ":": case ",": break;
				default:
					if (errors != null) errors.Add(new ParseError(token, rows, "unexpected delimiter \"" + delim.text + "\""));
					return false;
				}
				memberValue = state;
				return true;
			}
			Context.Entry context = meta as Context.Entry;
			if (context != null) {
				bool subContextUsingSameList = context.tokens == Current.tokens;
				int indexAfterContext = subContextUsingSameList ? Current.tokenIndex + context.tokenCount : Current.tokenIndex + 1;
				if (context.IsText) {
					memberValue = context.Text;
				} else {
					IList<Token> parseNext = subContextUsingSameList
							? Current.tokens.GetRange(Current.tokenIndex, indexAfterContext - Current.tokenIndex)
							: context.tokens;
					if (!CodeConvert.TryParse(memberType, parseNext, ref memberValue, rows, errors)) {
						return false;
					}
				}
				Current.tokenIndex = indexAfterContext - 1; // -1 because a for-loop increments tokenIndex right outside this method
				return true;
			}
			string s = meta as string;
			if (s != null) {
				memberValue = token.ToString(s);
				if (!CodeConvert.TryConvert(ref memberValue, memberType)) {
					if (errors != null) errors.Add(new ParseError(token, rows, "unable to convert (" + memberValue + ") to type '" + memberType + "'"));
					return false;
				}
				return true;
			}
			TokenSubstitution sub = meta as TokenSubstitution;
			if (sub != null) {
				memberValue = sub.value;
				if (!CodeConvert.TryConvert(ref memberValue, memberType)) {
					if (errors != null) errors.Add(new ParseError(token, rows, "unable to convert substitution (" + memberValue + ") to type '" + memberType + "'"));
					return false;
				}
				return true;
			}
			if (errors != null) errors.Add(new ParseError(token, rows, "unable to parse token with meta data " + meta));
			return false;
		}

		public const char defaultWildcard = '¤';
		public static int FindIndexWithWildcard(string[] names, string name, bool isSorted, char wildcard = defaultWildcard) {
			if (name.Length == 1 && name[0] == wildcard) return 0;
			bool startsWith = name.EndsWith(wildcard), endsWith = name.StartsWith(wildcard);
			if (startsWith && endsWith) { return Array.FindIndex(names, s => s.Contains(name.Substring(1, name.Length - 2))); }
			if (endsWith) { name = name.Substring(1); return Array.FindIndex(names, s => s.EndsWith(name)); }
			if (startsWith) { name = name.Substring(0, name.Length - 1); }
			int index = isSorted ? Array.BinarySearch(names, name) : (startsWith)
				? Array.FindIndex(names, s => s.StartsWith(name)) : Array.IndexOf(names, name);
			if (startsWith && index < 0) { return ~index; }
			return index;
		}
	}
}
