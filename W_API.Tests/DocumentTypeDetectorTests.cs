using FluentAssertions;
using Microsoft.Extensions.Options;
using W_API.Core.Configuration;
using W_API.Core.Services;

namespace W_API.Tests;

public class DocumentTypeDetectorTests
{
    private static IOptions<AppSettings> BuildOptions() => Options.Create(new AppSettings
    {
        DocumentTypes =
        [
            new DocumentTypeConfig
            {
                Name = "Bon de commande",
                Regex = @"(?<site>TO|AB|SO|TA)?/?BC[-]?(?<yy>\d{2})[-]?(?<seq>\d{4,6})",
                TableName = "AQManagerData.PurchaseOrders,AQManagerData",
                DocumentCategoryId = 11,
                NumberColumns = ["PONumber"]
            },
            new DocumentTypeConfig
            {
                Name = "Facture",
                Regex = @"(?<site>TO|AB|SO|TA)?/?FACT[-]?(?<yy>\d{2})[-]?(?<seq>\d{4,6})",
                TableName = "AQManagerData.SupplierInvoices,AQManagerData",
                DocumentCategoryId = 13,
                NumberColumns = ["SISupplierNumber", "SINumber"]
            },
            new DocumentTypeConfig
            {
                Name = "Bon de livraison",
                Regex = @"(?<site>TO|AB|SO|TA)?/?BL[-]?(?<yy>\d{2})[-]?(?<seq>\d{4,6})",
                TableName = "AQManagerData.ReceivingSlips,AQManagerData",
                DocumentCategoryId = 10,
                NumberColumns = ["RSNumber"]
            }
        ],
        Sites = new SiteSettings
        {
            Prefixes =
            [
                new SitePrefix { Prefix = "TO", SiteId = 2, SiteName = "LRA TOULEPLEU" },
                new SitePrefix { Prefix = "AB", SiteId = 3, SiteName = "LRA ABOISSO" },
                new SitePrefix { Prefix = "SO", SiteId = 4, SiteName = "LRA SOUBRE" },
                new SitePrefix { Prefix = "TA", SiteId = 5, SiteName = "LRA TAI" }
            ]
        }
    });

    [Theory]
    [InlineData("Réf : TO/BC-26-00001 Bon de commande Toulepleu", "Bon de commande", "TO/BC-26-00001", 2)]
    [InlineData("Numéro BC-25-00566", "Bon de commande", "BC-25-00566", null)]
    [InlineData("TOBC-25-00566 matériaux", "Bon de commande", "TOBC-25-00566", 2)]
    [InlineData("AB/FACT-26-00012 facture fournisseur", "Facture", "AB/FACT-26-00012", 3)]
    [InlineData("SO/BL-26-00001 bon livraison", "Bon de livraison", "SO/BL-26-00001", 4)]
    public void Detect_KnownPatterns_ReturnsCorrectTypeAndNumber(
        string text, string expectedType, string expectedNumber, int? expectedSiteId)
    {
        var detector = new DocumentTypeDetector(BuildOptions());

        var (type, number, siteId) = detector.Detect(text);

        type.Should().NotBeNull();
        type!.Name.Should().Be(expectedType);
        number.Should().Be(expectedNumber);
        siteId.Should().Be(expectedSiteId);
    }

    [Fact]
    public void Detect_UnknownText_ReturnsNull()
    {
        var detector = new DocumentTypeDetector(BuildOptions());
        var (type, number, _) = detector.Detect("Lorem ipsum dolor sit amet.");
        type.Should().BeNull();
        number.Should().BeNull();
    }

    [Fact]
    public void Detect_BonDeCommande_HasCorrectCategoryAndTable()
    {
        var detector = new DocumentTypeDetector(BuildOptions());
        var (type, _, _) = detector.Detect("BC-26-00001");
        type!.DocumentCategoryId.Should().Be(11);
        type.TableName.Should().Be("AQManagerData.PurchaseOrders,AQManagerData");
        type.NumberColumns.Should().ContainSingle().Which.Should().Be("PONumber");
    }

    [Fact]
    public void Detect_Facture_HasTwoNumberColumns()
    {
        var detector = new DocumentTypeDetector(BuildOptions());
        var (type, _, _) = detector.Detect("FACT-26-00012");
        type!.NumberColumns.Should().HaveCount(2);
        type.NumberColumns[0].Should().Be("SISupplierNumber");
        type.NumberColumns[1].Should().Be("SINumber");
    }
}
