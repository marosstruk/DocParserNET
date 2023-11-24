using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tesseract;

namespace DocParser.ExtensionMethods
{
    public static class RectExtensions
    {
        public static (float X, float Y) GetCentroid(this Rect r)
        {
            float x = Math.Abs(r.X1 + ((r.X2 - r.X1) / 2));
            float y = Math.Abs(r.Y1 + ((r.Y2 - r.Y1) / 2));
            return (x, y);
        }
    }

    public static class StringExtensions
    {
        public static string ToTitleCase(this string s)
        {
            TextInfo textInfo = new CultureInfo("en-US", false).TextInfo;
            return textInfo.ToTitleCase(s.ToLower());
        }
    }
}
