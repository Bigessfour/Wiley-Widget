using Xunit;

namespace WileyWidget.WinForms.Tests.Infrastructure;

[CollectionDefinition(CollectionName, DisableParallelization = true)]
public sealed class WinFormsUiCollection : ICollectionFixture<WinFormsUiThreadFixture>, ICollectionFixture<SyncfusionLicenseFixture>
{
    public const string CollectionName = "WinForms UI";
}
