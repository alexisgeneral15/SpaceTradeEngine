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
    /// AI system for managing NPC faction behavior: expansion, clan creation, diplomacy, satellite faction spawning.
    /// </summary>
    public class FactionAISystem : ECS.System
    {
        private readonly EntityManager _entityManager;
        private readonly EventSystem _eventSystem;
        private readonly DiplomacySystem _diplomacySystem;
        private readonly ClanSystem _clanSystem;
        private readonly Dictionary<string, FactionAIController> _factionControllers = new();
        private readonly Random _random = new();

        private float _updateTimer = 0f;
        private const float UPDATE_INTERVAL = 5f; // AI decisions every 5 seconds

        public FactionAISystem(EntityManager entityManager, EventSystem eventSystem, 
            DiplomacySystem diplomacySystem, ClanSystem clanSystem)
        {
            _entityManager = entityManager;
            _eventSystem = eventSystem;
            _diplomacySystem = diplomacySystem;
            _clanSystem = clanSystem;
        }

        protected override bool ShouldProcess(Entity entity)
        {
            return false; // System-level AI, not entity-based
        }

        public override void Update(float deltaTime)
        {
            _updateTimer += deltaTime;
            if (_updateTimer < UPDATE_INTERVAL) return;
            _updateTimer = 0f;

            // Update all faction AI controllers
            foreach (var controller in _factionControllers.Values)
            {
                UpdateFactionBehavior(controller, deltaTime);
            }
        }

        public void RegisterFaction(string factionId, FactionAIProfile profile)
        {
            if (_factionControllers.ContainsKey(factionId)) return;

            var controller = new FactionAIController
            {
                FactionId = factionId,
                Profile = profile,
                Treasury = profile.StartingTreasury,
                MotherFactionId = profile.MotherFactionId
            };

            _factionControllers[factionId] = controller;
            _eventSystem.Publish(new FactionRegisteredEvent(factionId, DateTime.UtcNow));
        }

        private void UpdateFactionBehavior(FactionAIController controller, float deltaTime)
        {
            // Update economy
            controller.Treasury += controller.Profile.IncomeRate * (deltaTime / 60f);
            controller.TimeSinceLastAction += deltaTime;

            // Decision tree based on AI profile
            if (controller.TimeSinceLastAction < controller.Profile.DecisionCooldown)
                return;

            // 1. Check if should create satellite faction
            if (ShouldCreateSatelliteFaction(controller))
            {
                CreateSatelliteFaction(controller);
                controller.TimeSinceLastAction = 0f;
                return;
            }

            // 2. Check if should create new clan
            if (ShouldCreateClan(controller))
            {
                CreateFactionClan(controller);
                controller.TimeSinceLastAction = 0f;
                return;
            }

            // 3. Check if should expand (spawn ships/bases)
            if (ShouldExpand(controller))
            {
                ExpandFactionTerritory(controller);
                controller.TimeSinceLastAction = 0f;
                return;
            }

            // 4. Diplomatic actions
            if (ShouldTakeDiplomaticAction(controller))
            {
                TakeDiplomaticAction(controller);
                controller.TimeSinceLastAction = 0f;
                return;
            }

            // 5. Clan management
            if (ShouldManageClans(controller))
            {
                ManageClanRelationships(controller);
                controller.TimeSinceLastAction = 0f;
            }
        }

        #region Satellite Faction Creation

        private bool ShouldCreateSatelliteFaction(FactionAIController controller)
        {
            // Don't create if already a satellite
            if (controller.MotherFactionId != null) return false;

            // Check treasury and expansion desire
            if (controller.Treasury < controller.Profile.SatelliteFactionCost) return false;
            if (controller.SatelliteFactions.Count >= controller.Profile.MaxSatelliteFactions) return false;

            // Chance based on expansionist trait
            float chance = controller.Profile.Expansionist * 0.15f; // 0-15% per check
            return _random.NextDouble() < chance;
        }

        private void CreateSatelliteFaction(FactionAIController controller)
        {
            string satelliteId = $"{controller.FactionId}_satellite_{controller.SatelliteFactions.Count + 1}";
            string satelliteName = $"{controller.FactionId} Colony {controller.SatelliteFactions.Count + 1}";

            // Create AI profile for satellite (inherit traits but reduced)
            var satelliteProfile = new FactionAIProfile
            {
                FactionName = satelliteName,
                StartingTreasury = controller.Profile.SatelliteFactionCost * 0.5f,
                IncomeRate = controller.Profile.IncomeRate * 0.7f,
                Aggressiveness = controller.Profile.Aggressiveness * 0.8f,
                Expansionist = controller.Profile.Expansionist * 0.6f,
                Diplomatic = controller.Profile.Diplomatic, // Inherit diplomatic stance
                MotherFactionId = controller.FactionId,
                Autonomous = controller.Profile.SatelliteAutonomy,
                MaxSatelliteFactions = 0 // Satellites can't create their own satellites
            };

            RegisterFaction(satelliteId, satelliteProfile);
            controller.SatelliteFactions.Add(satelliteId);
            controller.Treasury -= controller.Profile.SatelliteFactionCost;

            // Set initial relationship with mother faction
            _diplomacySystem.SetRelationship(controller.FactionId, satelliteId, 85f);
            _diplomacySystem.SetRelationship(satelliteId, controller.FactionId, 85f);

            // Inherit some diplomatic relationships
            var motherRelations = _diplomacySystem.GetAllRelationships(controller.FactionId);
            foreach (var rel in motherRelations)
            {
                string otherFaction = rel.Faction1 == controller.FactionId ? rel.Faction2 : rel.Faction1;
                if (otherFaction == satelliteId) continue;

                // Inherit 70% of mother's standing
                float inheritedStanding = rel.Standing * 0.7f;
                _diplomacySystem.SetRelationship(satelliteId, otherFaction, inheritedStanding);
            }

            _eventSystem.Publish(new SatelliteFactionCreatedEvent(
                controller.FactionId, satelliteId, satelliteName, DateTime.UtcNow));
        }

        #endregion

        #region Clan Creation

        private bool ShouldCreateClan(FactionAIController controller)
        {
            var existingClans = _clanSystem.GetClansForFaction(controller.FactionId);
            if (existingClans.Count >= controller.Profile.MaxClans) return false;
            if (controller.Treasury < controller.Profile.ClanCreationCost) return false;

            // Higher chance if expansionist or has many ships
            float chance = controller.Profile.Expansionist * 0.2f;
            return _random.NextDouble() < chance;
        }

        private void CreateFactionClan(FactionAIController controller)
        {
            var existingClans = _clanSystem.GetClansForFaction(controller.FactionId);
            int clanIndex = existingClans.Count + 1;

            string clanId = $"{controller.FactionId}_clan_{clanIndex}";
            string clanName = GenerateClanName(controller.FactionId, clanIndex);

            var clan = _clanSystem.CreateClan(clanId, clanName, controller.FactionId, null);
            clan.MaxStrength = 200f;
            clan.Reputation = _random.Next(-20, 40);

            controller.Treasury -= controller.Profile.ClanCreationCost;
            controller.ManagedClans.Add(clanId);

            // Set initial relationships with other clans in faction
            foreach (var otherClan in existingClans)
            {
                float standing = _random.Next(-30, 60); // Some internal rivalry
                _clanSystem.SetClanRelationship(clanId, otherClan.ClanId, standing);
            }

            _eventSystem.Publish(new ClanCreatedByAIEvent(
                controller.FactionId, clanId, clanName, DateTime.UtcNow));
        }

        private string GenerateClanName(string factionId, int index)
        {
            var prefixes = new[] { "Elite", "Prime", "Vanguard", "Shadow", "Iron", "Storm", "Crimson", "Golden" };
            var suffixes = new[] { "Legion", "Guard", "Fleet", "Brotherhood", "Order", "Corps", "Division", "Wing" };

            string prefix = prefixes[_random.Next(prefixes.Length)];
            string suffix = suffixes[_random.Next(suffixes.Length)];

            return $"{prefix} {suffix}";
        }

        #endregion

        #region Territory Expansion

        private bool ShouldExpand(FactionAIController controller)
        {
            if (controller.Treasury < controller.Profile.ExpansionCost) return false;
            
            float chance = controller.Profile.Expansionist * 0.25f;
            return _random.NextDouble() < chance;
        }

        private void ExpandFactionTerritory(FactionAIController controller)
        {
            // Spawn expansion fleet (3-5 ships)
            int shipCount = _random.Next(3, 6);
            var spawnCenter = new Vector2(_random.Next(-5000, 5000), _random.Next(-5000, 5000));

            var existingClans = _clanSystem.GetClansForFaction(controller.FactionId);
            string? assignedClan = existingClans.Count > 0 
                ? existingClans[_random.Next(existingClans.Count)].ClanId 
                : null;

            for (int i = 0; i < shipCount; i++)
            {
                var ship = _entityManager.CreateEntity($"{controller.FactionId}_expansion_{Guid.NewGuid().ToString().Substring(0, 8)}");
                var offset = new Vector2(_random.Next(-200, 200), _random.Next(-200, 200));
                
                ship.AddComponent(new ECS.Components.TransformComponent { Position = spawnCenter + offset });
                ship.AddComponent(new ECS.Components.VelocityComponent());
                ship.AddComponent(new ECS.Components.HealthComponent { MaxHealth = 180, CurrentHealth = 180 });
                ship.AddComponent(new ECS.Components.FactionComponent(controller.FactionId));
                
                if (assignedClan != null)
                {
                    ship.AddComponent(new ClanComponent { ClanId = assignedClan, ClanRole = "member" });
                    _clanSystem.AddMemberToClan(assignedClan, ship.Id.ToString(), 1.0f);
                }

                ship.AddComponent(new AIBehaviorComponent 
                { 
                    DefaultBehavior = AIBehaviorType.Patrol,
                    CruiseSpeed = 160f,
                    Aggressiveness = controller.Profile.Aggressiveness,
                    ClanId = assignedClan
                });
            }

            controller.Treasury -= controller.Profile.ExpansionCost;
            controller.ExpansionCount++;

            _eventSystem.Publish(new FactionExpandedEvent(
                controller.FactionId, spawnCenter, shipCount, DateTime.UtcNow));
        }

        #endregion

        #region Diplomacy

        private bool ShouldTakeDiplomaticAction(FactionAIController controller)
        {
            if (!controller.Profile.Diplomatic) return false;
            return _random.NextDouble() < 0.3f;
        }

        private void TakeDiplomaticAction(FactionAIController controller)
        {
            var allRelations = _diplomacySystem.GetAllRelationships(controller.FactionId);
            if (allRelations.Count == 0) return;

            var targetRel = allRelations[_random.Next(allRelations.Count)];
            string targetFaction = targetRel.Faction1 == controller.FactionId ? targetRel.Faction2 : targetRel.Faction1;

            // Aggressive factions more likely to declare war
            if (controller.Profile.Aggressiveness > 0.7f && targetRel.Standing < -50f)
            {
                if (_random.NextDouble() < 0.4f)
                {
                    _diplomacySystem.DeclareWar(controller.FactionId, targetFaction, "AI expansion policy");
                    
                    // Propagate to satellite factions
                    foreach (var satelliteId in controller.SatelliteFactions)
                    {
                        _diplomacySystem.DeclareWar(satelliteId, targetFaction, "Supporting mother faction");
                    }
                }
            }
            // Diplomatic factions more likely to improve relations
            else if (controller.Profile.Diplomatic && targetRel.Standing > 0f && targetRel.Standing < 75f)
            {
                if (_random.NextDouble() < 0.5f)
                {
                    float improvement = _random.Next(10, 30);
                    _diplomacySystem.ModifyRelationship(controller.FactionId, targetFaction, improvement, "AI diplomatic outreach");
                }
            }
            // Consider alliance
            else if (targetRel.Standing > 75f && _random.NextDouble() < 0.2f)
            {
                _diplomacySystem.DeclareAlliance(controller.FactionId, targetFaction, "AI strategic alliance");
            }
        }

        #endregion

        #region Clan Management

        private bool ShouldManageClans(FactionAIController controller)
        {
            return controller.ManagedClans.Count > 1 && _random.NextDouble() < 0.25f;
        }

        private void ManageClanRelationships(FactionAIController controller)
        {
            if (controller.ManagedClans.Count < 2) return;

            // Pick two random clans and adjust their relationship
            var clan1Id = controller.ManagedClans[_random.Next(controller.ManagedClans.Count)];
            var clan2Id = controller.ManagedClans[_random.Next(controller.ManagedClans.Count)];
            
            if (clan1Id == clan2Id) return;

            float currentStanding = _clanSystem.GetClanStanding(clan1Id, clan2Id);

            // Faction tries to keep internal clans friendly (unless rivalry is policy)
            if (controller.Profile.AllowInternalRivalry && _random.NextDouble() < 0.3f)
            {
                // Encourage rivalry for competition
                float change = _random.Next(-15, -5);
                _clanSystem.ModifyClanRelationship(clan1Id, clan2Id, change, "Faction-sanctioned rivalry");
            }
            else if (currentStanding < 20f)
            {
                // Improve relations between internal clans
                float change = _random.Next(10, 25);
                _clanSystem.ModifyClanRelationship(clan1Id, clan2Id, change, "Faction unity directive");
            }
        }

        #endregion

        #region Query Methods

        public FactionAIController? GetFactionController(string factionId)
        {
            return _factionControllers.TryGetValue(factionId, out var controller) ? controller : null;
        }

        public List<string> GetSatelliteFactions(string motherFactionId)
        {
            return _factionControllers.TryGetValue(motherFactionId, out var controller) 
                ? controller.SatelliteFactions 
                : new List<string>();
        }

        public bool IsSatelliteFaction(string factionId)
        {
            return _factionControllers.TryGetValue(factionId, out var controller) 
                && controller.MotherFactionId != null;
        }

        public string? GetMotherFaction(string satelliteId)
        {
            return _factionControllers.TryGetValue(satelliteId, out var controller) 
                ? controller.MotherFactionId 
                : null;
        }

        #endregion
    }

    /// <summary>
    /// AI controller state for a single faction.
    /// </summary>
    public class FactionAIController
    {
        public string FactionId { get; set; } = string.Empty;
        public FactionAIProfile Profile { get; set; } = new();
        public float Treasury { get; set; } = 0f;
        public float TimeSinceLastAction { get; set; } = 0f;
        public int ExpansionCount { get; set; } = 0;
        
        // Hierarchy
        public string? MotherFactionId { get; set; }
        public List<string> SatelliteFactions { get; set; } = new();
        public List<string> ManagedClans { get; set; } = new();
    }

    /// <summary>
    /// AI personality profile for a faction.
    /// </summary>
    public class FactionAIProfile
    {
        public string FactionName { get; set; } = "Unknown";
        
        // Economic
        public float StartingTreasury { get; set; } = 10000f;
        public float IncomeRate { get; set; } = 100f; // Per minute
        
        // Personality traits (0-1)
        public float Aggressiveness { get; set; } = 0.5f;
        public float Expansionist { get; set; } = 0.5f;
        public bool Diplomatic { get; set; } = true;
        public bool AllowInternalRivalry { get; set; } = false;
        
        // Costs
        public float ExpansionCost { get; set; } = 2000f;
        public float ClanCreationCost { get; set; } = 1500f;
        public float SatelliteFactionCost { get; set; } = 5000f;
        
        // Limits
        public int MaxClans { get; set; } = 5;
        public int MaxSatelliteFactions { get; set; } = 3;
        
        // Decision timing
        public float DecisionCooldown { get; set; } = 30f; // Seconds between decisions
        
        // Satellite configuration
        public string? MotherFactionId { get; set; }
        public float Autonomous { get; set; } = 0.5f; // 0=puppet, 1=independent
        public float SatelliteAutonomy { get; set; } = 0.6f; // How autonomous satellites will be
    }

    // Events
    public record FactionRegisteredEvent(string FactionId, DateTime Timestamp) : BaseEvent(Timestamp);
    public record SatelliteFactionCreatedEvent(string MotherFactionId, string SatelliteId, string SatelliteName, DateTime Timestamp) : BaseEvent(Timestamp);
    public record ClanCreatedByAIEvent(string FactionId, string ClanId, string ClanName, DateTime Timestamp) : BaseEvent(Timestamp);
    public record FactionExpandedEvent(string FactionId, Vector2 Location, int ShipCount, DateTime Timestamp) : BaseEvent(Timestamp);
}
