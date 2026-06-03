namespace W_API.Core.Models;

public class DocumentRecord
{
    public int Id { get; set; }
    public string TableName { get; set; } = string.Empty;
    public int RecordId { get; set; }
    public int DocumentCategoryId { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string OriginalName { get; set; } = string.Empty;
    public string Sites { get; set; } = string.Empty;
    public int Position { get; set; } = 1;
    public int CreatedBy { get; set; }
    public int ModifiedBy { get; set; }
    public bool Disabled { get; set; } = false;
    public bool IsSystem { get; set; } = false;
}
