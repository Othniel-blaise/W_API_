using W_API.Core.Models;

namespace W_API.Core.Interfaces;

public interface IPdfTextExtractor
{
    Task<PdfExtractionResult> ExtractAsync(string filePath);
}
