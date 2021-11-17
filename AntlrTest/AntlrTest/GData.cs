using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AntlrTest
{
    public enum GDataType
    {
        I32, I16, I8
    }

    class GData
    {
        public static int GetByteSize(GDataType type)
        {
            switch (type)
            {
                case GDataType.I8: { return 1; }
                case GDataType.I16: { return 2; }
                case GDataType.I32: { return 4; }
            }
            return 4;
        }
    }
}
