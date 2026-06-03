using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using W_API.Core.Configuration;
using W_API.Core.Interfaces;

namespace W_API.Core.Services;

public class DocumentTypeDetector : IDocumentTypeDetector
{
    private readonly List<DocumentTypeConfig> _types;
    private readonly SiteSettings _sites;

    public DocumentTypeDetector(IOptions<AppSettings> opts)
    {
        _types = opts.Value.DocumentTypes;
        _sites = opts.Value.Sites;
    }

    public (DocumentTypeConfig? Type, string? Number, int? SiteId) Detect(string text)
    {
        var normalizedText = text.Replace("\r", " ").Replace("\n", " ");

        DocumentTypeConfig? bestType = null;
        string? bestNumber = null;
        int? bestSiteId = null;
        int bestPriority = int.MaxValue;
        bool ambiguous = false;

        for (int i = 0; i < _types.Count; i++)
        {
            var typeConfig = _types[i];
            var match = Regex.Match(normalizedText, typeConfig.Regex,
                RegexOptions.IgnoreCase | RegexOptions.Compiled);

            if (!match.Success)
                continue;

            var number = NormalizeNumber(match.Value);
            var siteId = ExtractSiteId(match);

            if (i < bestPriority)
            {
                bestPriority = i;
                bestType = typeConfig;
                bestNumber = number;
                bestSiteId = siteId;
                ambiguous = false;
            }
            else if (i == bestPriority)
            {
                ambiguous = true;
            }
        }

        if (ambiguous)
        {
            // caller will log ambiguity; return best match found
        }

        return (bestType, bestNumber, bestSiteId);
    }

    private int? ExtractSiteId(Match match)
    {
        if (match.Groups["site"].Success)
        {
            var prefix = match.Groups["site"].Value.ToUpperInvariant();
            var site = _sites.Prefixes.FirstOrDefault(p =>
                p.Prefix.Equals(prefix, StringComparison.OrdinalIgnoreCase));
            return site?.SiteId;
        }
        return null;
    }

    private static string NormalizeNumber(string raw)
        => Regex.Replace(raw.Trim(), @"\s+", " ");
}
