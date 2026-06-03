using FluentAssertions;
using Microsoft.Extensions.Options;
using W_API.Core.Configuration;
using W_API.Core.Services;

namespace W_API.Tests;

public class SiteResolverTests
{
    private static IOptions<AppSettings> BuildOptions() => Options.Create(new AppSettings
    {
        Sites = new SiteSettings
        {
            Prefixes =
            [
                new SitePrefix { Prefix = "TO", SiteId = 2 },
                new SitePrefix { Prefix = "AB", SiteId = 3 },
                new SitePrefix { Prefix = "SO", SiteId = 4 },
                new SitePrefix { Prefix = "TA", SiteId = 5 }
            ]
        }
    });

    [Theory]
    [InlineData(3, null, "BC-26-00001", ",3,")]    // site from record wins
    [InlineData(null, 2, "BC-26-00001", ",2,")]    // site from pattern
    [InlineData(null, null, "TO/BC-26-00001", ",2,")] // site from number prefix
    [InlineData(null, null, "BC-26-00001", ",1,")]    // default GRAND ABIDJAN
    public void Resolve_CorrectSiteFormat(int? fromRecord, int? fromPattern, string number, string expected)
    {
        var resolver = new SiteResolver(BuildOptions());
        var result = resolver.Resolve(fromRecord, fromPattern, number);
        result.Should().Be(expected);
    }
}
