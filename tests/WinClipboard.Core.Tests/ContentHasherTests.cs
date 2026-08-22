using WinClipboard.Core.Models;
using WinClipboard.Core.Utils;
using Xunit;

namespace WinClipboard.Core.Tests;

public class ContentHasherTests
{
    [Fact]
    public void HashText_SameInput_ProducesSameHash()
    {
        var a = ContentHasher.HashText("hello world");
        var b = ContentHasher.HashText("hello world");
        Assert.Equal(a, b);
    }

    [Fact]
    public void HashText_DifferentInput_ProducesDifferentHash()
    {
        var a = ContentHasher.HashText("hello");
        var b = ContentHasher.HashText("world");
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Hash_SamePayload_DifferentType_ProducesDifferentHash()
    {
        var text = ContentHasher.HashText("C:\\shared\\file.txt");
        var path = ContentHasher.HashPath(ContentType.File, "C:\\shared\\file.txt");
        Assert.NotEqual(text, path);
    }

    [Fact]
    public void HashPath_SamePath_ProducesSameHash()
    {
        var a = ContentHasher.HashPath(ContentType.File, @"C:\a\b.txt");
        var b = ContentHasher.HashPath(ContentType.File, @"C:\a\b.txt");
        Assert.Equal(a, b);
    }
}
