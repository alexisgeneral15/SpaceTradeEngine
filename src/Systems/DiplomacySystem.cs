using System;
using System.Collections.Generic;
using System.Linq;
using SpaceTradeEngine.ECS;
using SpaceTradeEngine.Events;
using SpaceTradeEngine.AI;

#nullable enable
namespace SpaceTradeEngine.Systems
{
    /// <summary>
    /// Manages diplomatic relationships between factions: alliances, wars, standings.
    /// </summary>
    public class DiplomacySystem : ECS.System
    {
        private readonly Dictionary<string, Dictionary<string, Relationship>> _relationships = new();
        private readonly EventSystem _eventSystem;

        public DiplomacySystem(EventSystem eventSystem)
        {
            _eventSystem = eventSystem;
        }

        protected override bool ShouldProcess(Entity entity)
        {
            return false; // Diplomacy doesn't process entities
        }

        public override void Update(float deltaTime)
        {
            // Decay relationships over time
            foreach (var faction1 in _relationships.Keys.ToList())
            {
                foreach (var faction2 in _relationships[faction1].Keys.ToList())
                {
                    var rel = _relationships[faction1][faction2];
                    
                    // Decay towards neutral if no recent events
                    rel.TimeSinceLastInteraction += deltaTime;
                    if (rel.TimeSinceLastInteraction > rel.DecayDelay && rel.Standing != 0)
                    {
                        float decay = rel.DecayRate * deltaTime;
                        if (rel.Standing > 0)
                            rel.Standing = Math.Max(0, rel.Standing - decay);
                        else
                            rel.Standing = Math.Min(0, rel.Standing + decay);
                    }
                }
            }
        }

        public void SetRelationship(string faction1, string faction2, float standing)
        {
            EnsureRelationshipExists(faction1, faction2);
            _relationships[faction1][faction2].Standing = Math.Clamp(standing, -100f, 100f);
            _relationships[faction1][faction2].TimeSinceLastInteraction = 0f;
        }

        public void ModifyRelationship(string faction1, string faction2, float delta, string reason = "")
        {
            EnsureRelationshipExists(faction1, faction2);
            var rel = _relationships[faction1][faction2];
            float oldStanding = rel.Standing;
            rel.Standing = Math.Clamp(rel.Standing + delta, -100f, 100f);
            rel.TimeSinceLastInteraction = 0f;

            if (!string.IsNullOrEmpty(reason))
            {
                rel.RecentEvents.Add(new DiplomaticEvent
                {
                    Timestamp = DateTime.UtcNow,
                    Description = reason,
                    StandingChange = delta
                });

                // Keep only recent events
                if (rel.RecentEvents.Count > 10)
                    rel.RecentEvents.RemoveAt(0);
            }

            // Publish event if relationship state changed
            var oldState = GetRelationshipState(oldStanding);
            var newState = GetRelationshipState(rel.Standing);
            if (oldState != newState)
            {
                _eventSystem.Publish(new RelationshipChangedEvent(faction1, faction2, oldState, newState, DateTime.UtcNow));
            }
        }

        public float GetStanding(string faction1, string faction2)
        {
            if (!_relationships.TryGetValue(faction1, out var f1Rels))
                return 0f;
            if (!f1Rels.TryGetValue(faction2, out var rel))
                return 0f;
            return rel.Standing;
        }

        public RelationshipState GetRelationship(string faction1, string faction2)
        {
            return GetRelationshipState(GetStanding(faction1, faction2));
        }

        public bool AreHostile(string faction1, string faction2)
        {
            return GetStanding(faction1, faction2) < -50f;
        }

        public bool AreAllies(string faction1, string faction2)
        {
            return GetStanding(faction1, faction2) > 75f;
        }

        public bool AreFriendly(string faction1, string faction2)
        {
            return GetStanding(faction1, faction2) > 25f;
        }

        public List<Relationship> GetAllRelationships(string faction)
        {
            var result = new List<Relationship>();
            if (_relationships.TryGetValue(faction, out var rels))
            {
                result.AddRange(rels.Values);
            }
            return result;
        }

        public void DeclareWar(string faction1, string faction2, string reason = "War declared")
        {
            SetRelationship(faction1, faction2, -100f);
            SetRelationship(faction2, faction1, -100f);
            ModifyRelationship(faction1, faction2, 0, reason);
            _eventSystem.Publish(new WarDeclaredEvent(faction1, faction2, reason, DateTime.UtcNow));
        }

