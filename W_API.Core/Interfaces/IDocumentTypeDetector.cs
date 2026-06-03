using W_API.Core.Configuration;

namespace W_API.Core.Interfaces;

public interface IDocumentTypeDetector
{
    (DocumentTypeConfig? Type, string? Number, int? SiteId) Detect(string text);
}
