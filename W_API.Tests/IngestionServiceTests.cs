using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using W_API.Core.Configuration;
using W_API.Core.Interfaces;
using W_API.Core.Models;
using W_API.Core.Services;

namespace W_API.Tests;

public class IngestionServiceTests
{
    private static IOptions<AppSettings> BuildOptions(string inputFolder) => Options.Create(new AppSettings
    {
        Paths = new PathsSettings
        {
            InputFolder = inputFolder,
            DocsPhysicalFolder = Path.GetTempPath(),
            ToVerifyFolder = Path.Combine(Path.GetTempPath(), "_a_verifier")
        },
        Ingestion = new IngestionSettings { MaxFilesPerRun = 5 },
        User = new UserSettings { TechnicalUserId = 99 },
        DocumentTypes =
        [
            new DocumentTypeConfig
            {
                Name = "Bon de commande",
                Regex = @"(?<site>TO|AB|SO|TA)?/?BC[-]?(?<yy>\d{2})[-]?(?<seq>\d{4,6})",
                TableName = "AQManagerData.PurchaseOrders,AQManagerData",
                DocumentCategoryId = 11,
                NumberColumns = ["PONumber"]
            }
        ],
        Sites = new SiteSettings
        {
            Prefixes = [new SitePrefix { Prefix = "TO", SiteId = 2 }]
        }
    });

    private static IngestionService BuildService(
        string inputFolder,
        IPdfTextExtractor extractor,
        IAqManagerRepository repo,
        IFileManager fileManager)
    {
        var opts = BuildOptions(inputFolder);
        var detector = new DocumentTypeDetector(opts);
        var siteResolver = new SiteResolver(opts);
        return new IngestionService(
            extractor, detector, repo, fileManager, siteResolver,
            opts, NullLogger<IngestionService>.Instance);
    }

    [Fact]
    public async Task ProcessFile_DuplicateDocument_ReturnsDuplonStatus()
    {
        var extractor = new Mock<IPdfTextExtractor>();
        extractor.Setup(e => e.ExtractAsync(It.IsAny<string>()))
            .ReturnsAsync(new PdfExtractionResult
            {
                Text = "BC-26-00001 bon de commande",
                IsConfident = true,
                OcrConfidence = 95
            });

        var repo = new Mock<IAqManagerRepository>();
        repo.Setup(r => r.FindBusinessRecordAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<string>()))
            .ReturnsAsync(new BusinessRecord { Id = 42, SiteId = 1, TableName = "AQManagerData.PurchaseOrders,AQManagerData" });
        repo.Setup(r => r.DocumentExistsAsync(It.IsAny<string>(), 42, It.IsAny<string>()))
            .ReturnsAsync(true);

        var fileManager = new Mock<IFileManager>();
        var svc = BuildService(Path.GetTempPath(), extractor.Object, repo.Object, fileManager.Object);

        var result = await svc.ProcessFileAsync("dummy.pdf");

