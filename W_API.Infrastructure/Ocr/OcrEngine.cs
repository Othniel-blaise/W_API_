using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PDFtoImage;
using SkiaSharp;
using Tesseract;
using W_API.Core.Configuration;
using W_API.Core.Models;

namespace W_API.Infrastructure.Ocr;

public class OcrEngine : IDisposable
{
    private readonly OcrSettings _cfg;
    private readonly ILogger<OcrEngine> _log;
    private TesseractEngine? _engine;
    private bool _engineReady;

    public OcrEngine(IOptions<AppSettings> opts, ILogger<OcrEngine> log)
    {
        _cfg = opts.Value.Ocr;
        _log = log;
        TryInitEngine();
    }

    private void TryInitEngine()
    {
        try
        {
            _engine = new TesseractEngine(_cfg.TessDataPath, _cfg.Languages, EngineMode.Default);
            _engineReady = true;
            _log.LogInformation("Tesseract initialisé — langues : {Langs}, tessdata : {Path}",
                _cfg.Languages, _cfg.TessDataPath);
        }
        catch (Exception ex)
        {
            _engineReady = false;
            _log.LogWarning(ex, "Impossible d'initialiser Tesseract — OCR désactivé. " +
                "Vérifiez TessDataPath ({Path}) et les fichiers .traineddata ({Langs}).",
                _cfg.TessDataPath, _cfg.Languages);
        }
    }

    public async Task<PdfExtractionResult> RecognizeAsync(string pdfPath)
    {
        if (!_engineReady)
        {
            _log.LogError("OCR demandé mais Tesseract non disponible pour : {File}", Path.GetFileName(pdfPath));
            return new PdfExtractionResult
            {
                Text = string.Empty,
                UsedOcr = true,
                OcrConfidence = 0,
                IsConfident = false,
            };
        }

        var sb = new System.Text.StringBuilder();
        double totalConf = 0;
        int pageCount = 0;

        var renderOptions = new RenderOptions(Dpi: _cfg.Dpi);

        await using var fileStream = File.OpenRead(pdfPath);

        // ToImages returns IEnumerable<SKBitmap>; we iterate synchronously (CPU-bound)
        var bitmaps = Conversion.ToImages(fileStream, leaveOpen: false, password: null, options: renderOptions);

        await Task.Run(() =>
        {
            foreach (var bitmap in bitmaps)
            {
                using (bitmap)
                {
                    var imageBytes = bitmap.Encode(SKEncodedImageFormat.Png, 100).ToArray();

                    using var pix = Pix.LoadFromMemory(imageBytes);
                    using var ocrPage = _engine!.Process(pix);

                    sb.AppendLine(ocrPage.GetText());
                    totalConf += ocrPage.GetMeanConfidence() * 100.0;
                    pageCount++;
                }
            }
        });

        var avgConfidence = pageCount > 0 ? totalConf / pageCount : 0;
        var text = sb.ToString().Trim();

        _log.LogDebug("OCR terminé — {Pages} page(s), confiance moy. {Conf:F1}%", pageCount, avgConfidence);

        return new PdfExtractionResult
        {
            Text = text,
            UsedOcr = true,
            OcrConfidence = avgConfidence,
            IsConfident = avgConfidence >= _cfg.MinConfidence,
        };
    }

    public bool IsReady => _engineReady;

    public void Dispose()
    {
        _engine?.Dispose();
        GC.SuppressFinalize(this);
    }
}
