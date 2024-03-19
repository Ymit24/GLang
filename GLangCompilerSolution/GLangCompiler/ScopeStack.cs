using System;
using System.Collections.Generic;
using System.Linq;

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
            WHILE,
            STRUCT
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
                return GetSymbol(symbolName).EffectiveOffset;
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

            /// Determine the change in offset from the currentOffset that
            /// the IncludeSymbol function will use. This is always a positive
            /// number.
            protected int ComputeInclusionOffset(GDataType type)
            {
                int offset = 0;

                if (type.IsPrimitive)
                {
                    Console.WriteLine($"{type.TypeString} :: Is Word Aligned: {IsWORDAligned()}. Size: {type.AlignedSize}. Current Offset: {currentOffset}");
                    // so u8, u16, u32, etc...
                    if (type.AlignedSize == 4 && !IsDWORDAligned())
                    {
                        // Align ourselves to a DWORD boundary
                        offset += GetDWORDAlignmentOffset();
                    }
                    else if (type.AlignedSize == 2 && !IsWORDAligned())
                    {
                        // Align ourselves to a WORD boundary
                        offset += GetWORDAlignmentOffset();
                    }

                    offset += type.IdealSize;
                }
                else
                {
                    Console.WriteLine($"Computing inclusion offset for non-primitive: {type.TypeString} of size {type.AlignedSize}");
                    // All structs are assumed DWORD aligned.
                    if (!IsDWORDAligned())
                    {
                        offset += GetDWORDAlignmentOffset();
                    }
                    offset += type.AlignedSize;
                }
                Console.WriteLine($"Changed offse: {offset}");
                return offset;
            }

            /// Include a symbol in the scope. Automatically handles adding
            /// padding bytes for alignment.
            public override int IncludeSymbol(string symbolName, GDataType type)
            {
                if (symbolTable.ContainsKey(symbolName)) return -1;

                int offset = ComputeInclusionOffset(type);
                currentOffset += offset;
                symbolTable.Add(symbolName, new GDataSymbol(symbolName, type, currentOffset));
                return currentOffset;
            }

            /// Determine if this scope is aligned to a WORD (16bit) boundary.
            protected bool IsWORDAligned()
            {
                return currentOffset % 2 == 0;
            }

            /// Compute how many bytes are needed to ensure WORD alignment.
            protected int GetWORDAlignmentOffset()
            {
                return (2 - (currentOffset % 2)) % 2;
            }

            /// Determine if this scope is aligned to a WORD (16bit) boundary.
            protected bool IsDWORDAligned()
            {
                return currentOffset % 4 == 0;
            }

            /// Compute how many bytes are needed to ensure DWORD alignment.
            protected int GetDWORDAlignmentOffset()
            {
                return (4 - (currentOffset % 4)) % 4;
            }

            /// Call after all variables have been pushed into the scope.
            /// This will add any extra padding to make sure the scope is 
            /// DWORD aligned.
            ///
            /// Note: If used for struct alignment, this only needs to be aligned
            /// to highest alignment condition. If the struct only contains BYTES
            /// and WORDS, then only WORD alignment is required. This optimization
            /// is currently unimplemented.
            public void CompleteAndAlignStack()
            {
                if (!IsDWORDAligned())
                {
                    currentOffset += GetDWORDAlignmentOffset();
                }
            }
        }

        public class FunctionScope : IncrementingScope
        {
            public readonly GFunctionSignature Signature;
            public FunctionScope(GFunctionSignature signature) : base(ScopeType.FUNCTION, 0, "_", "_")
            {
                Signature = signature;
            }
        }

        public class ParameterScope : IncrementingScope
        {
            public ParameterScope() : base(ScopeType.PARAMETER, -8, "_", "_") { }

            public override int IncludeSymbol(string symbolName, GDataType type)
            {
                if (symbolTable.ContainsKey(symbolName)) return -1;

                // NOTE: All parameters are DWORD aligned.
                symbolTable.Add(symbolName, new GDataSymbol(symbolName, type, currentOffset));

                const int offset = 4;
                currentOffset -= offset;
                return currentOffset;
            }
        }

        public class StructScope : IncrementingScope
        {
            public readonly string Name;
            public StructScope(string name) : base(ScopeType.STRUCT, 0, "_", "_")
            {
                Name = name;
            }
        }

        private static Stack<Scope> VariableScope = new Stack<Scope>();

        private static int ifCounter = 0;
        private static int whileCounter = 0;
        private static int forCounter = 0;

        public static void PushFunctionScope(GFunctionSignature func_signature)
        {
            VariableScope.Push(new FunctionScope(func_signature));
        }

        public static StructScope PushStructScope(string struct_name)
        {
            var scope = new StructScope(struct_name);
            VariableScope.Push(scope);
            return scope;
        }

        public static int PushScope(ScopeType type)
        {
            if (type == ScopeType.PARAMETER)
            {
                VariableScope.Push(new ParameterScope());
                return -1;
            }
            else
            {
                int size = VariableScope.Peek().CurrentOffset;
                switch (type)
                {
                    case ScopeType.IF:
                        {
                            VariableScope.Push(new IncrementingScope(type, size, "_", "_"));
                            return ifCounter++;
                        }
                    case ScopeType.WHILE:
                        {
                            VariableScope.Push(new IncrementingScope(type, size, $".__whileend_{whileCounter}", $".__while_{whileCounter}"));
                            return whileCounter++;
                        }
                    case ScopeType.FOR:
                        {
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

                if (curr == scope) { foundScope = true; }
                if (foundScope) { size += curr.LocalSize; }
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
            return GetOffsetString(offset);
        }

        /// Determine the offset string for a given offset value
        public static string GetOffsetString(int offset)
        {
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
            return GetSymbol(symbolName).EffectiveOffset;
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

        public static Scope GetInnerScopeOf(ScopeType type)
        {
            for (int i = 0; i < VariableScope.Count; i++)
            {
                Scope scope = VariableScope.ElementAt(i);
                if (scope.Type == type)
                    return scope;
            }
            throw new Exception("Could not find scope of type.");
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
