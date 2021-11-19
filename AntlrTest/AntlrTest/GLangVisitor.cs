using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Example.Generated;
using Antlr4.Runtime.Misc;
namespace AntlrTest
{
    class GLangVisitor : gLangBaseVisitor<string>
    {
        public override string VisitProgram([NotNull] gLangParser.ProgramContext context)
        {
            var headers = context.header_statement();
            var function_declarations = context.function_declaration();
            
            string ASM = "BITS 32\n" +
                         "global main\n";
            foreach (var header in headers)
            {
                ASM += Visit(header);
            }
            ASM += "section .data\n";
            
            var dataTable = LiteralValueExtractorVisitor.StringLiteralHolder.stringValueToSymbol;

            foreach (string key in dataTable.Keys)
            {
                string literal = key.Substring(1, key.Length - 2);
                string code = "";
                for (int i = 0; i < literal.Length; i++)
                {
                    if (i != literal.Length - 1)
                    {
                        bool wasSpecial = true;
                        string seg = literal.Substring(i, 2);
                        if (seg.Equals("\\n"))
                        {
                            code += (i == 0 ? "" : "', ") + "0xA";
                        }
                        else if (seg.Equals("\\0"))
                        {
                            code += (i == 0 ? "" : "', ") + "', 0x0";
                        }
                        else
                        {
                            wasSpecial = false;
                        }
                        if (wasSpecial)
                        {
                            i++;
                            code += (i != literal.Length - 1 ? ", '" : "");
                            continue;
                        }
                    }

                    if (i == 0) { code += "'"; }
                    code += literal[i] + (i == literal.Length - 1 ? "'" : "");
                }
                ASM += $"{dataTable[key]}: db {code}, 0\n";
            }
            ASM += "section .text\n";
            foreach (var func_decl in function_declarations)
            {
                ASM += VisitFunction_declaration(func_decl);
            }
            return ASM;
        }

        public override string VisitHeader_statement([NotNull] gLangParser.Header_statementContext context)
        {
            return $"extern {context.SYMBOL_NAME().GetText()}\n";
        }

        public override string VisitFunction_declaration([NotNull] gLangParser.Function_declarationContext context)
        {
            string ASM = $"\n;function declaration {context.SYMBOL_NAME().GetText()}\n{context.SYMBOL_NAME().GetText()}:\n" +
                          "push ebp\n" +
                          "mov ebp, esp\n";
            
            var stmt_block = context.statement_block();

            var parameters = context.function_parameter_decl();

            if (parameters.Length != 0)
            {
                ScopeStack.PushParameterScope();
                foreach (var parameter in parameters)
                {
                    ScopeStack.IncludeSymbol(parameter.SYMBOL_NAME().GetText(), GetDataType(parameter.DATATYPE().GetText()));
                }
            }
            ScopeStack.PushFunctionScope();
            
            string innerASM = "";
            foreach (var stmt in stmt_block.statement())
            {
                innerASM += VisitStatement(stmt);
            }
            ASM += $"sub esp, {ScopeStack.GetFunctionScopeSize() - 4}\n"; // Remove starting offset bias.
            ASM += innerASM + $"\n;end function {context.SYMBOL_NAME().GetText()}\n";
            
            ScopeStack.PopFunctionScope();
            if (parameters.Length != 0)
            {
                ScopeStack.PopParameterScope();
            }
            return ASM;
        }

        public GDataType GetDataType(string datatypeString)
        {
            GDataType type = GDataType.I32;
            switch (datatypeString.ToLower())
            {
                case "i8": { type = GDataType.I8; break; }
                case "i16": { type = GDataType.I16; break; }
                case "i32": { type = GDataType.I32; break; }
                default: { type = GDataType.I32; break; }
            }
            return type;
        }

        public override string VisitVaraibleDecl([NotNull] gLangParser.VaraibleDeclContext context)
        {
            string symbolName = context.SYMBOL_NAME().GetText();
            string datatype = context.DATATYPE().GetText();
            GDataType type = GetDataType(datatype);

            int offset = ScopeStack.IncludeSymbol(symbolName, type);
            if (offset == -1)
            {
                Console.WriteLine("Failed to get offset for symbol.");
            }

            string asm = $"\n;variable {symbolName}={context.expression().GetText()}\n";

            asm += EvaluateExpressionASM(context.expression());

            string offsetValue = (offset < 0) ? $"+{Math.Abs(offset)}" : $"-{offset}";
            asm += $"mov [ebp{offsetValue}], eax\n";
            return asm;
        }

