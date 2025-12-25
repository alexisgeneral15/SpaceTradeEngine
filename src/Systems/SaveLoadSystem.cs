using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Microsoft.Xna.Framework;
using System.Text.Json;
using System.Text.Json.Serialization;
using SpaceTradeEngine.ECS;
using SpaceTradeEngine.ECS.Components;

#nullable enable

namespace SpaceTradeEngine.Systems
{
    /// <summary>
    /// Basic save/load system that serializes core entity/component state to JSON.
    /// </summary>
    public class SaveLoadSystem
    {
        private readonly EntityManager _entityManager;
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public SaveLoadSystem(EntityManager entityManager)
        {
            _entityManager = entityManager;
        }

        [RequiresUnreferencedCode("JSON serialization requires preserved types when trimming.")]
        public void Save(string filePath)
        {
            var save = new SaveGame
            {
                Entities = new List<SaveEntity>()
            };

            foreach (var e in _entityManager.GetAllEntities())
            {
                var se = new SaveEntity
                {
                    Id = e.Id,
                    Name = e.Name,
                    IsActive = e.IsActive
                };

                var t = e.GetComponent<TransformComponent>();
                if (t != null)
                    se.Transform = new SaveTransform { X = t.Position.X, Y = t.Position.Y, Rotation = t.Rotation, ScaleX = t.Scale.X, ScaleY = t.Scale.Y };

                var v = e.GetComponent<VelocityComponent>();
                if (v != null)
                    se.Velocity = new SaveVelocity { VX = v.LinearVelocity.X, VY = v.LinearVelocity.Y, AX = v.Acceleration.X, AY = v.Acceleration.Y, Angular = v.AngularVelocity };

                var c = e.GetComponent<CollisionComponent>();
                if (c != null)
                    se.Collision = new SaveCollision { Radius = c.Radius, IsTrigger = c.IsTrigger };

                var h = e.GetComponent<HealthComponent>();
                if (h != null)
                    se.Health = new SaveHealth { Max = h.MaxHealth, Current = h.CurrentHealth };

                var f = e.GetComponent<FactionComponent>();
                if (f != null)
                    se.Faction = new SaveFaction { Id = f.FactionId, Name = f.FactionName };

                var tags = e.GetComponent<TagComponent>();
                if (tags != null)
                    se.Tags = new List<string>(tags.Tags);

                var sel = e.GetComponent<SelectionComponent>();
                if (sel != null)
                    se.Selection = new SaveSelection
                    {
                        IsSelectable = sel.IsSelectable,
                        IsSelected = sel.IsSelected,
                        Radius = sel.SelectionRadius,
                        Color = new int[] { sel.SelectionColor.R, sel.SelectionColor.G, sel.SelectionColor.B, sel.SelectionColor.A }
                    };

                save.Entities.Add(se);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(filePath) ?? ".");
            var json = JsonSerializer.Serialize(save, _jsonOptions);
            File.WriteAllText(filePath, json);
        }

