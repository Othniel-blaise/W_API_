using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UglyToad.PdfPig;
using W_API.Core.Configuration;
using W_API.Core.Interfaces;
using W_API.Core.Models;
using W_API.Infrastructure.Ocr;

namespace W_API.Infrastructure.Pdf;

public class PdfTextExtractor : IPdfTextExtractor
{
    private readonly OcrSettings _ocr;
    private readonly ILogger<PdfTextExtractor> _log;
    private readonly OcrEngine _ocrEngine;

    private const int MinNativeCharCount = 50;

    public PdfTextExtractor(IOptions<AppSettings> opts, OcrEngine ocrEngine, ILogger<PdfTextExtractor> log)
    {
        _ocr = opts.Value.Ocr;
        _ocrEngine = ocrEngine;
        _log = log;
    }

    public async Task<PdfExtractionResult> ExtractAsync(string filePath)
    {
        var nativeText = ExtractNativeText(filePath);

        if (nativeText.Length >= MinNativeCharCount)
        {
            _log.LogDebug("Extraction native ({Chars} chars) : {File}", nativeText.Length, Path.GetFileName(filePath));
            return new PdfExtractionResult
            {
                Text = nativeText,
                UsedOcr = false,
                OcrConfidence = 100,
                IsConfident = true,
            };
        }

        _log.LogDebug("Texte natif insuffisant ({Chars} chars) → OCR : {File}", nativeText.Length, Path.GetFileName(filePath));
        return await _ocrEngine.RecognizeAsync(filePath);
    }

    private static string ExtractNativeText(string filePath)
    {
        using var pdf = PdfDocument.Open(filePath);
        var sb = new System.Text.StringBuilder();
        foreach (var page in pdf.GetPages())
            sb.AppendLine(page.Text);
        return sb.ToString().Trim();
    }
}
