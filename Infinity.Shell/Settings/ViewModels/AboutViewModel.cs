using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Presentation;
using System.Reflection;

namespace Infinity.Shell;

public partial class AboutViewModel(IServiceProvider provider,
    IServiceFactory factory,
    IMessenger messenger,
    IDisposer disposer) :
    ObservableViewModel(provider, factory, messenger, disposer)
{
    private static readonly Assembly EntryAssembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();

    public string Copyright => EntryAssembly.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright ?? $"© {DateTime.Now.Year} Elysium Studio";

    public string Title => EntryAssembly.GetCustomAttribute<AssemblyTitleAttribute>()?.Title ?? "Infinity";

    public string Version => GetVersion();

    public string Website => "https://elysiumstud.io/";

    public Uri WebsiteUri => new(Website);

    private static string GetVersion()
    {
        string? informationalVersion = EntryAssembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        if (informationalVersion is null)
        {
            return "1.0.0";
        }

        int metadataIndex = informationalVersion.IndexOf('+');

        return metadataIndex >= 0 ? informationalVersion[..metadataIndex] : informationalVersion;
    }
}