namespace W_API.Core.Models;

public class PdfExtractionResult
{
    public string Text { get; set; } = string.Empty;
    public bool UsedOcr { get; set; }
    public double OcrConfidence { get; set; }
    public bool IsConfident { get; set; }
}
