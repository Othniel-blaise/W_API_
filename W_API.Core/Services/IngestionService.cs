using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using W_API.Core.Configuration;
using W_API.Core.Interfaces;
using W_API.Core.Models;

namespace W_API.Core.Services;

public class IngestionService : IIngestionService
{
    private readonly IPdfTextExtractor _extractor;
    private readonly IDocumentTypeDetector _detector;
    private readonly IAqManagerRepository _repo;
    private readonly IFileManager _fileManager;
    private readonly SiteResolver _siteResolver;
    private readonly AppSettings _cfg;
    private readonly ILogger<IngestionService> _log;

    private static readonly ConcurrentDictionary<string, IngestionResult> _batches = new();

    public IngestionService(
        IPdfTextExtractor extractor,
        IDocumentTypeDetector detector,
        IAqManagerRepository repo,
        IFileManager fileManager,
        SiteResolver siteResolver,
        IOptions<AppSettings> opts,
        ILogger<IngestionService> log)
    {
        _extractor = extractor;
        _detector = detector;
        _repo = repo;
        _fileManager = fileManager;
        _siteResolver = siteResolver;
        _cfg = opts.Value;
        _log = log;
    }

    public async Task<IngestionResult> RunBatchAsync(CancellationToken ct = default)
    {
        var result = new IngestionResult { StartedAt = DateTime.UtcNow };
        _batches[result.BatchId] = result;

        var inputDir = _cfg.Paths.InputFolder;
        if (!Directory.Exists(inputDir))
        {
            _log.LogError("Dossier d'entrée introuvable : {Dir}", inputDir);
            result.FinishedAt = DateTime.UtcNow;
            return result;
        }

        var files = Directory.GetFiles(inputDir, "*.pdf", SearchOption.TopDirectoryOnly)
            .Take(_cfg.Ingestion.MaxFilesPerRun)
            .ToList();

        result.Total = files.Count;
        _log.LogInformation("=== DÉBUT LOT {Id} — {Count} fichier(s) à traiter ===", result.BatchId, result.Total);

        foreach (var file in files)
        {
            if (ct.IsCancellationRequested) break;

            var detail = await ProcessFileAsync(file);
            result.Erreurs.Add(detail);

            switch (detail.Statut)
            {
                case "rattache": result.Rattaches++; break;
                case "doublon": result.Doublons++; break;
                case "a_verifier": result.AVerifier++; break;
            }
        }

        result.FinishedAt = DateTime.UtcNow;
        PrintSummary(result);
        return result;
    }

