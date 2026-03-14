using System;
using System.Net.Http;
using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using WileyWidget.Services;
using WileyWidget.Services.Abstractions;
using Xunit;

namespace WileyWidget.WinForms.Tests.Unit.Services;

public sealed class QuickBooksCompanyInfoServiceTests
{
    [Fact]
    public void ParseCompanyInfoFromResponse_ObjectWebAddr_DoesNotThrowAndExtractsUri()
    {
        var service = new QuickBooksCompanyInfoService(
            NullLogger<QuickBooksCompanyInfoService>.Instance,
            Mock.Of<IQuickBooksAuthService>(),
            tokenStore: null,
            new HttpClient(),
            new MemoryCache(new MemoryCacheOptions()));

        var method = typeof(QuickBooksCompanyInfoService).GetMethod(
            "ParseCompanyInfoFromResponse",
            BindingFlags.Instance | BindingFlags.NonPublic);

        method.Should().NotBeNull();

        const string json = """
        {
          "QueryResponse": {
            "CompanyInfo": [
              {
                "CompanyName": "Town of Wiley",
                "PrimaryEmailAddr": { "Address": "finance@townofwiley.gov" },
                "WebAddr": { "URI": "https://app.townofwiley.gov" },
                "CurrencyRef": { "value": "USD" }
              }
            ]
          }
        }
        """;

        var result = method!.Invoke(service, new object[] { json, "9341456554914940" });

        result.Should().BeOfType<QuickBooksCompanyInfo>();

        var companyInfo = (QuickBooksCompanyInfo)result!;
        companyInfo.CompanyName.Should().Be("Town of Wiley");
        companyInfo.PrimaryEmailAddress.Should().Be("finance@townofwiley.gov");
        companyInfo.WebAddr.Should().Be("https://app.townofwiley.gov");
        companyInfo.CurrencyCode.Should().Be("USD");
        companyInfo.RealmId.Should().Be("9341456554914940");
    }

    [Fact]
    public void ParseCompanyInfoFromResponse_ObjectPayloadAndNestedScalarObjects_ExtractsValues()
    {
        var service = new QuickBooksCompanyInfoService(
            NullLogger<QuickBooksCompanyInfoService>.Instance,
            Mock.Of<IQuickBooksAuthService>(),
            tokenStore: null,
            new HttpClient(),
            new MemoryCache(new MemoryCacheOptions()));

        var method = typeof(QuickBooksCompanyInfoService).GetMethod(
            "ParseCompanyInfoFromResponse",
            BindingFlags.Instance | BindingFlags.NonPublic);

        method.Should().NotBeNull();

        const string json = """
        {
          "QueryResponse": {
            "CompanyInfo": {
              "CompanyName": "Town of Wiley",
              "Country": { "value": "US" },
              "CurrencyRef": { "name": "USD" },
              "TaxIdentifier": { "value": "84-1234567" }
            }
          }
        }
        """;

        var result = method!.Invoke(service, new object[] { json, "9341456554914940" });

        result.Should().BeOfType<QuickBooksCompanyInfo>();

        var companyInfo = (QuickBooksCompanyInfo)result!;
        companyInfo.CompanyName.Should().Be("Town of Wiley");
        companyInfo.CountryCode.Should().Be("US");
        companyInfo.CurrencyCode.Should().Be("USD");
        companyInfo.TaxIdentifier.Should().Be("84-1234567");
    }
}
