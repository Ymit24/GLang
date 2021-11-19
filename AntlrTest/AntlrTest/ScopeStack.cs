﻿using System;
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

        public class FunctionScope : Scope
        {
            public FunctionScope() : base(ScopeType.FUNCTION, 4) { }

            public override int IncludeSymbol(string symbolName, GDataType type)
            {
                if (symbolTable.ContainsKey(symbolName)) return -1;

                symbolTable.Add(symbolName, currentOffset);
                int cacheOffset = currentOffset;

                currentOffset += Math.Min(4, GData.GetByteSize(type)); // TODO: CHECK SIZE RESPECTING

                return cacheOffset;
            }
        }

        public class IfScope : Scope
        {
            public IfScope(int startingOffset) : base(ScopeType.IF, startingOffset) { }
            public override int IncludeSymbol(string symbolName, GDataType type)
            {
                if (symbolTable.ContainsKey(symbolName)) return -1;

                symbolTable.Add(symbolName, currentOffset);
                int cacheOffset = currentOffset;

                currentOffset += Math.Min(4, GData.GetByteSize(type)); // TODO: CHECK SIZE RESPECTING

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
            if (!(VariableScope.Peek() is ParameterScope))
            {
                throw new Exception("Tried to pop Parameter scope when next scope up was not Parameter.");
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
                throw new Exception("Tried to pop function scope when next scope up was not function.");
            }
            VariableScope.Pop();
        }

        public static void PushIfScope()
        {
            int size = GetCurrentScopeSize();
            VariableScope.Push(new IfScope(size));
        }

        public static void PopIfScope()
        {
            if (!(VariableScope.Peek() is IfScope))
            {
                throw new Exception("Tried to pop if scope when next scope up was not If.");
            }
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

        public static int GetCurrentScopeSize()
        {
            int upperScope = VariableScope.Count > 2 ? VariableScope.ElementAt(VariableScope.Count - 2).CurrentSize : 0;
            return VariableScope.Peek().CurrentSize - upperScope;
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
