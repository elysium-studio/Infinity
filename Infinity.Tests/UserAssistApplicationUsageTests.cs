using System.Buffers.Binary;
using Infinity.Platform.Abstractions;
using Infinity.Platform.Windows;

namespace Infinity.Tests;

public sealed class UserAssistApplicationUsageTests
{
    [Fact]
    public void ParserDecodesIdentifierAndUsageMetadata()
    {
        DateTime lastUsedUtc = new(2026, 8, 30, 12, 30, 0, DateTimeKind.Utc);
        byte[] data = new byte[72];
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(4), 18);
        BinaryPrimitives.WriteInt64LittleEndian(data.AsSpan(60), lastUsedUtc.ToFileTimeUtc());
        UserAssistApplicationUsageEntry? entry = UserAssistEntryParser.Parse("Zvpebfbsg.JvaqbjfPnyphyngbe_8jrxlo3q8oojr!Ncc", data);
        Assert.NotNull(entry);
        Assert.Equal("Microsoft.WindowsCalculator_8wekyb3d8bbwe!App", entry.Identifier);
        Assert.Equal(18, entry.UseCount);
        Assert.Equal(lastUsedUtc, entry.LastUsedUtc);
    }


    [Fact]
    public void MatcherReturnsMostUsedStartApplications()
    {
        LaunchableApplication calculator = new(@"shell:AppsFolder\Microsoft.WindowsCalculator_8wekyb3d8bbwe!App", "Calculator");
        LaunchableApplication paint = new(@"shell:AppsFolder\Microsoft.Paint_8wekyb3d8bbwe!App", "Paint");
        LaunchableApplication unused = new(@"shell:AppsFolder\Unused.App", "Unused");
        IReadOnlyList<LaunchableApplication> result = UserAssistApplicationMatcher.Match([calculator, paint, unused], [new UserAssistApplicationUsageEntry("Microsoft.WindowsCalculator_8wekyb3d8bbwe!App", 100, new DateTime(2026, 8, 28, 12, 0, 0, DateTimeKind.Utc)), new UserAssistApplicationUsageEntry("Microsoft.Paint_8wekyb3d8bbwe!App", 3, new DateTime(2026, 8, 30, 12, 0, 0, DateTimeKind.Utc))], 2);
        Assert.Equal([calculator, paint], result);
    }
}
