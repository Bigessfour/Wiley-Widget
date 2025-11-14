#!/usr/bin/env dotnet-script
/*
 * Test: SQL Server MCP Integration Validation
 * Purpose: Verify SQL Server MCP is properly configured and accessible
 * Category: MCP Integration Testing
 * 
 * This test validates:
 * 1. SQL Server service is running
 * 2. Connection can be established
 * 3. Basic query execution works
 * 4. WileyWidget database exists (if expected)
 * 
 * Usage:
 *   docker run --rm -w /app \
 *     -v "${workspaceFolder}:/app:ro" \
 *     -v "${workspaceFolder}/logs:/logs:rw" \
 *     -e WW_REPO_ROOT=/app \
 *     -e WW_LOGS_DIR=/logs \
 *     wiley-widget/csx-mcp:local \
 *     scripts/examples/csharp/95-sql-mcp-validation-test.csx
 */

#r "nuget: System.Data.SqlClient, 4.8.6"

using System;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Linq;

// Test configuration
var testName = "SQL Server MCP Validation";
var serverName = Environment.GetEnvironmentVariable("SQL_SERVER_NAME") ?? "localhost";
var connectionTimeout = int.Parse(Environment.GetEnvironmentVariable("SQL_CONNECTION_TIMEOUT") ?? "30");
var logDir = Environment.GetEnvironmentVariable("WW_LOGS_DIR") ?? "./logs";

Console.WriteLine($"╔══════════════════════════════════════════════════════════╗");
Console.WriteLine($"║  {testName,-54}  ║");
Console.WriteLine($"╚══════════════════════════════════════════════════════════╝\n");

// Test results tracking
var totalTests = 0;
var passedTests = 0;
var failedTests = 0;
var warnings = new List<string>();

void LogTest(string testName, bool passed, string message = "")
{
    totalTests++;
    if (passed)
    {
        passedTests++;
        Console.WriteLine($"✓ {testName}");
        if (!string.IsNullOrEmpty(message))
            Console.WriteLine($"  → {message}");
    }
    else
    {
        failedTests++;
        Console.WriteLine($"✗ {testName}");
        if (!string.IsNullOrEmpty(message))
            Console.WriteLine($"  → ERROR: {message}");
    }
}

void LogWarning(string message)
{
    warnings.Add(message);
    Console.WriteLine($"⚠ WARNING: {message}");
}

// Test 1: Check if SQL Server service is accessible
Console.WriteLine("\n[Test 1] SQL Server Service Accessibility");
Console.WriteLine("─────────────────────────────────────────");

try
{
    var resolved = System.Net.Dns.GetHostEntry(serverName.Split('\\')[0]);
    LogTest("Server Name Resolution", true, $"Resolved to {resolved.AddressList[0]}");
}
catch (Exception ex)
{
    LogTest("Server Name Resolution", false, ex.Message);
}

// Test 2: Basic Connection Test
Console.WriteLine("\n[Test 2] SQL Server Connection");
Console.WriteLine("─────────────────────────────────────────");

