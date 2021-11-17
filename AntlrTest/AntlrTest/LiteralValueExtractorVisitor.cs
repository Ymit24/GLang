using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Example.Generated;
using Antlr4.Runtime.Misc;

namespace AntlrTest
{
    public class LiteralValueExtractorVisitor : gLangBaseVisitor<string>
    {
        public class StringLiteralHolder
        {
            public static Dictionary<string, string> stringValueToSymbol = new Dictionary<string, string>();
            private static string literalPrefix = "__str";
            private static int currentLiteralIndex = 0;

            public static void IncludeStringLiteral(string literal)
            {
                if (stringValueToSymbol.ContainsKey(literal)) return;

                stringValueToSymbol.Add(literal, literalPrefix + currentLiteralIndex);
                currentLiteralIndex++;
            }

            public static string GetStringSymbol(string literal)
            {
                if (stringValueToSymbol.ContainsKey(literal)) return stringValueToSymbol[literal];
                return null;
            }
        }

        /// <summary>
        /// Sets up the string literal holder for all unique string literals in source code.
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public override string VisitStringLiteral([NotNull] gLangParser.StringLiteralContext context)
        {
            string literal = context.STRING().GetText();
            Console.WriteLine("Found string literal: " + literal);
            StringLiteralHolder.IncludeStringLiteral(literal);
            return "[unused]";
        }
    }
}
