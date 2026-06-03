using System.Text;
using System.Text.RegularExpressions;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using W_API.Core.Interfaces;
using W_API.Core.Models;

namespace W_API.Infrastructure.Data;

public class AqManagerRepository : IAqManagerRepository
{
    private readonly string _connectionString;
    private readonly ILogger<AqManagerRepository> _log;

    public AqManagerRepository(IConfiguration config, ILogger<AqManagerRepository> log)
    {
        _connectionString = config.GetConnectionString("AqManager")
            ?? throw new InvalidOperationException("ConnectionStrings:AqManager manquant dans appsettings.json");
        _log = log;
    }

    public async Task<BusinessRecord?> FindBusinessRecordAsync(
        string tableName,
        IEnumerable<string> numberColumns,
        string number)
    {
        // Extract bare table name from full .NET format: "AQManagerData.PurchaseOrders,AQManagerData"
        var bareTable = ExtractBareTableName(tableName);
        var cols = numberColumns.ToList();

        // Build variants of the number to try (with/without prefix slash)
        var variants = BuildVariants(number);

        foreach (var col in cols)
        {
            var results = new List<BusinessRecord>();

            foreach (var variant in variants)
            {
                var sql = $"""
                    SELECT TOP 10 Id, {col} AS Number, Sites
                    FROM [{bareTable}]
                    WHERE REPLACE(REPLACE(LOWER([{col}]), ' ', ''), '/', '')
                          = REPLACE(REPLACE(LOWER(@variant), ' ', ''), '/', '')
                      AND (Disabled IS NULL OR Disabled = 0)
                    """;

                await using var conn = new SqlConnection(_connectionString);
                var rows = await conn.QueryAsync<dynamic>(sql, new { variant });
                foreach (var row in rows)
                {
                    results.Add(new BusinessRecord
                    {
                        Id = (int)row.Id,
                        Number = (string)(row.Number ?? string.Empty),
                        SiteId = ParseSiteFromSitesColumn(row.Sites?.ToString()),
                        TableName = tableName,
                        MatchedColumn = col,
                    });
                }

                if (results.Count > 0) break;
            }

            if (results.Count == 0) continue;

            if (results.Count > 1)
            {
                var ids = string.Join(", ", results.Select(r => r.Id));
                _log.LogWarning("Résultat ambigu pour '{Number}' dans {Table}.{Col} : IDs candidats = [{Ids}]",
                    number, bareTable, col, ids);
                return null; // never attach to ambiguous record
            }

            _log.LogDebug("Match sur {Table}.{Col} = '{Variant}'", bareTable, col, results[0].Number);
            return results[0];
        }

        return null;
    }

    public async Task<bool> DocumentExistsAsync(string tableName, int recordId, string originalName)
    {
        const string sql = """
            SELECT COUNT(1) FROM [Documents]
            WHERE TableName = @TableName
              AND RecordID   = @RecordId
              AND OriginalName = @OriginalName
              AND (Disabled IS NULL OR Disabled = 0)
            """;

        await using var conn = new SqlConnection(_connectionString);
        var count = await conn.ExecuteScalarAsync<int>(sql, new { TableName = tableName, RecordId = recordId, OriginalName = originalName });
        return count > 0;
    }

    public async Task InsertDocumentAsync(DocumentRecord doc)
    {
        const string sql = """
            INSERT INTO [Documents]
                (Description, TableName, RecordID, DocumentCategoryID, Position,
                 CreatedDate, ModifiedDate, CreatedBy, ModifiedBy,
                 Disabled, [Path], IsSystem, Sites, OriginalName)
            VALUES
                (@Description, @TableName, @RecordId, @DocumentCategoryId, @Position,
                 GETDATE(), GETDATE(), @CreatedBy, @ModifiedBy,
                 @Disabled, @Path, @IsSystem, @Sites, @OriginalName)
            """;

        await using var conn = new SqlConnection(_connectionString);
        await conn.ExecuteAsync(sql, new
        {
            doc.Description,
            doc.TableName,
            doc.RecordId,
            doc.DocumentCategoryId,
            doc.Position,
            doc.CreatedBy,
            doc.ModifiedBy,
            Disabled = doc.Disabled ? 1 : 0,
            doc.Path,
            IsSystem = doc.IsSystem ? 1 : 0,
            doc.Sites,
            doc.OriginalName,
        });
    }

    public async Task<IEnumerable<string>> GetNotNullColumnsAsync(string tableName)
    {
        var bare = ExtractBareTableName(tableName);
        const string sql = """
            SELECT COLUMN_NAME
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_NAME = @TableName
              AND IS_NULLABLE = 'NO'
              AND COLUMN_DEFAULT IS NULL
              AND COLUMNPROPERTY(OBJECT_ID(TABLE_NAME), COLUMN_NAME, 'IsIdentity') = 0
            """;

        await using var conn = new SqlConnection(_connectionString);
        return await conn.QueryAsync<string>(sql, new { TableName = bare });
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static string ExtractBareTableName(string fullName)
    {
        // "AQManagerData.PurchaseOrders,AQManagerData"  →  "PurchaseOrders"
        var part = fullName.Split(',')[0];          // "AQManagerData.PurchaseOrders"
        var dot = part.LastIndexOf('.');
        return dot >= 0 ? part[(dot + 1)..] : part;
    }

    private static List<string> BuildVariants(string number)
    {
        // Produce several normalized forms so we can handle TO/BC-26-001, TOBC-26-001 etc.
        var variants = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { number };

        // Remove slash prefix variant
        var noSlash = Regex.Replace(number, @"^([A-Za-z]{2,4})/", "$1");
        variants.Add(noSlash);

        // Add slash prefix variant
        var withSlash = Regex.Replace(number, @"^([A-Za-z]{2,4})(?!/)(\w)", "$1/$2");
        variants.Add(withSlash);

        return variants.ToList();
    }

    private static int? ParseSiteFromSitesColumn(string? sites)
    {
        if (string.IsNullOrEmpty(sites)) return null;
        var m = Regex.Match(sites, @",(\d+),");
        return m.Success ? int.Parse(m.Groups[1].Value) : null;
    }
}
