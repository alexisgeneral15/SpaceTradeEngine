using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using SpaceTradeEngine.ECS;
using SpaceTradeEngine.ECS.Components;
using SpaceTradeEngine.Systems;
using Microsoft.Xna.Framework;

namespace SpaceTradeEngine.World
{
    /// <summary>
    /// Loads galaxy/sector structures from JSON and optionally converts from UnendingGalaxy map format.
    /// </summary>
    public class GalaxyLoader
    {
        private readonly EntityManager _entityManager;
        private readonly SpatialPartitioningSystem? _spatialSystem;
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public GalaxyLoader(EntityManager entityManager, SpatialPartitioningSystem? spatialSystem = null)
        {
            _entityManager = entityManager;
            _spatialSystem = spatialSystem;
        }

        [RequiresUnreferencedCode("JSON deserialization requires preserved types when trimming.")]
        public Galaxy? LoadFromJson(string filePath)
        {
            if (!File.Exists(filePath))
                return null;

            try
            {
                var json = File.ReadAllText(filePath);
                var template = JsonSerializer.Deserialize<GalaxyTemplate>(json, _jsonOptions);
                if (template == null) return null;

                var galaxy = new Galaxy
                {
                    Id = template.Id,
                    Name = template.Name,
                    Bounds = new Rectangle(0, 0, template.Width * 100, template.Height * 100)
                };

                var sectorsDir = Path.Combine(Path.GetDirectoryName(filePath) ?? ".", "sectors");
                if (Directory.Exists(sectorsDir))
                {
                    var sectorFiles = Directory.GetFiles(sectorsDir, "*.json");
                    foreach (var sFile in sectorFiles)
                    {
                        var sJson = File.ReadAllText(sFile);
                        var sTemplate = JsonSerializer.Deserialize<SectorTemplate>(sJson, _jsonOptions);
                        if (sTemplate != null)
                        {
                            var sector = CreateSectorFromTemplate(sTemplate, galaxy);
                            galaxy.AddSector(new Vector2Int(sTemplate.X, sTemplate.Y), sector);
                        }
                    }
                }

                Console.WriteLine($"✓ Loaded galaxy '{galaxy.Name}' with {galaxy.Sectors.Count} sectors");
                return galaxy;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Error loading galaxy from {filePath}: {ex.Message}");
                return null;
            }
        }

        private Sector CreateSectorFromTemplate(SectorTemplate template, Galaxy galaxy)
        {
            var sector = new Sector
            {
                Id = template.Id,
                Name = template.Name,
                Coordinates = new Vector2Int(template.X, template.Y),
                CenterPosition = new Vector2(template.CenterX, template.CenterY),
                ThreatLevel = template.ThreatLevel
            };

            sector.Factions.AddRange(template.Factions);

            foreach (var gTemplate in template.Gates)
            {
                var gate = new Gate
                {
                    Id = gTemplate.Id,
                    Name = gTemplate.Name,
                    SourceSector = new Vector2Int(template.X, template.Y),
                    DestinationSector = new Vector2Int(gTemplate.DestX, gTemplate.DestY),
                    Position = new Vector2(gTemplate.PosX, gTemplate.PosY)
                };
                // StabilityRequired is already a List<string> in GateTemplate
                gate.StabilityRequiredFactions.AddRange(gTemplate.StabilityRequired);
                sector.AddGate(gate);
            }

            // Create a sector entity in ECS for rendering/interaction
            var entity = _entityManager.CreateEntity($"Sector_{template.Id}");
            entity.AddComponent(new TransformComponent { Position = new Vector2(template.CenterX, template.CenterY) });
            entity.AddComponent(new TagComponent("sector"));
            sector.EntityId = entity.Id;

            return sector;
        }

        public Galaxy? LoadFromUGM(string ugmPath)
        {
            // Basic implementation: parse UnendingGalaxy .ugm format (likely binary or custom text)
            // For now, return null as placeholder - actual parser depends on .ugm file format
            Console.WriteLine($"Note: .ugm parsing not yet implemented ({ugmPath})");
            return null;
        }
    }
}
