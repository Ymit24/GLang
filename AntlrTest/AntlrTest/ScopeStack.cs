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
            PARAMETER
        }

        public class ParameterScope : Scope
        {
            public ParameterScope() : base(ScopeType.PARAMETER, -8) { }

            public override int IncludeSymbol(string symbolName, GDataType type)
            {
                if (symbolTable.ContainsKey(symbolName)) return -1;

                symbolTable.Add(symbolName, currentOffset);
                int cacheOffset = currentOffset;

                currentOffset -= Math.Min(4, GData.GetByteSize(type));

                return cacheOffset;
            }
        }

        public class FunctionScope : Scope
        {
            public FunctionScope() : base(ScopeType.FUNCTION, 4) { }

            public override int IncludeSymbol(string symbolName, GDataType type)
            {
                if (symbolTable.ContainsKey(symbolName)) return -1;

                symbolTable.Add(symbolName, currentOffset);
                int cacheOffset = currentOffset;

                currentOffset += Math.Min(4, GData.GetByteSize(type));

                return cacheOffset;
            }
        }

        public abstract class Scope
        {
            public readonly ScopeType Type;
            protected Dictionary<string, int> symbolTable = new Dictionary<string, int>();
            protected int currentOffset;

            public int CurrentSize
            {
                get
                {
                    return currentOffset;
                }
            }

            public Scope(ScopeType type, int startingOffset)
            {
                Type = type;
                currentOffset = startingOffset;
            }

            public abstract int IncludeSymbol(string symbolName, GDataType type);

            public int GetSymbolOffset(string symbolName)
            {
                if (symbolTable.ContainsKey(symbolName)) return symbolTable[symbolName];
                return -1;
            }
        }

        private static Stack<Scope> VariableScope = new Stack<Scope>();

        public static void PushParameterScope()
        {
            VariableScope.Push(new ParameterScope());
        }

        public static void PopParameterScope()
        {
            VariableScope.Pop();
        }

        public static void PushFunctionScope() {
            VariableScope.Push(new FunctionScope());
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
            Scope[] scopes = VariableScope.ToArray();

            for (int i = scopes.Length - 1; i >= 0; i--)
            {
                int offset = scopes[i].GetSymbolOffset(symbolName);
                if (offset != -1) return offset;
            }
            return -1;
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
