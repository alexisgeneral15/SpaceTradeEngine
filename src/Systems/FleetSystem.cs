using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using SpaceTradeEngine.ECS;
using SpaceTradeEngine.ECS.Components;

#nullable enable
namespace SpaceTradeEngine.Systems
{
    /// <summary>
    /// Manages fleets and squadrons of ships with formation patterns and coordinated commands.
    /// </summary>
    public class FleetSystem : ECS.System
    {
        private readonly EntityManager _entityManager;

        public FleetSystem(EntityManager entityManager)
        {
            _entityManager = entityManager;
        }

        protected override bool ShouldProcess(Entity entity)
        {
            return entity.HasComponent<FleetComponent>() || entity.HasComponent<SquadronMemberComponent>();
        }

        public override void Update(float deltaTime)
        {
            // Update formations for each fleet
            foreach (var entity in _entities)
            {
                var fleet = entity.GetComponent<FleetComponent>();
                if (fleet == null || !entity.IsActive) continue;

                UpdateFleetFormation(fleet);
                UpdateFleetMovement(fleet, deltaTime);
            }
        }

        private void UpdateFleetFormation(FleetComponent fleet)
        {
            if (fleet.MemberIds.Count == 0) return;

            var formationPositions = CalculateFormation(fleet);
            
            for (int i = 0; i < fleet.MemberIds.Count; i++)
            {
                var member = _entityManager.GetEntity(fleet.MemberIds[i]);
                if (member == null) continue;

                var squadMember = member.GetComponent<SquadronMemberComponent>();
                if (squadMember != null)
                {
                    squadMember.FormationOffset = i < formationPositions.Count ? formationPositions[i] : Vector2.Zero;
                }
            }
        }

        private List<Vector2> CalculateFormation(FleetComponent fleet)
        {
            var positions = new List<Vector2>();
            int count = fleet.MemberIds.Count;

            switch (fleet.Formation)
            {
                case FormationType.Line:
                    for (int i = 0; i < count; i++)
                    {
                        positions.Add(new Vector2(i * fleet.FormationSpacing, 0));
                    }
                    break;

                case FormationType.Column:
                    for (int i = 0; i < count; i++)
                    {
                        positions.Add(new Vector2(0, i * fleet.FormationSpacing));
                    }
                    break;

                case FormationType.Wedge:
                    for (int i = 0; i < count; i++)
                    {
                        int row = (int)Math.Sqrt(i);
                        int col = i - row * row;
                        positions.Add(new Vector2((col - row / 2f) * fleet.FormationSpacing, row * fleet.FormationSpacing));
                    }
                    break;

                case FormationType.Box:
                    int side = (int)Math.Ceiling(Math.Sqrt(count));
                    for (int i = 0; i < count; i++)
                    {
                        int row = i / side;
                        int col = i % side;
                        positions.Add(new Vector2(col * fleet.FormationSpacing, row * fleet.FormationSpacing));
                    }
                    break;

                case FormationType.Circle:
                    float radius = fleet.FormationSpacing * count / (2f * MathF.PI);
                    for (int i = 0; i < count; i++)
                    {
                        float angle = (i / (float)count) * MathF.PI * 2f;
                        positions.Add(new Vector2(MathF.Cos(angle) * radius, MathF.Sin(angle) * radius));
                    }
                    break;

                default:
                    for (int i = 0; i < count; i++)
                    {
                        positions.Add(Vector2.Zero);
                    }
                    break;
            }

            return positions;
        }

        private void UpdateFleetMovement(FleetComponent fleet, float deltaTime)
        {
            // Update each squadron member to follow formation
            foreach (var memberId in fleet.MemberIds)
            {
                var member = _entityManager.GetEntity(memberId);
                if (member == null) continue;

                var squadMember = member.GetComponent<SquadronMemberComponent>();
                var transform = member.GetComponent<TransformComponent>();
                var velocity = member.GetComponent<VelocityComponent>();

                if (squadMember == null || transform == null || velocity == null) continue;

                // Calculate target position based on fleet leader + formation offset
                var leader = _entityManager.GetEntity(fleet.LeaderId);
                if (leader == null) continue;

                var leaderTransform = leader.GetComponent<TransformComponent>();
                if (leaderTransform == null) continue;

                Vector2 targetPos = leaderTransform.Position + Rotate(squadMember.FormationOffset, leaderTransform.Rotation);
                Vector2 toTarget = targetPos - transform.Position;
                float dist = toTarget.Length();

                if (dist > fleet.FormationTolerance)
                {
                    toTarget.Normalize();
                    velocity.LinearVelocity = toTarget * squadMember.MaxSpeed;
                }
                else
                {
                    // Match leader velocity when in formation
                    var leaderVel = leader.GetComponent<VelocityComponent>();
                    if (leaderVel != null)
                    {
                        velocity.LinearVelocity = leaderVel.LinearVelocity;
                    }
                }
            }
        }

        private static Vector2 Rotate(Vector2 v, float radians)
        {
            float c = MathF.Cos(radians);
            float s = MathF.Sin(radians);
            return new Vector2(v.X * c - v.Y * s, v.X * s + v.Y * c);
        }

        public void AddShipToFleet(int fleetId, int shipId)
        {
            var fleet = _entityManager.GetEntity(fleetId)?.GetComponent<FleetComponent>();
            var ship = _entityManager.GetEntity(shipId);
            if (fleet == null || ship == null) return;

            fleet.MemberIds.Add(shipId);
            
            if (!ship.HasComponent<SquadronMemberComponent>())
            {
                ship.AddComponent(new SquadronMemberComponent
                {
                    FleetId = fleetId,
                    FormationOffset = Vector2.Zero,
                    MaxSpeed = 300f
                });
            }
        }

        public void RemoveShipFromFleet(int fleetId, int shipId)
        {
            var fleet = _entityManager.GetEntity(fleetId)?.GetComponent<FleetComponent>();
            if (fleet == null) return;

            fleet.MemberIds.Remove(shipId);
            
            var ship = _entityManager.GetEntity(shipId);
            if (ship != null && ship.HasComponent<SquadronMemberComponent>())
            {
                ship.RemoveComponent<SquadronMemberComponent>();
            }
        }

        public void SetFleetFormation(int fleetId, FormationType formation)
        {
            var fleet = _entityManager.GetEntity(fleetId)?.GetComponent<FleetComponent>();
            if (fleet != null)
            {
                fleet.Formation = formation;
            }
        }
    }

    /// <summary>
    /// Fleet management component for commanding groups of ships.
    /// </summary>
    public class FleetComponent : Component
    {
        public string Name { get; set; } = "Fleet";
        public int LeaderId { get; set; } = -1;
        public List<int> MemberIds { get; } = new();
        public FormationType Formation { get; set; } = FormationType.Wedge;
        public float FormationSpacing { get; set; } = 100f;
        public float FormationTolerance { get; set; } = 50f;
        public string CommanderId { get; set; } = string.Empty;
        public Dictionary<string, object> Metadata { get; } = new();
    }

    /// <summary>
    /// Squadron member component for ships that belong to a fleet.
    /// </summary>
    public class SquadronMemberComponent : Component
    {
        public int FleetId { get; set; } = -1;
        public Vector2 FormationOffset { get; set; } = Vector2.Zero;
        public float MaxSpeed { get; set; } = 300f;
        public bool FollowLeader { get; set; } = true;
    }

    /// <summary>
    /// Formation patterns for fleets.
    /// </summary>
    public enum FormationType
    {
        None,
        Line,
        Column,
        Wedge,
        Box,
        Circle,
        Diamond,
        Arrow
    }
}
