using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;

async Task<int> MainAsync(string[] args)
{
    try
    {
        string? code = null;
        // Support two modes:
        // 1) simple eval: -e "code" or -f path-to-file or stdin code
        // 2) JSON RPC: -j or when stdin contains JSON object
        bool jsonMode = args.Length > 0 && args[0] == "-j";
        if (!jsonMode)
        {
            // inspect stdin to detect JSON
            if (!Console.IsInputRedirected)
            {
                // no redirected stdin
            }
            else
            {
                using var rdrPeek = new StreamReader(Console.OpenStandardInput());
                var peek = await rdrPeek.ReadToEndAsync().ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(peek) && peek.TrimStart().StartsWith("{"))
                {
                    jsonMode = true;
                    // We'll parse below using peek content
                    // Reset stdin by writing peek to a temp stream the parser will read
                    code = peek;
                }
            }
        }
        if (args.Length >= 1 && args[0] == "--debug-combo")
        {
            try
            {
                // Run a small built-in experiment: create a PDF with a combo box and print loaded info
                var doc = new Syncfusion.Pdf.PdfDocument();
                var page = doc.Pages.Add();
                var combo = new Syncfusion.Pdf.Interactive.PdfComboBoxField(page, "PickCombo");
                combo.Bounds = new System.Drawing.RectangleF(10, 70, 120, 20);
                combo.Items.Add(new Syncfusion.Pdf.Interactive.PdfListFieldItem("One", "One"));
                combo.Items.Add(new Syncfusion.Pdf.Interactive.PdfListFieldItem("Two", "Two"));
                doc.Form.Fields.Add(combo);

                using var ms2 = new MemoryStream();
                doc.Save(ms2);
                var bytes2 = ms2.ToArray();

                Console.WriteLine("Saved bytes: " + bytes2.Length);
                using var loaded2 = new Syncfusion.Pdf.Parsing.PdfLoadedDocument(new MemoryStream(bytes2));
                foreach (var f in loaded2.Form.Fields)
                {
                    if (f is Syncfusion.Pdf.Parsing.PdfLoadedComboBoxField cb)
                    {
                        Console.WriteLine($"Loaded combo items: {cb.Items.Count}, selectedIndex={cb.SelectedIndex}");
                        for (int i = 0; i < cb.Items.Count; i++)
                        {
                            var item = cb.Items[i];
                            Console.WriteLine($" item[{i}] type={item?.GetType().FullName} str={item?.ToString()}");
                        }
                    }
                }

                return 0;
            }
            catch (Exception ex)
            {
                // Print an informational JSON error so callers can parse it easily and
                // continue using the evaluator for non-Syncfusion usages.
                Console.WriteLine(JsonSerializer.Serialize(new { error = ex.GetType().Name, message = ex.Message }));
                return 1;
            }
        }
        // server mode: run as a long-lived process that accepts JSON per-line on stdin
        if (args.Length >= 1 && args[0] == "--server")
        {
            Console.WriteLine(JsonSerializer.Serialize(new { status = "server-started" }));
            string? line;
            while ((line = Console.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) { Console.WriteLine(JsonSerializer.Serialize(new { error = "empty" })); continue; }
                try
                {
                    var req = JsonSerializer.Deserialize<CommandRequest>(line, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (req == null) { Console.WriteLine(JsonSerializer.Serialize(new { error = "invalid json" })); continue; }

                    if (!string.IsNullOrWhiteSpace(req.File) && File.Exists(req.File))
                    {
                        req.Code = await File.ReadAllTextAsync(req.File).ConfigureAwait(false);
                    }

                    var importsList = req.Imports ?? Array.Empty<string>();
                    var refsList = req.References ?? Array.Empty<string>();

                    var serverOptions = ScriptOptions.Default
                        .WithImports(importsList)
                        .WithReferences(AppDomain.CurrentDomain.GetAssemblies());

                    foreach (var r in refsList)
                    {
                        try
                        {
                            if (File.Exists(r))
                            {
                                var asmPath = Path.GetFullPath(r);
                                var asm = System.Reflection.Assembly.LoadFrom(asmPath);
                                if (asm != null) serverOptions = serverOptions.WithReferences(asm);
                            }
                            else
                            {
                                var asm = System.Reflection.Assembly.Load(new System.Reflection.AssemblyName(r));
                                if (asm != null) serverOptions = serverOptions.WithReferences(asm);
                            }
                        }
                        catch { }
                    }

                    var codeToEval = req.Code ?? string.Empty;
                    var evalRes = await CSharpScript.EvaluateAsync<object>(codeToEval, serverOptions).ConfigureAwait(false);
                    Console.WriteLine(JsonSerializer.Serialize(new { result = evalRes?.ToString(), type = evalRes?.GetType().FullName }));
                }
                catch (Exception ex)
                {
                    Console.WriteLine(JsonSerializer.Serialize(new { error = ex.GetType().Name, message = ex.Message }));
                }
            }
            return 0;
        }

        if (jsonMode)
        {
            // parse JSON request from stdin or args
            string json;
            if (code != null && code.TrimStart().StartsWith("{"))
            {
                json = code; // from early stdin peek
            }
            else if (args.Length >= 2)
            {
                // args after -j may contain compact JSON
                json = args[1];
            }
            else
            {
                using var srJson = new StreamReader(Console.OpenStandardInput());
                json = await srJson.ReadToEndAsync().ConfigureAwait(false);
            }

            if (string.IsNullOrWhiteSpace(json))
            {
                Console.WriteLine(JsonSerializer.Serialize(new { error = "no json payload" }));
                return 1;
            }

            var req = JsonSerializer.Deserialize<CommandRequest>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (req == null) { Console.WriteLine(JsonSerializer.Serialize(new { error = "invalid json" })); return 1; }

            if (!string.IsNullOrWhiteSpace(req.File) && File.Exists(req.File))
            {
                req.Code = await File.ReadAllTextAsync(req.File).ConfigureAwait(false);
            }

            code = req.Code ?? string.Empty;

            // build options from request
            var importsList = req.Imports ?? Array.Empty<string>();
            var refsList = req.References ?? Array.Empty<string>();

            var jsonOptions = ScriptOptions.Default
                .WithImports(importsList)
                .WithReferences(AppDomain.CurrentDomain.GetAssemblies());

            // attempt to load additional assemblies by name or path
            foreach (var r in refsList)
            {
                try
                {
                    try
                    {
                        if (File.Exists(r))
                        {
                            var asmPath = Path.GetFullPath(r);
                            var asm = System.Reflection.Assembly.LoadFrom(asmPath);
                            if (asm != null) jsonOptions = jsonOptions.WithReferences(asm);
                        }
                        else
                        {
                            var asm = System.Reflection.Assembly.Load(new System.Reflection.AssemblyName(r));
                            if (asm != null) jsonOptions = jsonOptions.WithReferences(asm);
                        }
                    }
                    catch { /* ignore load failures */ }
                }
                catch { /* best-effort */ }
            }

            var evalResult = await CSharpScript.EvaluateAsync<object>(code ?? string.Empty, jsonOptions).ConfigureAwait(false);
            Console.WriteLine(JsonSerializer.Serialize(new { result = evalResult?.ToString(), type = evalResult?.GetType().FullName }));
            return 0;
        }

        if (args.Length >= 2 && args[0] == "-f")
        {
            code = await File.ReadAllTextAsync(args[1]).ConfigureAwait(false);
        }
        else if (args.Length >= 1 && args[0] == "-e")
        {
            code = args[1];
        }
        else
        {
            // read stdin fully
            using var sr = new StreamReader(Console.OpenStandardInput());
            code = await sr.ReadToEndAsync().ConfigureAwait(false);
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            Console.WriteLine("{\"error\": \"no code provided\"}");
            return 1;
        }

        var options = ScriptOptions.Default
            .WithImports("System", "System.IO", "System.Linq", "System.Collections.Generic")
            .WithReferences(AppDomain.CurrentDomain.GetAssemblies());

        var result = await CSharpScript.EvaluateAsync<object>(code, options).ConfigureAwait(false);

        var output = JsonSerializer.Serialize(new { result = result?.ToString(), type = result?.GetType().FullName });
        Console.WriteLine(output);
        return 0;
    }
    catch (CompilationErrorException compEx)
    {
        var output = JsonSerializer.Serialize(new { error = "compilation", diagnostics = compEx.Diagnostics.Select(d => d.ToString()).ToArray() });
        Console.WriteLine(output);
        return 2;
    }
    catch (Exception ex)
    {
        var output = JsonSerializer.Serialize(new { error = ex.GetType().Name, message = ex.Message });
        Console.WriteLine(output);
        return 3;
    }
}

return await MainAsync(args);

public class CommandRequest
{
    public string? Command { get; set; }
    public string? Code { get; set; }
    public string? File { get; set; }
    public string[]? References { get; set; }
    public string[]? Imports { get; set; }
}
