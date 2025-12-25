using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using SpaceTradeEngine.ECS;
using SpaceTradeEngine.Events;
using SpaceTradeEngine.Systems;

#nullable enable
namespace SpaceTradeEngine.AI
{
    /// <summary>
    /// Manages clan hierarchies, sub-clans, and clan relationships within and across factions.
    /// </summary>
    public class ClanSystem : ECS.System
    {
        private readonly EventSystem _eventSystem;
        private readonly Dictionary<string, Clan> _clans = new();
        private readonly Dictionary<string, Dictionary<string, ClanRelationship>> _clanRelationships = new();

        public ClanSystem(EventSystem eventSystem)
        {
            _eventSystem = eventSystem;
        }

        protected override bool ShouldProcess(Entity entity)
        {
            return entity.HasComponent<ClanComponent>();
        }

        public override void Update(float deltaTime)
        {
            // Update clan relationships and decay
            foreach (var clan1 in _clans.Keys.ToList())
            {
                if (!_clanRelationships.ContainsKey(clan1)) continue;

                foreach (var clan2 in _clanRelationships[clan1].Keys.ToList())
                {
                    var rel = _clanRelationships[clan1][clan2];
                    
                    // Decay relationships over time
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

            // Update clan strength (based on member count and health)
            foreach (var clan in _clans.Values)
            {
                clan.UpdateStrength();
            }
        }

        public Clan CreateClan(string clanId, string clanName, string parentFaction, string? parentClanId = null)
        {
            var clan = new Clan
            {
                ClanId = clanId,
                ClanName = clanName,
                ParentFaction = parentFaction,
                ParentClanId = parentClanId,
                CreatedAt = DateTime.UtcNow,
                CurrentStrength = 100f,
                MaxStrength = 100f,
                Reputation = 0f
            };

            _clans[clanId] = clan;
            return clan;
        }

        public Clan? GetClan(string clanId)
        {
            return _clans.TryGetValue(clanId, out var clan) ? clan : null;
        }

        public List<Clan> GetSubClans(string parentClanId)
        {
            return _clans.Values
                .Where(c => c.ParentClanId == parentClanId)
                .ToList();
        }

        public List<Clan> GetClansForFaction(string factionId)
        {
            return _clans.Values
                .Where(c => c.ParentFaction == factionId && c.ParentClanId == null)
                .ToList();
        }

        public void AddMemberToClan(string clanId, string memberId, float strength = 1f)
        {
            if (_clans.TryGetValue(clanId, out var clan))
            {
                clan.Members.Add(memberId);
                clan.MemberStrengths[memberId] = strength;
                clan.TotalMemberStrength += strength;
            }
        }

        public void RemoveMemberFromClan(string clanId, string memberId)
        {
            if (_clans.TryGetValue(clanId, out var clan))
            {
                clan.Members.Remove(memberId);
                if (clan.MemberStrengths.TryGetValue(memberId, out float strength))
                {
                    clan.TotalMemberStrength -= strength;
                    clan.MemberStrengths.Remove(memberId);
                }
            }
        }

        public void SetClanRelationship(string clan1, string clan2, float standing)
        {
            EnsureClanRelationshipExists(clan1, clan2);
            _clanRelationships[clan1][clan2].Standing = Math.Clamp(standing, -100f, 100f);
            _clanRelationships[clan1][clan2].TimeSinceLastInteraction = 0f;
        }

        public void ModifyClanRelationship(string clan1, string clan2, float delta, string reason = "")
        {
            EnsureClanRelationshipExists(clan1, clan2);
            var rel = _clanRelationships[clan1][clan2];
            float oldStanding = rel.Standing;
            rel.Standing = Math.Clamp(rel.Standing + delta, -100f, 100f);
            rel.TimeSinceLastInteraction = 0f;

            if (!string.IsNullOrEmpty(reason))
            {
                rel.RecentEvents.Add(new ClanDiplomaticEvent
                {
                    Timestamp = DateTime.UtcNow,
                    Description = reason,
                    StandingChange = delta
                });

                if (rel.RecentEvents.Count > 10)
                    rel.RecentEvents.RemoveAt(0);
            }

            // Publish event if relationship state changed
            var oldState = GetClanRelationshipState(oldStanding);
            var newState = GetClanRelationshipState(rel.Standing);
            if (oldState != newState)
            {
                _eventSystem.Publish(new ClanRelationshipChangedEvent(clan1, clan2, oldState, newState, DateTime.UtcNow));
            }
        }

        public float GetClanStanding(string clan1, string clan2)
        {
            if (!_clanRelationships.TryGetValue(clan1, out var c1Rels))
                return 0f;
            if (!c1Rels.TryGetValue(clan2, out var rel))
                return 0f;
            return rel.Standing;
        }

        public ClanRelationshipState GetClanRelationship(string clan1, string clan2)
        {
            return GetClanRelationshipState(GetClanStanding(clan1, clan2));
        }

        public bool AreClansHostile(string clan1, string clan2)
        {
            return GetClanStanding(clan1, clan2) < -50f;
        }

        public bool AreClansAllied(string clan1, string clan2)
        {
            return GetClanStanding(clan1, clan2) > 75f;
        }

        public bool AreClansFriendly(string clan1, string clan2)
        {
            return GetClanStanding(clan1, clan2) > 25f;
        }

        public List<ClanRelationship> GetAllClanRelationships(string clanId)
        {
            var result = new List<ClanRelationship>();
            if (_clanRelationships.TryGetValue(clanId, out var rels))
            {
                result.AddRange(rels.Values);
            }
            return result;
        }

        public void DeclareClanWar(string clan1, string clan2, string reason = "War declared")
        {
            SetClanRelationship(clan1, clan2, -100f);
            SetClanRelationship(clan2, clan1, -100f);
            ModifyClanRelationship(clan1, clan2, 0, reason);
            _eventSystem.Publish(new ClanWarDeclaredEvent(clan1, clan2, reason, DateTime.UtcNow));
        }

        public void DeclareClanAlliance(string clan1, string clan2, string reason = "Alliance formed")
        {
            SetClanRelationship(clan1, clan2, 100f);
            SetClanRelationship(clan2, clan1, 100f);
            ModifyClanRelationship(clan1, clan2, 0, reason);
            _eventSystem.Publish(new ClanAllianceFormedEvent(clan1, clan2, reason, DateTime.UtcNow));
        }

        private void EnsureClanRelationshipExists(string clan1, string clan2)
        {
            if (!_clanRelationships.ContainsKey(clan1))
                _clanRelationships[clan1] = new Dictionary<string, ClanRelationship>();

            if (!_clanRelationships[clan1].ContainsKey(clan2))
            {
                _clanRelationships[clan1][clan2] = new ClanRelationship
                {
                    Clan1 = clan1,
                    Clan2 = clan2,
                    Standing = 0f
                };
            }
        }

        private static ClanRelationshipState GetClanRelationshipState(float standing)
        {
            return standing switch
            {
                < -75f => ClanRelationshipState.Hostile,
                < -25f => ClanRelationshipState.Unfriendly,
                < 25f => ClanRelationshipState.Neutral,
                < 75f => ClanRelationshipState.Friendly,
                _ => ClanRelationshipState.Allied
            };
        }
    }

    /// <summary>
    /// Represents a clan: a group of ships with shared allegiance and hierarchy.
    /// </summary>
    public class Clan
    {
        public string ClanId { get; set; } = string.Empty;
        public string ClanName { get; set; } = string.Empty;
        public string ParentFaction { get; set; } = string.Empty;
        public string? ParentClanId { get; set; } // Null for main clans, set for sub-clans
        public List<string> Members { get; set; } = new();
        public Dictionary<string, float> MemberStrengths { get; set; } = new();
        public float TotalMemberStrength { get; set; } = 0f;
        public float CurrentStrength { get; set; }
        public float MaxStrength { get; set; }
        public float Reputation { get; set; } // -100 to 100
        public DateTime CreatedAt { get; set; }
        public List<string> Allies { get; set; } = new();
        public List<string> Enemies { get; set; } = new();

        public float GetStrengthPercent() => CurrentStrength / MaxStrength;

        public void UpdateStrength()
        {
            // Strength based on member count and their health
            CurrentStrength = TotalMemberStrength > 0 
                ? (TotalMemberStrength / MaxStrength) * 100f 
                : 0f;
        }

        public void ModifyReputation(float delta)
        {
            Reputation = Math.Clamp(Reputation + delta, -100f, 100f);
        }
    }

    /// <summary>
    /// Component for entities that belong to a clan.
    /// </summary>
    public class ClanComponent : Component
    {
        public string ClanId { get; set; } = string.Empty;
        public string ClanRole { get; set; } = "member"; // "leader", "officer", "member"
        public int ClanRank { get; set; } = 0;
        public float Contribution { get; set; } = 0f; // Points towards clan strength
    }

    /// <summary>
    /// Relationship between two clans.
    /// </summary>
    public class ClanRelationship
    {
        public string Clan1 { get; set; } = string.Empty;
        public string Clan2 { get; set; } = string.Empty;
        public float Standing { get; set; } = 0f;
        public float DecayRate { get; set; } = 2f;
        public float DecayDelay { get; set; } = 30f;
        public float TimeSinceLastInteraction { get; set; } = 0f;
        public List<ClanDiplomaticEvent> RecentEvents { get; set; } = new();
    }

    public class ClanDiplomaticEvent
    {
        public DateTime Timestamp { get; set; }
        public string Description { get; set; } = string.Empty;
        public float StandingChange { get; set; }
    }

    public enum ClanRelationshipState
    {
        Hostile,
        Unfriendly,
        Neutral,
        Friendly,
        Allied
    }

    // Events
    public record ClanRelationshipChangedEvent(string Clan1, string Clan2, ClanRelationshipState OldState, ClanRelationshipState NewState, DateTime Timestamp) : BaseEvent(Timestamp);
    public record ClanWarDeclaredEvent(string Clan1, string Clan2, string Reason, DateTime Timestamp) : BaseEvent(Timestamp);
    public record ClanAllianceFormedEvent(string Clan1, string Clan2, string Reason, DateTime Timestamp) : BaseEvent(Timestamp);
}