        public override string VisitVaraibleStdAssign([NotNull] gLangParser.VaraibleStdAssignContext context)
        {
            string symbolName = context.SYMBOL_NAME().GetText();

            int offset = ScopeStack.GetSymbolOffset(symbolName);
            if (offset == -1)
            {
                Console.WriteLine("Failed to get offset for symbol.");
            }

            string asm = $"\n;variable {symbolName}={context.expression().GetText()}\n";

            string offsetValue = (offset < 0) ? $"+{Math.Abs(offset)}" : $"-{offset}";
            asm += EvaluateExpressionASM(context.expression());
            asm += $"mov [ebp{offsetValue}], eax\n";

            return asm;
        }

        public override string VisitVariableDerefAssign([NotNull] gLangParser.VariableDerefAssignContext context)
        {
            string symbolName = context.SYMBOL_NAME().GetText();

            int offset = ScopeStack.GetSymbolOffset(symbolName);
            if (offset == -1)
            {
                Console.WriteLine("Failed to get offset for symbol.");
            }

            string asm = $"\n;variable {(context.DOLLAR() == null ? "" : "$")}{symbolName}={context.expression().GetText()}\n";

            string offsetValue = (offset < 0) ? $"+{Math.Abs(offset)}" : $"-{offset}";
            if (context.DOLLAR() == null)
            {
                asm += EvaluateExpressionASM(context.expression());
                asm += $"mov [ebp{offsetValue}], eax\n";
            }
            else
            {
                asm += EvaluateExpressionASM(context.expression());
                asm += $"lea edx, [ebp{offsetValue}] ; Move address of pointer varaible into edx\n" +
                       $"mov edx, [edx] ; Deref the pointer into edx\n" +
                       $"mov [edx], eax ; mov right hand result into address in edx.\n";
            }
            return asm;
        }

        public override string VisitVariableDerefExprAssign([NotNull] gLangParser.VariableDerefExprAssignContext context)
        {
            string asm = ";variable defref expr\n";

            ExprVisitor lhs_visitor = new ExprVisitor();
            ExprNode lhs_root = lhs_visitor.Visit(context.expression(0));
            string lhs_asm = ExprEvaluator.EvaluateExpressionTree(lhs_root);

            ExprVisitor rhs_visitor = new ExprVisitor();
            ExprNode rhs_root = rhs_visitor.Visit(context.expression(1));
            string rhs_asm = ExprEvaluator.EvaluateExpressionTree(rhs_root);

            asm += rhs_asm; // stack will contain value to write
            asm += lhs_asm; // stack will contain address to write to

            asm += $"pop edx ; store address in edx\n" +
                   $";mov edx, [edx] ; deref address\n" +
                   $"pop DWORD [edx] ; write value into memory\n";
            return asm;
        }

        public override string VisitStatement_block([NotNull] gLangParser.Statement_blockContext context)
        {
            string asm = "";
            foreach (var stmt in context.statement())
            {
                asm += Visit(stmt);
            }
            return asm;
        }

        #region temp_if_tracker
        private int if_counter = 0;
        #endregion

