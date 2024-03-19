using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AntlrTest
{
    public class GDataSymbol
    {
        public readonly string Name;
        public readonly GDataType Type;

        /// <summary>
        /// For primitive types this is their normal offset
        /// For non primitive types (e.g. arrays) this is the offset to their
        /// lowest address. For struct types, this is their offset relative
        /// to their struct base pointer.
        /// </summary>
        public readonly int EffectiveOffset;

        public GDataSymbol(string symbol, GDataType type, int offset)
        {
            Name = symbol;
            Type = type;
            EffectiveOffset = offset;
        }
    }

    public class GDataType
    {
        public readonly string TypeString;
        /// <summary>
        /// Primitives are 4 bytes or less, or pointers.
        /// Arrays and structs are Non-Primitive.
        /// </summary>
        public readonly bool IsPrimitive;
        public readonly bool IsSigned;
        public readonly int IdealSize;

        /// <summary>
        /// This represents the size in bytes
        /// that should be read or written with for assignment.
        /// </summary>
        public int MemorySize
        {
            get
            {
                if (IsArray)
                {
                    return UnderlyingDataType.IdealSize;
                }
                else if (IsPointer)
                {
                    return IdealSize;
                }
                //throw new Exception("This is a warning. Might not want to do this.");
                return IdealSize;
            }
        }

        public int AlignedSize
        {
            get
            {
                if (IsPointer) return 4;

                switch (IdealSize)
                {
                    case 1: return 1;
                    case 2: return 2;
                    case 3: return 4; // There are no 3 byte primitives so this might not be needed
                    case 4: return 4;
                    default: throw new Exception("Invalid IdealSize");
                }

                // NOTE: Updating how this works.
                // return IdealSize % 4 == 0 ? IdealSize : (IdealSize + (4 - (IdealSize % 4)));
            }
        }

        /// <summary>
        /// Compute which register to use for this type.
        /// </summary>
        public string MemoryRegister
        {
            get
            {
                // int size = AlignedSize;
                int size = MemorySize;
                switch (size)
                {
                    case 4: { return "eax"; }
                    case 2: { return "ax"; }
                    case 1: { return "al"; }
                    default:
                        {
                            throw new Exception("Could not determine correct register.");
                        }
                }
            }
        }

        public string AsmType
        {
            get
            {
                // probably should use AlignedSize
                // int size = MemorySize;
                int size = AlignedSize;
                switch (size)
                {
                    case 4: { return "DWORD"; }
                    case 2: { return "WORD"; }
                    case 1: { return "BYTE"; }
                    default:
                        {
                            throw new Exception("Could not determine correct register.");
                        }
                }
            }
        }

        public readonly bool IsArray;
        public readonly bool IsPointer;
        public readonly int ElementCount;
        public readonly GDataType UnderlyingDataType;

        public static GDataType MakePointerOf(GDataType underlying)
        {
            return new GDataType(underlying);
        }

        /// <summary>
        /// Pointer constructor
        /// </summary>
        /// <param name="underlying"></param>
        private GDataType(GDataType underlying)
        {
            IsPointer = true;
            IdealSize = 4;
            UnderlyingDataType = underlying;
            IsPrimitive = true; // Pointer is a primitive type.
            IsSigned = false;
            ElementCount = 0;
            TypeString = underlying.TypeString + "*";
        }

        public GDataType(string type)
        {
            if (type.Contains("("))
            {
                IsArray = true;
                IsPointer = false;
                UnderlyingDataType = new GDataType(type.Split('(')[0]);
                IsPrimitive = false; // array is not a primitive type.
                IsSigned = false;
                ElementCount = int.Parse(type.Split('(')[1].Split(')')[0]);
                IdealSize = UnderlyingDataType.IdealSize * ElementCount;
                TypeString = type;
                return;
            }

            if (type.Contains("*"))
            {
                IsPointer = true;
                IdealSize = 4;
                UnderlyingDataType = new GDataType(type.Split('*')[0]);
                IsPrimitive = true; // Pointer is a primitive type.
                IsSigned = false;
                ElementCount = 0;
                TypeString = type;
                return;
            }

            IsPointer = false;

            IsPrimitive = true;
            if (type.ToLower().Contains("8")) { IdealSize = 1; }
            else if (type.ToLower().Contains("16")) { IdealSize = 2; }
            else if (type.ToLower().Contains("32")) { IdealSize = 4; }
            else
            {
                var struct_type = GStructSignature.GetSignature(type).Type;
                IdealSize = struct_type.IdealSize;
                Console.WriteLine($"Found existing struct type: {struct_type.TypeString}");
            }

            IsSigned = IsPrimitive && type.ToLower().Contains("i");
            UnderlyingDataType = null;
            ElementCount = 0;
            IsArray = false;

            TypeString = type;
        }

        /// This is for defining types for structures.
        public GDataType(string struct_name, int alignedSize)
        {
            IsPrimitive = false;
            IsPointer = false;
            IsArray = false;
            ElementCount = 0;
            TypeString = struct_name;
            IsSigned = false;

            IdealSize = alignedSize;
            UnderlyingDataType = null;
        }
    }
}
