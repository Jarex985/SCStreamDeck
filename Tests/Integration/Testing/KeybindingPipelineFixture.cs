using SCStreamDeck.Common;
using SCStreamDeck.Services.Keybinding;

// ReSharper disable ClassNeverInstantiated.Global

namespace Tests.Integration.Testing;

public sealed class KeybindingPipelineFixture : IDisposable
{
    public KeybindingLoaderService LoaderService { get; } = new(new SystemFileSystem());

    public string KeybindingsPath { get; } = Path.Combine(AppContext.BaseDirectory, "TestData", "LIVE", "LIVE-keybindings.json");

    public void Dispose()
    {
    }
}
