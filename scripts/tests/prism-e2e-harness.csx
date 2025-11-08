// (Duplicate content removed)
using System.Diagnostics;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json.Linq;
using Xunit;

class TestCaseResult
{
{
    var harness = new Harness();
    var argv = Environment.GetCommandLineArgs().Skip(1).ToArray();
    harness.ParseArgs(argv);

    Console.WriteLine($"Prism E2E harness starting. Filter={harness.FilterTag ?? "<none>"}");

    // Run external module scripts as separate processes
    harness.RunModuleProcesses();

    // Run in-harness BudgetModule tests (always run if filter matches)
    if (string.IsNullOrEmpty(harness.FilterTag) || harness.FilterTag.IndexOf("Budget", StringComparison.OrdinalIgnoreCase) >= 0)
    {
        Console.WriteLine("Running in-harness BudgetModule checks...");
        harness.RunBudgetModuleTest();
    }

    // JUnit XML output
    var outDir = Path.Combine(Directory.GetCurrentDirectory(), "test-results");
    var outFile = Path.Combine(outDir, "prism-e2e-results.xml");
    harness.WriteJUnitXml(outFile);

    Console.WriteLine($"Finished. Results written to {outFile}");

    // Exit code: 0 if all tests passed
    var failed = harness.Results.Any(r => !r.Passed);
    Environment.Exit(failed ? 1 : 0);
}
    public string Name { get; set; }
    public double TimeSeconds { get; set; }
    public bool Passed { get; set; }
    public string Output { get; set; }
    public string FailureMessage { get; set; }
}

class Harness
{
    // Relative paths (from this script) to the existing module CSX files. Adjust if your repo places them elsewhere.
    static string[] ModuleFiles = new[] {
        "../examples/csharp/20-prism-container-e2e-test.csx",
        "../examples/csharp/21-prism-modules-e2e-test.csx",
        "../examples/csharp/22-prism-di-registration-e2e-test.csx",
        "../examples/csharp/23-prism-module-lifecycle-e2e-test.csx",
    public string Name { get; set; } = string.Empty;
    public double TimeSeconds { get; set; }
    public bool Passed { get; set; }
    public string Output { get; set; } = string.Empty;
    public string? FailureMessage { get; set; } = null;
        "../examples/csharp/24-prism-container-resolution-e2e-test.csx",
        "../examples/csharp/25-prism-region-adapters-e2e-test.csx",
    };

    public List<TestCaseResult> Results { get; } = new List<TestCaseResult>();

        "20-prism-container-e2e-test.csx",
        "21-prism-modules-e2e-test.csx",
        "22-prism-di-registration-e2e-test.csx",
        "23-prism-module-lifecycle-e2e-test.csx",
        "24-prism-container-resolution-e2e-test.csx",
        "25-prism-region-adapters-e2e-test.csx",
        {
            var a = args[i];
            if (a.StartsWith("--filter=")) FilterTag = a.Substring("--filter=".Length);
            else if (a == "--filter" && i + 1 < args.Length) { FilterTag = args[i + 1]; i++; }
    public string? FilterTag { get; private set; } = null;
    }

