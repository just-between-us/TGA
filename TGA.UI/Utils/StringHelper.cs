namespace TGA.UI.Utils;

public static class StringHelper
{
    public static string Truncate(string text, int reduce = 10)
    {
        if (string.IsNullOrEmpty(text)) return text;
        if  (text.Length < reduce) return text;
        return text.Length <= 10 ? text : text.Substring(0, reduce) + "...";
    }
}