    public async Task<FileIngestionDetail> ProcessFileAsync(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        var detail = new FileIngestionDetail { FileName = fileName };

        _log.LogInformation("--- Traitement : {File}", fileName);

        // ── FAST PATH : détection par nom de fichier ─────────────────────────
        // Les opérateurs nomment les PDF avec le numéro de référence dedans
        // (ex : "GEO-PLUS BTP_TO-BC-26-00424.pdf"). On tente d'abord cette
        // voie rapide avant toute extraction PDF ou OCR.
        var fileNameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
        var (typeConfig, number, siteIdFromPattern) = _detector.Detect(fileNameWithoutExt);

        if (typeConfig != null && number != null)
        {
            _log.LogInformation("  [NOM FICHIER] Numéro : {Num} | Type : {Type}", number, typeConfig.Name);
        }
        else
        {
            // ── SLOW PATH : extraction texte + OCR ───────────────────────────
            PdfExtractionResult extraction;
            try
            {
                extraction = await _extractor.ExtractAsync(filePath);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Échec extraction texte : {File}", fileName);
                detail.Statut = "erreur";
                detail.Message = $"Extraction texte : {ex.Message}";
                _fileManager.MoveToVerifyFolder(filePath);
                return detail;
            }

            if (!extraction.IsConfident)
            {
                _log.LogWarning("Confiance OCR insuffisante ({Conf:F1}%) : {File} → dossier _a_verifier", extraction.OcrConfidence, fileName);
                detail.Statut = "a_verifier";
                detail.Message = $"Confiance OCR {extraction.OcrConfidence:F1}% < seuil";
                _fileManager.MoveToVerifyFolder(filePath);
                return detail;
            }

            (typeConfig, number, siteIdFromPattern) = _detector.Detect(extraction.Text);

            if (typeConfig != null && number != null)
                _log.LogInformation("  [TEXTE PDF] Numéro : {Num} | Type : {Type}", number, typeConfig.Name);
        }

        if (typeConfig == null || number == null)
        {
            _log.LogWarning("Numéro de référence non détecté (ni nom fichier, ni texte) : {File}", fileName);
            detail.Statut = "a_verifier";
            detail.Message = "Aucun numéro de référence identifié (nom fichier + texte PDF)";
            _fileManager.MoveToVerifyFolder(filePath);
            return detail;
        }

        detail.NumeroExtrait = number;
        detail.TypeDetecte = typeConfig.Name;
        detail.TableCible = typeConfig.TableName;
        _log.LogInformation("  Numéro : {Num} | Type : {Type} | Table : {Table}", number, typeConfig.Name, typeConfig.TableName);

        // 3. Find business record
        BusinessRecord? record;
        try
        {
            record = await _repo.FindBusinessRecordAsync(typeConfig.TableName, typeConfig.NumberColumns, number);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Erreur SQL recherche enregistrement : {File}", fileName);
            detail.Statut = "erreur";
            detail.Message = $"Recherche SQL : {ex.Message}";
            return detail;
        }

        if (record == null)
        {
            _log.LogWarning("Enregistrement introuvable pour {Num} dans {Table}", number, typeConfig.TableName);
            detail.Statut = "a_verifier";
            detail.Message = $"Aucun enregistrement {typeConfig.Name} trouvé pour '{number}'";
            _fileManager.MoveToVerifyFolder(filePath);
            return detail;
        }

        detail.RecordId = record.Id;
        _log.LogInformation("  RecordID={Id} (colonne '{Col}', site={Site})", record.Id, record.MatchedColumn, record.SiteId);

        // 4. Anti-doublon
        bool exists;
        try
        {
            exists = await _repo.DocumentExistsAsync(typeConfig.TableName, record.Id, fileName);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Erreur vérification doublon : {File}", fileName);
            detail.Statut = "erreur";
            detail.Message = $"Vérification doublon : {ex.Message}";
            return detail;
        }

        if (exists)
        {
            _log.LogWarning("Déjà rattaché (doublon) : {File} → {Table} RecordID={Id}", fileName, typeConfig.TableName, record.Id);
            detail.Statut = "doublon";
            detail.Message = "Document déjà rattaché à cet enregistrement";
            return detail;
        }

        // 5. Copy file
        string copiedPath;
        string? uniqueName = null;
        try
        {
            copiedPath = _fileManager.CopyToDocsFolder(filePath, fileName);
            uniqueName = Path.GetFileName(copiedPath);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Erreur copie fichier : {File}", fileName);
            detail.Statut = "erreur";
            detail.Message = $"Copie physique : {ex.Message}";
            return detail;
        }

        // 6. Build sites
        var sitesCol = _siteResolver.Resolve(record.SiteId, siteIdFromPattern, number);

        // 7. Insert document (transactional)
        var doc = new DocumentRecord
        {
            TableName = typeConfig.TableName,
            RecordId = record.Id,
            DocumentCategoryId = typeConfig.DocumentCategoryId,
            Description = Path.GetFileNameWithoutExtension(fileName),
            Path = $"/Docs/{uniqueName}",
            OriginalName = fileName,
            Sites = sitesCol,
            CreatedBy = _cfg.User.TechnicalUserId,
            ModifiedBy = _cfg.User.TechnicalUserId,
        };

        try
        {
            await _repo.InsertDocumentAsync(doc);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Échec INSERT Documents — suppression du fichier copié : {File}", uniqueName);
            _fileManager.DeleteFromDocsFolder($"/Docs/{uniqueName}");
            detail.Statut = "erreur";
            detail.Message = $"INSERT SQL : {ex.Message}";
            return detail;
        }

        _log.LogInformation("  ✓ Rattaché : {File} → Documents.ID liée à {Table} #{Id}", fileName, typeConfig.TableName, record.Id);
        detail.Statut = "rattache";
        return detail;
    }

    public IngestionResult? GetBatchStatus(string batchId)
        => _batches.TryGetValue(batchId, out var r) ? r : null;

    private void PrintSummary(IngestionResult r)
    {
        _log.LogInformation(
            "\n╔══════════════════════════════════════════════════════════════╗\n" +
            "║              RÉCAPITULATIF LOT {BatchId,-10}                   ║\n" +
            "╠══════════════════════════════════════════════════════════════╣\n" +
            "║  Traités   : {Total,-6}                                         ║\n" +
            "║  Rattachés : {Rattaches,-6}                                         ║\n" +
            "║  Doublons  : {Doublons,-6}                                         ║\n" +
            "║  À vérifier: {AVerifier,-6}                                         ║\n" +
            "║  Erreurs   : {Erreurs,-6}                                         ║\n" +
            "╚══════════════════════════════════════════════════════════════╝",
            r.BatchId, r.Total, r.Rattaches, r.Doublons, r.AVerifier,
            r.Erreurs.Count(e => e.Statut == "erreur"));
    }
}
