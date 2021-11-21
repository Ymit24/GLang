using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Example.Generated;
using Antlr4.Runtime.Misc;

namespace AntlrTest
{
    public enum ExprNodeType
    {
        LITERAL,
        FUNCTION_CALL,
        ADD,
        SUB,
        MUL,
        DIV,
        NEG,

        PREFIX_INC,
        PREFIX_DEC,
        POSTFIX_INC,
        POSTFIX_DEC,

        AND,
        OR,
        EQEQ,
        LSS,
        LEQ,
        GTR,
        GEQ,
        NEQ
    }

    public abstract class ExprNode
    {
        public ExprNodeType type;

        protected ExprNode(ExprNodeType type) { this.type = type; }
        public abstract void Evaluate();
        public abstract string GenerateASM();
        public abstract List<ExprNode> GetChildren();

        public GDataType EvaluateExpressionType()
        {
            GDataType type = null;
            List<ExprNode> children = GetChildren();
            children.Add(this);

            while (children.Count > 0)
            {
                ExprNode node = children[0];
                children.RemoveAt(0);

                if (node is RefExpr)
                {
                    string symbolName = (node as RefExpr).symbol;
                    type = ScopeStack.GetSymbol(symbolName).Type;
                    break;
                }
                if (node is SymbolLiteralExprNode)
                {
                    SymbolLiteralExprNode symbolNode = node as SymbolLiteralExprNode;
                    GDataSymbol symbol = ScopeStack.GetSymbol(symbolNode.symbol);
                    if (symbol.Type.IsPointer)
                    {
                        type = symbol.Type;
                    }
                }
            }

            if (type == null)
            {
                throw new Exception("Could not determine type of expression.");
            }

            return type;
        }
    }

    public abstract class BinExprNode : ExprNode
    {
        public ExprNode left, right;
        protected BinExprNode(ExprNodeType type, ExprNode left, ExprNode right)
            : base(type)
        {
            this.left = left;
            this.right = right;
        }
        public override List<ExprNode> GetChildren()
        {
            List<ExprNode> children = new List<ExprNode>();
            children.Add(left);
            children.Add(right);
            children.AddRange(left.GetChildren());
            children.AddRange(right.GetChildren());
            return children;
        }
    }

    #region StdExprNodes
    public class AddExprNode : BinExprNode
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

    public class SubExprNode : BinExprNode
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

    public class NegateExprNode : ExprNode
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