        public override string VisitIf_statement([NotNull] gLangParser.If_statementContext context)
        {
            var if_condition = context.logical_expression();
            var if_body = context.statement_block();
            var elseifs = context.else_if();
            var else_stmt = context.else_stmt();

            string asm = "; IF BLOCK\n";

            int current_if_level = if_counter++; // if we encounter a nested if, we make sure it can have its own level space.

            asm += EvaluateLogicalExpressionASM(if_condition);
            asm += "pop eax\n ; eax now has result of condition (either 0 or 1)\n" +
                   "test eax, 1\n ; check if condition was true or false\n";

            string nextLabel = $".__endif_{current_if_level}";

            if (elseifs.Length != 0)
            {
                nextLabel = $".__elseif_{current_if_level}_0";
            }
            else if (else_stmt != null)
            {
                nextLabel = $".__else_{current_if_level}";
            }

            asm += $"jz {nextLabel} ; jump to next label if condition is false\n";

            ScopeStack.PushIfScope();
            string body_asm = Visit(if_body); // push all of if-body code.
            int localSize = ScopeStack.GetCurrentScopeSize();
            if (localSize != 0) {
                asm += $"sub esp, {localSize} ; make room for block locals\n";
            }
            asm += body_asm;
            ScopeStack.PopIfScope();
            if (localSize != 0)
            {
                asm += $"add esp, {localSize} ; restore stack pointer from locals in if_block\n";
            }
            asm += $"jmp .__endif_{current_if_level}\n ; Jump out of if-block once completed.\n";

            for (int i = 0; i < elseifs.Length; i++)
            {
                asm += $".__elseif_{current_if_level}_{i}:\n";
                var condition = elseifs[i].logical_expression();
                asm += EvaluateLogicalExpressionASM(condition);
                asm += "pop eax\n ; eax now has result of condition (either 0 or 1)\n" +
                       "test eax, 1\n ; check if condition was true or false\n";

                nextLabel = $".__endif_{current_if_level}";
                if (i != elseifs.Length - 1)
                {
                    nextLabel = $".__elseif_{current_if_level}_{i+1}";
                }
                else if (else_stmt != null)
                {
                    nextLabel = $".__else_{current_if_level}";
                }

                asm += $"jz {nextLabel} ; jump to next label if condition is false\n";

                ScopeStack.PushIfScope();
                body_asm = Visit(elseifs[i].statement_block()); // push all of if-body code.
                localSize = ScopeStack.GetCurrentScopeSize();
                if (localSize != 0)
                {
                    asm += $"sub esp, {localSize} ; make room for block locals\n";
                }
                asm += body_asm;
                ScopeStack.PopIfScope();
                if (localSize != 0)
                {
                    asm += $"add esp, {localSize} ; restore stack pointer from locals in if_block\n";
                }

                asm += $"jmp .__endif_{current_if_level}\n ; Jump out of if-block once completed.\n";
            }

            if (else_stmt != null)
            {
                asm += $".__else_{current_if_level}:\n";

                ScopeStack.PushIfScope();
                body_asm = Visit(else_stmt.statement_block()); // push all of if-body code.
                localSize = ScopeStack.GetCurrentScopeSize();
                if (localSize != 0)
                {
                    asm += $"sub esp, {localSize} ; make room for block locals\n";
                }
                asm += body_asm;
                ScopeStack.PopIfScope();
                if (localSize != 0)
                {
                    asm += $"add esp, {localSize} ; restore stack pointer from locals in if_block\n";
                }
            }

            asm += $".__endif_{current_if_level}:\n";
            return asm;
        }

        /// <summary>
        /// This will add a "pop eax\n" line to the end of the expression assembly.
        /// E.g. puts result of expression in EAX register.
        /// </summary>
        /// <param name="context">Expression context to evaluate</param>
        /// <returns>Assembly to evaluate the expression</returns>
        public string EvaluateExpressionASM([NotNull] gLangParser.ExpressionContext context)
        {
            ExprVisitor visitor = new ExprVisitor();
            ExprNode root = visitor.Visit(context);
            string asm = ExprEvaluator.EvaluateExpressionTree(root);
            asm += "pop eax\n";
            return asm;
        }

        /// <summary>
        /// This will leave the result (either 0 or 1) at last position on stack.
        /// </summary>
        /// <param name="context">Logical Expression context to evaluate</param>
        /// <returns>Assembly to evaluate the expression</returns>
        public string EvaluateLogicalExpressionASM([NotNull] gLangParser.Logical_expressionContext context)
        {
            LogicExprVisitor visitor = new LogicExprVisitor();
            ExprNode root = visitor.Visit(context);
            return ExprEvaluator.EvaluateExpressionTree(root);
        }

        public override string VisitReturn_stmt([NotNull] gLangParser.Return_stmtContext context)
        {
            string asm = "";
            if (context.expression() != null)
            {
                asm += EvaluateExpressionASM(context.expression());
            }

            return asm +
                   "\n;return\n" +
                   "mov esp, ebp\n" +
                   "pop ebp\n" +
                   "ret\n";
        }

        public override string VisitFunction_call([NotNull] gLangParser.Function_callContext context)
        {
            if (context.function_arguments() == null)
            {
                return $"\n;call no arguments.\ncall {context.SYMBOL_NAME().GetText()}\n";
            }

            var args = context.function_arguments().expression();
            string ASM = $"\n;call with {args.Length} arguments.\n";

            foreach (var argv in args.Reverse())
            {
                if (argv is gLangParser.StringLiteralContext)
                {
                    string arg = argv.GetText();
                    string symbol = LiteralValueExtractorVisitor.StringLiteralHolder.GetStringSymbol(arg);
                    if (symbol == null)
                    {
                        Console.WriteLine($"Could not expand {arg} to symbol or primative literal.");
                        ASM += "push DWORD 0\n";
                        continue;
                    }
                    ASM += $"push {symbol}\n";
                    continue;
                }

                ASM += EvaluateExpressionASM(argv);
                ASM += "push eax\n";
            }

            ASM += $"call {context.SYMBOL_NAME().GetText()}\n" +
                   $"add esp, {args.Length * 4}\n"; // TODO: Fix assumption of 4 bytes per arg
            return ASM;
        }
    }
}
