using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace DocParser
{
    public class RuleBasedTextDenoiser : ITextDenoiser
    {
        public string RemoveNoise(string text)
        {
            text = Regex.Replace(text, @"((\s|^)(\W(\s|$))+)", " ");
            text = Regex.Replace(text, @"(\s\W\n)", "\n");
            return text;
        }

        public Task<string> RemoveNoiseAsync(string text)
        {
            return new Task<string>(() => this.RemoveNoise(text));
        }
    }
}
