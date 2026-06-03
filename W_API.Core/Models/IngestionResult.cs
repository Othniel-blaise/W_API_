namespace W_API.Core.Models;

public class IngestionResult
{
    public string BatchId { get; set; } = Guid.NewGuid().ToString("N")[..8].ToUpper();
    public int Total { get; set; }
    public int Rattaches { get; set; }
    public int Doublons { get; set; }
    public int AVerifier { get; set; }
    public List<FileIngestionDetail> Erreurs { get; set; } = [];
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? FinishedAt { get; set; }
}

public class FileIngestionDetail
{
    public string FileName { get; set; } = string.Empty;
    public string? NumeroExtrait { get; set; }
    public string? TypeDetecte { get; set; }
    public string? TableCible { get; set; }
    public int? RecordId { get; set; }
    public string Statut { get; set; } = string.Empty;  // rattache | doublon | a_verifier | erreur
    public string? Message { get; set; }
}
