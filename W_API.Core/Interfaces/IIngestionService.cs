using W_API.Core.Models;

namespace W_API.Core.Interfaces;

public interface IIngestionService
{
    Task<IngestionResult> RunBatchAsync(CancellationToken ct = default);
    Task<FileIngestionDetail> ProcessFileAsync(string filePath);
    IngestionResult? GetBatchStatus(string batchId);
}
