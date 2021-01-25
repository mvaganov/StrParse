using System;
using System.Collections.Generic;

namespace NonStandard.Data.Parse {
	public class Expression {
		private List<Token> tokens;
		public Expression(List<Token> tokens) { this.tokens = tokens; }
		public override string ToString() {
			return Context.Entry.PrintAll(tokens);
		}
		public string Stringify() { return ToString(); }
		public string DebugPrint(int depth = 0, string indent = "  ") {
			return Tokenizer.DebugPrint(tokens, depth, indent);
		}
		public List<object> Resolve(Tokenizer tok, object scope = null) {
			List<object> results = new List<object>();
			Context.Entry.ResolveTerms(tok, scope, tokens, 0, tokens.Count, results);
			return results;
		}
	}
}
