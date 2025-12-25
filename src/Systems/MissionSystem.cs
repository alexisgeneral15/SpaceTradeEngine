using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using SpaceTradeEngine.ECS;
using SpaceTradeEngine.ECS.Components;
using SpaceTradeEngine.Events;

#nullable enable
namespace SpaceTradeEngine.Systems
{
    /// <summary>
    /// Manages missions/jobs: generation, tracking, rewards, completion.
    /// </summary>
    public class MissionSystem : ECS.System
    {
        private readonly EntityManager _entityManager;
        private readonly EventSystem _eventSystem;
        private readonly Dictionary<int, Mission> _missions = new();
        private int _nextMissionId = 1;

        public MissionSystem(EntityManager entityManager, EventSystem eventSystem)
        {
            _entityManager = entityManager;
            _eventSystem = eventSystem;
        }

        protected override bool ShouldProcess(Entity entity)
        {
            return entity.HasComponent<MissionComponent>();
        }

        public override void Update(float deltaTime)
        {
            // Update mission states
            foreach (var mission in _missions.Values.ToList())
            {
                if (mission.State == MissionState.Active)
                {
                    UpdateMissionProgress(mission, deltaTime);
                }
                else if (mission.State == MissionState.Expired)
                {
                    // Clean up expired missions
                    _missions.Remove(mission.Id);
                }
            }

            // Update entities with mission components
            foreach (var entity in _entities)
            {
                if (!entity.IsActive) continue;

                var missionComp = entity.GetComponent<MissionComponent>();
                if (missionComp == null) continue;

                // Remove completed/failed missions from entity
                missionComp.ActiveMissionIds.RemoveAll(id =>
                {
                    if (!_missions.TryGetValue(id, out var m)) return true;
                    return m.State == MissionState.Completed || m.State == MissionState.Failed;
                });
            }
        }

        private void UpdateMissionProgress(Mission mission, float deltaTime)
        {
            mission.TimeRemaining -= deltaTime;
            
            if (mission.TimeRemaining <= 0 && mission.TimeLimit > 0)
            {
                FailMission(mission.Id, "Time expired");
                return;
            }

            // Check objectives
            bool allComplete = true;
            foreach (var objective in mission.Objectives)
            {
                if (!objective.Completed)
                {
                    CheckObjectiveCompletion(mission, objective);
                    if (!objective.Completed)
                        allComplete = false;
                }
            }

            if (allComplete && mission.State == MissionState.Active)
            {
                CompleteMission(mission.Id);
            }
        }

        private void CheckObjectiveCompletion(Mission mission, MissionObjective objective)
        {
            switch (objective.Type)
            {
                case ObjectiveType.DestroyTarget:
                    // Check if target entity still exists
                    if (objective.TargetEntityId > 0)
                    {
                        var target = _entityManager.GetEntity(objective.TargetEntityId);
                        if (target == null || !target.IsActive)
                        {
                            objective.Progress = objective.RequiredCount;
                            objective.Completed = true;
                        }
                    }
                    break;

                case ObjectiveType.DeliverCargo:
                    // Check if at destination with cargo
                    if (mission.AssignedToEntityId > 0)
                    {
                        var entity = _entityManager.GetEntity(mission.AssignedToEntityId);
                        var cargo = entity?.GetComponent<CargoComponent>();
                        var transform = entity?.GetComponent<TransformComponent>();
                        
                        if (cargo != null && transform != null && objective.DestinationPosition.HasValue)
                        {
                            float dist = Vector2.Distance(transform.Position, objective.DestinationPosition.Value);
                            if (dist < 200f && cargo.Contains(objective.RequiredWareId ?? "", objective.RequiredCount))
                            {
                                objective.Progress = objective.RequiredCount;
                                objective.Completed = true;
                            }
                        }
                    }
                    break;

                case ObjectiveType.TravelTo:
                    if (mission.AssignedToEntityId > 0)
                    {
                        var entity = _entityManager.GetEntity(mission.AssignedToEntityId);
                        var transform = entity?.GetComponent<TransformComponent>();
                        
                        if (transform != null && objective.DestinationPosition.HasValue)
                        {
                            float dist = Vector2.Distance(transform.Position, objective.DestinationPosition.Value);
                            if (dist < 200f)
                            {
                                objective.Completed = true;
                            }
                        }
                    }
                    break;

                case ObjectiveType.Escort:
                    // Check if escorted entity is safe
                    if (objective.TargetEntityId > 0)
                    {
                        var target = _entityManager.GetEntity(objective.TargetEntityId);
                        var targetTransform = target?.GetComponent<TransformComponent>();
                        
                        if (targetTransform != null && objective.DestinationPosition.HasValue)
                        {
                            float dist = Vector2.Distance(targetTransform.Position, objective.DestinationPosition.Value);
                            if (dist < 200f)
                            {
                                objective.Completed = true;
                            }
                        }
                    }
                    break;
            }
        }

