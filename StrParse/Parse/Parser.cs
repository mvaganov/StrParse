using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace NonStandard.Data.Parse {
	public class Parser {
		/// used by wildcard searches, for member names and enums. dramatically reduces structural typing
		public const char Wildcard = '¤';
		/// current data being parsed
		protected object memberValue = null;
		/// the object being parsed into, the final result
		public object result { get; protected set; }
		public object scope;
		/// the type that the result needs to be
		protected Type resultType;
		/// the type that the next value needs to be
		protected Type memberType = null;
		// parse state
		public class ParseState {
			public int tokenIndex = 0;
			public List<Token> tokens;
			public Token GetToken() { return tokens[tokenIndex]; }
		}
		protected List<ParseState> state = new List<ParseState>();
		protected Tokenizer tok;
		// for parsing a list
		protected List<object> listData = null;
		// for objects and dictionaries
		protected object memberId;
		protected Token memberToken;
		// for parsing an object
		protected MemberReflectionTable reflectTable = new MemberReflectionTable();
		protected FieldInfo field = null;
		protected PropertyInfo prop = null;
		// for parsing a dictionary
		protected KeyValuePair<Type, Type> dictionaryTypes;
		protected MethodInfo dictionaryAdd = null;

		protected ParseState Current { get { return state[state.Count - 1]; } }
		protected void AddParseState(List<Token> tokenList, int index = 0) {
			state.Add(new ParseState { tokens = tokenList, tokenIndex = index });
		}
		protected bool Increment() {
			if (state.Count <= 0) return false;
			ParseState pstate = state[state.Count - 1];
			//Show.Warning(pstate.GetToken());
			++pstate.tokenIndex;
			while (pstate.tokenIndex >= pstate.tokens.Count) {
				state.RemoveAt(state.Count - 1);
				if (state.Count <= 0) return false;
				pstate = state[state.Count - 1];
				++pstate.tokenIndex;
			}
			return true;
		}
		protected bool SkipComments(bool incrementAtLeastOnce = false) {
			Context.Entry e = incrementAtLeastOnce ? Context.Entry.None : null;
			do {
				if (e != null && !Increment()) return false;
				e = Current.GetToken().GetAsContextEntry();
			} while (e != null && e.IsComment());
			return true;
		}

		public void SetResultType(Type type) {
			resultType = type;
			reflectTable.SetType(type);
		}
		protected Type SetResultType(string typeName) {
			Type t = Type.GetType(typeName);
			if (t == null) {
				Type[] childTypes = resultType.GetSubClasses();
				string[] typeNames = Array.ConvertAll(childTypes, ty => ty.ToString());
				string nameSearch = !typeName.StartsWith(Parser.Wildcard) ? Parser.Wildcard + typeName : typeName;
				int index = FindIndexWithWildcard(typeNames, nameSearch, false);
				if (index >= 0) { t = childTypes[index]; }
			}
			if (t != null && (result == null || result.GetType() != t)) {
				SetResultType(t);
				result = resultType.GetNewInstance();
			}
			return t;
		}
		public bool Init(Type type, List<Token> tokens, object dataStructure, Tokenizer tokenizer, object scope) {
			resultType = type;
			tok = tokenizer;
			state.Clear();
			AddParseState(tokens);
			result = dataStructure;
			SetResultType(type);
			memberType = type.GetIListType();
			memberToken.Invalidate();
			this.scope = scope;
			if (memberType != null) {
				listData = new List<object>();
			} else {
				try {
					if (result == null && !resultType.IsAbstract) { result = type.GetNewInstance(); }
				} catch (Exception e) {
					AddError("failed to create " + type + "\n" + e.ToString());
					return false;
				}
				dictionaryTypes = type.GetIDictionaryType();
				if (dictionaryTypes.Value != null) {
					memberType = dictionaryTypes.Value;
					dictionaryAdd = resultType.GetMethod("Add", new Type[] { dictionaryTypes.Key, dictionaryTypes.Value });
				}
			}
			return true;
		}

		protected Type FindInternalType() {
			if (Current.tokenIndex >= Current.tokens.Count) return null;
			if (!SkipComments()) { AddError("failed skipping comment for initial type"); return null; }
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
					//Show.Log("internal type " + typeName + " (" + typeName + ")");
					if (t == null) { AddError("unknown type " + typeName); }
					return t;
				} else {
					AddError("unexpected beginning token " + d.text);
				}
			}
			return null;
		}

		public bool TryParse() {
			Token token = Current.GetToken();
			Context.Entry e = token.GetAsContextEntry();
			if(e != null && e.tokens == Current.tokens) { Increment(); } // skip past the opening bracket
			FindInternalType(); // first, check if this has a more correct internal type defined
			if (result == null && listData == null) {
				AddError("need specific " + resultType + ", eg: \"" +resultType.GetSubClasses().Join("\", \"")+"\"");
				return false;
			}
			if (!SkipComments()) { return true; }
			while (state.Count > 0 && Current.tokenIndex < Current.tokens.Count) {
				token = Current.GetToken();
				e = token.GetAsContextEntry();
				if(e != null && e.tokens == Current.tokens) {
					if (!token.IsContextEnding()) { AddError("unexpected state. we should never see this. ever."); }
					break;
				} // found the closing bracket!
				if (listData == null) {
					if (!memberToken.IsValid) {
						if (!GetMemberNameAndAssociatedType()) { return false; }
					} else {
						if (!TryGetValue()) { return false; }
						if (memberValue != state) AssignValueToMember();
					}
				} else {
					if (!TryGetValue()) { return false; }
					if (memberValue != state) listData.Add(memberValue);
				}
				SkipComments(true);
			}
			FinalParseDataCompile();
			return true;
		}
		protected void FinalParseDataCompile() {
			if (listData != null) {
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
		}
		protected bool GetMemberNameAndAssociatedType() {
			memberToken = Current.GetToken();
			if (SkipStructuredDelimiters(memberToken.GetAsDelimiter())) { memberToken.Invalidate(); return true; }
			memberId = null;
			Context.Entry e = memberToken.GetAsContextEntry();
			if (e != null) {
				if (dictionaryAdd == null) {
					if (e.IsText()) {
						memberId = e.GetText();
					} else {
						AddError("unable to parse member ("+e.context.name+") as member name for " + resultType);
					}
				} else {
					memberId = e.Resolve(tok, scope);// "dictionary member value will be resolved later";
				}
				if (e.tokens == Current.tokens) {
					Current.tokenIndex += e.tokenCount - 1;
				}
			} else {
				memberId = memberToken.GetAsBasicToken();
			}
			if (memberId == null) {
				memberToken.index = -1; memberValue = state;
				return true;
			}
			memberValue = null;
			return CalculateMemberTypeBasedOnName();
		}
		protected bool CalculateMemberTypeBasedOnName() {
			if (dictionaryAdd != null) { return true; } // dictionary has no field to find
			string memberName = memberId as string;
			if(!reflectTable.TryGetMemberDetails(memberName, out memberType, out field, out prop)) {
				AddError("could not find \"" + memberName + "\" in " + result.GetType() + ". eg: " + reflectTable);
				return false;
			}
			return true;
		}
		protected bool SkipStructuredDelimiters(Delim delim) {
			if (delim == null) return false;
			switch (delim.text) {
			// skip these delimiters as though they were whitespace.
			case "=": case ":": case ",": break;
			default:
				AddError("unexpected delimiter \"" + delim.text + "\"");
				return false;
			}
			memberValue = state;
			return true;
		}
		public static int AssignDictionaryMember(KeyValuePair<Type,Type> dType, MethodInfo dictionaryAddMethod,
			object dict, object key, object value) {
			if (!dType.Key.IsAssignableFrom(key.GetType())) { return 1; }
			if (!dType.Value.IsAssignableFrom(value.GetType())) { return 2; }
			dictionaryAddMethod.Invoke(dict, new object[] { key, value });
			return 0;
		}
		protected void AssignValueToMember() {
			if (dictionaryAdd != null) {
				switch(AssignDictionaryMember(dictionaryTypes, dictionaryAdd, result, memberId, memberValue)) {
				case 1: AddError("unable to convert key \"" + memberId + "\" (" + memberId.GetType() + 
					") to " + dictionaryTypes.Key); break;
				case 2: AddError("unable to convert \"" + memberId + "\" value (" + memberValue.GetType() + 
					") \"" + memberValue + "\" to type " + memberType); break;
				}
			} else {
				if (field != null) {
					field.SetValue(result, memberValue);
				} else if (prop != null) {
					prop.SetValue(result, memberValue, null);
				} else {
					throw new Exception("huh? how did we get here?");
				}
				field = null; prop = null; memberType = dictionaryTypes.Value; memberToken.Invalidate();
			}
		}
		protected bool TryGetValue() {
			memberValue = null;
			Token token = Current.GetToken();
			object meta = token.meta;
			if(SkipStructuredDelimiters(meta as Delim)) { return true; }
			Context.Entry context = meta as Context.Entry;
			if (context != null) {
				bool subContextUsingSameList = context.tokens == Current.tokens;
				if (context.IsText()) {
					memberValue = context.GetText();
				} else {
					int index = Current.tokenIndex;
					List<Token> parseNext = subContextUsingSameList
							? Current.tokens.GetRange(index, context.tokenCount)
							: context.tokens;
					if (memberType == typeof(Expression)) {
						memberValue = new Expression(parseNext);
					} else {
						if (CodeConvert.IsConvertable(memberType) && !subContextUsingSameList) {
							memberValue = context.Resolve(tok, scope);
						} else {
							if (!CodeConvert.TryParse(memberType, parseNext, ref memberValue, scope, tok)) { return false; }
						}
					}
				}
				if (subContextUsingSameList) {
					Current.tokenIndex += context.tokenCount - 1; // -1 because increment happens after this method
				}
				return true;
			}
			string s = meta as string;
			if (s != null) {
				memberValue = token.ToString(s);
				if (!CodeConvert.TryConvert(ref memberValue, memberType)) {
					AddError("unable to convert (" + memberValue + ") to type '" + memberType + "'");
					return false;
				}
				return true;
			}
			TokenSubstitution sub = meta as TokenSubstitution;
			if (sub != null) {
				memberValue = sub.value;
				if (!CodeConvert.TryConvert(ref memberValue, memberType)) {
					AddError("unable to convert substitution (" + memberValue + ") to type '" + memberType + "'");
					return false;
				}
				return true;
			}
			AddError("unable to parse token with meta data " + meta);
			return false;
		}

		protected void AddError(string message) { tok.AddError(Current.GetToken(), message); }

		/// <param name="names"></param>
		/// <param name="n">name to find. the needle in the names haystack</param>
		/// <param name="sorted"></param>
		/// <param name="wildcard"></param>
		/// <returns></returns>
		public static int FindIndexWithWildcard(string[] names, string n, bool sorted, char wildcard = Wildcard) {
			if (n.Length == 1 && n[0] == wildcard) return 0;
			bool startsW = n.EndsWith(wildcard), endsW = n.StartsWith(wildcard);
			if (startsW && endsW) { return Array.FindIndex(names, s => s.Contains(n.Substring(1, n.Length - 2))); }
			if (endsW) { n = n.Substring(1); return Array.FindIndex(names, s => s.EndsWith(n)); }
			if (startsW) { n = n.Substring(0, n.Length - 1); }
			int index = sorted ? Array.BinarySearch(names, n) : (startsW)
				? Array.FindIndex(names, s => s.StartsWith(n)) : Array.IndexOf(names, n);
			if (startsW && index < 0) { return ~index; }
			return index;
		}
		public static bool IsWildcardMatch(string possibility, string n, char wildcard = Wildcard) {
			if (n.Length == 1 && n[0] == wildcard) return true;
			bool startsW = n.EndsWith(wildcard), endsW = n.StartsWith(wildcard);
			if (startsW && endsW) { return possibility.Contains(n.Substring(1, n.Length - 2)); }
			if (endsW) { n = n.Substring(1); return possibility.EndsWith(n); }
			if (startsW) { n = n.Substring(0, n.Length - 1); }
			return possibility.StartsWith(n);
		}
	}
	public class MemberReflectionTable {
		public string[] fieldNames, propNames;
		public FieldInfo[] fields;
		public PropertyInfo[] props;
		public void SetType(Type type) {
			fields = type.GetFields();
			props = type.GetProperties();
			Array.Sort(fields, (a, b) => a.Name.CompareTo(b.Name));
			Array.Sort(props, (a, b) => a.Name.CompareTo(b.Name));
			fieldNames = Array.ConvertAll(fields, f => f.Name);
			propNames = Array.ConvertAll(props, p => p.Name);
		}
		public override string ToString() {
			StringBuilder sb = new StringBuilder();
			sb.Append(fieldNames.Join(", "));
			if (fieldNames.Length > 0 && propNames.Length > 0) { sb.Append(", "); }
			sb.Append(propNames.Join(", "));
			return sb.ToString();
		}
		public FieldInfo GetField(string name) {
			int index = Parser.FindIndexWithWildcard(fieldNames, name, true); return (index < 0) ? null : fields[index];
		}
		public PropertyInfo GetProperty(string name) {
			int index = Parser.FindIndexWithWildcard(propNames, name, true); return (index < 0) ? null : props[index];
		}
		public bool TryGetMemberDetails(string memberName, out Type memberType, out FieldInfo field, out PropertyInfo prop) {
			field = GetField(memberName);
			if (field != null) {
				memberType = field.FieldType;
				prop = null;
			} else {
				prop = GetProperty(memberName);
				if (prop != null) {
					memberType = prop.PropertyType;
				} else {
					memberType = null;
					return false;
				}
			}
			return true;
		}
	}
}
