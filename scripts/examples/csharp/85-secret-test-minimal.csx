#!/usr/bin/env dotnet-script
#nullable enable

#r "nuget: Microsoft.Extensions.Logging, 8.0.0"
#r "nuget: Serilog, 4.1.0"
#r "nuget: Serilog.Sinks.Console, 6.0.0"

using System;
using Serilog;

Console.WriteLine("Test 1: Basic output");

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console()
    .CreateLogger();

Console.WriteLine("Test 2: Serilog initialized");

Log.Information("Test 3: Serilog logging works");

Console.WriteLine("Test 4: All working!");

Environment.Exit(0);
