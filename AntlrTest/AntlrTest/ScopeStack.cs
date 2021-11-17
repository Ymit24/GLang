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
            FUNCTION
        }

        public enum GDataType
        {
            I32, I16, I8
        }

        public class Scope
        {
            public readonly ScopeType Type;
            private Dictionary<string, int> symbolTable = new Dictionary<string, int>();
            private int currentOffset;

            public int CurrentSize
            {
                get
                {
                    return currentOffset;
                }
            }

            public Scope(ScopeType type, int startingOffset = 4)
            {
                Type = type;
                currentOffset = startingOffset;
            }

            public int IncludeSymbol(string symbolName, GDataType type)
            {
                if (symbolTable.ContainsKey(symbolName)) return -1;

                symbolTable.Add(symbolName, currentOffset);
                int cacheOffset = currentOffset;

                switch (type)
                {
                    case GDataType.I8: { currentOffset += 2; break; }
                    case GDataType.I16: { currentOffset += 2; break; }
                    case GDataType.I32: { currentOffset += 4; break; }
                    default: { currentOffset += 4; break; }
                }

                return cacheOffset;
            }

            public int GetSymbolOffset(string symbolName)
            {
                if (symbolTable.ContainsKey(symbolName)) return symbolTable[symbolName];
                return -1;
            }
        }

        private static Stack<Scope> VariableScope = new Stack<Scope>();

        public static void PushFunctionScope() {
            VariableScope.Push(new Scope(ScopeType.FUNCTION));
        }

        public static void PopFunctionScope() {
            VariableScope.Pop();
        }

        public static int IncludeSymbol(string symbolName, GDataType type)
        {
            return VariableScope.Peek().IncludeSymbol(symbolName, type);
        }

        public static int GetSymbolOffset(string symbolName)
        {
            return VariableScope.Peek().GetSymbolOffset(symbolName);
        }

        public static int GetFunctionScopeSize()
        {
            Scope[] scopes = VariableScope.ToArray();

            for (int i = scopes.Length - 1; i >= 0; i--)
            {
                if (scopes[i].Type == ScopeType.FUNCTION)
                {
                    return scopes[i].CurrentSize;
                }
            }
            return -1;
        }
    }
}
