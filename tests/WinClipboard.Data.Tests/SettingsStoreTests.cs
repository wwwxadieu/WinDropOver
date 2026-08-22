using WinClipboard.Core.Models;
using WinClipboard.Data;
using Xunit;

namespace WinClipboard.Data.Tests;

public class SettingsStoreTests : IDisposable
{
    private readonly string _path;

    public SettingsStoreTests()
    {
        _path = Path.Combine(Path.GetTempPath(), $"winclipboard-settings-{Guid.NewGuid():N}.json");
    }

    public void Dispose()
    {
        if (File.Exists(_path)) File.Delete(_path);
    }

    [Fact]
    public void Load_WhenFileMissing_ReturnsDefaults()
    {
        var store = new SettingsStore(_path);
        var settings = store.Load();

        Assert.Equal(500, settings.MaxHistoryItems);
        Assert.Contains(ScreenEdge.Right, settings.EnabledEdges);
    }

    [Fact]
    public void SaveThenLoad_RoundTripsValues()
    {
        var store = new SettingsStore(_path);
        var settings = store.Load();
        settings.MaxHistoryItems = 250;
        settings.HoldKey = ModifierHoldKey.RightControl;
        settings.EnabledEdges = [ScreenEdge.Left, ScreenEdge.Top];

        store.Save(settings);
        var reloaded = store.Load();

        Assert.Equal(250, reloaded.MaxHistoryItems);
        Assert.Equal(ModifierHoldKey.RightControl, reloaded.HoldKey);
        Assert.Equal([ScreenEdge.Left, ScreenEdge.Top], reloaded.EnabledEdges);
    }

    [Fact]
    public void Load_WhenFileCorrupt_FallsBackToDefaults()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, "{ not valid json");

        var store = new SettingsStore(_path);
        var settings = store.Load();

        Assert.Equal(500, settings.MaxHistoryItems);
    }
}
