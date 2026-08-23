using WinClipboard.Core.Utils;
using Xunit;

namespace WinClipboard.Core.Tests;

public class LinkDetectorTests
{
    [Theory]
    [InlineData("https://github.com/wwwxadieu/WinDropOver")]
    [InlineData("http://example.com")]
    [InlineData("HTTPS://EXAMPLE.COM/Path")]
    [InlineData("ftp://files.example.com/a.zip")]
    [InlineData("mailto:someone@example.com")]
    [InlineData("  https://example.com  ")]
    public void RecognisesLinks(string text) => Assert.True(LinkDetector.IsLink(text));

    [Theory]
    [InlineData("just some text")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("Xem tại https://example.com nhé")]   // a sentence containing a URL is not a link
    [InlineData(@"C:\Users\me\file.txt")]              // a Windows path parses as a URI but is not a link
    [InlineData("file:///C:/secret.txt")]              // file: is deliberately not in the scheme list
    public void RejectsNonLinks(string? text) => Assert.False(LinkDetector.IsLink(text));
}
