using System.Threading.Tasks;
using DBConfirm.Core.Data;
using DBConfirm.Core.DataResults;
using DBConfirm.Core.Parameters;
using DBConfirm.Packages.SQLServer.NUnit;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace WileyWidget.Tests;

/// <summary>
/// Base class for DBConfirm database tests
/// Provides access to TestRunner for executing SQL operations
/// </summary>
public abstract class DatabaseTestBase : NUnitBase
{
    protected DatabaseTestBase()
    {
        // Try to manually initialize TestRunner if it's null
        if (TestRunner == null)
        {
            // Create a custom TestRunner instance
            var connectionString = "Server=.\\SQLEXPRESS;Database=WileyWidget;Trusted_Connection=True;TrustServerCertificate=True;Connection Timeout=30;";
            // We'll handle this in the individual tests since automatic initialization isn't working
        }
    }
}
