using Microsoft.AspNetCore.Mvc;
using W_API.Core.Interfaces;
using W_API.Core.Models;

namespace W_API.Api.Controllers;

[ApiController]
[Route("api/ingestion")]
public class IngestionController : ControllerBase
{
    private readonly IIngestionService _svc;
    private readonly ILogger<IngestionController> _log;

    public IngestionController(IIngestionService svc, ILogger<IngestionController> log)
    {
        _svc = svc;
        _log = log;
    }

    /// <summary>Lance un traitement par lot sur le dossier d'entrée.</summary>
    [HttpPost("run")]
    public async Task<IActionResult> Run(CancellationToken ct)
    {
        try
        {
            var result = await _svc.RunBatchAsync(ct);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Erreur lors du traitement par lot");
            return StatusCode(500, new { code = "BATCH_ERROR", message = ex.Message });
        }
    }

    /// <summary>Traite un seul fichier PDF uploadé.</summary>
    [HttpPost("file")]
    [RequestSizeLimit(50 * 1024 * 1024)] // 50 MB
    public async Task<IActionResult> ProcessFile(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { code = "NO_FILE", message = "Aucun fichier fourni." });

        if (!file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { code = "INVALID_TYPE", message = "Seuls les fichiers PDF sont acceptés." });

        var tempPath = Path.Combine(Path.GetTempPath(), $"aqm_{Guid.NewGuid():N}_{file.FileName}");
        try
        {
            await using (var fs = System.IO.File.Create(tempPath))
                await file.CopyToAsync(fs);

            var detail = await _svc.ProcessFileAsync(tempPath);
            detail.FileName = file.FileName; // restore original name in response
            return Ok(detail);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Erreur traitement fichier unique : {Name}", file.FileName);
            return StatusCode(500, new { code = "FILE_ERROR", message = ex.Message });
        }
        finally
        {
            if (System.IO.File.Exists(tempPath))
                System.IO.File.Delete(tempPath);
        }
    }

    /// <summary>Retourne l'état d'un lot par son ID.</summary>
    [HttpGet("status/{batchId}")]
    public IActionResult GetStatus(string batchId)
    {
        var result = _svc.GetBatchStatus(batchId);
        if (result == null)
            return NotFound(new { code = "BATCH_NOT_FOUND", message = $"Lot '{batchId}' introuvable." });
        return Ok(result);
    }
}