        [RequiresUnreferencedCode("JSON deserialization requires preserved types when trimming.")]
        public void Load(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("Save file not found", filePath);

            var json = File.ReadAllText(filePath);
            var save = JsonSerializer.Deserialize<SaveGame>(json, _jsonOptions);
            if (save == null)
                throw new InvalidOperationException("Failed to deserialize save file");

            // Destroy existing entities (simple approach for now)
            foreach (var e in _entityManager.GetAllEntities())
            {
                _entityManager.DestroyEntity(e.Id);
            }

            // Recreate entities
            foreach (var se in save.Entities)
            {
                var e = _entityManager.CreateEntity(se.Name);
                e.IsActive = se.IsActive;

                if (se.Transform != null)
                {
                    e.AddComponent(new TransformComponent
                    {
                        Position = new Vector2(se.Transform.X, se.Transform.Y),
                        Rotation = se.Transform.Rotation,
                        Scale = new Vector2(se.Transform.ScaleX, se.Transform.ScaleY)
                    });
                }
                if (se.Velocity != null)
                {
                    e.AddComponent(new VelocityComponent
                    {
                        LinearVelocity = new Vector2(se.Velocity.VX, se.Velocity.VY),
                        Acceleration = new Vector2(se.Velocity.AX, se.Velocity.AY),
                        AngularVelocity = se.Velocity.Angular
                    });
                }
                if (se.Collision != null)
                {
                    e.AddComponent(new CollisionComponent
                    {
                        Radius = se.Collision.Radius,
                        IsTrigger = se.Collision.IsTrigger
                    });
                }
                if (se.Health != null)
                {
                    var hc = new HealthComponent { MaxHealth = se.Health.Max };
                    e.AddComponent(hc);
                    // After Initialize(), set current (clamped inside component methods)
                    hc.CurrentHealth = Math.Min(se.Health.Current, hc.MaxHealth);
                }
                if (se.Faction != null)
                {
                    e.AddComponent(new FactionComponent(se.Faction.Id, se.Faction.Name));
                }
                if (se.Tags != null && se.Tags.Count > 0)
                {
                    e.AddComponent(new TagComponent(se.Tags.ToArray()));
                }
                if (se.Selection != null)
                {
                    var color = se.Selection.Color != null && se.Selection.Color.Length == 4
                        ? new Color(se.Selection.Color[0], se.Selection.Color[1], se.Selection.Color[2], se.Selection.Color[3])
                        : Color.Yellow;
                    e.AddComponent(new SelectionComponent
                    {
                        IsSelectable = se.Selection.IsSelectable,
                        IsSelected = se.Selection.IsSelected,
                        SelectionRadius = se.Selection.Radius,
                        SelectionColor = color
                    });
                }
            }
        }

        // DTOs for serialization
        private class SaveGame
        {
            [JsonPropertyName("entities")] public List<SaveEntity> Entities { get; set; } = new();
        }

        private class SaveEntity
        {
            [JsonPropertyName("id")] public int Id { get; set; }
            [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
            [JsonPropertyName("active")] public bool IsActive { get; set; }
            [JsonPropertyName("transform")] public SaveTransform? Transform { get; set; }
            [JsonPropertyName("velocity")] public SaveVelocity? Velocity { get; set; }
            [JsonPropertyName("collision")] public SaveCollision? Collision { get; set; }
            [JsonPropertyName("health")] public SaveHealth? Health { get; set; }
            [JsonPropertyName("faction")] public SaveFaction? Faction { get; set; }
            [JsonPropertyName("tags")] public List<string>? Tags { get; set; }
            [JsonPropertyName("selection")] public SaveSelection? Selection { get; set; }
        }

        private class SaveTransform
        {
            [JsonPropertyName("x")] public float X { get; set; }
            [JsonPropertyName("y")] public float Y { get; set; }
            [JsonPropertyName("rotation")] public float Rotation { get; set; }
            [JsonPropertyName("scale_x")] public float ScaleX { get; set; } = 1f;
            [JsonPropertyName("scale_y")] public float ScaleY { get; set; } = 1f;
        }

        private class SaveVelocity
        {
            [JsonPropertyName("vx")] public float VX { get; set; }
            [JsonPropertyName("vy")] public float VY { get; set; }
            [JsonPropertyName("ax")] public float AX { get; set; }
            [JsonPropertyName("ay")] public float AY { get; set; }
            [JsonPropertyName("angular")] public float Angular { get; set; }
        }

        private class SaveCollision
        {
            [JsonPropertyName("radius")] public float Radius { get; set; }
            [JsonPropertyName("is_trigger")] public bool IsTrigger { get; set; }
        }

        private class SaveHealth
        {
            [JsonPropertyName("max")] public float Max { get; set; }
            [JsonPropertyName("current")] public float Current { get; set; }
        }

        private class SaveFaction
        {
            [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
            [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
        }

        private class SaveSelection
        {
            [JsonPropertyName("is_selectable")] public bool IsSelectable { get; set; }
            [JsonPropertyName("is_selected")] public bool IsSelected { get; set; }
            [JsonPropertyName("radius")] public float Radius { get; set; }
            [JsonPropertyName("color")] public int[]? Color { get; set; }
        }
    }
}
