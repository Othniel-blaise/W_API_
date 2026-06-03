using W_API.Core.Models;

namespace W_API.Core.Interfaces;

public interface IAqManagerRepository
{
    Task<BusinessRecord?> FindBusinessRecordAsync(string tableName, IEnumerable<string> numberColumns, string number);
    Task<bool> DocumentExistsAsync(string tableName, int recordId, string originalName);
    Task InsertDocumentAsync(DocumentRecord doc);
    Task<IEnumerable<string>> GetNotNullColumnsAsync(string tableName);
}