        public void DeclareAlliance(string faction1, string faction2, string reason = "Alliance formed")
        {
            SetRelationship(faction1, faction2, 100f);
            SetRelationship(faction2, faction1, 100f);
            ModifyRelationship(faction1, faction2, 0, reason);
            _eventSystem.Publish(new AllianceFormedEvent(faction1, faction2, reason, DateTime.UtcNow));
        }

        private void EnsureRelationshipExists(string faction1, string faction2)
        {
            if (!_relationships.ContainsKey(faction1))
                _relationships[faction1] = new Dictionary<string, Relationship>();
            
            if (!_relationships[faction1].ContainsKey(faction2))
            {
                _relationships[faction1][faction2] = new Relationship
                {
                    Faction1 = faction1,
                    Faction2 = faction2,
                    Standing = 0f
                };
            }
        }

        private static RelationshipState GetRelationshipState(float standing)
        {
            if (standing >= 75f) return RelationshipState.Allied;
            if (standing >= 25f) return RelationshipState.Friendly;
            if (standing >= -25f) return RelationshipState.Neutral;
            if (standing >= -50f) return RelationshipState.Unfriendly;
            return RelationshipState.Hostile;
        }

        /// <summary>
        /// Checks if a clan belongs to a faction and if there are allied/enemy clans within same faction.
        /// </summary>
        public bool AreClansAlliedWithinFaction(ClanSystem clanSystem, string clan1, string clan2)
        {
            var c1 = clanSystem.GetClan(clan1);
            var c2 = clanSystem.GetClan(clan2);
            if (c1 == null || c2 == null) return false;

            // Must be in same parent faction to have internal alliances
            if (c1.ParentFaction != c2.ParentFaction) return false;

            // Check clan relationships
            return clanSystem.AreClansAllied(clan1, clan2);
        }

        /// <summary>
        /// Checks if clans from same faction are enemies.
        /// </summary>
        public bool AreClansEnemiesWithinFaction(ClanSystem clanSystem, string clan1, string clan2)
        {
            var c1 = clanSystem.GetClan(clan1);
            var c2 = clanSystem.GetClan(clan2);
            if (c1 == null || c2 == null) return false;

            if (c1.ParentFaction != c2.ParentFaction) return false;
            return clanSystem.AreClansHostile(clan1, clan2);
        }

        /// <summary>
        /// Propagates faction war to all allied clans.
        /// </summary>
        public void PropagateWarToClanAllies(ClanSystem clanSystem, string faction1, string faction2)
        {
            var clans1 = clanSystem.GetClansForFaction(faction1);
            var clans2 = clanSystem.GetClansForFaction(faction2);

            foreach (var clan1 in clans1)
            {
                foreach (var clan2 in clans2)
                {
                    // Allied clans share wars
                    if (clan1.Allies.Contains(clan2.ClanId) || clan1.ClanId == clan2.ClanId)
                    {
                        clanSystem.DeclareClanWar(clan1.ClanId, clan2.ClanId, 
                            $"Faction war propagated: {faction1} vs {faction2}");
                    }
                }
            }
        }
    }

    /// <summary>
    /// Relationship data between two factions.
    /// </summary>
    public class Relationship
    {
        public string Faction1 { get; set; } = string.Empty;
        public string Faction2 { get; set; } = string.Empty;
        public float Standing { get; set; } = 0f; // -100 (war) to +100 (allied)
        public float TimeSinceLastInteraction { get; set; } = 0f;
        public float DecayRate { get; set; } = 1f; // Standing decay per second
        public float DecayDelay { get; set; } = 300f; // 5 minutes before decay starts
        public List<DiplomaticEvent> RecentEvents { get; } = new();
    }

    public class DiplomaticEvent
    {
        public DateTime Timestamp { get; set; }
        public string Description { get; set; } = string.Empty;
        public float StandingChange { get; set; }
    }

    public enum RelationshipState
    {
        Hostile,
        Unfriendly,
        Neutral,
        Friendly,
        Allied
    }

    // Events
    public record RelationshipChangedEvent(string Faction1, string Faction2, RelationshipState OldState, RelationshipState NewState, DateTime Timestamp) : BaseEvent(Timestamp);
    public record WarDeclaredEvent(string Faction1, string Faction2, string Reason, DateTime Timestamp) : BaseEvent(Timestamp);
    public record AllianceFormedEvent(string Faction1, string Faction2, string Reason, DateTime Timestamp) : BaseEvent(Timestamp);
}
