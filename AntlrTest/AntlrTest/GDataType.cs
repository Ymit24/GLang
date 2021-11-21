using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AntlrTest
{
    public class GDataType
    {
        public readonly string Type;
        public readonly bool IsPrimitive;
        public readonly int IdealSize;
        public int AlignedSize
        {
            get
            {
                return IdealSize + (4 - (IdealSize % 4));
            }
        }
        public readonly bool IsPointer;

        public GDataType(string type)
        {
            IsPointer = type.Contains("*");
            if      (type.ToLower().Contains("u8"))  { IsPrimitive = true; IdealSize = 1; }
            else if (type.ToLower().Contains("u16")) { IsPrimitive = true; IdealSize = 2; }
            else if (type.ToLower().Contains("u32")) { IsPrimitive = true; IdealSize = 4; }
            else if (type.ToLower().Contains("i8"))  { IsPrimitive = true; IdealSize = 1; }
            else if (type.ToLower().Contains("i16")) { IsPrimitive = true; IdealSize = 2; }
            else if (type.ToLower().Contains("i32")) { IsPrimitive = true; IdealSize = 4; }
            else
            {
                IsPrimitive = false;
                IdealSize = -1;
                throw new NotImplementedException("Non primitive types are not done.");
            }

            Type = type;
        }
    }
}
