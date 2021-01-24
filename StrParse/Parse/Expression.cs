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
	}
}
