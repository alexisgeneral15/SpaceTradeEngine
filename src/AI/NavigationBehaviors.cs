using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using SpaceTradeEngine.AI;
using SpaceTradeEngine.ECS;
using SpaceTradeEngine.ECS.Components;
using SpaceTradeEngine.Economy;
using SpaceTradeEngine.Systems;
using SpaceTradeEngine.World;

#nullable enable
namespace SpaceTradeEngine.AI.Behaviors
{
    /// <summary>
    /// Navigation and trading behaviors for sector-level movement and commerce.
    /// </summary>
    public static class NavigationBehaviors
    {
        /// <summary>
        /// Move entity towards a target sector via jumpgates.
        /// </summary>
        public static BehaviorNode MoveToSector(
            Galaxy galaxy,
            Vector2Int targetSectorCoord,
            float shipSpeed = 300f,
            float arrivalDistance = 100f)
        {
            return new ActionNode(context =>
            {
                var transform = context.Entity.GetComponent<TransformComponent>();
                if (transform == null) return NodeStatus.Failure;

                var currentSector = FindSectorAtPosition(galaxy, transform.Position);
                if (currentSector == null) return NodeStatus.Running;

                // If already at target sector, success
                if (currentSector.Coordinates == targetSectorCoord)
                {
                    return NodeStatus.Success;
                }

                // Find best gate to take
                var path = FindPath(galaxy, currentSector.Coordinates, targetSectorCoord);
                if (path.Count == 0) return NodeStatus.Failure;

                var nextGate = path[0];
                Vector2 toGate = nextGate.Position - transform.Position;
                if (toGate.Length() < arrivalDistance)
                {
                    // Jump gate transition handled by AI state change
                    context.SetValue("TargetGate", nextGate.Id);
                    return NodeStatus.Success;
                }

                toGate.Normalize();
                var velocity = context.Entity.GetComponent<VelocityComponent>();
                if (velocity != null)
                {
                    velocity.LinearVelocity = toGate * shipSpeed;
                }

                return NodeStatus.Running;
            }, "MoveToSector");
        }

        /// <summary>
        /// Plan a trading route based on price deltas and supply/demand.
        /// </summary>
        public static BehaviorNode PlanTradeRoute(
            MarketManager markets,
            Galaxy galaxy,
            List<int> stationIds,
            float minProfitMargin = 0.1f)
        {
            return new ActionNode(context =>
            {
                var cargo = context.Entity.GetComponent<CargoComponent>();
                if (cargo == null) return NodeStatus.Failure;

                var tradeRoute = new List<(int StationId, string WareId, int Quantity, float ExpectedProfit)>();

                // Identify profitable trades from stationIds
                for (int i = 0; i < stationIds.Count; i++)
                {
                    int buyStationId = stationIds[i];
                    var buyMarket = markets.GetMarket(buyStationId);
                    if (buyMarket == null) continue;

                    foreach (var (wareId, buyPrice) in buyMarket.Goods
                        .Select(g => (g.Key, g.Value.CurrentPrice))
                        .Where(x => x.CurrentPrice > 0))
                    {
                        // Find best sell station
                        for (int j = i + 1; j < stationIds.Count; j++)
                        {
                            int sellStationId = stationIds[j];
                            var sellPrice = markets.GetPrice(sellStationId, wareId);
                            if (sellPrice == null) continue;

                            float margin = (sellPrice.Value - buyPrice) / buyPrice;
                            if (margin > minProfitMargin)
                            {
                                int quantity = Math.Min(10, markets.GetMarket(buyStationId)?.GetAvailable(wareId) ?? 0);
                                float profit = quantity * (sellPrice.Value - buyPrice);
                                tradeRoute.Add((buyStationId, wareId, quantity, profit));
                            }
                        }
                    }
                }

                if (tradeRoute.Count > 0)
                {
                    context.SetValue("TradeRoute", tradeRoute);
                    context.SetValue("RouteIndex", 0);
                    return NodeStatus.Success;
                }

                return NodeStatus.Failure;
            }, "PlanTradeRoute");
        }

