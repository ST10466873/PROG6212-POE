extern alias RaceDayWeb;

using RaceDayWeb::RaceDay.Web.Helpers;
using RaceDayWeb::RaceDay.Web.Models;

namespace RaceDay.Tests;

public class WebPresentationTests
{
    [Theory]
    [InlineData(1, "1st")]
    [InlineData(2, "2nd")]
    [InlineData(3, "3rd")]
    [InlineData(4, "4th")]
    [InlineData(11, "11th")]
    [InlineData(12, "12th")]
    [InlineData(13, "13th")]
    [InlineData(21, "21st")]
    [InlineData(47, "47th")]
    public void Position_AddsCorrectOrdinalSuffix(int position, string expected)
        => Assert.Equal(expected, ImageUrlHelper.Position(position));

    [Fact]
    public void Resolve_UsesPlaceholderWhenNoImageExists()
        => Assert.Equal(ImageUrlHelper.Placeholder, ImageUrlHelper.Resolve(null));

    [Fact]
    public void Resolve_KeepsAbsoluteAzureUrls()
    {
        const string url = "https://raceday.blob.core.windows.net/images/banner.png";
        Assert.Equal(url, ImageUrlHelper.Resolve(url));
    }

    [Fact]
    public void Resolve_ProxiesLocalStoragePathsThroughMediaController()
    {
        var resolved = ImageUrlHelper.Resolve("uploads/banner.png");
        Assert.StartsWith("/Media/Get?path=", resolved);
        Assert.Contains("banner.png", resolved);
    }

    [Fact]
    public void RaceDate_UsesConsistentShortFormat()
        => Assert.Equal("Wed 18 Nov 2026", ImageUrlHelper.RaceDate(new DateTime(2026, 11, 18)));

    [Fact]
    public void NotInFutureAttribute_RejectsFutureDates()
    {
        var attribute = new NotInFutureAttribute();
        Assert.False(attribute.IsValid(DateTime.Today.AddDays(1)));
        Assert.True(attribute.IsValid(DateTime.Today));
    }

    [Fact]
    public void FutureDateAttribute_RequiresTomorrowOrLater()
    {
        var attribute = new FutureDateAttribute();
        Assert.True(attribute.IsValid(DateTime.Today.AddDays(1)));
        Assert.False(attribute.IsValid(DateTime.Today));
    }
}
