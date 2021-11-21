using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AntlrTest
{
    public class ScopeStack
    {
        public enum ScopeType
        {
            FUNCTION,
            PARAMETER,
            IF,
            FOR,
            WHILE
        }

        public abstract class Scope
        {
            public readonly ScopeType Type;
            protected Dictionary<string, int> symbolTable = new Dictionary<string, int>();
            protected int startingOffset;
            protected int currentOffset;

            public int CurrentOffset
            {
                get
                {
                    return currentOffset;
                }
            }

            public int LocalSize
            {
                get
                {
                    return currentOffset - startingOffset;
                }
            }

            public Scope(ScopeType type, int startingOffset)
            {
                Type = type;
                this.startingOffset = startingOffset;
                currentOffset = startingOffset;
            }

            public abstract int IncludeSymbol(string symbolName, GDataType type);

            public int GetSymbolOffset(string symbolName)
            {
                if (symbolTable.ContainsKey(symbolName)) return symbolTable[symbolName];
                return -1;
            }
        }

        class IncrementingScope : Scope
        {
            public IncrementingScope(ScopeType type, int startingOffset) : base(type, startingOffset) { }
            public override int IncludeSymbol(string symbolName, GDataType type)
            {
                if (symbolTable.ContainsKey(symbolName)) return -1;

                symbolTable.Add(symbolName, currentOffset);
                int cacheOffset = currentOffset;

                currentOffset += Math.Min(4, GData.GetByteSize(type)); // TODO: CHECK SIZE RESPECTING

                return cacheOffset;
            }
        }

        // TODO: Replace scopes with just parameterized incrementing scopes.

        class FunctionScope : IncrementingScope
        {
            public FunctionScope() : base(ScopeType.FUNCTION, 4) { }
        }

        class IfScope : IncrementingScope
        {
            public IfScope(int startingOffset) : base(ScopeType.IF, startingOffset) { }
        }

        class WhileScope : IncrementingScope
        {
            public WhileScope(int startingOffset) : base(ScopeType.WHILE, startingOffset) { }
        }

        class ForScope : IncrementingScope
        {
            public ForScope(int startingOffset) : base(ScopeType.FOR, startingOffset) { }
        }


        public class ParameterScope : Scope
        {
            public ParameterScope() : base(ScopeType.PARAMETER, -8) { }

            public override int IncludeSymbol(string symbolName, GDataType type)
            {
                if (symbolTable.ContainsKey(symbolName)) return -1;

                symbolTable.Add(symbolName, currentOffset);
                int cacheOffset = currentOffset;

                currentOffset -= Math.Min(4, GData.GetByteSize(type)); // TODO: CHECK SIZE RESPECTING

                return cacheOffset;
            }
        }

        private static Stack<Scope> VariableScope = new Stack<Scope>();

        // TODO: REPLACE REDUNDANT PUSH/POP FUNCTIONS

        public static void PushParameterScope()
        {
            VariableScope.Push(new ParameterScope());
        }

        public static void PopParameterScope()
        {
            if (!(VariableScope.Peek() is ParameterScope))
            {
                throw new Exception("Tried to pop Parameter scope when next scope up was not correct type.");
            }
            VariableScope.Pop();
        }

        public static void PushFunctionScope() {
            VariableScope.Push(new FunctionScope());
        }

        public static void PopFunctionScope()
        {
            if (!(VariableScope.Peek() is FunctionScope))
            {
                throw new Exception("Tried to pop function scope when next scope up was not correct type.");
            }
            VariableScope.Pop();
        }

        public static void PushIfScope()
        {
            int size = VariableScope.Peek().CurrentOffset;
            VariableScope.Push(new IfScope(size));
        }

        public static void PopIfScope()
        {
            if (!(VariableScope.Peek() is IfScope))
            {
                throw new Exception("Tried to pop if scope when next scope up was not correct type.");
            }
            VariableScope.Pop();
        }

        public static void PushWhileScope()
        {
            int size = VariableScope.Peek().CurrentOffset;
            VariableScope.Push(new WhileScope(size));
        }

        public static void PopWhileScope()
        {
            if (!(VariableScope.Peek() is WhileScope))
            {
                throw new Exception("Tried to pop while scope when next scope up was not correct type.");
            }
            VariableScope.Pop();
        }

        public static void PushForScope()
        {
            int size = VariableScope.Peek().CurrentOffset;
            VariableScope.Push(new ForScope(size));
        }

        public static void PopForScope()
        {
            if (!(VariableScope.Peek() is ForScope))
            {
                throw new Exception("Tried to pop for scope when next scope up was not correct type.");
            }
            VariableScope.Pop();
        }

        public static int IncludeSymbol(string symbolName, GDataType type)
        {
            return VariableScope.Peek().IncludeSymbol(symbolName, type);
        }

        /// <summary>
        /// Returns string like "[ebp-4]"
        /// </summary>
        /// <param name="symbolName">Symbol name to find offset for</param>
        /// <returns>String offset for embedding into assembly generation.</returns>
        public static string GetSymbolOffsetString(string symbolName)
        {
            int offset = GetSymbolOffset(symbolName);
            if (offset == -1)
            {
                throw new Exception($"Failed to find offset for symbol \"{symbolName}\".");
            }
            string offsetValue = (offset < 0) ? $"+{Math.Abs(offset)}" : $"-{offset}";
            return $"[ebp{offsetValue}]"; // TODO: respect datasize.
        }

        public static int GetSymbolOffset(string symbolName)
        {
            Scope[] scopes = VariableScope.ToArray();

            for (int i = scopes.Length - 1; i >= 0; i--)
            {
                int offset = scopes[i].GetSymbolOffset(symbolName);
                if (offset != -1) return offset;
            }
            return -1;
        }

        public static int GetCurrentLocalSize()
        {
            return VariableScope.Peek().LocalSize;
        }

        public static int GetLocalSizeUpTo(ScopeType type)
        {
            Scope[] scopes = VariableScope.ToArray();
            int size = 0;

            for (int i = scopes.Length - 1; i >= 0; i--)
            {
                size += scopes[i].LocalSize;
                if (scopes[i].Type == type) return size;
            }
            throw new Exception("Could not find scope of type " + type);
        }

        public static int GetFunctionScopeSize()
        {
            Scope[] scopes = VariableScope.ToArray();

            for (int i = scopes.Length - 1; i >= 0; i--)
            {
                if (scopes[i].Type == ScopeType.FUNCTION)
                {
                    return scopes[i].CurrentOffset;
                }
            }
            return -1;
        }
    }
}
