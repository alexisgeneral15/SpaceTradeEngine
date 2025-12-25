using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SpaceTradeEngine.World;
#nullable enable

namespace SpaceTradeEngine.ECS.Components
{
    /// <summary>
    /// Transform component - handles position, rotation, scale
    /// </summary>
    public class TransformComponent : Component
    {
        public Vector2 Position { get; set; }
        public float Rotation { get; set; }
        public Vector2 Scale { get; set; } = Vector2.One;

        public TransformComponent()
        {
            Position = Vector2.Zero;
            Rotation = 0f;
        }

        public void Translate(Vector2 delta)
        {
            Position += delta;
        }

        public void Rotate(float deltaRotation)
        {
            Rotation += deltaRotation;
        }

        public Matrix GetTransformMatrix()
        {
            return Matrix.CreateScale(new Vector3(Scale, 1f)) *
                   Matrix.CreateRotationZ(Rotation) *
                   Matrix.CreateTranslation(new Vector3(Position, 0f));
        }
    }

    /// <summary>
    /// Sprite renderer component
    /// </summary>
    public class SpriteComponent : Component
    {
        public Texture2D? Texture { get; set; }
        public Color Tint { get; set; } = Color.White;
        public Color Color { get; set; } = Color.White; // Alias for Tint
        public float LayerDepth { get; set; } = 0f;
        public int Width { get; set; } = 32; // For drawing colored rectangles
        public int Height { get; set; } = 32;
        public bool IsVisible { get; set; } = true;

        public Vector2 GetOrigin() =>
            Texture != null ? new Vector2(Texture.Width / 2f, Texture.Height / 2f) : Vector2.Zero;

        public Rectangle GetSourceRectangle() =>
            Texture != null ? new Rectangle(0, 0, Texture.Width, Texture.Height) : Rectangle.Empty;
    }

    /// <summary>
    /// Velocity component - for movement physics
    /// </summary>
    public class VelocityComponent : Component
    {
        public Vector2 LinearVelocity { get; set; }
        public float AngularVelocity { get; set; }
        public Vector2 Acceleration { get; set; }

        public override void Update(float deltaTime)
        {
            var transform = Entity.GetComponent<TransformComponent>();
            if (transform == null)
                return;

            // Update velocity
            LinearVelocity += Acceleration * deltaTime;

            // Apply velocity to position
            transform.Translate(LinearVelocity * deltaTime);

            // Apply angular velocity
            transform.Rotate(AngularVelocity * deltaTime);
        }
    }

    /// <summary>
    /// Collision component
    /// </summary>
    public class CollisionComponent : Component
    {
        public float Radius { get; set; } = 10f;
        public bool IsTrigger { get; set; } = false;

        public Rectangle GetBounds()
        {
            var transform = Entity.GetComponent<TransformComponent>();
            if (transform == null)
                return new Rectangle(0, 0, (int)Radius * 2, (int)Radius * 2);

            var size = (int)Radius * 2;
            return new Rectangle(
                (int)(transform.Position.X - Radius),
                (int)(transform.Position.Y - Radius),
                size,
                size
            );
        }

        public bool Intersects(CollisionComponent other)
        {
            return GetBounds().Intersects(other.GetBounds());
        }
    }

    /// <summary>
    /// Health component
    /// </summary>
    public class HealthComponent : Component
    {
        public float MaxHealth { get; set; } = 100f;
        public float CurrentHealth { get; set; }

        // Optional notification hooks
        public event Action<float, float>? Damaged; // (damage, newHealth)
        public event Action<float>? Healed; // (amount)

        public override void Initialize()
        {
            CurrentHealth = MaxHealth;
        }

        public void TakeDamage(float damage)
        {
            CurrentHealth = Math.Max(0, CurrentHealth - damage);
            Damaged?.Invoke(damage, CurrentHealth);
        }

