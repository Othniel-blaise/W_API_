namespace W_API.Core.Configuration;

public class AppSettings
{
    public PathsSettings Paths { get; set; } = new();
    public IngestionSettings Ingestion { get; set; } = new();
    public OcrSettings Ocr { get; set; } = new();
    public UserSettings User { get; set; } = new();
    public List<DocumentTypeConfig> DocumentTypes { get; set; } = [];
    public SiteSettings Sites { get; set; } = new();
}

public class PathsSettings
{
    public string InputFolder { get; set; } = string.Empty;
    public string DocsPhysicalFolder { get; set; } = string.Empty;
    public string ToVerifyFolder { get; set; } = string.Empty;
}

public class IngestionSettings
{
    public int MaxFilesPerRun { get; set; } = 5;
    public int WatchIntervalSeconds { get; set; } = 60;
    public bool EnableBackgroundWorker { get; set; } = false;
}

public class OcrSettings
{
    public double MinConfidence { get; set; } = 60.0;
    public int Dpi { get; set; } = 300;
    public string TessDataPath { get; set; } = "tessdata";
    public string Languages { get; set; } = "fra+eng";
}

public class UserSettings
{
    public int TechnicalUserId { get; set; } = 1;
}

public class DocumentTypeConfig
{
    public string Name { get; set; } = string.Empty;
    public string Regex { get; set; } = string.Empty;
    public List<string> Keywords { get; set; } = [];
    public string TableName { get; set; } = string.Empty;
    public int DocumentCategoryId { get; set; }
    public List<string> NumberColumns { get; set; } = [];
}

public class SiteSettings
{
    public List<SitePrefix> Prefixes { get; set; } = [];
}

public class SitePrefix
{
    public string Prefix { get; set; } = string.Empty;
    public int SiteId { get; set; }
    public string SiteName { get; set; } = string.Empty;
}
