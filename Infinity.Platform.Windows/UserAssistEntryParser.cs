using System.Buffers.Binary;

namespace Infinity.Platform.Windows;

internal static class UserAssistEntryParser
{
    private const int UseCountOffset = 4;
    private const int LastUsedFileTimeOffset = 60;
    private const int MinimumEntryLength = LastUsedFileTimeOffset + sizeof(long);

    public static UserAssistApplicationUsageEntry? Parse(string encodedIdentifier, byte[] data)
    {
        ArgumentNullException.ThrowIfNull(encodedIdentifier);
        ArgumentNullException.ThrowIfNull(data);
        if (data.Length < MinimumEntryLength)
        {
            return null;
        }

        int useCount = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(UseCountOffset, sizeof(int)));
        long fileTime = BinaryPrimitives.ReadInt64LittleEndian(data.AsSpan(LastUsedFileTimeOffset, sizeof(long)));
        if (useCount < 0 || fileTime <= 0)
        {
            return null;
        }

        try
        {
            return new(UserAssistValueNameDecoder.Decode(encodedIdentifier), useCount, DateTime.FromFileTimeUtc(fileTime));
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }
}