        public int CreateMission(Mission mission)
        {
            mission.Id = _nextMissionId++;
            mission.State = MissionState.Available;
            mission.TimeRemaining = mission.TimeLimit;
            _missions[mission.Id] = mission;
            
            _eventSystem.Publish(new MissionCreatedEvent(mission.Id, mission.Title, DateTime.UtcNow));
            return mission.Id;
        }

        public bool AssignMission(int missionId, int entityId)
        {
            if (!_missions.TryGetValue(missionId, out var mission)) return false;
            if (mission.State != MissionState.Available) return false;

            var entity = _entityManager.GetEntity(entityId);
            if (entity == null) return false;

            var missionComp = entity.GetComponent<MissionComponent>();
            if (missionComp == null)
            {
                missionComp = new MissionComponent();
                entity.AddComponent(missionComp);
            }

            mission.State = MissionState.Active;
            mission.AssignedToEntityId = entityId;
            missionComp.ActiveMissionIds.Add(missionId);
            
            _eventSystem.Publish(new MissionAcceptedEvent(missionId, entityId, DateTime.UtcNow));
            return true;
        }

        public void CompleteMission(int missionId)
        {
            if (!_missions.TryGetValue(missionId, out var mission)) return;
            if (mission.State != MissionState.Active) return;

            mission.State = MissionState.Completed;

            // Award rewards
            if (mission.AssignedToEntityId > 0)
            {
                var entity = _entityManager.GetEntity(mission.AssignedToEntityId);
                var cargo = entity?.GetComponent<CargoComponent>();
                if (cargo != null)
                {
                    cargo.Credits += mission.RewardCredits;
                }
            }

            _eventSystem.Publish(new MissionCompletedEvent(missionId, mission.AssignedToEntityId, mission.RewardCredits, DateTime.UtcNow));
        }

        public void FailMission(int missionId, string reason)
        {
            if (!_missions.TryGetValue(missionId, out var mission)) return;

            mission.State = MissionState.Failed;
            mission.FailureReason = reason;
            
            _eventSystem.Publish(new MissionFailedEvent(missionId, mission.AssignedToEntityId, reason, DateTime.UtcNow));
        }

        public Mission? GetMission(int missionId)
        {
            return _missions.TryGetValue(missionId, out var m) ? m : null;
        }

        public List<Mission> GetAvailableMissions()
        {
            return _missions.Values.Where(m => m.State == MissionState.Available).ToList();
        }

        public List<Mission> GetActiveMissionsForEntity(int entityId)
        {
            return _missions.Values.Where(m => m.State == MissionState.Active && m.AssignedToEntityId == entityId).ToList();
        }
    }

    /// <summary>
    /// Mission data structure.
    /// </summary>
    public class Mission
    {
        public int Id { get; set; }
        public string Title { get; set; } = "Untitled Mission";
        public string Description { get; set; } = string.Empty;
        public MissionState State { get; set; } = MissionState.Available;
        public List<MissionObjective> Objectives { get; } = new();
        public int AssignedToEntityId { get; set; } = -1;
        public float TimeLimit { get; set; } = 0f; // 0 = no limit
        public float TimeRemaining { get; set; } = 0f;
        public float RewardCredits { get; set; } = 1000f;
        public List<string> RewardItems { get; } = new();
        public string IssuerFaction { get; set; } = string.Empty;
        public string FailureReason { get; set; } = string.Empty;
        public Dictionary<string, object> Metadata { get; } = new();
    }

    public class MissionObjective
    {
        public ObjectiveType Type { get; set; }
        public string Description { get; set; } = string.Empty;
        public bool Completed { get; set; } = false;
        public int Progress { get; set; } = 0;
        public int RequiredCount { get; set; } = 1;
        public int TargetEntityId { get; set; } = -1;
        public string? RequiredWareId { get; set; }
        public Vector2? DestinationPosition { get; set; }
    }

    public enum MissionState
    {
        Available,
        Active,
        Completed,
        Failed,
        Expired
    }

    public enum ObjectiveType
    {
        DestroyTarget,
        DeliverCargo,
        TravelTo,
        Escort,
        CollectItems,
        ScanTarget,
        DefendLocation
    }

    /// <summary>
    /// Component for entities that can accept missions.
    /// </summary>
    public class MissionComponent : Component
    {
        public List<int> ActiveMissionIds { get; } = new();
        public int CompletedMissions { get; set; } = 0;
        public int FailedMissions { get; set; } = 0;
        public float Reputation { get; set; } = 0f;
    }

    // Events
    public record MissionCreatedEvent(int MissionId, string Title, DateTime Timestamp) : BaseEvent(Timestamp);
    public record MissionAcceptedEvent(int MissionId, int EntityId, DateTime Timestamp) : BaseEvent(Timestamp);
    public record MissionCompletedEvent(int MissionId, int EntityId, float Reward, DateTime Timestamp) : BaseEvent(Timestamp);
    public record MissionFailedEvent(int MissionId, int EntityId, string Reason, DateTime Timestamp) : BaseEvent(Timestamp);
}
