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
                         "global main\n" +
                         "extern memcpy ; Make sure we always have this\n";
            foreach (var header in headers)
            {
                ASM += Visit(header);
            }
            ASM += "section .data\n";

            var dataTable = StringLiteralExtractor.StringLiteralHolder.stringValueToSymbol;

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
            GFunctionSignature signature = GFunctionSignature.GetSignature(context.SYMBOL_NAME().GetText());
            string ASM = $"\n;function declaration {signature.Name}\n{context.SYMBOL_NAME().GetText()}:\n" +
                          "push ebp\n" +
                          "mov ebp, esp\n";

            var stmt_block = context.statement_block();

            var parameters = context.function_parameter_decl();

            if (parameters.Length != 0 || signature.ReturnIsSpecial)
            {
                ScopeStack.PushScope(ScopeStack.ScopeType.PARAMETER);
                // TODO: If return type is non-primitive (e.g. struct or fixed-size-array)
                //       Push hidden first parameter as pointer to return space.
                if (signature.ReturnIsSpecial)
                {
                    ScopeStack.IncludeSymbol("__retSpecial", GDataType.MakePointerOf(signature.ReturnType));
                }
                foreach (var parameter in parameters)
                {
                    ScopeStack.IncludeSymbol(parameter.SYMBOL_NAME().GetText(), new GDataType(parameter.datatype().GetText()));
                }
            }
            ScopeStack.PushScope(ScopeStack.ScopeType.FUNCTION, signature);

            string innerASM = "";
            foreach (var stmt in stmt_block.statement())
            {
                innerASM += VisitStatement(stmt);
            }
            ASM += $"sub esp, {ScopeStack.GetFunctionScopeSize() - 4}\n"; // Remove starting offset bias.
            ASM += innerASM + $"\n;end function {context.SYMBOL_NAME().GetText()}\n";

            ScopeStack.PopScope(ScopeStack.ScopeType.FUNCTION);
            if (parameters.Length != 0)
            {
                ScopeStack.PopScope(ScopeStack.ScopeType.PARAMETER);
            }
            return ASM;
        }

        public override string VisitVariableDecl([NotNull] gLangParser.VariableDeclContext context)
        {
            string symbolName = context.SYMBOL_NAME().GetText();
            string datatypeString = context.datatype().GetText();

            GDataType lhs_type = new GDataType(datatypeString);

            ScopeStack.IncludeSymbol(symbolName, lhs_type);

            string asm = "";

            if (context.NULL_OP() != null)
            {
                return ""; // we are not initializing the memory with anything.
            }
            asm += $"\n;variable {symbolName}={context.expression().GetText()}\n";

            ExprVisitor visitor = new ExprVisitor();
            ExprNode root = visitor.Visit(context.expression());
            asm += ExprEvaluator.EvaluateExpressionTree(root);

            if (lhs_type.IsPrimitive)
            {
                asm += "pop eax\n";
                // Using EAX to move full value into variable.
                // This is because we don't properly handle variables below 4 bytes.
                // If we say try and printf a u16 what happens is the two padding bytes
                // (which could have any value) get loaded into printf.
                // We could add extra instructions so that we only push the 2 bytes
                // but that isn't implemented. Since everything is 4 byte aligned,
                // we can safely overwrite the padding bytes to 0 and everything should
                // still work.
                asm += $"mov {ScopeStack.GetSymbolOffsetString(symbolName)}, eax\n";
            }
            else
            {
                GDataType rhs_type = root.EvaluateExpressionType();
                asm += $"mov eax, esp ; Store rhs address\n" +
                       $"push {rhs_type.AlignedSize} ; Push size\n" +
                       $"push eax ; Push rhs address\n" +
                       $"lea eax, {ScopeStack.GetSymbolOffsetString(symbolName)} ; Compute lhs address\n" +
                       $"push eax ; Push lhs address\n" +
                       $"call memcpy\n" +
                       $"add esp, 12\n" +
                       $"add esp, {rhs_type.AlignedSize} ; restore stack from temporary type\n";
            }
            return asm;
        }

        public override string VisitVariableStdAssign([NotNull] gLangParser.VariableStdAssignContext context)
        {
            string symbolName = context.SYMBOL_NAME().GetText();

            GDataSymbol symbol = ScopeStack.GetSymbol(symbolName);

            string asm = $"\n;variable {symbolName}={context.expression().GetText()}\n";

            ExprVisitor visitor = new ExprVisitor();
            ExprNode root = visitor.Visit(context.expression());
            asm += ExprEvaluator.EvaluateExpressionTree(root);

            GDataType type = root.EvaluateExpressionType();
            if (type.IsPrimitive == false)
            {
                // Stack should contain the non primitive type
                // need to memcpy result into LHS and clean stack

                asm += ";lea edx, esp ; Store current stack address in edx\n" +
                      $"lea eax, {ScopeStack.GetSymbolOffsetString(symbolName)} ; Store LHS address in eax\n";
                // edx has pointer to "from" space
                // TODO: COME BACK TO THIS AND CHECK IT
                // eax has pointer to "to" space
                asm += $"push {type.AlignedSize} ; Push size\n" +
                       $"push edx ; Push src*\n" +
                       $"push eax ; Push dst*\n" +
                       $"call memcpy\n" +
                       $"add esp, 12 ; restore stack from memcpy\n" +
                       $"add esp, {type.AlignedSize} ; restore stack from temporary type\n";
            }
            else
            {
                asm += "pop eax\n";
                asm += $"mov {ScopeStack.GetSymbolOffsetString(symbolName)}, {symbol.Type.MemoryRegister}\n";
            }

            return asm;
        }

        public override string VisitVariableDerefAssign([NotNull] gLangParser.VariableDerefAssignContext context)
        {
            string symbolName = context.SYMBOL_NAME().GetText();

            GDataSymbol symbol = ScopeStack.GetSymbol(symbolName);

            string asm = $"\n;variable {(context.DOLLAR() == null ? "" : "$")}{symbolName}={context.expression().GetText()}\n";



            ExprVisitor visitor = new ExprVisitor();
            ExprNode rhs_root = visitor.Visit(context.expression());
            asm += ExprEvaluator.EvaluateExpressionTree(rhs_root);

            GDataType rhs_type = rhs_root.EvaluateExpressionType();
            if (rhs_type.IsPrimitive == false)
            {
                asm += $"mov eax, esp ; Store rhs address\n" +
                       $"push {rhs_type.AlignedSize} ; Push size\n" +
                       $"push eax ; Push rhs address" +
                       $"lea eax, {ScopeStack.GetSymbolOffsetString(symbolName)} ; Compute lhs address\n" +
                       $"mov eax, [eax] ; Deref pointer\n" +
                       $"push eax ; Push lhs address\n" +
                       $"call memcpy\n" +
                       $"add esp, 12\n" +
                       $"add esp, {rhs_type.AlignedSize} ; restore stack from temporary type\n";
            }
            else
            {
                asm += "pop eax\n";
                asm += $"lea edx, {ScopeStack.GetSymbolOffsetString(symbolName)} ; Move address of pointer varaible into edx\n" +
                       $"mov edx, [edx] ; Deref the pointer into edx\n" +
                       $"mov [edx], {symbol.Type.UnderlyingDataType.MemoryRegister} ; mov right hand result into address in edx.\n";
            }

            return asm;
        }

        public override string VisitVariableDerefExprAssign([NotNull] gLangParser.VariableDerefExprAssignContext context)
        {
            string asm = ";variable defref expr\n";

            ExprVisitor lhs_visitor = new ExprVisitor();
            ExprNode lhs_root = lhs_visitor.Visit(context.expression(0));
            string lhs_asm = ExprEvaluator.EvaluateExpressionTree(lhs_root);

            GDataType lhs_type = lhs_root.EvaluateExpressionType();

            ExprVisitor rhs_visitor = new ExprVisitor();
            ExprNode rhs_root = rhs_visitor.Visit(context.expression(1));
            string rhs_asm = ExprEvaluator.EvaluateExpressionTree(rhs_root);

            // TODO: Once we have floats, this changes things


            GDataType rhs_type = rhs_root.EvaluateExpressionType();
            if (rhs_type.IsPrimitive == false)
            {
                asm += rhs_asm; // stack will contain rhs
                asm += lhs_asm; // stack will contain address to write to
                asm += $"pop edx ; Store lhs_address\n";
                asm += $"mov eax, esp ; Store rhs address\n" +
                       $"push {rhs_type.AlignedSize} ; Push size\n" +
                       $"push eax ; Push rhs address" +
                       $"call memcpy\n" +
                       $"add esp, 12\n" +
                       $"add esp, {rhs_type.AlignedSize} ; restore stack from temporary type\n";
            }
            else
            {
                asm += rhs_asm; // stack will contain value to write
                asm += lhs_asm; // stack will contain address to write to
                asm += "pop edx ; Load address\n" +
                       "pop eax ; Load value\n" +
                       $"mov [edx], {lhs_type.UnderlyingDataType.MemoryRegister} ; type sensitive write\n";
            }
            return asm;
        }

        public override string VisitVariableIncrementAssign([NotNull] gLangParser.VariableIncrementAssignContext context)
        {
            string symbolName = context.SYMBOL_NAME().GetText();

            ExprNode root = new PostIncrementLiteral(new SymbolLiteralExprNode(symbolName));
            return root.GenerateASM();
        }

        public override string VisitVariableDecrementAssign([NotNull] gLangParser.VariableDecrementAssignContext context)
        {
            string symbolName = context.SYMBOL_NAME().GetText();

            ExprNode root = new PostDecrementLiteral(new SymbolLiteralExprNode(symbolName));
            return root.GenerateASM();
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

        public override string VisitIf_statement([NotNull] gLangParser.If_statementContext context)
        {
            var if_condition = context.logical_expression();
            var if_body = context.statement_block();
            var elseifs = context.else_if();
            var else_stmt = context.else_stmt();

            string asm = "; IF BLOCK\n";

            int current_if_level = ScopeStack.PushScope(ScopeStack.ScopeType.IF);

            asm += EvaluateLogicalExpressionASM(if_condition);
            asm += "pop eax\n ; eax now has result of condition (either 0 or 1)\n" +
                   "test eax, 1\n ; check if condition was true or false\n"; // TODO: Change conditional requirement to 0 is false, non-zero is true

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

            string body_asm = Visit(if_body); // push all of if-body code.
            int localSize = ScopeStack.GetCurrentLocalSize();
            if (localSize > 0)
            {
                asm += $"sub esp, {localSize} ; make room for block locals\n";
            }
            asm += body_asm;
            ScopeStack.PopScope(ScopeStack.ScopeType.IF);
            if (localSize > 0)
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
                    nextLabel = $".__elseif_{current_if_level}_{i + 1}";
                }
                else if (else_stmt != null)
                {
                    nextLabel = $".__else_{current_if_level}";
                }

                asm += $"jz {nextLabel} ; jump to next label if condition is false\n";

                ScopeStack.PushScope(ScopeStack.ScopeType.IF);
                body_asm = Visit(elseifs[i].statement_block()); // push all of if-body code.
                localSize = ScopeStack.GetCurrentLocalSize();
                if (localSize > 0)
                {
                    asm += $"sub esp, {localSize} ; make room for block locals\n";
                }
                asm += body_asm;
                ScopeStack.PopScope(ScopeStack.ScopeType.IF);
                if (localSize > 0)
                {
                    asm += $"add esp, {localSize} ; restore stack pointer from locals in if_block\n";
                }

                asm += $"jmp .__endif_{current_if_level}\n ; Jump out of if-block once completed.\n";
            }

            if (else_stmt != null)
            {
                asm += $".__else_{current_if_level}:\n";

                ScopeStack.PushScope(ScopeStack.ScopeType.IF);
                body_asm = Visit(else_stmt.statement_block()); // push all of if-body code.
                localSize = ScopeStack.GetCurrentLocalSize();
                if (localSize != 0)
                {
                    asm += $"sub esp, {localSize} ; make room for block locals\n";
                }
                asm += body_asm;
                ScopeStack.PopScope(ScopeStack.ScopeType.IF);
                if (localSize != 0)
                {
                    asm += $"add esp, {localSize} ; restore stack pointer from locals in if_block\n";
                }
            }

            asm += $".__endif_{current_if_level}:\n";
            return asm;
        }

        public override string VisitFor_statement([NotNull] gLangParser.For_statementContext context)
        {
            int currentFor = ScopeStack.PushScope(ScopeStack.ScopeType.FOR);
            string asm = "";

            string initial_condition_asm = context.for_initial() == null ? "" : Visit(context.for_initial());
            string loop_condition_check_asm = context.logical_expression() == null ? ""
                    : (EvaluateLogicalExpressionASM(context.logical_expression()) +
                        "pop eax\n ; eax now has result of condition (either 0 or 1)\n" +
                        "cmp eax, 0\n ; check if condition was true or false\n" +
                        $"je .__forend_natural_{currentFor}\n");


            string body_asm = VisitStatement_block(context.statement_block());
            int localSize = ScopeStack.GetCurrentLocalSize(); // For loops size

            string incrementor_statement_asm = context.for_incrementor() == null ? "" : Visit(context.for_incrementor());

            asm += initial_condition_asm;
            if (localSize != 0)
            {
                asm += $"sub esp, {localSize} ; make room for scope variables\n";
            }
            asm += $".__for_{currentFor}:\n";

            asm += "; loop condition\n";
            asm += loop_condition_check_asm;
            asm += "; for body\n";
            asm += body_asm;
            asm += "; incrementor\n" +
                  $".__forinc_{currentFor}:\n";
            asm += incrementor_statement_asm;
            asm += "; jump to start of loop\n";
            asm += $"jmp .__for_{currentFor}\n";

            asm += $".__forend_natural_{currentFor}:\n";
            asm += $"add esp, {localSize} ; pop natural for scope\n";

            asm += $".__forend_{currentFor}:\n";

            ScopeStack.PopScope(ScopeStack.ScopeType.FOR);
            return asm;
        }

        public override string VisitWhile_statement([NotNull] gLangParser.While_statementContext context)
        {
            int currentWhile = ScopeStack.PushScope(ScopeStack.ScopeType.WHILE);
            string asm = $".__while_{currentWhile}:\n";

            asm += EvaluateLogicalExpressionASM(context.logical_expression());
            asm += "pop eax\n ; eax now has result of condition (either 0 or 1)\n" +
                   "cmp eax, 0\n ; check if condition was true or false\n" +
                   $"je .__whileend_{currentWhile}\n";

            string body_asm = Visit(context.statement_block());
            int localSize = ScopeStack.GetCurrentLocalSize();
            if (localSize != 0)
            {
                asm += $"sub esp, {localSize} ; make room for block locals\n";
            }
            asm += body_asm;
            asm += $"jmp .__while_{currentWhile}\n";
            ScopeStack.PopScope(ScopeStack.ScopeType.WHILE);
            if (localSize != 0)
            {
                asm += $"add esp, {localSize} ; restore stack pointer from locals in while_block\n";
            }
            asm += $".__whileend_{currentWhile}:\n";

            return asm;
        }

        public override string VisitBreak_stmt([NotNull] gLangParser.Break_stmtContext context)
        {
            ScopeStack.IncrementingScope scope = ScopeStack.GetBreakScope();

            string asm = "";
            int size = ScopeStack.GetSizeUnderScope(scope);
            if (size != 0)
            {
                asm += $"add esp, {size} ; clean up sub stack\n";
            }
            asm += $"jmp {scope.BreakLabel} ; break\n";
            return asm;
        }

        public override string VisitContinue_stmt([NotNull] gLangParser.Continue_stmtContext context)
        {
            string continueLabel = ScopeStack.GetContinueLabel();
            return $"jmp {continueLabel} ; continue\n";
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
            return ExprEvaluator.EvaluateExpressionTree(root);
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
            GFunctionSignature signature
                = (ScopeStack.GetInnerScopeOf(ScopeStack.ScopeType.FUNCTION)
                    as ScopeStack.FunctionScope).Signature;

            string asm = "";

            if (context.expression() != null)
            {
                asm += EvaluateExpressionASM(context.expression());
                if (signature.ReturnIsSpecial)
                {
                    // NOTE: IS THIS EVEN USED..?
                    // GDataSymbol retSymbol = ScopeStack.GetSymbol("__retSpecial");
                    // stack holds special thing
                    asm += $"mov edx, esp ; Compute stack address\n" +
                           $"push {signature.ReturnSize} ; Push size\n" +
                           $"push edx; Push src*\n" +
                           $"lea edx, {ScopeStack.GetSymbolOffsetString("__retSpecial")} ; Compute address of return space\n" +
                           $"push edx ; Push dst*\n" +
                           $"call memcpy\n" +
                           $"add esp, 12\n" +
                           $"add esp, {signature.ReturnSize} ; clean up special stack\n";
                    asm += "mov eax, edx ; Move address of special stack into eax\n";
                }
                else
                {
                    asm += "pop eax\n";
                }
            }

            return asm +
                   "\n;return\n" +
                   "mov esp, ebp\n" +
                   "pop ebp\n" +
                   "ret\n";
        }

        public override string VisitFunction_call([NotNull] gLangParser.Function_callContext context)
        {
            GFunctionSignature signature = GFunctionSignature.GetSignature(context.SYMBOL_NAME().GetText());

            bool hasNonPrimitiveReturn = signature.ReturnType != null && signature.ReturnType.IsPrimitive == false;

            string asm = "";
            int returnSize = 0;
            if (hasNonPrimitiveReturn)
            {
                // Non primative return type.
                returnSize = signature.ReturnType.AlignedSize;
                asm += $"sub esp, {returnSize} ; Push space for return type\n";
            }

            if (context.function_arguments() != null && context.function_arguments().expression().Length != 0)
            {
                if (signature.IsExtern == false && context.function_arguments().expression().Length != signature.Parameters.Count)
                    throw new Exception("Incorrect number of parameters passed to function.");

                var args = context.function_arguments().expression();
                asm = $"";

                foreach (var argv in args.Reverse())
                {
                    if (argv is gLangParser.StringLiteralContext)
                    {
                        string arg = argv.GetText();
                        string symbol = StringLiteralExtractor.StringLiteralHolder.GetStringSymbol(arg);

                        asm += $"push {symbol}\n";
                        continue;
                    }
                    // TODO: these could be non-primitives, if so, evaluate expression leaving
                    //       non-primitive on stack to be passed as parameter.
                    asm += EvaluateExpressionASM(argv);
                    //asm += "pop eax\n";
                    //asm += "push eax\n";
                }

                if (hasNonPrimitiveReturn)
                {
                    // Non primative return type.
                    asm += $"lea edx, [esp+{args.Length * 4}] ; lowest address related to special space" +
                           $"push edx ; push return space pointer as hidden first parameter\n";
                }

                asm += $"call {signature.Name}\n" +
                       $"add esp, {(args.Length * 4)}\n"; // TODO: Fix assumption of 4 bytes per arg

                // Remove special stack from standalone function call.
                if (hasNonPrimitiveReturn)
                {
                    asm += $"add esp, {returnSize} ; clean up special stack\n";
                }
                return asm;
            }

            if (hasNonPrimitiveReturn)
            {
                // Non primative return type.
                asm += "push esp ; push return space pointer as hidden first parameter\n";
            }

            asm += $"call {signature.Name}\n";

            // Remove special stack from standalone function call.
            if (hasNonPrimitiveReturn)
            {
                asm += $"add esp, {returnSize} ; clean up special stack\n";
            }
            return asm;
        }
    }
}
