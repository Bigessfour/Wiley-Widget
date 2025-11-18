// Simple C# Evaluation Test
// This file demonstrates basic C# evaluation capabilities

using System;
using System.Linq;

Console.WriteLine("=== Basic C# Evaluation Test ===\n");

// Test 1: Variables and types
var message = "Hello from C# MCP!";
var number = 42;
var pi = 3.14159;

Console.WriteLine($"String: {message}");
Console.WriteLine($"Integer: {number}");
Console.WriteLine($"Double: {pi}");

// Test 2: Collections and LINQ
var numbers = new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
var evens = numbers.Where(n => n % 2 == 0);
var sum = numbers.Sum();

Console.WriteLine($"\nNumbers: [{string.Join(", ", numbers)}]");
Console.WriteLine($"Evens: [{string.Join(", ", evens)}]");
Console.WriteLine($"Sum: {sum}");

// Test 3: String manipulation
var words = new[] { "C#", "is", "awesome!" };
var sentence = string.Join(" ", words);

Console.WriteLine($"\nSentence: {sentence}");

// Test 4: DateTime
var now = DateTime.Now;
Console.WriteLine($"\nCurrent time: {now:yyyy-MM-dd HH:mm:ss}");
Console.WriteLine($"Day of week: {now.DayOfWeek}");

Console.WriteLine("\n✅ Test completed successfully!");
return 0;
