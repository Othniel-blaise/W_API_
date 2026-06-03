namespace W_API.Core.Models;

public class BusinessRecord
{
    public int Id { get; set; }
    public string Number { get; set; } = string.Empty;
    public int? SiteId { get; set; }
    public string TableName { get; set; } = string.Empty;
    public string MatchedColumn { get; set; } = string.Empty;
}
