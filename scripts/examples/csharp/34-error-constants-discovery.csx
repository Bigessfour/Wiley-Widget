#!/usr/bin/env dotnet-script
#r "nuget: DryIoc, 5.4.3"

using System;
using System.Linq;
using System.Reflection;
using DryIoc;

Console.WriteLine("=== DryIoc Error Constants Discovery ===");

var errorType = typeof(Error);
Console.WriteLine($"Type: {errorType.FullName}");

var fields = errorType.GetFields(BindingFlags.Public | BindingFlags.Static)
    .Where(f => f.FieldType == typeof(int))
    .ToList();

Console.WriteLine($"\nTotal Error Constants: {fields.Count}");
Console.WriteLine("\nFirst 10 Error Constants:");

foreach (var field in fields.Take(10))
{
    var value = field.GetValue(null);
    Console.WriteLine($"  {field.Name} = {value}");
}

Console.WriteLine("\n✓ Discovery complete");
