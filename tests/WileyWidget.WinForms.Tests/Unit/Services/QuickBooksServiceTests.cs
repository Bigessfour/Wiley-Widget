using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FluentAssertions;
using Intuit.Ipp.Data;
using WileyWidget.Services;
using Xunit;

namespace WileyWidget.WinForms.Tests.Unit.Services;

public sealed class QuickBooksServiceTests
{
    [Fact]
    public void BuildAuthorizationUrl_UsesProvidedRedirectUri()
    {
        var method = typeof(QuickBooksService).GetMethod(
            "BuildAuthorizationUrl",
            BindingFlags.NonPublic | BindingFlags.Static,
            binder: null,
            types: new[] { typeof(string), typeof(string), typeof(string), typeof(IReadOnlyList<string>), typeof(string) },
            modifiers: null);

        method.Should().NotBeNull();

        var redirectUri = "http://localhost:5007/callback/";
        var url = method!.Invoke(
            null,
            new object[]
            {
                "https://appcenter.intuit.com/connect/oauth2",
                "client-id-123",
                redirectUri,
                new[] { "com.intuit.quickbooks.accounting" },
                "state-123"
            }) as string;

        url.Should().NotBeNull();
        url.Should().Contain(Uri.EscapeDataString(redirectUri));
        url.Should().Contain("client_id=client-id-123");
    }

    [Fact]
    public void BuildTokenExchangeFormValues_UsesProvidedRedirectUri()
    {
        var method = typeof(QuickBooksService).GetMethod(
            "BuildTokenExchangeFormValues",
            BindingFlags.NonPublic | BindingFlags.Static,
            binder: null,
            types: new[] { typeof(string), typeof(string) },
            modifiers: null);

        method.Should().NotBeNull();

        var redirectUri = "http://localhost:5007/callback/";
        var values = method!.Invoke(null, new object[] { "auth-code-123", redirectUri }) as IReadOnlyList<KeyValuePair<string, string>>;

        values.Should().NotBeNull();
        values!.Should().Contain(entry => entry.Key == "redirect_uri" && entry.Value == redirectUri);
        values.Should().Contain(entry => entry.Key == "code" && entry.Value == "auth-code-123");
        values!.Select(entry => entry.Key).Should().Contain(new[] { "grant_type", "code", "redirect_uri" });
    }

    [Fact]
    public void QuickBooksService_NoLongerExposesDynamicFallbackPrefixHelper()
    {
        var method = typeof(QuickBooksService).GetMethod("BuildFallbackListenerPrefix", BindingFlags.NonPublic | BindingFlags.Static);

        method.Should().BeNull();
    }

    [Fact]
    public void ValidateRedirectUriForEnvironment_RejectsProductionHttpRedirect()
    {
        var method = typeof(QuickBooksService).GetMethod(
            "ValidateRedirectUriForEnvironment",
            BindingFlags.NonPublic | BindingFlags.Static,
            binder: null,
            types: new[] { typeof(string), typeof(string) },
            modifiers: null);

        method.Should().NotBeNull();

        var message = method!.Invoke(null, new object[] { "http://localhost:5000/callback/", "production" }) as string;

        message.Should().NotBeNull();
        message.Should().Contain("HTTPS");
    }

    [Fact]
    public void ValidateRedirectUriForEnvironment_AllowsSandboxLocalhostRedirect()
    {
        var method = typeof(QuickBooksService).GetMethod(
            "ValidateRedirectUriForEnvironment",
            BindingFlags.NonPublic | BindingFlags.Static,
            binder: null,
            types: new[] { typeof(string), typeof(string) },
            modifiers: null);

        method.Should().NotBeNull();

        var message = method!.Invoke(null, new object[] { "http://localhost:5000/callback/", "sandbox" }) as string;

        message.Should().BeNull();
    }

    [Fact]
    public void BuildPurchaseDateRangeQuery_UsesOnlyTxnDateFilters()
    {
        var adapterType = typeof(QuickBooksService).Assembly.GetType("WileyWidget.Services.IntuitDataServiceAdapter");
        adapterType.Should().NotBeNull();

        var method = adapterType!.GetMethod("BuildPurchaseDateRangeQuery", BindingFlags.NonPublic | BindingFlags.Static);
        method.Should().NotBeNull();

        var query = method!.Invoke(null, new object[]
        {
            new DateTime(2026, 1, 1),
            new DateTime(2026, 1, 31)
        }) as string;

        query.Should().Be("SELECT * FROM Purchase WHERE TxnDate >= '2026-01-01' AND TxnDate <= '2026-01-31'");
        query.Should().NotContain("DepartmentRef");
    }

    [Fact]
    public void PurchaseMatchesDepartment_UsesDepartmentReferenceNameCaseInsensitively()
    {
        var method = typeof(QuickBooksService).GetMethod(
            "PurchaseMatchesDepartment",
            BindingFlags.NonPublic | BindingFlags.Static,
            binder: null,
            types: new[] { typeof(Purchase), typeof(string) },
            modifiers: null);

        method.Should().NotBeNull();

        var purchase = new Purchase
        {
            DepartmentRef = new ReferenceType { name = "Water" }
        };

        var result = method!.Invoke(null, new object[] { purchase, "water" });

        result.Should().BeOfType<bool>();
        ((bool)result!).Should().BeTrue();
    }
}
