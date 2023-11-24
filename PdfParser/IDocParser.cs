using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DocParser
{
    public interface IDocParser
    {
        public IDocData Parse(string documentPath);

        public IDocData Parse(byte[] document);

        public IDocData Parse(Stream document);
    }
}
