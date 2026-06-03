using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using W_API.Core.Configuration;

namespace W_API.Core.Services;

public class SiteResolver
{
    private readonly SiteSettings _settings;

    public SiteResolver(IOptions<AppSettings> opts) => _settings = opts.Value.Sites;

    // Returns formatted `,<id>,`
    public string Resolve(int? siteIdFromRecord, int? siteIdFromPattern, string number)
    {
        var siteId = siteIdFromRecord ?? siteIdFromPattern ?? ResolveFromNumber(number) ?? 1;
        return $",{siteId},";
    }

    private int? ResolveFromNumber(string number)
    {
        foreach (var s in _settings.Prefixes)
        {
            if (Regex.IsMatch(number, $@"^{Regex.Escape(s.Prefix)}[/-]?", RegexOptions.IgnoreCase))
                return s.SiteId;
        }
        return null;
    }
}
