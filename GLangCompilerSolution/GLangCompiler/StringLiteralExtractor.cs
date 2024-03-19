using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Example.Generated;
using Antlr4.Runtime.Misc;

namespace AntlrTest
{
    public class GFunctionSignature
    {
        public readonly string Name;
        public readonly List<GDataSymbol> Parameters;
        public readonly GDataType ReturnType;
        public readonly bool IsExtern;

        public bool ReturnIsSpecial
        {
            get
            {
                if (ReturnType != null)
                    return !ReturnType.IsPrimitive;
                return false;
            }
        }

        public int ReturnSize
        {
            get
            {
                if (ReturnType != null)
                    return ReturnType.AlignedSize;
                return 0;
            }
        }

        private static Dictionary<string, GFunctionSignature> FunctionSignatures
            = new Dictionary<string, GFunctionSignature>();

        public GFunctionSignature(string name, List<GDataSymbol> parameters, GDataType return_type, bool isExtern)
        {
            Name = name;
            Parameters = parameters;
            ReturnType = return_type;
            IsExtern = isExtern;
        }

        public static void IncludeSignature(GFunctionSignature signature)
        {
            if (FunctionSignatures.ContainsKey(signature.Name)) return;
            FunctionSignatures.Add(signature.Name, signature);
        }

        public static GFunctionSignature GetSignature(string name)
        {
            if (FunctionSignatures.ContainsKey(name)) return FunctionSignatures[name];
            throw new Exception($"Function Signature \"{name}\" Not Found.");
        }
    }

    public class GStructSignature
    {
        public readonly string Name;
        public readonly List<GDataSymbol> Fields;

        private static Dictionary<string, GStructSignature> StructSignatures
            = new Dictionary<string, GStructSignature>();

        public GStructSignature(string name, List<GDataSymbol> fields)
        {
            Name = name;
            Fields = fields;
        }

        public static void IncludeSignature(GStructSignature signature)
        {
            if (StructSignatures.ContainsKey(signature.Name)) return;
            StructSignatures.Add(signature.Name, signature);
        }

        public static GStructSignature GetSignature(string name)
        {
            if (StructSignatures.ContainsKey(name)) return StructSignatures[name];
            throw new Exception($"Struct Signature \"{name}\" Not Found.");
        }


        public override string ToString()
        {
            return $"<Name:{Name},FieldCount:{Fields.Count}>";
        }
    }

    public class FunctionExtractor : gLangBaseVisitor<string>
    {
        public override string VisitHeader_statement([NotNull] gLangParser.Header_statementContext context)
        {
            string name = context.SYMBOL_NAME().GetText();
            var arguments = context.function_parameter_decl();

            List<GDataSymbol> parameters = new List<GDataSymbol>();
            foreach (var arg in arguments)
            {
                parameters.Add(
                    new GDataSymbol(
                        arg.SYMBOL_NAME().GetText(),
                        new GDataType(arg.datatype().GetText()),
                        -1
                    )
                );
            }

            GDataType return_type =
                (context.function_return_type() == null)
                ? null
                : new GDataType(context.function_return_type().datatype().GetText());
            GFunctionSignature signature = new GFunctionSignature(name, parameters, return_type, true);

            GFunctionSignature.IncludeSignature(signature);
            return null;
        }

        public override string VisitFunction_declaration([NotNull] gLangParser.Function_declarationContext context)
        {
            string name = context.SYMBOL_NAME().GetText();
            var arguments = context.function_parameter_decl();
            List<GDataSymbol> parameters = new List<GDataSymbol>();
            foreach (var arg in arguments)
            {
                parameters.Add(
                    new GDataSymbol(
                        arg.SYMBOL_NAME().GetText(),
                        new GDataType(arg.datatype().GetText()),
                        -1
                    )
                );
            }

            GDataType return_type =
                (context.function_return_type() == null)
                ? null
                : new GDataType(context.function_return_type().datatype().GetText());

            GFunctionSignature signature = new GFunctionSignature(name, parameters, return_type, false);

            GFunctionSignature.IncludeSignature(signature);
            return null;
        }
    }

    public class StructExtractor : gLangBaseVisitor<string>
    {
        public override string VisitStruct_definition(gLangParser.Struct_definitionContext context)
        {
            var name = context.SYMBOL_NAME().GetText();
            var fields = new List<GDataSymbol>();

            foreach (var raw_field in context.function_parameter_decl())
            {
                
            }

            GStructSignature signature = new GStructSignature(name, fields);
            Console.WriteLine($"Extracted Struct Signature: {signature.ToString()}");
            GStructSignature.IncludeSignature(signature);
            return null;
        }
    }

    public class StringLiteralExtractor : gLangBaseVisitor<string>
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
                throw new Exception("Could not find symbol for string literal.");
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
            return null;
        }
    }
}
