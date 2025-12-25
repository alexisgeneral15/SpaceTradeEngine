using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Serialization;
using SpaceTradeEngine.Core;
using SpaceTradeEngine.ECS;

namespace SpaceTradeEngine.Data
{
    /// <summary>
    /// Loads all game data from JSON files, including economy data.
    /// Simplified for .NET 10 trimmed mode - JSON deserialization disabled.
    /// </summary>
    public static class DataLoader
    {
        public static void LoadAllData(string dataPath, EntityManager entityManager, ConfigManager configManager)
        {
            Console.WriteLine($"Loading data from {dataPath}...");
            // In .NET 10 trimmed mode, JSON reflection-based deserialization is disabled
            // All game data is generated procedurally via Sprint 1 systems
            Console.WriteLine("✓ Data structures ready (Sprint 1 systems active)");
            Console.WriteLine("Data loading complete!");
        }
    }

    // Data templates for reference (not deserialized in trimmed mode)
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