        public void Heal(float amount)
        {
            CurrentHealth = Math.Min(MaxHealth, CurrentHealth + amount);
            Healed?.Invoke(amount);
        }

        public bool IsAlive => CurrentHealth > 0;
        public float HealthPercent => CurrentHealth / MaxHealth;
    }

    /// <summary>
    /// Armor component - reduces incoming damage.
    /// Sprint 3: Combat enhancement.
    /// </summary>
    public class ArmorComponent : Component
    {
        public float ArmorRating { get; set; } = 0f;
        public float MaxArmor { get; set; } = 100f;
        public float CurrentArmor { get; set; }
        
        public override void Initialize()
        {
            CurrentArmor = MaxArmor;
        }
        
        public float CalculateDamageReduction()
        {
            if (CurrentArmor <= 0) return 0f;
            float effectiveArmor = ArmorRating * (CurrentArmor / MaxArmor);
            return effectiveArmor / (effectiveArmor + 100f);
        }
        
        public void DamageArmor(float amount)
        {
            CurrentArmor = Math.Max(0, CurrentArmor - amount);
        }
    }

    /// <summary>
    /// Salvage component - defines what a ship drops on death.
    /// Sprint 3: Salvage system.
    /// </summary>
    public class SalvageComponent : Component
    {
        public Dictionary<string, int> SalvageWares { get; set; } = new Dictionary<string, int>();
        public float SalvageCredits { get; set; } = 0f;
        public float SalvageValueMultiplier { get; set; } = 1.0f;
        public bool IsCollected { get; set; } = false;
    }

    /// <summary>
    /// Faction component - identifies which faction an entity belongs to.
    /// Sprint 2: Extended with territory, power indicators, and faction color.
    /// </summary>
    public class FactionComponent : Component
    {
        public string FactionId { get; set; } = string.Empty;
        public string FactionName { get; set; } = string.Empty;
        public Color FactionColor { get; set; } = Color.Gray;
        
        /// <summary>
        /// Territory control: sectors owned by this faction.
        /// </summary>
        public List<string> ControlledSectors { get; set; } = new List<string>();
        
        /// <summary>
        /// Military strength indicator (0-100).
        /// </summary>
        public float MilitaryPower { get; set; } = 50f;
        
        /// <summary>
        /// Economic strength indicator (0-100).
        /// </summary>
        public float EconomicPower { get; set; } = 50f;
        
        public FactionComponent(string factionId, string? factionName = null, Color? color = null)
        {
            FactionId = factionId;
            FactionName = factionName ?? factionId;
            FactionColor = color ?? Color.Gray;
        }
    }

    /// <summary>
    /// Tracks player reputation with factions (-100 to +100).
    /// Affects contract availability, pricing, and faction behavior.
    /// Sprint 2: Reputation system for diplomatic gameplay.
    /// </summary>
    public class ReputationComponent : Component
    {
        public Dictionary<string, float> Reputations { get; set; } = new Dictionary<string, float>();

        public void SetReputation(string factionId, float value)
        {
            value = Math.Clamp(value, -100f, 100f);
            Reputations[factionId] = value;
        }

        public float GetReputation(string factionId)
        {
            return Reputations.TryGetValue(factionId, out var rep) ? rep : 0f;
        }

        public void ModifyReputation(string factionId, float delta)
        {
            float current = GetReputation(factionId);
            SetReputation(factionId, current + delta);
        }

        public string GetStanding(string factionId)
        {
            float rep = GetReputation(factionId);
            if (rep >= 75) return "Allied";
            if (rep >= 25) return "Friendly";
            if (rep >= -25) return "Neutral";
            if (rep >= -75) return "Unfriendly";
            return "Hostile";
        }
    }

    /// <summary>
    /// Tag component - for categorizing entities (ship, station, asteroid, etc.)
    /// </summary>
    public class TagComponent : Component
    {
        public HashSet<string> Tags { get; private set; } = new HashSet<string>();

