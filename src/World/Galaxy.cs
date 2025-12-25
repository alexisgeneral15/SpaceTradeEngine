using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using System.Text.Json.Serialization;

#nullable enable
namespace SpaceTradeEngine.World
{
    /// <summary>
    /// Galaxy structure: contains sectors with planetary systems.
    /// </summary>
    public class Galaxy
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public Dictionary<Vector2Int, Sector> Sectors { get; } = new();
        public Rectangle Bounds { get; set; }

        public void AddSector(Vector2Int coord, Sector sector)
        {
            Sectors[coord] = sector;
        }

        public Sector? GetSector(Vector2Int coord)
        {
            return Sectors.TryGetValue(coord, out var s) ? s : null;
        }

        public float GetDistance(Vector2Int from, Vector2Int to)
        {
            var delta = to - from;
            return (float)Math.Sqrt(delta.X * delta.X + delta.Y * delta.Y);
        }
    }

    /// <summary>
    /// Sector: a region of space containing stations, anomalies, jumpgates.
    /// </summary>
    public class Sector
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public Vector2Int Coordinates { get; set; }
        public Vector2 CenterPosition { get; set; }
        public int EntityId { get; set; } = -1;
        public List<Gate> Gates { get; } = new();
        public List<int> StationIds { get; } = new();
        public List<int> AnomalyIds { get; } = new();
        public List<string> Factions { get; } = new();
        public float ThreatLevel { get; set; } = 0f;
        public Dictionary<string, object> Metadata { get; } = new();

        public void AddGate(Gate gate) => Gates.Add(gate);
        public void AddStation(int stationId) => StationIds.Add(stationId);
        public void AddAnomaly(int anomalyId) => AnomalyIds.Add(anomalyId);
    }

    /// <summary>
    /// Jumpgate connecting two sectors.
    /// </summary>
    public class Gate
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public Vector2Int SourceSector { get; set; }
        public Vector2Int DestinationSector { get; set; }
        public Vector2 Position { get; set; }
        public bool IsActive { get; set; } = true;
        public float Range { get; set; } = 5000f;
        public float JumpTime { get; set; } = 3f;
        public List<string> StabilityRequiredFactions { get; } = new();

        public bool CanJump(string fromFaction)
        {
            if (!IsActive) return false;
            if (StabilityRequiredFactions.Count == 0) return true;
            return StabilityRequiredFactions.Contains(fromFaction);
        }
    }

    /// <summary>
    /// Vector2 with integer coordinates for sector/galaxy indexing.
    /// </summary>
    public struct Vector2Int : IEquatable<Vector2Int>
    {
        public int X { get; set; }
        public int Y { get; set; }

        public Vector2Int(int x, int y)
        {
            X = x;
            Y = y;
        }

        public override bool Equals(object? obj) => obj is Vector2Int v && Equals(v);
        public bool Equals(Vector2Int other) => X == other.X && Y == other.Y;
        public override int GetHashCode() => HashCode.Combine(X, Y);
        public override string ToString() => $"({X}, {Y})";

        public static bool operator ==(Vector2Int a, Vector2Int b) => a.Equals(b);
        public static bool operator !=(Vector2Int a, Vector2Int b) => !a.Equals(b);
        public static Vector2Int operator +(Vector2Int a, Vector2Int b) => new(a.X + b.X, a.Y + b.Y);
        public static Vector2Int operator -(Vector2Int a, Vector2Int b) => new(a.X - b.X, a.Y - b.Y);
    }

    /// <summary>
    /// Template for loading galaxy/sector data from JSON.
    /// </summary>
    public class GalaxyTemplate
    {
        [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
        [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
        [JsonPropertyName("width")] public int Width { get; set; } = 100;
        [JsonPropertyName("height")] public int Height { get; set; } = 100;
    }

    public class SectorTemplate
    {
        [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
        [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
        [JsonPropertyName("x")] public int X { get; set; }
        [JsonPropertyName("y")] public int Y { get; set; }
        [JsonPropertyName("center_x")] public float CenterX { get; set; }
        [JsonPropertyName("center_y")] public float CenterY { get; set; }
        [JsonPropertyName("threat_level")] public float ThreatLevel { get; set; } = 0f;
        [JsonPropertyName("factions")] public List<string> Factions { get; set; } = new();
        [JsonPropertyName("gates")] public List<GateTemplate> Gates { get; set; } = new();
    }

    public class GateTemplate
    {
        [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
        [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
        [JsonPropertyName("dest_x")] public int DestX { get; set; }
        [JsonPropertyName("dest_y")] public int DestY { get; set; }
        [JsonPropertyName("pos_x")] public float PosX { get; set; }
        [JsonPropertyName("pos_y")] public float PosY { get; set; }
        [JsonPropertyName("stability_required")] public List<string> StabilityRequired { get; set; } = new();
    }
}
