using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DocParser
{
    public interface IDocData
    {
        public string ToJson(bool prettyPrint);
    }
}
