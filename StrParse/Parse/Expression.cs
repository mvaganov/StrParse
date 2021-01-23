using System;
using System.Collections.Generic;

namespace NonStandard.Data.Parse {
	public class Expression {
		private List<Token> tokens;
		public Expression(List<Token> tokens) { this.tokens = tokens; }
	}
}