SqlConnection connection = null;
try
{
    var connectionString = $"Server={serverName};Database=master;Integrated Security=True;TrustServerCertificate=True;Connection Timeout={connectionTimeout}";
    connection = new SqlConnection(connectionString);
    
    var stopwatch = Stopwatch.StartNew();
    connection.Open();
    stopwatch.Stop();
    
    LogTest("Connection Established", true, $"Connected in {stopwatch.ElapsedMilliseconds}ms");
    
    // Test 3: Query Execution
    Console.WriteLine("\n[Test 3] Query Execution");
    Console.WriteLine("─────────────────────────────────────────");
    
    var command = connection.CreateCommand();
    command.CommandText = "SELECT @@VERSION AS Version, SERVERPROPERTY('ServerName') AS ServerName, DB_NAME() AS CurrentDatabase";
    
    using (var reader = command.ExecuteReader())
    {
        if (reader.Read())
        {
            var version = reader["Version"].ToString();
            var serverNameResult = reader["ServerName"].ToString();
            var currentDb = reader["CurrentDatabase"].ToString();
            
            LogTest("Query Execution", true, "SELECT query successful");
            Console.WriteLine($"  → Server: {serverNameResult}");
            Console.WriteLine($"  → Database: {currentDb}");
            Console.WriteLine($"  → Version: {version.Split('\n')[0]}");
        }
        else
        {
            LogTest("Query Execution", false, "No results returned");
        }
    }
    
    // Test 4: List Databases
    Console.WriteLine("\n[Test 4] Database Discovery");
    Console.WriteLine("─────────────────────────────────────────");
    
    command.CommandText = "SELECT name FROM sys.databases WHERE name NOT IN ('master', 'tempdb', 'model', 'msdb') ORDER BY name";
    
    var databases = new List<string>();
    using (var reader = command.ExecuteReader())
    {
        while (reader.Read())
        {
            databases.Add(reader["name"].ToString());
        }
    }
    
    LogTest("Database Enumeration", true, $"Found {databases.Count} user databases");
    
    if (databases.Count > 0)
    {
        Console.WriteLine("  → Databases:");
        foreach (var db in databases)
        {
            Console.WriteLine($"    • {db}");
        }
    }
    
    // Test 5: Check for WileyWidget database
    Console.WriteLine("\n[Test 5] WileyWidget Database Check");
    Console.WriteLine("─────────────────────────────────────────");
    
    var hasWileyWidget = databases.Any(db => db.Equals("WileyWidget", StringComparison.OrdinalIgnoreCase));
    
    if (hasWileyWidget)
    {
        LogTest("WileyWidget Database Exists", true, "Database found and accessible");
        
        command.CommandText = "USE WileyWidget; SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE'";
        var tableCount = (int)command.ExecuteScalar();
        Console.WriteLine($"  → Tables: {tableCount}");
    }
    else
    {
        LogWarning("WileyWidget database not found - may need to be created");
        LogTest("WileyWidget Database Exists", false, "Database not found (expected for fresh installs)");
    }
}
catch (SqlException sqlEx)
{
    LogTest("SQL Server Connection", false, sqlEx.Message);
    
    Console.WriteLine("\n⚠ Troubleshooting Tips:");
    Console.WriteLine("  1. Ensure SQL Server service is running");
    Console.WriteLine("  2. Verify TCP/IP is enabled in SQL Configuration Manager");
    Console.WriteLine("  3. Check Windows Firewall allows SQL Server connections");
    Console.WriteLine("  4. Confirm authentication method (Windows/SQL Auth)");
    Console.WriteLine($"  5. Server name used: {serverName}");
}
catch (InvalidOperationException invEx)
{
    LogTest("SQL Server Connection", false, "Integrated Security not available (expected in Docker)");
    LogWarning("This test requires Windows host or SQL Authentication");
    Console.WriteLine("\n  → Running in Docker/Linux environment");
    Console.WriteLine("  → On Windows host, run: .\\scripts\\tools\\setup-sql-mcp.ps1 -TestConnection");
}
catch (Exception ex)
{
    LogTest("SQL Server Connection", false, ex.Message);
}
finally
{
    connection?.Close();
}

// Summary
Console.WriteLine("\n╔══════════════════════════════════════════════════════════╗");
Console.WriteLine($"║  Test Summary                                            ║");
Console.WriteLine($"╚══════════════════════════════════════════════════════════╝");
Console.WriteLine($"\nTotal Tests: {totalTests}");
Console.WriteLine($"✓ Passed:    {passedTests}");
Console.WriteLine($"✗ Failed:    {failedTests}");

if (warnings.Count > 0)
{
    Console.WriteLine($"\n⚠ Warnings: {warnings.Count}");
    foreach (var warning in warnings)
    {
        Console.WriteLine($"  • {warning}");
    }
}

var exitCode = failedTests > 0 ? 1 : 0;
Console.WriteLine($"\n{(exitCode == 0 ? "✓ All tests passed!" : "✗ Some tests failed")}");

Environment.Exit(exitCode);
