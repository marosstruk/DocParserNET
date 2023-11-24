using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DocParser
{
    public interface ITextDenoiser
    {
        public string RemoveNoise(string text);

        public Task<string> RemoveNoiseAsync(string text);
    }
}