    bool MatchesFilter(string name)
    {
        if (string.IsNullOrEmpty(FilterTag)) return true;
        return name.IndexOf(FilterTag, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    public void RunModuleProcesses()
            // Resolve candidate locations for module file
            var candidate1 = Path.GetFullPath(Path.Combine(workDir, "scripts", "examples", "csharp", rel));
            var candidate2 = Path.GetFullPath(Path.Combine(workDir, "scripts", "examples", "csharp", name));
            var candidate3 = Path.GetFullPath(Path.Combine(workDir, "scripts", "tests", rel));
            var candidate4 = Path.GetFullPath(rel);

            var fullPath = new[] { candidate1, candidate2, candidate3, candidate4 }
                .FirstOrDefault(p => File.Exists(p));

            if (fullPath == null)
            {
                Results.Add(new TestCaseResult {
                    Name = name,
                    TimeSeconds = 0,
                    Passed = false,
                    Output = string.Empty,
                    FailureMessage = $"Module file not found: tried {candidate1}, {candidate2}, {candidate3}, {candidate4}"
                });
                continue;
            }

            var sw = Stopwatch.StartNew();
            try
            {
                var psi = new ProcessStartInfo("dotnet", $"script \"{fullPath}\"") {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                var proc = Process.Start(psi);
                if (proc == null)
                    throw new InvalidOperationException("Process.Start returned null");

                var stdout = proc.StandardOutput.ReadToEnd();
                var stderr = proc.StandardError.ReadToEnd();
                proc.WaitForExit();
                sw.Stop();

                var output = (stdout ?? string.Empty) + (stderr ?? string.Empty);
                var passed = proc.ExitCode == 0;

                Results.Add(new TestCaseResult {
                    Name = name,
                    TimeSeconds = sw.Elapsed.TotalSeconds,
                    Passed = passed,
                    Output = output,
                    FailureMessage = passed ? null : $"ExitCode={proc.ExitCode}" + (string.IsNullOrWhiteSpace(output) ? string.Empty : "\n" + output)
                });
            }
            catch (Exception ex)
            {
                sw.Stop();
                Results.Add(new TestCaseResult {
                    Name = name,
                    TimeSeconds = sw.Elapsed.TotalSeconds,
                    Passed = false,
                    Output = string.Empty,
                    FailureMessage = ex.ToString()
                });
            }
                Name = name,
                TimeSeconds = sw.Elapsed.TotalSeconds,
                Passed = passed,
                Output = output,
                FailureMessage = passed ? null : $"ExitCode={proc.ExitCode}" + (string.IsNullOrWhiteSpace(output) ? "" : "\n" + output)
            });
        }
    }

    public void RunBudgetModuleTest()
    {
        var tc = new TestCaseResult { Name = "BudgetModule_BudgetAnalysisView_DataBinding", TimeSeconds = 0 };
        var sw = Stopwatch.StartNew();
        try
        {
            // Find mock data path (relative to this script)
            var possible = new[] {
                Path.Combine(Directory.GetCurrentDirectory(), "scripts/tests/mock/budgetdata.json"),
                Path.Combine(Directory.GetCurrentDirectory(), "scripts/tests/mock/budgetdata.json".Replace('/', Path.DirectorySeparatorChar)),
                Path.Combine(AppContext.BaseDirectory, "scripts/tests/mock/budgetdata.json")
            };
            var dataPath = possible.FirstOrDefault(File.Exists);
            if (dataPath == null)
            {
                // fallback: same folder as script
                var here = Path.Combine(Path.GetDirectoryName(typeof(Harness).Assembly.Location) ?? ".", "mock", "budgetdata.json");
                if (File.Exists(here)) dataPath = here;
            var arr = Newtonsoft.Json.Linq.JArray.Parse(text);

            if (dataPath == null)
            Xunit.Assert.True(arr.Count > 0, "budgetdata.json should contain at least one budget entry");

            var text = File.ReadAllText(dataPath);
            var arr = JArray.Parse(text);

            // Basic assertion: parsed array non-empty
            Assert.True(arr.Count > 0, "budgetdata.json should contain at least one budget entry");

            // Try to locate the built assembly for deeper checks (optional)
            var workspaceRoot = Directory.GetCurrentDirectory();
            // look for WileyWidget*.dll in bin folders
            var dlls = Directory.EnumerateFiles(workspaceRoot, "WileyWidget*.dll", SearchOption.AllDirectories)
                .Where(p => p.IndexOf("obj", StringComparison.OrdinalIgnoreCase) < 0) // skip obj
                .ToList();

            if (dlls.Count > 0)
            {
                // pick the most recent
                var asmPath = dlls.OrderByDescending(File.GetLastWriteTimeUtc).First();
                var asm = Assembly.LoadFrom(asmPath);
                // find type named BudgetAnalysisViewModel
                var vmType = asm.GetTypes().FirstOrDefault(t => t.Name.IndexOf("BudgetAnalysisViewModel", StringComparison.OrdinalIgnoreCase) >= 0);
                if (vmType != null)
                {
                    object vm = null;
                    // try parameterless constructor
                    var ctor = vmType.GetConstructor(Type.EmptyTypes);
                    if (ctor != null) vm = Activator.CreateInstance(vmType);
                    else
                    {
                        // try to find any constructor with optional params
                        var ctors = vmType.GetConstructors();
                        var paramless = ctors.FirstOrDefault(c => c.GetParameters().Length == 0);
                        if (paramless != null) vm = Activator.CreateInstance(vmType);
                    }

                    if (vm != null)
                    {
                        // If there is a method named LoadBudgetData or LoadData that accepts a string path or JArray, call it.
                        var loadMethod = vmType.GetMethod("LoadBudgetData") ?? vmType.GetMethod("LoadData");
                        if (loadMethod != null)
                        {
                            var p = loadMethod.GetParameters();
                            if (p.Length == 1 && p[0].ParameterType == typeof(string))
                                loadMethod.Invoke(vm, new object[] { dataPath });
                            else if (p.Length == 1 && p[0].ParameterType == typeof(JArray))
                                loadMethod.Invoke(vm, new object[] { arr });
                        }

                        // look for a collection property that likely holds the items (Items, Entries, BudgetEntries)
                        var prop = vmType.GetProperties().FirstOrDefault(p2 => new[] { "Items", "Entries", "BudgetEntries" }.Contains(p2.Name));
                        if (prop != null)
                        {
                            var val = prop.GetValue(vm) as System.Collections.IEnumerable;
                            Xunit.Assert.Equal(arr.Count, count);
                            if (val != null)
                            {
                                foreach (var _ in val) count++;
                            }
                            // assert counts match (best-effort)
                            Assert.Equal(arr.Count, count);
                        }
                    }
                }
            }

            tc.Passed = true;
            tc.Output = $"Parsed {arr.Count} budget entries from {dataPath}";
        }
        catch (Exception ex)
        {
            tc.Passed = false;
            tc.FailureMessage = ex.ToString();
        }
        finally
        {
            sw.Stop();
            tc.TimeSeconds = sw.Elapsed.TotalSeconds;
            Results.Add(tc);
        }
    }

    public void WriteJUnitXml(string outPath)
    {
        var suite = new XElement("testsuite",
            new XAttribute("name", "PrismE2E"),
            new XAttribute("tests", Results.Count),
            new XAttribute("failures", Results.Count(r => !r.Passed)),
            new XAttribute("time", Results.Sum(r => r.TimeSeconds).ToString("0.000"))
        );

        foreach (var r in Results)
        {
            var tc = new XElement("testcase",
                new XAttribute("classname", "PrismE2E"),
                new XAttribute("name", r.Name),
                new XAttribute("time", r.TimeSeconds.ToString("0.000"))
            );
            if (!r.Passed)
            {
                var fail = new XElement("failure", new XAttribute("message", r.FailureMessage ?? "failure"));
                tc.Add(fail);
            }
            if (!string.IsNullOrWhiteSpace(r.Output)) tc.Add(new XElement("system-out", new XCData(r.Output)));
            suite.Add(tc);
        }

        var doc = new XDocument(new XElement("testsuites", suite));
        Directory.CreateDirectory(Path.GetDirectoryName(outPath) ?? ".");
        doc.Save(outPath);
    }
}

// ---------- Main ----------
var harness = new Harness();
var argv = Environment.GetCommandLineArgs().Skip(1).ToArray();
harness.ParseArgs(argv);

Console.WriteLine($"Prism E2E harness starting. Filter={harness.FilterTag ?? "<none>"}");

// Run external module scripts as separate processes
harness.RunModuleProcesses();

// Run in-harness BudgetModule tests (always run if filter matches)
if (string.IsNullOrEmpty(harness.FilterTag) || harness.FilterTag.IndexOf("Budget", StringComparison.OrdinalIgnoreCase) >= 0)
{
    Console.WriteLine("Running in-harness BudgetModule checks...");
    harness.RunBudgetModuleTest();
}

// JUnit XML output
var outDir = Path.Combine(Directory.GetCurrentDirectory(), "test-results");
var outFile = Path.Combine(outDir, "prism-e2e-results.xml");
harness.WriteJUnitXml(outFile);

Console.WriteLine($"Finished. Results written to {outFile}");

// Exit code: 0 if all tests passed
var failed = harness.Results.Any(r => !r.Passed);
Environment.Exit(failed ? 1 : 0);
