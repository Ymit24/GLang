using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Example.Generated;
using Antlr4.Runtime.Misc;

namespace AntlrTest
{
    enum ExprNodeType
    {
        LITERAL,
        FUNCTION_CALL,
        ADD,
        SUB,
        MUL,
        DIV,
        NEG,

        AND,
        OR,
        EQEQ,
        LSS,
        LEQ,
        GTR,
        GEQ,
        NEQ
    }
    
    abstract class ExprNode
    {
        public ExprNodeType type;

        protected ExprNode(ExprNodeType type) { this.type = type; }
        public abstract void Evaluate();
        public abstract string GenerateASM();
    }

    abstract class BinExprNode : ExprNode
    {
        public ExprNode left, right;
        protected BinExprNode(ExprNodeType type, ExprNode left, ExprNode right)
            : base(type)
        {
            this.left = left;
            this.right = right;
        }
    }

    #region StdExprNodes
    class AddExprNode : BinExprNode
    {
        public AddExprNode(ExprNode left, ExprNode right) : base(ExprNodeType.ADD, left, right) {}
        public override void Evaluate()
        {
            left.Evaluate();
            right.Evaluate();
            ExprEvaluator.currentExprStack.Add(this);
        }
        public override string GenerateASM()
        {
            return $"pop edx ; Get Right\npop eax ; Get left\nadd eax, edx; Add\npush eax ; Push result\n";
        }
    }
    class SubExprNode : BinExprNode
    {
        public SubExprNode(ExprNode left, ExprNode right) : base(ExprNodeType.SUB, left, right) { }
        public override void Evaluate()
        {
            left.Evaluate();
            right.Evaluate();
            ExprEvaluator.currentExprStack.Add(this);
        }
        public override string GenerateASM()
        {
            return $"pop edx ; Get Right\npop eax ; Get Left\nsub eax, edx ; Subtract\npush eax ; Push result\n";
        }
    }

    class NegateExprNode : ExprNode
    {
        public ExprNode value;
        public NegateExprNode(ExprNode value) : base(ExprNodeType.NEG) { this.value = value; }
        public override void Evaluate()
        {
            value.Evaluate();
            ExprEvaluator.currentExprStack.Add(this);
        }
        
        public override string GenerateASM()
        {
            throw new NotImplementedException();
        }
    }

    class NumberExprNode : ExprNode
    {
        public int value;
        public NumberExprNode(int value) : base(ExprNodeType.LITERAL) { this.value = value; }
        public override void Evaluate()
        {
            ExprEvaluator.currentExprStack.Add(this);
        }
        public override string GenerateASM()
        {
            return $"push DWORD {value} ; Push {value}\n"; // TODO: MAKE THIS DATATYPE FRIENDLY?
        }
    }

    class FunctionCallExprNode : ExprNode
    {
        public string functionName;
        public ExprNode[] arguments;
        public FunctionCallExprNode(string functionName, ExprNode[] arguments)
            : base (ExprNodeType.FUNCTION_CALL)
        {
            this.functionName = functionName;
            this.arguments = arguments;
        }
        
        public override void Evaluate()
        {
            ExprEvaluator.currentExprStack.Add(this);
        }

        public override string GenerateASM()
        {
            string asm = "";
            foreach (ExprNode arg in arguments.Reverse())
            {
                asm += ExprEvaluator.EvaluateExpressionTree(arg);
            }
            asm += $"call {functionName}\n" +
                   $"add esp, {arguments.Length * 4}\n" + // TODO: REMOVE HARDCODE ARG SIZE
                   $"push eax\n";
            return asm;
        }
    }

    class DefrefExpr : ExprNode
    {
        public ExprNode operand;
        public DefrefExpr(ExprNode operand) : base(ExprNodeType.LITERAL) { this.operand = operand; }
        public override void Evaluate()
        {
            operand.Evaluate();
            ExprEvaluator.currentExprStack.Add(this);
        }
        public override string GenerateASM()
        {
            // TODO: Dont hardcode arg size.
            return "pop eax\npush DWORD [eax]; push value at address in eax\n";
        }
    }

    class RefExpr : ExprNode
    {
        public string symbol;
        public RefExpr(string symbol) : base(ExprNodeType.LITERAL) { this.symbol = symbol; }
        public override void Evaluate()
        {
            ExprEvaluator.currentExprStack.Add(this);
        }
        public override string GenerateASM()
        {
            int offset = ScopeStack.GetSymbolOffset(symbol);
            if (offset == -1)
            {
                Console.WriteLine("failed to find offset for symbol: " + symbol);
                return "push DWORD 0\n";
            }
            string offsetValue = (offset < 0) ? $"+{Math.Abs(offset)}" : $"-{offset}";

            return $"lea eax, [ebp{offsetValue}]; Get address of {symbol}\npush eax; push address onto stack\n";
        }
    }

    class SymbolLiteralExprNode : ExprNode
    {
        public string symbol;
        public SymbolLiteralExprNode(string symbol) : base(ExprNodeType.LITERAL) { this.symbol = symbol; }
        public override void Evaluate()
        {
            ExprEvaluator.currentExprStack.Add(this);
        }
        public override string GenerateASM()
        {
            // get offset for symbol
            int offset = ScopeStack.GetSymbolOffset(symbol);
            if (offset == -1)
            {
                Console.WriteLine("failed to find offset for symbol: " + symbol);
                return "push DWORD 0\n";
            }
            string offsetValue = (offset < 0) ? $"+{Math.Abs(offset)}" : $"-{offset}";
            return $"push DWORD [ebp{offsetValue}] ; Push {symbol}\n"; // TODO: respect datasize.
        }
    }
    #endregion
    #region LogicExprNodes
    class EqEqExprNode : BinExprNode
    {
        public EqEqExprNode(ExprNode left, ExprNode right) : base(ExprNodeType.EQEQ, left, right) { }
        public override void Evaluate()
        {
            left.Evaluate();
            right.Evaluate();
            ExprEvaluator.currentExprStack.Add(this);
        }

