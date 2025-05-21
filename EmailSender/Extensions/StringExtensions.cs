using System;
using System.Text.RegularExpressions;

namespace EmailSender.Extensions
{
    public static class StringExtensions
    {
        public static string StripHTML(this string input) => Regex.Replace(input, "<.*?>", String.Empty);
    }
}
