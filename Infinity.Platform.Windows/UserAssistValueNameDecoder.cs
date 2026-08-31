namespace Infinity.Platform.Windows;

internal static class UserAssistValueNameDecoder
{
    public static string Decode(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return string.Create(value.Length, value, static (characters, source) =>
        {
            for (int index = 0; index < source.Length; index++)
            {
                char character = source[index];
                characters[index] = character switch
                {
                    >= 'A' and <= 'Z' => (char)('A' + ((character - 'A' + 13) % 26)),
                    >= 'a' and <= 'z' => (char)('a' + ((character - 'a' + 13) % 26)),
                    _ => character
                };
            }
        });
    }
}