        public override string GenerateASM()
        {
            string asm = "; == expression\n" +
                         "pop edx\n" +
                         "pop eax\n" +
                         "cmp eax,edx\n" +
                         "sete al\n" +
                         "push eax\n";
            return asm;
        }
    }
    class NeqExprNode : BinExprNode
    {
        public NeqExprNode(ExprNode left, ExprNode right) : base(ExprNodeType.NEQ, left, right) { }
        public override void Evaluate()
        {
            left.Evaluate();
            right.Evaluate();
            ExprEvaluator.currentExprStack.Add(this);
        }

        public override string GenerateASM()
        {
            string asm = "; == expression\n" +
                         "pop eax\n" +
                         "pop edx\n" +
                         "cmp eax,edx\n" +
                         "setz al\n" +
                         "push eax\n";
            return asm;
        }
    }
    class AndExprNode : BinExprNode
    {
        public AndExprNode(ExprNode left, ExprNode right) : base(ExprNodeType.ADD, left, right) { }

        public override void Evaluate()
        {
            left.Evaluate();
            right.Evaluate();
            ExprEvaluator.currentExprStack.Add(this);
        }

        public override string GenerateASM()
        {
            string asm = "; AND expression\n" +
                         "pop eax\n" +
                         "pop edx\n" +
                         "test eax, edx\n" +
                         "setnz al ; make sure lhs & rhs == 1 (CONDITIONAL AND)\n" +
                         "push eax\n";
            return asm;
        }
    }
    #endregion

    class LogicExprVisitor : gLangBaseVisitor<ExprNode>
    {
        public override ExprNode VisitLogicExprLiteral([NotNull] gLangParser.LogicExprLiteralContext context)
        {
            ExprVisitor visitor = new ExprVisitor();
            return visitor.Visit(context.expression());
        }
        public override ExprNode VisitParenLogicExpr([NotNull] gLangParser.ParenLogicExprContext context)
        {
            return Visit(context.logical_expression());
        }
        public override ExprNode VisitEqEqExpr([NotNull] gLangParser.EqEqExprContext context)
            { return new EqEqExprNode(Visit(context.logical_expression(0)), Visit(context.logical_expression(1))); }
        public override ExprNode VisitAndExpr([NotNull] gLangParser.AndExprContext context)
            { return new AndExprNode(Visit(context.logical_expression(0)), Visit(context.logical_expression(1))); }
    }

    class ExprVisitor : gLangBaseVisitor<ExprNode>
    {
        public override ExprNode VisitAddExpr([NotNull] gLangParser.AddExprContext context)
            { return new AddExprNode(Visit(context.expression(0)), Visit(context.expression(1))); }
        public override ExprNode VisitSubExpr([NotNull] gLangParser.SubExprContext context)
            { return new SubExprNode(Visit(context.expression(0)), Visit(context.expression(1))); }

        public override ExprNode VisitParenExpr([NotNull] gLangParser.ParenExprContext context)
            { return Visit(context.expression()); }

        public override ExprNode VisitNegateExpr([NotNull] gLangParser.NegateExprContext context)
        {
            return new NegateExprNode(Visit(context.expression()));
        }

        public override ExprNode VisitNumberLiteral([NotNull] gLangParser.NumberLiteralContext context)
            { return new NumberExprNode(int.Parse(context.NUMBER().GetText())); }
        
        public override ExprNode VisitSymbolLiteral([NotNull] gLangParser.SymbolLiteralContext context)
            { return new SymbolLiteralExprNode(context.SYMBOL_NAME().GetText()); }

        public override ExprNode VisitFuncCallExpr([NotNull] gLangParser.FuncCallExprContext context)
        {
            var args = context.function_call().function_arguments().expression();
            if (args.Length == 0)
                return new FunctionCallExprNode(context.function_call().SYMBOL_NAME().GetText(), new ExprNode[] { });

            List<ExprNode> exprArgs = new List<ExprNode>();
            foreach (var arg in args)
            {
                exprArgs.Add(Visit(arg));
            }
            return new FunctionCallExprNode(
                context.function_call().SYMBOL_NAME().GetText(),
                exprArgs.ToArray()
            );
        }

        public override ExprNode VisitDefrefExpr([NotNull] gLangParser.DefrefExprContext context)
        {
            return new DefrefExpr(Visit(context.expression()));
        }

        public override ExprNode VisitDefrefSymbolLiteral([NotNull] gLangParser.DefrefSymbolLiteralContext context)
        {
            return new DefrefExpr(
                new SymbolLiteralExprNode(context.SYMBOL_NAME().GetText())
            );
        }

        public override ExprNode VisitRefLiteral([NotNull] gLangParser.RefLiteralContext context)
        {
            return new RefExpr(context.SYMBOL_NAME().GetText());
        }
    }

    class ExprEvaluator
    {
        public static List<List<ExprNode>> expressionStacksStack = new List<List<ExprNode>>();
        public static List<ExprNode> currentExprStack
        {
            get
            {
                return expressionStacksStack.Last();
            }
        }

        public static string EvaluateExpressionTree(ExprNode root)
        {
            expressionStacksStack.Add(new List<ExprNode>());
            root.Evaluate();

            string asm = "";
            while (currentExprStack.Count > 0)
            {
                ExprNode current = currentExprStack.First();
                currentExprStack.RemoveAt(0);
                asm += current.GenerateASM();
            }

            expressionStacksStack.RemoveAt(expressionStacksStack.Count - 1);
            return asm;
        }
    }
}