        public TagComponent(params string[] tags)
        {
            foreach (var tag in tags)
            {
                Tags.Add(tag);
            }
        }

        public void AddTag(string tag) => Tags.Add(tag);
        public void RemoveTag(string tag) => Tags.Remove(tag);
        public bool HasTag(string tag) => Tags.Contains(tag);
        public bool HasAnyTag(params string[] tags) => tags.Any(t => Tags.Contains(t));
        public bool HasAllTags(params string[] tags) => tags.All(t => Tags.Contains(t));
    }

    /// <summary>
    /// Selection component - marks entities as selectable/selected
    /// </summary>
    public class SelectionComponent : Component
    {
        public bool IsSelectable { get; set; } = true;
        public bool IsSelected { get; set; } = false;
        public Color SelectionColor { get; set; } = Color.Yellow;
        public float SelectionRadius { get; set; } = 50f;
    }

    /// <summary>
    /// Weapon definition and per-weapon state (cooldown, autofire).
    /// </summary>
    public class WeaponComponent : Component
    {
        public string Id { get; set; } = string.Empty;
        public float Damage { get; set; } = 10f;
        public float Range { get; set; } = 600f;
        public float Cooldown { get; set; } = 0.5f;
        public float CooldownRemaining { get; set; } = 0f;
        public float ProjectileSpeed { get; set; } = 450f;
        public float ProjectileRadius { get; set; } = 6f;
        public float ProjectileTTL { get; set; } = 4f;
        public bool AutoFire { get; set; } = true;
        public bool FireWithoutTarget { get; set; } = false;
        public float SpreadDegrees { get; set; } = 0f;
        public int ProjectilesPerShot { get; set; } = 1;
        public float EnergyCost { get; set; } = 0f;
        public float ShieldPierceRatio { get; set; } = 0f; // portion of damage that bypasses shields
        public string? AudioCue { get; set; }
    }

    /// <summary>
    /// Collection of weapon slots for a ship/station.
    /// </summary>
    public class WeaponSlotComponent : Component
    {
        public List<WeaponSlot> Slots { get; } = new List<WeaponSlot>();
    }

    public class WeaponSlot
    {
        public string SlotId { get; set; } = string.Empty;
        public WeaponComponent Weapon { get; set; } = new WeaponComponent();
        public Vector2 LocalOffset { get; set; } = Vector2.Zero;
        public float FacingOffsetRadians { get; set; } = 0f;
        public bool Enabled { get; set; } = true;
    }

    /// <summary>
    /// Projectile state updated by ProjectileSystem.
    /// </summary>
    public class ProjectileComponent : Component
    {
        public float Damage { get; set; }
        public float Speed { get; set; }
        public float MaxRange { get; set; }
        public Vector2 Origin { get; set; }
        public float TTL { get; set; }
        public int OwnerId { get; set; }
        public float ShieldPierceRatio { get; set; } = 0f;
    }

    /// <summary>
    /// Simple energy pool with regen, used by weapons/shields/abilities.
    /// </summary>
    public class EnergyComponent : Component
    {
        public float Capacity { get; set; } = 100f;
        public float Current { get; set; } = 100f;
        public float RegenPerSecond { get; set; } = 5f;

        public override void Update(float deltaTime)
        {
            Current = Math.Min(Capacity, Current + RegenPerSecond * deltaTime);
        }

        public bool Consume(float amount)
        {
            if (amount <= 0f) return true;
            if (Current + 0.0001f < amount) return false;
            Current -= amount;
            return true;
        }
    }

    /// <summary>
    /// Shields absorb damage before hull; recharges over time after a delay.
    /// </summary>
    public class ShieldComponent : Component
    {
        public float MaxShield { get; set; } = 100f;
        public float CurrentShield { get; set; } = 100f;
        public float RechargePerSecond { get; set; } = 10f;
        public float RechargeDelay { get; set; } = 2f;
        public float DamageMitigationRatio { get; set; } = 0f; // 0..1 reduces incoming dmg
        private float _cooldown;

