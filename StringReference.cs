using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace vividstasis_Text_Editor
{
    internal class StringReference
    {
        public StringReference(string referenced, long position, uint pointer)
        {
            ReferencedString = referenced;
            Position = position;
            Pointer = pointer;
        }
        public string ReferencedString;
        public long Position;
        public uint Pointer;
    }
}
