using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using NJsonSchema;

namespace DataValidator
{
    internal class Program
    {
        static int Main(string[] args)
        {
            var root = args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "assets", "data");
            root = Path.GetFullPath(root);
            var schemasDir = Path.Combine(AppContext.BaseDirectory, "schemas");

            var schemaMap = new Dictionary<string, string>
            {
                { "wares", Path.Combine(schemasDir, "wares.schema.json") },
                { "stations", Path.Combine(schemasDir, "stations.schema.json") },
                { "factions", Path.Combine(schemasDir, "factions.schema.json") },
                { "events", Path.Combine(schemasDir, "events.schema.json") },
            };

            Console.WriteLine($"SpaceTradeEngine Data Validator\nRoot: {root}\nSchemas: {schemasDir}\n");

            int filesChecked = 0;
            int filesFailed = 0;

            foreach (var kvp in schemaMap)
            {
                var folder = kvp.Key;
                var schemaPath = kvp.Value;

                if (!File.Exists(schemaPath))
                {
                    Console.WriteLine($"[WARN] Missing schema: {schemaPath}");
                    continue;
                }

                var dataDir = Path.Combine(root, folder);
                if (!Directory.Exists(dataDir))
                {
                    Console.WriteLine($"[SKIP] No data directory: {dataDir}");
                    continue;
                }

                Console.WriteLine($"\nValidating {folder} against {Path.GetFileName(schemaPath)}...");

                var schema = JsonSchema.FromJsonAsync(File.ReadAllText(schemaPath)).GetAwaiter().GetResult();
                foreach (var file in Directory.GetFiles(dataDir, "*.json", SearchOption.TopDirectoryOnly))
                {
                    filesChecked++;
                    try
                    {
                        var jsonText = File.ReadAllText(file);
                        var token = JToken.Parse(jsonText);
                        var errors = schema.Validate(token);
                        if (errors != null && errors.Count > 0)
                        {
                            filesFailed++;
                            Console.WriteLine($"✗ {Path.GetFileName(file)} ({errors.Count} errors)");
                            foreach (var err in errors.Take(10))
                            {
                                Console.WriteLine($"  - {err.Path}: {err.Kind} ({err.ToString()})");
                            }
                            if (errors.Count > 10) Console.WriteLine("  ... (more) ...");
                        }
                        else
                        {
                            Console.WriteLine($"✓ {Path.GetFileName(file)}");
                        }
                    }
                    catch (Exception ex)
                    {
                        filesFailed++;
                        Console.WriteLine($"✗ {Path.GetFileName(file)} (exception)\n  - {ex.Message}");
                    }
                }
            }

            Console.WriteLine($"\nSummary: checked {filesChecked} file(s), failures {filesFailed}.");
            return filesFailed > 0 ? 1 : 0;
        }
    }
}