        public override void Initialize()
        {
            CurrentShield = Math.Min(CurrentShield, MaxShield);
        }

        public override void Update(float deltaTime)
        {
            if (CurrentShield >= MaxShield) return;

            if (_cooldown > 0f)
            {
                _cooldown = Math.Max(0f, _cooldown - deltaTime);
                return;
            }

            CurrentShield = Math.Min(MaxShield, CurrentShield + RechargePerSecond * deltaTime);
        }

        public float AbsorbDamage(float incoming, float shieldPierceRatio = 0f)
        {
            if (incoming <= 0f || CurrentShield <= 0f) return incoming;

            float mitigated = incoming * (1f - DamageMitigationRatio);
            float pierce = mitigated * Math.Clamp(shieldPierceRatio, 0f, 1f);
            float toShield = mitigated - pierce;

            float absorbed = Math.Min(CurrentShield, toShield);
            CurrentShield -= absorbed;
            _cooldown = RechargeDelay;

            float remaining = mitigated - absorbed + pierce;
            return remaining;
        }
    }

    /// <summary>
    /// Cargo component for storing wares and inventory
    /// </summary>
    public class CargoComponent : Component
    {
        public float Credits { get; set; } = 0f;
        public float MaxVolume { get; set; } = 100f;
        public float CurrentVolume { get; private set; } = 0f;
        public Dictionary<string, int> Inventory { get; } = new();

        public bool CanAdd(string wareId, int quantity, float volumePerUnit)
        {
            float volumeNeeded = quantity * volumePerUnit;
            return CurrentVolume + volumeNeeded <= MaxVolume;
        }

        public bool Add(string wareId, int quantity, float volumePerUnit)
        {
            if (!CanAdd(wareId, quantity, volumePerUnit))
                return false;

            if (Inventory.ContainsKey(wareId))
                Inventory[wareId] += quantity;
            else
                Inventory[wareId] = quantity;

            CurrentVolume += quantity * volumePerUnit;
            return true;
        }

        public bool Remove(string wareId, int quantity, float volumePerUnit)
        {
            if (!Inventory.ContainsKey(wareId) || Inventory[wareId] < quantity)
                return false;

            Inventory[wareId] -= quantity;
            if (Inventory[wareId] <= 0)
                Inventory.Remove(wareId);

            CurrentVolume -= quantity * volumePerUnit;
            CurrentVolume = Math.Max(0f, CurrentVolume);
            return true;
        }

        public int GetQuantity(string wareId)
        {
            return Inventory.TryGetValue(wareId, out var qty) ? qty : 0;
        }

        public bool Contains(string wareId, int quantity)
        {
            return GetQuantity(wareId) >= quantity;
        }

        public float GetFreeVolume()
        {
            return MaxVolume - CurrentVolume;
        }

        public float GetUsedPercent()
        {
            return CurrentVolume / MaxVolume;
        }
    }

    /// <summary>
    /// Navigation component for pathfinding state
    /// </summary>
    public class NavigationComponent : Component
    {
        public List<Vector2Int> Path { get; set; } = new();
        public int CurrentWaypointIndex { get; set; } = 0;
        public Vector2Int? Destination { get; set; }
        public bool IsNavigating => Path.Count > 0 && CurrentWaypointIndex < Path.Count;

        public Vector2Int? GetCurrentWaypoint()
        {
            if (IsNavigating)
                return Path[CurrentWaypointIndex];
            return null;
        }

        public void AdvanceWaypoint()
        {
            if (IsNavigating)
                CurrentWaypointIndex++;
        }

        public void ClearPath()
        {
            Path.Clear();
            CurrentWaypointIndex = 0;
            Destination = null;
        }

        public void SetPath(List<Vector2Int> newPath, Vector2Int destination)
        {
            Path = newPath;
            CurrentWaypointIndex = 0;
            Destination = destination;
        }
    }
}

