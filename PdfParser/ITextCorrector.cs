using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DocParser
{
    public interface ITextCorrector
    {
        public string Correct(string text);

        public Task<string> CorrectAsync(string text);
    }
}