        public override List<ExprNode> GetChildren()
        {
            return new List<ExprNode>() { value };
        }
    }

    public class PostIncrementLiteral : ExprNode
    {
        public SymbolLiteralExprNode value;
        public PostIncrementLiteral(SymbolLiteralExprNode value) : base(ExprNodeType.POSTFIX_INC) { this.value = value; }
        public override void Evaluate()
        {
            value.Evaluate();
            ExprEvaluator.currentExprStack.Add(this);
        }

        public override string GenerateASM()
        {
            // TODO: RESPECT DATASIZE
            return $"inc {ScopeStack.GetSymbol(value.symbol).Type.AsmType} {ScopeStack.GetSymbolOffsetString(value.symbol)} ; Increment {value.symbol}\n";
        }

        public override List<ExprNode> GetChildren()
        {
            return new List<ExprNode>() { value };
        }
    }

    public class PostDecrementLiteral : ExprNode
    {
        public SymbolLiteralExprNode value;
        public PostDecrementLiteral(SymbolLiteralExprNode value) : base(ExprNodeType.POSTFIX_DEC) { this.value = value; }
        public override void Evaluate()
        {
            value.Evaluate();
            ExprEvaluator.currentExprStack.Add(this);
        }

        public override string GenerateASM()
        {
            // TODO: RESPECT DATASIZE
            return $"dec {ScopeStack.GetSymbol(value.symbol).Type.AsmType} {ScopeStack.GetSymbolOffsetString(value.symbol)} ; Increment {value.symbol}\n";
        }

        public override List<ExprNode> GetChildren()
        {
            return new List<ExprNode>() { value };
        }
    }

    public class PreIncrementLiteral : ExprNode
    {
        public SymbolLiteralExprNode value;
        public PreIncrementLiteral(SymbolLiteralExprNode value) : base(ExprNodeType.PREFIX_INC) { this.value = value; }
        public override void Evaluate()
        {
            ExprEvaluator.currentExprStack.Add(this);
            value.Evaluate();
        }

        public override string GenerateASM()
        {
            // TODO: RESPECT DATASIZE
            return $"inc {ScopeStack.GetSymbol(value.symbol).Type.AsmType} {ScopeStack.GetSymbolOffsetString(value.symbol)} ; Increment {value.symbol}\n";
        }

        public override List<ExprNode> GetChildren()
        {
            return new List<ExprNode>() { value };
        }
    }

    public class PreDecrementLiteral : ExprNode
    {
        public SymbolLiteralExprNode value;
        public PreDecrementLiteral(SymbolLiteralExprNode value) : base(ExprNodeType.POSTFIX_DEC) { this.value = value; }
        public override void Evaluate()
        {
            ExprEvaluator.currentExprStack.Add(this);
            value.Evaluate();
        }

        public override string GenerateASM()
        {
            // TODO: RESPECT DATASIZE
            return $"dec {ScopeStack.GetSymbol(value.symbol).Type.AsmType} {ScopeStack.GetSymbolOffsetString(value.symbol)} ; Increment {value.symbol}\n";
        }

        public override List<ExprNode> GetChildren()
        {
            return new List<ExprNode>() { value };
        }
    }

    public class NumberExprNode : ExprNode
    {
        public int value;
        public NumberExprNode(int value) : base(ExprNodeType.LITERAL) { this.value = value; }
        public override void Evaluate()
        {
            ExprEvaluator.currentExprStack.Add(this);
        }
        public override string GenerateASM()
        {
            return $"push DWORD {value} ; Push {value}\n";
        }

        public override List<ExprNode> GetChildren()
        {
            return new List<ExprNode>() { };
        }
    }

    public class FunctionCallExprNode : ExprNode
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
                   $"add esp, {arguments.Length * 4}\n" +
                   $"push eax\n";
            return asm;
        }

        public override List<ExprNode> GetChildren()
        {
            throw new NotImplementedException();
        }
    }

    public class DefrefExpr : ExprNode
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
            GDataType type = operand.EvaluateExpressionType();

            string asmType = (type.IsPointer ? type.UnderlyingDataType.AsmType : type.AsmType);

            string asm = "pop eax ; Load address\n";

            if (asmType == "DWORD")
            {
                asm += "mov eax, [eax] ; Type sensitive Read\n";
            }
            else
            {
                asm += $"movzx eax, {asmType} [eax] ; Type sensitive Read\n";
            }

            asm += "push eax ; Push Value\n";
            return asm;
        }

        public override List<ExprNode> GetChildren()
        {
            return new List<ExprNode>() { operand };
        }
    }

    public class RefExpr : ExprNode
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
                throw new Exception("failed to find offset for symbol: " + symbol);
            }
            string offsetValue = (offset < 0) ? $"+{Math.Abs(offset)}" : $"-{offset}";

            return $"lea eax, [ebp{offsetValue}]; Get address of {symbol}\npush eax; push address onto stack\n";
        }

        public override List<ExprNode> GetChildren()
        {
            return new List<ExprNode>() { new SymbolLiteralExprNode(symbol) };
        }
    }

    public class SymbolLiteralExprNode : ExprNode
    {
        public string symbol;
        public SymbolLiteralExprNode(string symbol) : base(ExprNodeType.LITERAL) { this.symbol = symbol; }
        public override void Evaluate()
        {
            ExprEvaluator.currentExprStack.Add(this);
        }
        public override string GenerateASM()
        {
            return $"push DWORD {ScopeStack.GetSymbolOffsetString(symbol)} ; Push {symbol}\n";
        }

        public override List<ExprNode> GetChildren()
        {
            return new List<ExprNode>() { };
        }
    }

    public class StringLiteralExprNode : ExprNode
    {
        public string literal;
        public StringLiteralExprNode(string literal) : base(ExprNodeType.LITERAL) { this.literal = literal; }

        public override void Evaluate()
        {
            ExprEvaluator.currentExprStack.Add(this);
        }

        public override string GenerateASM()
        {
            string symbol = LiteralValueExtractorVisitor.StringLiteralHolder.GetStringSymbol(literal);
            return $"push {symbol} ; Push symbol for string literal\n";
        }

        public override List<ExprNode> GetChildren()
        {
            return new List<ExprNode>() { };
        }
    }
    #endregion
    #region LogicExprNodes
    public class EqEqExprNode : BinExprNode
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

    public class NeqExprNode : BinExprNode
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
            string asm = "; != expression\n" +
                         "pop edx\n" +
                         "pop eax\n" +
                         "cmp eax,edx\n" +
                         "setne al\n" +
                         "push eax\n";
            return asm;
        }
    }

    public class AndExprNode : BinExprNode
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
                         "pop edx\n" +
                         "pop eax\n" +
                         "test eax, edx\n" +
                         "setnz al ; make sure lhs & rhs == 1 (CONDITIONAL AND)\n" +
                         "push eax\n";
            return asm;
        }
    }

    public class OrExprNode : BinExprNode
    {
        public OrExprNode(ExprNode left, ExprNode right) : base(ExprNodeType.OR, left, right) { }

        public override void Evaluate()
        {
            left.Evaluate();
            right.Evaluate();
            ExprEvaluator.currentExprStack.Add(this);
        }

        public override string GenerateASM()
        {
            string asm = "; OR expression\n" +
                         "pop edx\n" +
                         "pop eax\n" +
                         "or eax, edx\n" +
                         "push eax\n";
            return asm;
        }
    }

    public class LeqExprNode : BinExprNode
    {
        public LeqExprNode(ExprNode left, ExprNode right) : base(ExprNodeType.LEQ, left, right) { }

        public override void Evaluate()
        {
            left.Evaluate();
            right.Evaluate();
            ExprEvaluator.currentExprStack.Add(this);
        }

        public override string GenerateASM()
        {
            string asm = "; LEQ expression\n" +
                         "pop edx\n" +
                         "pop eax\n" +
                         "cmp eax,edx\n" +
                         "setle al\n" +
                         "push eax\n";
            return asm;
        }
    }

    public class LssExprNode : BinExprNode
    {
        public LssExprNode(ExprNode left, ExprNode right) : base(ExprNodeType.LSS, left, right) { }

        public override void Evaluate()
        {
            left.Evaluate();
            right.Evaluate();
            ExprEvaluator.currentExprStack.Add(this);
        }

        public override string GenerateASM()
        {
            string asm = "; LSS expression\n" +
                         "pop edx\n" +
                         "pop eax\n" +
                         "cmp eax,edx\n" +
                         "setl al\n" +
                         "push eax\n";
            return asm;
        }
    }

    public class GeqExprNode : BinExprNode
    {
        public GeqExprNode(ExprNode left, ExprNode right) : base(ExprNodeType.GEQ, left, right) { }

        public override void Evaluate()
        {
            left.Evaluate();
            right.Evaluate();
            ExprEvaluator.currentExprStack.Add(this);
        }

        public override string GenerateASM()
        {
            string asm = "; GEQ expression\n" +
                         "pop edx\n" +
                         "pop eax\n" +
                         "cmp eax,edx\n" +
                         "setge al\n" +
                         "push eax\n";
            return asm;
        }
    }

    public class GtrExprNode : BinExprNode
    {
        public GtrExprNode(ExprNode left, ExprNode right) : base(ExprNodeType.GTR, left, right) { }

        public override void Evaluate()
        {
            left.Evaluate();
            right.Evaluate();
            ExprEvaluator.currentExprStack.Add(this);
        }

        public override string GenerateASM()
        {
            string asm = "; GTR expression\n" +
                         "pop edx\n" +
                         "pop eax\n" +
                         "cmp eax,edx\n" +
                         "setg al\n" +
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
        public override ExprNode VisitNeqExpr([NotNull] gLangParser.NeqExprContext context)
            { return new NeqExprNode(Visit(context.logical_expression(0)), Visit(context.logical_expression(1))); }
        public override ExprNode VisitAndExpr([NotNull] gLangParser.AndExprContext context)
            { return new AndExprNode(Visit(context.logical_expression(0)), Visit(context.logical_expression(1))); }
        public override ExprNode VisitOrExpr([NotNull] gLangParser.OrExprContext context)
            { return new OrExprNode(Visit(context.logical_expression(0)), Visit(context.logical_expression(1))); }

        public override ExprNode VisitLEQExpr([NotNull] gLangParser.LEQExprContext context)
            { return new LeqExprNode(Visit(context.logical_expression(0)), Visit(context.logical_expression(1))); }
        public override ExprNode VisitLSSExpr([NotNull] gLangParser.LSSExprContext context)
            { return new LssExprNode(Visit(context.logical_expression(0)), Visit(context.logical_expression(1))); }
        public override ExprNode VisitGEQExpr([NotNull] gLangParser.GEQExprContext context)
            { return new GeqExprNode(Visit(context.logical_expression(0)), Visit(context.logical_expression(1))); }
        public override ExprNode VisitGTRExpr([NotNull] gLangParser.GTRExprContext context)
            { return new GtrExprNode(Visit(context.logical_expression(0)), Visit(context.logical_expression(1))); }
    }

    class ExprVisitor : gLangBaseVisitor<ExprNode>
    {
        public override ExprNode VisitAddExpr([NotNull] gLangParser.AddExprContext context)
            { return new AddExprNode(Visit(context.expression(0)), Visit(context.expression(1))); }
        public override ExprNode VisitSubExpr([NotNull] gLangParser.SubExprContext context)
            { return new SubExprNode(Visit(context.expression(0)), Visit(context.expression(1))); }

        public override ExprNode VisitPostIncrementLiteral([NotNull] gLangParser.PostIncrementLiteralContext context)
            { return new PostIncrementLiteral(new SymbolLiteralExprNode(context.SYMBOL_NAME().GetText())); }
        public override ExprNode VisitPostDecrementLiteral([NotNull] gLangParser.PostDecrementLiteralContext context)
            { return new PostDecrementLiteral(new SymbolLiteralExprNode(context.SYMBOL_NAME().GetText())); }
        public override ExprNode VisitPreIncrementLiteral([NotNull] gLangParser.PreIncrementLiteralContext context)
            { return new PreIncrementLiteral(new SymbolLiteralExprNode(context.SYMBOL_NAME().GetText())); }
        public override ExprNode VisitPreDecrementLiteral([NotNull] gLangParser.PreDecrementLiteralContext context)
            { return new PreDecrementLiteral(new SymbolLiteralExprNode(context.SYMBOL_NAME().GetText())); }


        public override ExprNode VisitParenExpr([NotNull] gLangParser.ParenExprContext context)
            { return Visit(context.expression()); }

        public override ExprNode VisitNegateExpr([NotNull] gLangParser.NegateExprContext context)
        {
            return new NegateExprNode(Visit(context.expression()));
        }

        public override ExprNode VisitStringLiteral([NotNull] gLangParser.StringLiteralContext context)
            { return new StringLiteralExprNode(context.STRING().GetText()); }

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
