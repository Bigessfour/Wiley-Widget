using System.Threading.Tasks;
using DBConfirm.Core.Data;
using DBConfirm.Core.DataResults;
using DBConfirm.Core.Parameters;
using DBConfirm.Packages.SQLServer.NUnit;
using Microsoft.Data.SqlClient;
using Xunit;

namespace WileyWidget.Tests;

/// <summary>
/// Database tests for Enterprise operations using DBConfirm
/// Tests actual SQL Server database operations
/// </summary>
public class EnterpriseDatabaseTests : DatabaseTestBase
{
    [Fact]
    public async Task SimpleDatabaseConnectionTest()
    {
        // Simple test to verify database connection works
        // This bypasses DBConfirm to test basic connectivity
        var connectionString = "Server=.\\SQLEXPRESS;Database=WileyWidget;Trusted_Connection=True;TrustServerCertificate=True;Connection Timeout=30;";

        using (var connection = new SqlConnection(connectionString))
        {
            await connection.OpenAsync();

            using (var command = new SqlCommand("SELECT COUNT(*) FROM dbo.Enterprises", connection))
            {
                var result = await command.ExecuteScalarAsync();
                Assert.True((int)result >= 0); // Just verify we can query the table
            }
        }
    }

    [Fact]
    public async Task InsertEnterprise_DataIsInsertedCorrectly_UsingDirectConnection()
    {
        // Test data insertion using direct SQL connection with transaction isolation
        var connectionString = "Server=.\\SQLEXPRESS;Database=WileyWidget;Trusted_Connection=True;TrustServerCertificate=True;Connection Timeout=30;";
        var uniqueId = Guid.NewGuid().ToString().Substring(0, 8);
        var testCompanyName = $"Test Water Company {uniqueId}";

        using (var connection = new SqlConnection(connectionString))
        {
            await connection.OpenAsync();

            // Use transaction for test isolation
            using (var transaction = connection.BeginTransaction())
            {
                try
                {
                    // Insert test data
                    using (var command = new SqlCommand(
                        @"INSERT INTO dbo.Enterprises (Name, CurrentRate, MonthlyExpenses, CitizenCount, Notes)
                          VALUES (@Name, @CurrentRate, @MonthlyExpenses, @CitizenCount, @Notes)",
                        connection, transaction))
                    {
                        command.Parameters.AddWithValue("@Name", testCompanyName);
                        command.Parameters.AddWithValue("@CurrentRate", 2.50m);
                        command.Parameters.AddWithValue("@MonthlyExpenses", 15000.00m);
                        command.Parameters.AddWithValue("@CitizenCount", 5000);
                        command.Parameters.AddWithValue("@Notes", "Test enterprise for direct SQL");

                        await command.ExecuteNonQueryAsync();
                    }

                    // Query the data back to verify
                    using (var command = new SqlCommand(
                        "SELECT Name, CurrentRate, MonthlyExpenses, CitizenCount, Notes FROM dbo.Enterprises WHERE Name = @Name",
                        connection, transaction))
                    {
                        command.Parameters.AddWithValue("@Name", testCompanyName);

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            Assert.True(await reader.ReadAsync());
                            Assert.Equal(testCompanyName, reader["Name"].ToString());
                            Assert.Equal(2.50m, (decimal)reader["CurrentRate"]);
                            Assert.Equal(15000.00m, (decimal)reader["MonthlyExpenses"]);
                            Assert.Equal(5000, (int)reader["CitizenCount"]);
                            Assert.Equal("Test enterprise for direct SQL", reader["Notes"].ToString());
                        }
                    }

                    // Rollback to clean up test data
                    transaction.Rollback();
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
        }
    }

    [Fact]
    public async Task QueryEnterpriseByName_ReturnsCorrectData_UsingDirectConnection()
    {
        // Test querying by name using direct SQL connection with transaction isolation
        var connectionString = "Server=.\\SQLEXPRESS;Database=WileyWidget;Trusted_Connection=True;TrustServerCertificate=True;Connection Timeout=30;";
        var uniqueId = Guid.NewGuid().ToString().Substring(0, 8);
        var testCompanyName = $"Test Sewer Company {uniqueId}";

        using (var connection = new SqlConnection(connectionString))
        {
            await connection.OpenAsync();

            // Use transaction for test isolation
            using (var transaction = connection.BeginTransaction())
            {
                try
                {
                    // Insert test data
                    using (var command = new SqlCommand(
                        @"INSERT INTO dbo.Enterprises (Name, CurrentRate, MonthlyExpenses, CitizenCount, Notes)
                          VALUES (@Name, @CurrentRate, @MonthlyExpenses, @CitizenCount, @Notes)",
                        connection, transaction))
                    {
                        command.Parameters.AddWithValue("@Name", testCompanyName);
                        command.Parameters.AddWithValue("@CurrentRate", 3.75m);
                        command.Parameters.AddWithValue("@MonthlyExpenses", 25000.00m);
                        command.Parameters.AddWithValue("@CitizenCount", 8000);
                        command.Parameters.AddWithValue("@Notes", "Another test enterprise");

                        await command.ExecuteNonQueryAsync();
                    }

                    // Query the data back by name
                    using (var command = new SqlCommand(
                        "SELECT Name, CurrentRate FROM dbo.Enterprises WHERE Name = @Name",
                        connection, transaction))
                    {
                        command.Parameters.AddWithValue("@Name", testCompanyName);

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            Assert.True(await reader.ReadAsync());
                            Assert.Equal(testCompanyName, reader["Name"].ToString());
                            Assert.Equal(3.75m, (decimal)reader["CurrentRate"]);
                        }
                    }

                    // Rollback to clean up test data
                    transaction.Rollback();
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
        }
    }

    [Fact]
    public async Task CustomQuery_WithParameters_ReturnsFilteredData_UsingDirectConnection()
    {
        // Test parameterized query using direct SQL connection with transaction isolation
        var connectionString = "Server=.\\SQLEXPRESS;Database=WileyWidget;Trusted_Connection=True;TrustServerCertificate=True;Connection Timeout=30;";
        var uniqueId = Guid.NewGuid().ToString().Substring(0, 8);
        var highRateName = $"High Rate Company {uniqueId}";
        var lowRateName = $"Low Rate Company {uniqueId}";

        using (var connection = new SqlConnection(connectionString))
        {
            await connection.OpenAsync();

            // Use transaction for test isolation
            using (var transaction = connection.BeginTransaction())
            {
                try
                {
                    // Insert multiple test enterprises within transaction
                    using (var command = new SqlCommand(
                        @"INSERT INTO dbo.Enterprises (Name, CurrentRate, MonthlyExpenses, CitizenCount, Notes)
                          VALUES (@Name1, @CurrentRate1, @MonthlyExpenses1, @CitizenCount1, @Notes1),
                                 (@Name2, @CurrentRate2, @MonthlyExpenses2, @CitizenCount2, @Notes2)",
                        connection, transaction))
                    {
                        command.Parameters.AddWithValue("@Name1", highRateName);
                        command.Parameters.AddWithValue("@CurrentRate1", 5.00m);
                        command.Parameters.AddWithValue("@MonthlyExpenses1", 30000.00m);
                        command.Parameters.AddWithValue("@CitizenCount1", 10000);
                        command.Parameters.AddWithValue("@Notes1", "High rate enterprise");

                        command.Parameters.AddWithValue("@Name2", lowRateName);
                        command.Parameters.AddWithValue("@CurrentRate2", 1.50m);
                        command.Parameters.AddWithValue("@MonthlyExpenses2", 10000.00m);
                        command.Parameters.AddWithValue("@CitizenCount2", 3000);
                        command.Parameters.AddWithValue("@Notes2", "Low rate enterprise");

                        await command.ExecuteNonQueryAsync();
                    }

                    // Query with parameters to find high rate companies within transaction
                    using (var command = new SqlCommand(
                        $"SELECT Name, CurrentRate FROM dbo.Enterprises WHERE CurrentRate > @rateThreshold AND Name IN ('{highRateName}', '{lowRateName}')",
                        connection, transaction))
                    {
                        command.Parameters.AddWithValue("@rateThreshold", 3.00m);

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            Assert.True(await reader.ReadAsync());
                            Assert.Equal(highRateName, reader["Name"].ToString());
                            Assert.Equal(5.00m, (decimal)reader["CurrentRate"]);

                            // Should not have the low rate company
                            Assert.False(await reader.ReadAsync());
                        }
                    }

                    // Rollback to clean up test data
                    transaction.Rollback();
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
        }
    }

    [Fact]
    public async Task StoredProcedure_InsertAndRetrieveEnterprise_UsingDirectConnection()
    {
        // Test stored procedure-like functionality using direct SQL with transaction isolation
        var connectionString = "Server=.\\SQLEXPRESS;Database=WileyWidget;Trusted_Connection=True;TrustServerCertificate=True;Connection Timeout=30;";
        var uniqueId = Guid.NewGuid().ToString().Substring(0, 8);
        var testCompanyName = $"Stored Proc Company {uniqueId}";

        using (var connection = new SqlConnection(connectionString))
        {
            await connection.OpenAsync();

            // Use transaction for test isolation
            using (var transaction = connection.BeginTransaction())
            {
                try
                {
                    // Simulate stored procedure functionality with direct SQL
                    using (var command = new SqlCommand(
                        @"INSERT INTO dbo.Enterprises (Name, CurrentRate, MonthlyExpenses, CitizenCount, Notes)
                          VALUES (@Name, @CurrentRate, @MonthlyExpenses, @CitizenCount, @Notes)",
                        connection, transaction))
                    {
                        command.Parameters.AddWithValue("@Name", testCompanyName);
                        command.Parameters.AddWithValue("@CurrentRate", 4.25m);
                        command.Parameters.AddWithValue("@MonthlyExpenses", 20000.00m);
                        command.Parameters.AddWithValue("@CitizenCount", 6000);
                        command.Parameters.AddWithValue("@Notes", "Inserted via direct table access");

                        await command.ExecuteNonQueryAsync();
                    }

                    // Verify data was inserted (simulating stored procedure result)
                    using (var command = new SqlCommand(
                        "SELECT Name, CurrentRate FROM dbo.Enterprises WHERE Name = @Name",
                        connection, transaction))
                    {
                        command.Parameters.AddWithValue("@Name", testCompanyName);

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            Assert.True(await reader.ReadAsync());
                            Assert.Equal(testCompanyName, reader["Name"].ToString());
                            Assert.Equal(4.25m, (decimal)reader["CurrentRate"]);
                        }
                    }

                    // Rollback to clean up test data
                    transaction.Rollback();
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
        }
    }

    [Fact]
    public async Task ComplexAssertions_MultipleValidations_UsingDirectConnection()
    {
        // Test multiple validations using direct SQL connection with transaction isolation
        var connectionString = "Server=.\\SQLEXPRESS;Database=WileyWidget;Trusted_Connection=True;TrustServerCertificate=True;Connection Timeout=30;";
        var uniqueId = Guid.NewGuid().ToString().Substring(0, 8);
        var testCompanyName = $"Complex Test Company {uniqueId}";

        using (var connection = new SqlConnection(connectionString))
        {
            await connection.OpenAsync();

            // Use transaction for test isolation
            using (var transaction = connection.BeginTransaction())
            {
                try
                {
                    // Insert test data
                    using (var command = new SqlCommand(
                        @"INSERT INTO dbo.Enterprises (Name, CurrentRate, MonthlyExpenses, CitizenCount, Notes)
                          VALUES (@Name, @CurrentRate, @MonthlyExpenses, @CitizenCount, @Notes)",
                        connection, transaction))
                    {
                        command.Parameters.AddWithValue("@Name", testCompanyName);
                        command.Parameters.AddWithValue("@CurrentRate", 2.99m);
                        command.Parameters.AddWithValue("@MonthlyExpenses", 17500.50m);
                        command.Parameters.AddWithValue("@CitizenCount", 7500);
                        command.Parameters.AddWithValue("@Notes", "Complex validation test");

                        await command.ExecuteNonQueryAsync();
                    }

                    // Verify table structure and data
                    using (var command = new SqlCommand(
                        @"SELECT
                            (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
                             WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'Enterprises'
                             AND COLUMN_NAME IN ('Id', 'Name', 'CurrentRate', 'MonthlyExpenses', 'CitizenCount', 'Notes')) as ColumnCount,
                            Name, CurrentRate, MonthlyExpenses, CitizenCount, Notes
                          FROM dbo.Enterprises WHERE Name = @Name",
                        connection, transaction))
                    {
                        command.Parameters.AddWithValue("@Name", testCompanyName);

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            Assert.True(await reader.ReadAsync());

                            // Verify all expected columns exist
                            Assert.Equal(6, (int)reader["ColumnCount"]);

                            // Verify data values
                            Assert.Equal(testCompanyName, reader["Name"].ToString());
                            Assert.Equal(2.99m, (decimal)reader["CurrentRate"]);
                            Assert.Equal(17500.50m, (decimal)reader["MonthlyExpenses"]);
                            Assert.Equal(7500, (int)reader["CitizenCount"]);
                            Assert.Equal("Complex validation test", reader["Notes"].ToString());
                        }
                    }

                    // Rollback to clean up test data
                    transaction.Rollback();
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
        }
    }

    [Fact]
    public async Task DataIsolation_TestRollbackBehavior_UsingDirectConnection()
    {
        // Test transaction isolation using direct SQL connection
        var connectionString = "Server=.\\SQLEXPRESS;Database=WileyWidget;Trusted_Connection=True;TrustServerCertificate=True;Connection Timeout=30;";

        using (var connection = new SqlConnection(connectionString))
        {
            await connection.OpenAsync();

            // Start a transaction
            using (var transaction = connection.BeginTransaction())
            {
                try
                {
                    // Insert data within transaction
                    using (var command = new SqlCommand(
                        @"INSERT INTO dbo.Enterprises (Name, CurrentRate, MonthlyExpenses, CitizenCount, Notes)
                          VALUES (@Name, @CurrentRate, @MonthlyExpenses, @CitizenCount, @Notes)",
                        connection, transaction))
                    {
                        command.Parameters.AddWithValue("@Name", "Isolation Test Company");
                        command.Parameters.AddWithValue("@CurrentRate", 1.00m);
                        command.Parameters.AddWithValue("@MonthlyExpenses", 5000.00m);
                        command.Parameters.AddWithValue("@CitizenCount", 1000);
                        command.Parameters.AddWithValue("@Notes", "Should not persist after test");

                        await command.ExecuteNonQueryAsync();
                    }

                    // Verify data exists within this transaction
                    using (var command = new SqlCommand(
                        "SELECT COUNT(*) FROM dbo.Enterprises WHERE Name = @Name",
                        connection, transaction))
                    {
                        command.Parameters.AddWithValue("@Name", "Isolation Test Company");
                        var count = (int)await command.ExecuteScalarAsync();
                        Assert.Equal(1, count);
                    }

                    // Explicitly rollback the transaction (simulating test isolation)
                    transaction.Rollback();

                    // Verify data no longer exists after rollback
                    using (var command = new SqlCommand(
                        "SELECT COUNT(*) FROM dbo.Enterprises WHERE Name = @Name",
                        connection))
                    {
                        command.Parameters.AddWithValue("@Name", "Isolation Test Company");
                        var count = (int)await command.ExecuteScalarAsync();
                        Assert.Equal(0, count); // Data should not persist after rollback
                    }
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
        }
    }
}
