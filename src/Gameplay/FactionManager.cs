using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using SpaceTradeEngine.ECS;
using SpaceTradeEngine.ECS.Components;

namespace SpaceTradeEngine.Gameplay
{
    /// <summary>
    /// Diplomatic attitude between factions.
    /// </summary>
    public enum FactionAttitude
    {
        Allied,      // +75 to +100: full cooperation
        Friendly,    // +25 to +74: trading partners
        Neutral,     // -24 to +24: cautious
        Hostile,     // -74 to -25: no trade
        War          // -100 to -75: active combat
    }

    /// <summary>
    /// Treaty types between factions.
    /// </summary>
    public enum TreatyType
    {
        None,
        Ceasefire,       // Temporary halt to hostilities
        TradeAgreement,  // Bonus to trade prices
        Embargo,         // Block all trade
        MilitaryAlliance // Mutual defense
    }

    /// <summary>
    /// Relationship data between two factions.
    /// </summary>
    public class FactionRelation
    {
        public string FactionA { get; set; } = "";
        public string FactionB { get; set; } = "";
        public float Attitude { get; set; } = 0f; // -100 to +100
        public TreatyType Treaty { get; set; } = TreatyType.None;
        public DateTime TreatyExpiration { get; set; }

        public FactionAttitude GetAttitude()
        {
            if (Attitude >= 75) return FactionAttitude.Allied;
            if (Attitude >= 25) return FactionAttitude.Friendly;
            if (Attitude >= -25) return FactionAttitude.Neutral;
            if (Attitude >= -75) return FactionAttitude.Hostile;
            return FactionAttitude.War;
        }
    }

    /// <summary>
    /// Manages all factions, their relationships, treaties, and diplomacy.
    /// Sprint 2: Core faction system with attitudes and territory.
    /// </summary>
    public class FactionManager
    {
        private EntityManager _entityManager;
        private Dictionary<string, Entity> _factions = new Dictionary<string, Entity>();
        private Dictionary<string, FactionRelation> _relations = new Dictionary<string, FactionRelation>();
        
        public FactionManager(EntityManager entityManager)
        {
            _entityManager = entityManager;
            InitializeDefaultFactions();
        }

        /// <summary>
        /// Create default factions for the game.
        /// </summary>
        private void InitializeDefaultFactions()
        {
            // Human Federation - Lawful traders
            CreateFaction("human_federation", "Human Federation", Color.Blue, 60f, 70f);
            
            // Drath Empire - Militaristic expansionists
            CreateFaction("drath_empire", "Drath Empire", Color.Red, 80f, 50f);
            
            // Independent Traders - Neutral merchants
            CreateFaction("traders_guild", "Traders Guild", Color.Gold, 40f, 80f);
            
            // Pirates - Hostile outlaws
            CreateFaction("pirates", "Pirate Clans", Color.DarkRed, 55f, 45f);

            // Set initial relationships
            SetRelation("human_federation", "drath_empire", -60f, TreatyType.Ceasefire);
            SetRelation("human_federation", "traders_guild", 50f, TreatyType.TradeAgreement);
            SetRelation("human_federation", "pirates", -85f, TreatyType.None);
            SetRelation("drath_empire", "traders_guild", 20f, TreatyType.None);
            SetRelation("drath_empire", "pirates", -40f, TreatyType.None);
            SetRelation("traders_guild", "pirates", -30f, TreatyType.None);

            Console.WriteLine($"[FactionManager] Initialized {_factions.Count} factions with relationships");
        }

        /// <summary>
        /// Create a new faction entity.
        /// </summary>
        public Entity CreateFaction(string factionId, string name, Color color, float militaryPower, float economicPower)
        {
            var faction = _entityManager.CreateEntity();
            var factionComp = new FactionComponent(factionId, name, color)
            {
                MilitaryPower = militaryPower,
                EconomicPower = economicPower
            };
            faction.AddComponent(factionComp);
            _factions[factionId] = faction;
            return faction;
        }

        /// <summary>
        /// Set relationship between two factions.
        /// </summary>
        public void SetRelation(string factionA, string factionB, float attitude, TreatyType treaty = TreatyType.None)
        {
            string key = GetRelationKey(factionA, factionB);
            _relations[key] = new FactionRelation
            {
                FactionA = factionA,
                FactionB = factionB,
                Attitude = Math.Clamp(attitude, -100f, 100f),
                Treaty = treaty,
                TreatyExpiration = treaty != TreatyType.None ? DateTime.Now.AddDays(30) : DateTime.MinValue
            };
        }

        /// <summary>
        /// Get relationship between two factions.
        /// </summary>
        public FactionRelation? GetRelation(string factionA, string factionB)
        {
            string key = GetRelationKey(factionA, factionB);
            return _relations.TryGetValue(key, out var relation) ? relation : null;
        }

        /// <summary>
        /// Modify relationship attitude by delta.
        /// </summary>
        public void ModifyRelation(string factionA, string factionB, float delta)
        {
            var relation = GetRelation(factionA, factionB);
            if (relation != null)
            {
                relation.Attitude = Math.Clamp(relation.Attitude + delta, -100f, 100f);
                Console.WriteLine($"[FactionManager] {factionA} <-> {factionB} attitude now {relation.Attitude:F1} ({relation.GetAttitude()})");
            }
        }

        /// <summary>
        /// Check if two factions can trade (not under embargo, not at war).
        /// </summary>
        public bool CanTrade(string factionA, string factionB)
        {
            var relation = GetRelation(factionA, factionB);
            if (relation == null) return true; // No relation = default neutral
            if (relation.Treaty == TreatyType.Embargo) return false;
            if (relation.GetAttitude() == FactionAttitude.War) return false;
            return true;
        }

        /// <summary>
        /// Get all factions.
        /// </summary>
        public IEnumerable<Entity> GetAllFactions()
        {
            return _factions.Values;
        }

        /// <summary>
        /// Get faction entity by ID.
        /// </summary>
        public Entity? GetFaction(string factionId)
        {
            return _factions.TryGetValue(factionId, out var faction) ? faction : null;
        }

        /// <summary>
        /// Get all relationships.
        /// </summary>
        public IEnumerable<FactionRelation> GetAllRelations()
        {
            return _relations.Values;
        }

        /// <summary>
        /// Create normalized relation key (alphabetically sorted).
        /// </summary>
        private string GetRelationKey(string factionA, string factionB)
        {
            var sorted = new[] { factionA, factionB }.OrderBy(f => f).ToArray();
            return $"{sorted[0]}_{sorted[1]}";
        }

        /// <summary>
        /// Update faction system (check treaty expirations, etc.)
        /// </summary>
        public void Update(GameTime gameTime)
        {
            // Check for expired treaties
            foreach (var relation in _relations.Values)
            {
                if (relation.Treaty != TreatyType.None && 
                    relation.TreatyExpiration != DateTime.MinValue &&
                    DateTime.Now > relation.TreatyExpiration)
                {
                    Console.WriteLine($"[FactionManager] Treaty {relation.Treaty} expired between {relation.FactionA} and {relation.FactionB}");
                    relation.Treaty = TreatyType.None;
                }
            }
        }
    }
}