        /// <summary>
        /// Execute planned trade at current station.
        /// </summary>
        public static BehaviorNode ExecuteTrade(MarketManager markets, EconomySystem economySystem)
        {
            return new ActionNode(context =>
            {
                var route = context.GetValue<List<(int, string, int, float)>>("TradeRoute");
                if (route == null || route.Count == 0)
                    return NodeStatus.Failure;

                int routeIdx = context.GetValue<int>("RouteIndex");
                if (routeIdx >= route.Count)
                    return NodeStatus.Success;

                var (stationId, wareId, quantity, profit) = route[routeIdx];
                bool traded = economySystem.Trade(context.Entity, stationId, wareId, quantity, false); // buying
                if (traded)
                {
                    context.SetValue("RouteIndex", routeIdx + 1);
                }

                return traded ? NodeStatus.Running : NodeStatus.Failure;
            }, "ExecuteTrade");
        }

        /// <summary>
        /// Avoid obstacles by steering away from nearby entities.
        /// </summary>
        public static BehaviorNode AvoidObstacles(
            SpatialPartitioningSystem spatialSystem,
            float detectionRange = 200f,
            float avoidanceForce = 300f)
        {
            return new ActionNode(context =>
            {
                var transform = context.Entity.GetComponent<TransformComponent>();
                var velocity = context.Entity.GetComponent<VelocityComponent>();
                if (transform == null || velocity == null) return NodeStatus.Failure;

                var nearby = spatialSystem.QueryRadius(transform.Position, detectionRange);
                Vector2 avoidanceVector = Vector2.Zero;
                int obstacleCount = 0;

                foreach (var entity in nearby)
                {
                    if (entity.Id == context.Entity.Id) continue;
                    if (!entity.HasComponent<TransformComponent>()) continue;

                    var obstacleTransform = entity.GetComponent<TransformComponent>();
                    Vector2 toObstacle = obstacleTransform.Position - transform.Position;
                    float distance = toObstacle.Length();

                    if (distance > 0.001f)
                    {
                        avoidanceVector -= toObstacle / (distance * distance);
                        obstacleCount++;
                    }
                }

                if (obstacleCount > 0)
                {
                    avoidanceVector.Normalize();
                    velocity.LinearVelocity += avoidanceVector * avoidanceForce * 0.016f; // assuming ~60 FPS
                    return NodeStatus.Running;
                }

                return NodeStatus.Success;
            }, "AvoidObstacles");
        }

        #region Helper Methods

        private static Sector? FindSectorAtPosition(Galaxy galaxy, Vector2 position)
        {
            foreach (var sector in galaxy.Sectors.Values)
            {
                float dist = Vector2.Distance(position, sector.CenterPosition);
                if (dist < 100f) return sector;
            }
            return null;
        }

        private static List<Gate> FindPath(Galaxy galaxy, Vector2Int start, Vector2Int goal)
        {
            // Simple BFS for nearest gate path
            var queue = new Queue<(Vector2Int, List<Gate>)>();
            var visited = new HashSet<Vector2Int>();

            queue.Enqueue((start, new List<Gate>()));
            visited.Add(start);

            while (queue.Count > 0)
            {
                var (current, path) = queue.Dequeue();

                if (current == goal)
                    return path;

                var sector = galaxy.GetSector(current);
                if (sector == null) continue;

                foreach (var gate in sector.Gates)
                {
                    if (!visited.Contains(gate.DestinationSector))
                    {
                        visited.Add(gate.DestinationSector);
                        var newPath = new List<Gate>(path) { gate };
                        queue.Enqueue((gate.DestinationSector, newPath));
                    }
                }
            }

            return new List<Gate>();
        }

        #endregion
    }
}
