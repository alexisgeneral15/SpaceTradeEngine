using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using SpaceTradeEngine.Core;
using SpaceTradeEngine.ECS;

namespace SpaceTradeEngine.Data
{
    /// <summary>
    /// Loads all game data from JSON files, including economy data.
    /// </summary>
    public static class DataLoader
    {
        [RequiresUnreferencedCode("Runtime JSON deserialization needs preserved types when trimming.")]
        public static void LoadAllData(string dataPath, EntityManager entityManager, ConfigManager configManager)
        {
            Console.WriteLine($"Loading data from {dataPath}...");

            var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            
            // Load config
            var configFile = Path.Combine(dataPath, "config.json");
            if (File.Exists(configFile))
            {
                var configJson = File.ReadAllText(configFile);
                var configData = JsonSerializer.Deserialize<Dictionary<string, object>>(configJson, jsonOptions);
                Console.WriteLine("✓ Loaded config.json");
            }

            // Load wares
            // COMENTADO: carga innecesaria que consume ~10-15MB
            /*
            var waresDir = Path.Combine(dataPath, "wares");
            if (Directory.Exists(waresDir))
            {
                var wareFiles = Directory.GetFiles(waresDir, "*.json");
                foreach (var file in wareFiles)
                {
                    try
                    {
                        var waresJson = File.ReadAllText(file);
                        var wares = JsonConvert.DeserializeObject<List<Economy.WareTemplate>>(waresJson);
                        Console.WriteLine($"✓ Loaded {wares?.Count ?? 0} wares from {Path.GetFileName(file)}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"✗ Error loading wares from {Path.GetFileName(file)}: {ex.Message}");
                    }
                }
            }
            */
            Console.WriteLine("- Skipped wares loading (commented for optimization)");

            // Load stations
            // COMENTADO: carga innecesaria que consume ~5-10MB
            /*
            var stationsDir = Path.Combine(dataPath, "stations");
            if (Directory.Exists(stationsDir))
            {
                var stationFiles = Directory.GetFiles(stationsDir, "*.json");
                foreach (var file in stationFiles)
                {
                    try
                    {
                        var stationsJson = File.ReadAllText(file);
                        var stations = JsonConvert.DeserializeObject<List<Economy.StationTemplate>>(stationsJson);
                        Console.WriteLine($"✓ Loaded {stations?.Count ?? 0} stations from {Path.GetFileName(file)}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"✗ Error loading stations from {Path.GetFileName(file)}: {ex.Message}");
                    }
                }
            }
            */
            Console.WriteLine("- Skipped stations loading (commented for optimization)");

            // Load factories
            // COMENTADO: carga innecesaria que consume ~5-10MB
            /*
            var factoriesDir = Path.Combine(dataPath, "factories");
            if (Directory.Exists(factoriesDir))
            {
                var factoryFiles = Directory.GetFiles(factoriesDir, "*.json");
                foreach (var file in factoryFiles)
                {
                    try
                    {
                        var factoriesJson = File.ReadAllText(file);
                        var factories = JsonConvert.DeserializeObject<List<FactoryTemplate>>(factoriesJson);
                        Console.WriteLine($"✓ Loaded {factories?.Count ?? 0} factories from {Path.GetFileName(file)}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"✗ Error loading factories from {Path.GetFileName(file)}: {ex.Message}");
                    }
                }
            }
            */
            Console.WriteLine("- Skipped factories loading (commented for optimization)");

            // Load ships
            // COMENTADO: carga innecesaria que consume ~5-10MB
            /*
            var shipsFile = Path.Combine(dataPath, "ships", "ship_templates.json");
            if (File.Exists(shipsFile))
            {
                var shipsJson = File.ReadAllText(shipsFile);
                var shipsData = JsonConvert.DeserializeObject<List<ShipTemplate>>(shipsJson);
                Console.WriteLine($"✓ Loaded {shipsData?.Count ?? 0} ship templates");
            }
            */
            Console.WriteLine("- Skipped ships loading (commented for optimization)");

            // Load factions
            // COMENTADO: carga innecesaria que consume ~2-5MB
            /*
            var factionsDir = Path.Combine(dataPath, "factions");
            if (Directory.Exists(factionsDir))
            {
                var factionFiles = Directory.GetFiles(factionsDir, "*.json");
                foreach (var file in factionFiles)
                {
                    var factionJson = File.ReadAllText(file);
                    var factionData = JsonConvert.DeserializeObject<FactionData>(factionJson);
                    Console.WriteLine($"✓ Loaded faction: {factionData?.Name}");
                }
            }
            */
            Console.WriteLine("- Skipped factions loading (commented for optimization)");

            // Load items
            // COMENTADO: carga innecesaria que consume ~2-5MB
            /*
            var itemsFile = Path.Combine(dataPath, "items", "items.json");
            if (File.Exists(itemsFile))
            {
                var itemsJson = File.ReadAllText(itemsFile);
                var itemsData = JsonConvert.DeserializeObject<List<ItemTemplate>>(itemsJson);
                Console.WriteLine($"✓ Loaded {itemsData?.Count ?? 0} item templates");
            }
            */
            Console.WriteLine("- Skipped items loading (commented for optimization)");

            Console.WriteLine("Data loading complete!");
        }
    }

    // Data templates for deserialization
    public class ShipTemplate
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("hull")]
        public HullData Hull { get; set; }

        [JsonPropertyName("engines")]
        public EngineData Engines { get; set; }

        [JsonPropertyName("cost")]
        public int Cost { get; set; }
    }

    public class HullData
    {
        [JsonPropertyName("hp")]
        public float Hp { get; set; }

        [JsonPropertyName("armor")]
        public float Armor { get; set; }
    }

    public class EngineData
    {
        [JsonPropertyName("max_speed")]
        public float MaxSpeed { get; set; }

        [JsonPropertyName("acceleration")]
        public float Acceleration { get; set; }
    }

    public class FactionData
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("alignment")]
        public string Alignment { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }
    }

    public class ItemTemplate
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("value")]
        public int Value { get; set; }
    }

    public class FactoryTemplate
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("production")]
        public string Production { get; set; }

        [JsonPropertyName("input_wares")]
        public List<string> InputWares { get; set; } = new();

        [JsonPropertyName("output_quantity")]
        public int OutputQuantity { get; set; } = 1;
    }
}
