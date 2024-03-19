using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using Example.Generated;

namespace AntlrTest
{
    class Program
    {
        class ThrowingErrorListener : BaseErrorListener, IAntlrErrorListener<int>
        {
            public override void SyntaxError([NotNull] IRecognizer recognizer, [Nullable] IToken offendingSymbol, int line, int charPositionInLine, [NotNull] string msg, [Nullable] RecognitionException e)
            {
                throw new Exception($"Syntax error at line: {line}:{charPositionInLine}. Message: {msg}");
            }

            public void SyntaxError([NotNull] IRecognizer recognizer, [Nullable] int offendingSymbol, int line, int charPositionInLine, [NotNull] string msg, [Nullable] RecognitionException e)
            {
                throw new Exception($"Syntax error at line: {line}:{charPositionInLine}. Message: {msg}");
            }
        }
        static void Main(string[] args)
        {
            Console.WriteLine("Compiling..");

            string source = File.ReadAllText("test_program.g");

            var inputStream = new AntlrInputStream(source);
            var lexer = new gLangLexer(inputStream);
            lexer.RemoveErrorListeners();
            lexer.AddErrorListener(new ThrowingErrorListener());
            var tokenStream = new CommonTokenStream(lexer);

            if (false)
            {
                IList<IToken> tokens = lexer.GetAllTokens();
                foreach (IToken token in tokens)
                {
                    string typename = "";
                    foreach (var t in lexer.TokenTypeMap.Keys)
                    {
                        if (lexer.TokenTypeMap[t].Equals(token.Type))
                        {
                            typename = t;
                            break;
                        }
                    }
                    Console.WriteLine($"token: {token.Text}:{token.Type}:{typename}");
                }
            }

            var parser = new gLangParser(tokenStream);
            parser.RemoveErrorListeners();
            parser.AddErrorListener(new ThrowingErrorListener());

            var context = parser.program();

            var stringExtractor = new StringLiteralExtractor();
            var functionExtractor = new FunctionExtractor();
            var structExtractor = new StructExtractor();
            var visitor = new GLangVisitor();

            stringExtractor.Visit(context); // process the program for strings
            structExtractor.Visit(context);// process the program for structs
            functionExtractor.Visit(context);// process the program for functions

            var table = StringLiteralExtractor.StringLiteralHolder.stringValueToSymbol;
            Console.WriteLine($"string literals found: {table.Count}.");

            string code = visitor.Visit(context);
            Console.WriteLine("Output:\n" + code);

            File.WriteAllText("compiled.nasm", code);
        }

        static void ExecuteCommand(string Command)
        {
            ProcessStartInfo ProcessInfo;
            Process Process;

            ProcessInfo = new ProcessStartInfo("cmd.exe", "/K " + Command);
            ProcessInfo.CreateNoWindow = true;
            ProcessInfo.UseShellExecute = true;

            Process = Process.Start(ProcessInfo);
        }
    }
}
