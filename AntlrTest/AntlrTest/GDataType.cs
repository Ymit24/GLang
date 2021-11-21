using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AntlrTest
{
    public class GDataSymbol
    {
        public readonly string Symbol;
        public readonly GDataType Type;
        public readonly int Offset;

        public GDataSymbol(string symbol, GDataType type, int offset)
        {
            Symbol = symbol;
            Type = type;
            Offset = offset;
        }
    }

    public class GDataType
    {
        public readonly string Type;
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

                return IdealSize % 4 == 0 ? IdealSize : (IdealSize + (4 - (IdealSize % 4)));
            }
        }

        /// <summary>
        /// Compute which register to use for this type.
        /// </summary>
        public string MemoryRegister
        {
            get
            {
                int size = MemorySize;
                switch (size)
                {
                    case 4: { return "eax"; }
                    case 2: { return "ax";  }
                    case 1: { return "al";  }
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
                int size = MemorySize;
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

        public GDataType(string type)
        {
            if (type.Contains("*"))
            {
                IsPointer = true;
                IdealSize = 4;
                UnderlyingDataType = new GDataType(type.Split('*')[0]);
                IsPrimitive = true; // Pointer is a primitive type.
                IsSigned = false;
                ElementCount = 0;
                Type = type;
                return;
            }

            IsPointer = false;

            IsPrimitive = true;
            if      (type.ToLower().Contains("8"))  { IdealSize = 1; }
            else if (type.ToLower().Contains("16")) { IdealSize = 2; }
            else if (type.ToLower().Contains("32")) { IdealSize = 4; }
            else
            {
                IsPrimitive = false;
                IdealSize = -1;
                throw new NotImplementedException("Non primitive types are not done.");
            }
            
            IsSigned = IsPrimitive && type.ToLower().Contains("i");
            UnderlyingDataType = null;
            ElementCount = 0;
            IsArray = false;

            Type = type;
        }

        public GDataType(string type, int elementCount)
        {
            UnderlyingDataType = new GDataType(type);
            ElementCount = elementCount;
            
            IsPointer = false;
            IsArray = true;

            Type = type;
            IdealSize = UnderlyingDataType.IdealSize * elementCount;
            IsPrimitive = UnderlyingDataType.IsPrimitive;
        }
    }
}
