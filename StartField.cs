using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TN3270
{
    class StartField
    {
        public readonly int Index;
        public readonly byte Attribute;
        public bool ModifiedDataTag { get; set; }

        public readonly bool CanEdit;

        public StartField(int index, byte attribute)
        {
            Index = index;
            Attribute = attribute;
            CanEdit = (Attribute & 0x20) != 0x20;
            ModifiedDataTag = (Attribute & 0x01) == 0x01;
        }
        

        public override string ToString()
        {
            return $"Index: {Index}, Attribute:{Attribute}";
        }

    }
}
