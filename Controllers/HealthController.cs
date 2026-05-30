using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using W_API.Core.Configuration;
using W_API.Core.Interfaces;
using W_API.Infrastructure.Ocr;

namespace W_API.Api.Controllers;

[ApiController]
[Route("api/health")]
public class HealthController : ControllerBase
{
    private readonly IAqManagerRepository _repo;
    private readonly IFileManager _fileManager;
    private readonly OcrEngine _ocr;
    private readonly AppSettings _cfg;

    public HealthController(
        IAqManagerRepository repo,
        IFileManager fileManager,
        OcrEngine ocr,
        IOptions<AppSettings> opts)
    {
        _repo = repo;
        _fileManager = fileManager;
        _ocr = ocr;
        _cfg = opts.Value;
    }

    [HttpGet]
    public async Task<IActionResult> Check()
    {
        var checks = new Dictionary<string, object>();

        // SQL connectivity
        try
        {
            await _repo.GetNotNullColumnsAsync("Documents");
            checks["sql"] = new { ok = true, database = "AQManagerData" };
        }
        catch (Exception ex)
        {
            checks["sql"] = new { ok = false, error = ex.Message };
        }

        // Docs folder write access
        try
        {
            _fileManager.VerifyWriteAccess();
            checks["docsFolder"] = new { ok = true, path = _cfg.Paths.DocsPhysicalFolder };
        }
        catch (Exception ex)
        {
            checks["docsFolder"] = new { ok = false, error = ex.Message };
        }

        // OCR engine / tessdata
        checks["ocr"] = new
        {
            ok = _ocr.IsReady,
            tessDataPath = _cfg.Ocr.TessDataPath,
            languages = _cfg.Ocr.Languages
        };

        // Input folder
        var inputExists = Directory.Exists(_cfg.Paths.InputFolder);
        checks["inputFolder"] = new { ok = inputExists, path = _cfg.Paths.InputFolder };

        var allOk = checks.Values
            .OfType<Dictionary<string, object>>()
            .All(d => d.TryGetValue("ok", out var v) && v is true);

        return allOk ? Ok(checks) : StatusCode(503, checks);
    }
}