        result.Statut.Should().Be("doublon");
        repo.Verify(r => r.InsertDocumentAsync(It.IsAny<DocumentRecord>()), Times.Never);
    }

    [Fact]
    public async Task ProcessFile_NoMatchingRecord_ReturnsAVerifier()
    {
        var extractor = new Mock<IPdfTextExtractor>();
        extractor.Setup(e => e.ExtractAsync(It.IsAny<string>()))
            .ReturnsAsync(new PdfExtractionResult
            {
                Text = "BC-26-99999",
                IsConfident = true,
                OcrConfidence = 90
            });

        var repo = new Mock<IAqManagerRepository>();
        repo.Setup(r => r.FindBusinessRecordAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<string>()))
            .ReturnsAsync((BusinessRecord?)null);

        var fileManager = new Mock<IFileManager>();
        var svc = BuildService(Path.GetTempPath(), extractor.Object, repo.Object, fileManager.Object);

        var result = await svc.ProcessFileAsync("dummy.pdf");

        result.Statut.Should().Be("a_verifier");
        fileManager.Verify(f => f.MoveToVerifyFolder(It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task ProcessFile_InsertFails_RollsBackFileCopy()
    {
        var extractor = new Mock<IPdfTextExtractor>();
        extractor.Setup(e => e.ExtractAsync(It.IsAny<string>()))
            .ReturnsAsync(new PdfExtractionResult
            {
                Text = "BC-26-00001",
                IsConfident = true,
                OcrConfidence = 90
            });

        var repo = new Mock<IAqManagerRepository>();
        repo.Setup(r => r.FindBusinessRecordAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<string>()))
            .ReturnsAsync(new BusinessRecord { Id = 10, TableName = "AQManagerData.PurchaseOrders,AQManagerData" });
        repo.Setup(r => r.DocumentExistsAsync(It.IsAny<string>(), 10, It.IsAny<string>()))
            .ReturnsAsync(false);
        repo.Setup(r => r.InsertDocumentAsync(It.IsAny<DocumentRecord>()))
            .ThrowsAsync(new Exception("SQL constraint violated"));

        var fileManager = new Mock<IFileManager>();
        fileManager.Setup(f => f.CopyToDocsFolder(It.IsAny<string>(), It.IsAny<string>()))
            .Returns("/Docs/20260101120000-dummy.pdf");

        var svc = BuildService(Path.GetTempPath(), extractor.Object, repo.Object, fileManager.Object);

        var result = await svc.ProcessFileAsync("dummy.pdf");

        result.Statut.Should().Be("erreur");
        fileManager.Verify(f => f.DeleteFromDocsFolder(It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task ProcessFile_LowOcrConfidence_ReturnsAVerifier()
    {
        var extractor = new Mock<IPdfTextExtractor>();
        extractor.Setup(e => e.ExtractAsync(It.IsAny<string>()))
            .ReturnsAsync(new PdfExtractionResult
            {
                Text = "BC-26-00001",
                IsConfident = false,
                UsedOcr = true,
                OcrConfidence = 30
            });

        var repo = new Mock<IAqManagerRepository>();
        var fileManager = new Mock<IFileManager>();
        var svc = BuildService(Path.GetTempPath(), extractor.Object, repo.Object, fileManager.Object);

        var result = await svc.ProcessFileAsync("dummy.pdf");

        result.Statut.Should().Be("a_verifier");
        fileManager.Verify(f => f.MoveToVerifyFolder(It.IsAny<string>()), Times.Once);
        repo.Verify(r => r.FindBusinessRecordAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ProcessFile_SuccessfulAttachment_SetsCorrectDocumentFields()
    {
        var extractor = new Mock<IPdfTextExtractor>();
        extractor.Setup(e => e.ExtractAsync(It.IsAny<string>()))
            .ReturnsAsync(new PdfExtractionResult
            {
                Text = "TO/BC-26-00001 bon de commande",
                IsConfident = true,
                OcrConfidence = 92
            });

        DocumentRecord? captured = null;
        var repo = new Mock<IAqManagerRepository>();
        repo.Setup(r => r.FindBusinessRecordAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<string>()))
            .ReturnsAsync(new BusinessRecord { Id = 7, SiteId = 2, TableName = "AQManagerData.PurchaseOrders,AQManagerData", MatchedColumn = "PONumber" });
        repo.Setup(r => r.DocumentExistsAsync(It.IsAny<string>(), 7, It.IsAny<string>()))
            .ReturnsAsync(false);
        repo.Setup(r => r.InsertDocumentAsync(It.IsAny<DocumentRecord>()))
            .Callback<DocumentRecord>(d => captured = d)
            .Returns(Task.CompletedTask);

        var fileManager = new Mock<IFileManager>();
        fileManager.Setup(f => f.CopyToDocsFolder(It.IsAny<string>(), It.IsAny<string>()))
            .Returns("/Docs/20260101120000-TO_BC-26-00001.pdf");

        var svc = BuildService(Path.GetTempPath(), extractor.Object, repo.Object, fileManager.Object);

        var result = await svc.ProcessFileAsync("TO_BC-26-00001.pdf");

        result.Statut.Should().Be("rattache");
        captured.Should().NotBeNull();
        captured!.RecordId.Should().Be(7);
        captured.DocumentCategoryId.Should().Be(11);
        captured.Sites.Should().Be(",2,");
        captured.CreatedBy.Should().Be(99);
        captured.Disabled.Should().BeFalse();
        captured.Path.Should().StartWith("/Docs/");
    }
}
