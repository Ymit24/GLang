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
            protected Dictionary<string, GDataSymbol> symbolTable = new Dictionary<string, GDataSymbol>();
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

            public bool HasSymbol(string symbolName)
            {
                return symbolTable.ContainsKey(symbolName);
            }

            public GDataSymbol GetSymbol(string symbolName)
            {
                if (symbolTable.ContainsKey(symbolName)) return symbolTable[symbolName];
                throw new Exception("Symbol not found.");
            }

            public int GetSymbolOffset(string symbolName)
            {
                return GetSymbol(symbolName).Offset;
            }
        }

        public class IncrementingScope : Scope
        {
            public readonly string BreakLabel, ContinueLabel;
            public IncrementingScope(ScopeType type, int startingOffset, string breakLabel, string continueLabel)
                : base(type, startingOffset)
            {
                BreakLabel = breakLabel;
                ContinueLabel = continueLabel;
            }

            public override int IncludeSymbol(string symbolName, GDataType type)
            {
                if (symbolTable.ContainsKey(symbolName)) return -1;

                symbolTable.Add(symbolName, new GDataSymbol(symbolName, type, currentOffset));
                int cacheOffset = currentOffset;

                currentOffset += type.AlignedSize;

                return cacheOffset;
            }
        }

        public class ParameterScope : Scope
        {
            public ParameterScope() : base(ScopeType.PARAMETER, -8) { }

            public override int IncludeSymbol(string symbolName, GDataType type)
            {
                if (symbolTable.ContainsKey(symbolName)) return -1;

                symbolTable.Add(symbolName, new GDataSymbol(symbolName, type, currentOffset));
                int cacheOffset = currentOffset;

                currentOffset -= type.AlignedSize;

                return cacheOffset;
            }
        }

        private static Stack<Scope> VariableScope = new Stack<Scope>();

        private static int ifCounter = 0;
        private static int whileCounter = 0;
        private static int forCounter = 0;

        public static int PushScope(ScopeType type)
        {
            if (type == ScopeType.PARAMETER)
            {
                VariableScope.Push(new ParameterScope());
                return -1;
            }
            else if (type == ScopeType.FUNCTION)
            {
                VariableScope.Push(new IncrementingScope(type, 4, "_", "_"));
                return -1;
            }
            else
            {
                int size = VariableScope.Peek().CurrentOffset;
                switch (type)
                {
                    case ScopeType.IF: {
                        VariableScope.Push(new IncrementingScope(type, size, "_", "_"));
                        return ifCounter++;
                    }
                    case ScopeType.WHILE: {
                        VariableScope.Push(new IncrementingScope(type, size, $".__whileend_{whileCounter}", $".__while_{whileCounter}"));
                        return whileCounter++;
                    }
                    case ScopeType.FOR: {
                        VariableScope.Push(new IncrementingScope(type, size, $".__forend_{forCounter}", $".__forinc_{forCounter}"));
                        return forCounter++;
                    }
                    default: { throw new Exception("Failed to determine scope type."); }
                }
            }
        }

        public static void PopScope(ScopeType type)
        {
            if (VariableScope.Count == 0)
            {
                throw new Exception("Trying to pop scope when no scopes exist.");
            }
            if (VariableScope.Peek().Type != type)
            {
                throw new Exception("Tried to pop scope when next scope up was not correct type.");
            }
            VariableScope.Pop();
        }

        public static IncrementingScope GetBreakScope()
        {
            if (VariableScope.Count == 0)
            {
                throw new Exception("Trying to find break/continue scope that don't exist.");
            }

            for (int i = 0; i < VariableScope.Count; i++)
            {
                Scope scope = VariableScope.ElementAt(i);
                if (scope is IncrementingScope)
                {
                    if ((scope as IncrementingScope).BreakLabel != "_") return (scope as IncrementingScope);
                }
            }
            throw new Exception("Could not find a viable break/continue scope.");
        }

        public static int GetSizeUnderScope(Scope scope)
        {
            bool foundScope = false;
            int size = 0;
            for (int i = VariableScope.Count - 1; i > -1; i--)
            {
                Scope curr = VariableScope.ElementAt(i);

                if (curr == scope) { foundScope = true;      }
                if (foundScope)    { size += curr.LocalSize; }
            }
            return size;
        }

        public static string GetBreakLabel()
        {
            return GetBreakScope().BreakLabel;
        }

        public static string GetContinueLabel()
        {
            return GetBreakScope().ContinueLabel;
        }

        public static int IncludeSymbol(string symbolName, GDataType type)
        {
            if (VariableScope.Count == 0)
                throw new Exception("Trying to include variable outside of any scopes.");
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

        public static GDataSymbol GetSymbol(string symbolName)
        {
            for (int i = 0; i < VariableScope.Count; i++)
            {
                Scope scope = VariableScope.ElementAt(i);
                if (scope.HasSymbol(symbolName))
                {
                    return scope.GetSymbol(symbolName);
                }
            }
            throw new Exception($"Could not find offset for symbol {symbolName}");
        }

        public static int GetSymbolOffset(string symbolName)
        {
            return GetSymbol(symbolName).Offset;
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
