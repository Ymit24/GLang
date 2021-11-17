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
        NEG
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
            return $"push DWORD [ebp-{offset}] ; Push {symbol}\n"; // TODO: respect datasize.
        }
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
