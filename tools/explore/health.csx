Console.WriteLine("CSX Runner Healthy");
Console.WriteLine($"Working Directory: {Environment.CurrentDirectory}");
Console.WriteLine($"Environment: {Environment.GetEnvironmentVariable("WW_REPO_ROOT") ?? "Local"}");
Console.WriteLine("✓ All systems operational");
Environment.Exit(0);