using System.Text.RegularExpressions;

namespace Deucalion.Storage;

public static class StringExtensions
{
    // Source: https://stackoverflow.com/a/15087910/33244

    static readonly string InvalidChars = @"""\/?:<>*|";
    static readonly string EscapeChar = "%";

    static readonly Regex EncodeRegex = new("[" + Regex.Escape(EscapeChar + InvalidChars) + "]", RegexOptions.Compiled);
    static readonly Regex DecodeRegex = new(Regex.Escape(EscapeChar) + "([0-9A-Z]{4})", RegexOptions.Compiled);

    public static string EncodePath(this string path) => EncodeRegex.Replace(path, m => EscapeChar + ((short)m.Value[0]).ToString("X4"));

    public static string DecodePath(this string path) => DecodeRegex.Replace(path, m => ((char)Convert.ToInt16(m.Groups[1].Value, 16)).ToString());
}
