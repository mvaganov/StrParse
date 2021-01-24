using NonStandard.Data.Parse;
using System;
using System.Collections.Generic;

namespace NonStandard.Data {
	public class CodeConvert {
		public static bool TryFill<T>(string text, ref T data, Tokenizer tokenizer = null) {
			object value = data;
			bool result = TryParse(typeof(T), text, ref value, tokenizer);
			data = (T)value;
			return result;
		}
		public static bool TryParse<T>(string text, out T data, Tokenizer tokenizer = null) {
			object value = null;
			bool result = TryParse(typeof(T), text, ref value, tokenizer);
			data = (T)value;
			return result;
		}
		public static bool TryParse(Type type, string text, ref object data, Tokenizer tokenizer = null) {
			if(tokenizer == null) { tokenizer = new Tokenizer(); }
			tokenizer.Tokenize(text);
			//Show.Log(Show.GetStack(4));
			Show.Log(tokenizer.DebugPrint(-1));
			return TryParse(type, tokenizer.tokens, ref data, tokenizer);
		}
		public static bool TryParse(Type type, List<Token> tokens, ref object data, Tokenizer tokenizer) {
			Parser p = new Parser();
			p.Init(type, tokens, data, tokenizer);
			bool result = p.TryParse();
			data = p.result;
			return result;
		}

		public static bool IsConvertable(Type typeToGet) {
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
			return typeToGet.IsEnum;
		}

		public static bool TryConvert(ref object value, Type typeToGet) {
			try {
				if (typeToGet.IsEnum) {
					string str = value as string;
					if (str != null) { return TryConvertEnumWildcard(typeToGet, str, out value); }
				}
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
		public static bool TryConvertEnumWildcard(Type typeToGet, string str, out object value, char wildcard = Parser.Wildcard) {
			bool startsWith = str.EndsWith(wildcard), endsWidth = str.StartsWith(wildcard);
			if (startsWith || endsWidth) {
				Array a = Enum.GetValues(typeToGet);
				string[] names = new string[a.Length];
				for (int i = 0; i < a.Length; ++i) { names[i] = a.GetValue(i).ToString(); }
				int index = Parser.FindIndexWithWildcard(names, str, false, wildcard);
				if (index < 0) { value = null; return false; }
				str = names[index];
			}
			value = Enum.Parse(typeToGet, str);
			return true;
		}
	}
}
