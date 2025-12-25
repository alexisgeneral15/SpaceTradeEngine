using System;
using System.Collections.Generic;
using System.Linq;
using SpaceTradeEngine.World;

#nullable enable
namespace SpaceTradeEngine.Systems
{
    /// <summary>
    /// Pathfinding system for navigating sector graphs using A*.
    /// </summary>
    public class PathfindingSystem
    {
        private class PathNode
        {
            public Vector2Int Position { get; set; }
            public float GCost { get; set; } // Distance from start
            public float HCost { get; set; } // Heuristic to goal
            public float FCost => GCost + HCost;
            public PathNode? Parent { get; set; }
        }

        /// <summary>
        /// Find shortest path through sector graph from start to goal.
        /// Returns list of sector coordinates to traverse, or null if no path.
        /// </summary>
        public List<Vector2Int>? FindPath(Galaxy galaxy, Vector2Int start, Vector2Int goal)
        {
            if (!galaxy.Sectors.ContainsKey(start) || !galaxy.Sectors.ContainsKey(goal))
                return null;

            if (start == goal)
                return new List<Vector2Int> { start };

            var openSet = new List<PathNode>();
            var closedSet = new HashSet<Vector2Int>();
            
            var startNode = new PathNode
            {
                Position = start,
                GCost = 0f,
                HCost = Heuristic(start, goal)
            };
            
            openSet.Add(startNode);

            while (openSet.Count > 0)
            {
                // Get node with lowest F cost
                var current = openSet.OrderBy(n => n.FCost).ThenBy(n => n.HCost).First();
                
                if (current.Position == goal)
                {
                    return ReconstructPath(current);
                }

                openSet.Remove(current);
                closedSet.Add(current.Position);

                // Check neighbors (connected via gates)
                var neighbors = GetNeighbors(galaxy, current.Position);
                foreach (var neighborPos in neighbors)
                {
                    if (closedSet.Contains(neighborPos))
                        continue;

                    float tentativeG = current.GCost + 1f; // Each jump = 1 cost
                    
                    var existingNode = openSet.FirstOrDefault(n => n.Position == neighborPos);
                    if (existingNode != null)
                    {
                        if (tentativeG < existingNode.GCost)
                        {
                            existingNode.GCost = tentativeG;
                            existingNode.Parent = current;
                        }
                    }
                    else
                    {
                        var neighborNode = new PathNode
                        {
                            Position = neighborPos,
                            GCost = tentativeG,
                            HCost = Heuristic(neighborPos, goal),
                            Parent = current
                        };
                        openSet.Add(neighborNode);
                    }
                }
            }

            return null; // No path found
        }

        private List<Vector2Int> GetNeighbors(Galaxy galaxy, Vector2Int position)
        {
            var neighbors = new List<Vector2Int>();
            
            if (!galaxy.Sectors.TryGetValue(position, out var sector))
                return neighbors;

            foreach (var gate in sector.Gates)
            {
                if (galaxy.Sectors.ContainsKey(gate.DestinationSector))
                {
                    neighbors.Add(gate.DestinationSector);
                }
            }

            return neighbors;
        }

        private float Heuristic(Vector2Int a, Vector2Int b)
        {
            // Manhattan distance for grid-based sectors
            return Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);
        }

        private List<Vector2Int> ReconstructPath(PathNode endNode)
        {
            var path = new List<Vector2Int>();
            var current = endNode;
            
            while (current != null)
            {
                path.Add(current.Position);
                current = current.Parent;
            }
            
            path.Reverse();
            return path;
        }

        /// <summary>
        /// Calculate travel distance (number of jumps) for a path.
        /// </summary>
        public int GetPathDistance(List<Vector2Int> path)
        {
            return path.Count - 1; // Number of jumps = nodes - 1
        }
    }
}
