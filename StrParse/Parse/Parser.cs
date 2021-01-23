using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace NonStandard.Data.Parse {
	public class Parser {
		/// used by wildcard searches, for member names and enums
		public const char defaultWildcard = '¤';
		/// current data being parsed
		object memberValue = null;
		/// the object being parsed into, the final result
		public object result;
		/// the type that the result needs to be
		Type resultType;
		/// the type that the next value needs to be
		Type memberType = null;
		// parse state
		public class ParseState {
			public int tokenIndex = 0;
			public List<Token> tokens;
			public Token GetToken() { return tokens[tokenIndex]; }
		}
		List<ParseState> state = new List<ParseState>();
		Tokenizer tok;
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
		public string CurrentRowCol() { return tok.FilePositionOf(Current.GetToken()); }
		public void AddParseState(List<Token> tokenList, int index = 0) {
			state.Add(new ParseState { tokens = tokenList, tokenIndex = index });
		}
		public void PopState() { state.RemoveAt(state.Count - 1); }

		public bool Increment() {
			if (state.Count <= 0) return false;
			Show.Warning(Current.GetToken());
			ParseState pstate = state[state.Count-1];
			++pstate.tokenIndex;
			while (pstate.tokenIndex >= pstate.tokens.Count) {
				PopParseState();
				if (state.Count <= 0) return false;
				pstate = state[state.Count - 1];
				++pstate.tokenIndex;
			}
			return true;
		}
		public bool SkipComments(bool incrementAtLeastOnce = false) {
			Context.Entry e = incrementAtLeastOnce ? Context.Entry.None : null;
			do {
				if (e != null && !Increment()) return false;
				e = Current.GetToken().GetAsContextEntry();
			} while (e != null && e.IsComment());
			return true;
		}

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
		public Type SetResultType(string typeName) {
			Type t = Type.GetType(typeName);
			if (t == null) {
				Type[] childTypes = resultType.GetSubClasses();
				string[] typeNames = Array.ConvertAll(childTypes, ty => ty.ToString());
				string nameSearch = !typeName.StartsWith(Parser.defaultWildcard) ? Parser.defaultWildcard + typeName : typeName;
				int index = FindIndexWithWildcard(typeNames, nameSearch, false);
				if (index >= 0) { t = childTypes[index]; }
			}
			if (t != null && (result == null || result.GetType() != t)) {
				SetResultType(t);
				result = resultType.GetNewInstance();
			}
			return t;
		}
		public void Init(Type type, List<Token> tokens, object dataStructure, Tokenizer tokenizer) {
			resultType = type;
			tok = tokenizer;
			AddParseState(tokens);//tokens = a_tokens;
			result = dataStructure;
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
						ParseError.FilePositionOf(Current.tokens[0], tok.rows) + "\n" + e.ToString());
				}
				dictionaryTypes = type.GetIDictionaryType();
				if (dictionaryTypes.Value != null) {
					memberType = dictionaryTypes.Value;
					dictionaryAdd = resultType.GetMethod("Add", new Type[] { dictionaryTypes.Key, dictionaryTypes.Value });
				}
			}
			return;
		}

		private Type GoInAndFindInternalType() {
			if (Current.tokenIndex >= Current.tokens.Count) return null;
			if (!SkipComments()) {
				Show.Error("failed while skipping initial comment while looking for initial type");
				return null; }
			Token token = Current.GetToken();

			Delim d = token.GetAsDelimiter();
			if (d != null) {
				if (d.text == "=" || d.text == ":") {
					SkipComments(true);
					memberType = typeof(string);
					if (!TryGetValue()) { return null; }
					memberType = null;
					SkipComments(true);
					string typeName = memberValue.ToString();
					Type t = SetResultType(typeName);
					Show.Log("internal type " + typeName + " (" + typeName + ")");
					if (t == null) { tok.AddError(token, "unknown type " + typeName); }
					return t;
				} else {
					tok.AddError(token, "unexpected beginning token " + d.text);
				}
			}
			else {
				Show.Log("no initial type, using "+resultType);
			}
			return null;
		}

		public bool TryParse() {
			Token token = Current.GetToken();
			Context.Entry e = token.GetAsContextEntry();
			if(e != null && e.tokens == Current.tokens) { Increment(); } // skip past the opening bracket

			GoInAndFindInternalType(); // first, check if this has a more correct internal type
			if (result == null && !isList) {
				tok.AddError(Current.GetToken(), resultType + " needs to be more specific," +
					" eg: \""+resultType.GetSubClasses().Join("\", \"")+"\"");
				return false;
			}
			if (!SkipComments()) { return true; }
			while (state.Count > 0 && Current.tokenIndex < Current.tokens.Count) {
				token = Current.GetToken();
				e = token.GetAsContextEntry();
				if(e != null && e.tokens == Current.tokens) { break; } // found the closing bracket!
				if (!isList) {
					// how to parse non-lists: find what member is being assigned, get the value to assign, assign it.
					if (!memberToken.IsValid) {
						Show.Log("trying to get memberToken @" + CurrentRowCol() + " for "+resultType);
						if (!GetMemberNameAndAssociatedType()) { return false; }
						if (memberValue == state) { memberValue = null; SkipComments(true); continue; }
						Show.Log("memberToken: "+memberToken+" @"+ CurrentRowCol());
					} else {
						Show.Log("trying to get memberValue @" + CurrentRowCol());
						if (!TryGetValue()) {
							Show.Error("fail value");
							return false;
						}
						if (memberValue == state) {
							SkipComments(true);
							continue;
						} // this is how TryGetValue communicates value ignore
						Show.Log(memberToken + " : "+memberValue+" @"+ CurrentRowCol());
						if (dictionaryAdd != null) {
							object key = memberToken.Resolve();
							if (!memberType.IsAssignableFrom(memberValue.GetType())) {
								tok.AddError(Current.GetToken(), "unable to convert element \"" + key + "\" value ("+memberValue.GetType()+") \"" + memberValue + "\" to type " + memberType);
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
					Show.Log("trying to get listValue["+listData.Count+"] @" + CurrentRowCol());
					if (!TryGetValue()) { return false; }
					if (memberValue == state) {
						SkipComments(true);
						continue;
					}
					listData.Add(memberValue);
				}
				SkipComments(true);
			}
			// doen parsing. if this is a list being parsed, create the list!
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
			memberToken = Current.GetToken();
			if (SkipStructuredDelimiters(memberToken.GetAsDelimiter())) { memberToken.Invalidate(); return true; }
			string str = null;
			Context.Entry e = memberToken.GetAsContextEntry();
			if (e != null) {
				if (dictionaryAdd == null) {
					if (e.IsText()) {
						str = e.GetText();
					} else {
						tok.AddError(memberToken, "unable to parse member ("+e.context.name+") as member name for " + resultType);
					}
				} else {
					str = "dictionary member value will be resolved later";
				}
				if (e.tokens == Current.tokens) {
					Current.tokenIndex += e.tokenCount - 1;
				}
			} else {
				str = memberToken.GetAsBasicToken();
			}
			if (str == null) {
				memberToken.index = -1; memberValue = state;
				return true;
			}
			return CalculateMemberDetailsBasedOnName(str);
		}
		public bool CalculateMemberDetailsBasedOnName(string str) {
			if (dictionaryAdd != null) { return true; } // dictionary has no field to find
			int index = FindIndexWithWildcard(fieldNames, str, true);
			if (index < 0) {
				index = FindIndexWithWildcard(propNames, str, true);
				if (index < 0) {
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
					tok.AddError(memberToken, "could not find field or property \"" + str + "\" in " + result.GetType() + sb);
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
		public bool SkipStructuredDelimiters(Delim delim) {
			if (delim == null) return false;
			switch (delim.text) {
			// skip these delimiters as though they were whitespace.
			case "=": case ":": case ",": break;
			default:
				tok.AddError(Current.GetToken(), "unexpected delimiter \"" + delim.text + "\"");
				return false;
			}
			memberValue = state;
			return true;
		}
		public bool TryGetValue() {
			memberValue = null;
			Token token = Current.GetToken();
			object meta = token.meta;
			if(SkipStructuredDelimiters(meta as Delim)) { return true; }
			Context.Entry context = meta as Context.Entry;
			if (context != null) {
				bool subContextUsingSameList = context.tokens == Current.tokens;
				//Show.Log("next tokens list is same? " + subContextUsingSameList);
				if (context.IsText()) {
					memberValue = context.GetText();
				} else {
					int index = Current.tokenIndex;
					List<Token> parseNext = subContextUsingSameList
							? Current.tokens.GetRange(index, context.tokenCount)
							: context.tokens;
					if (memberType == typeof(Expression)) {
						memberValue = new Expression(parseNext);
						Show.Log("absorbing parsed Expression " + Tokenizer.DebugPrint(parseNext));
					} else {
						Show.Log("going to parse: " + Tokenizer.DebugPrint(parseNext));
						if (!CodeConvert.TryParse(memberType, parseNext, ref memberValue, tok)) {
							return false;
						}
					}
					//Show.Log("memberValue: " + memberValue);
				}
				if (subContextUsingSameList) {
					Current.tokenIndex += context.tokenCount - 1; // -1 because we assume an increment happens after this method
				}
				return true;
			}
			string s = meta as string;
			if (s != null) {
				memberValue = token.ToString(s);
				if (!CodeConvert.TryConvert(ref memberValue, memberType)) {
					tok.AddError(token, "unable to convert (" + memberValue + ") to type '" + memberType + "'");
					return false;
				}
				return true;
			}
			TokenSubstitution sub = meta as TokenSubstitution;
			if (sub != null) {
				memberValue = sub.value;
				if (!CodeConvert.TryConvert(ref memberValue, memberType)) {
					tok.AddError(token, "unable to convert substitution (" + memberValue + ") to type '" + memberType + "'");
					return false;
				}
				return true;
			}
			tok.AddError(token, "unable to parse token with meta data " + meta);
			return false;
		}

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
