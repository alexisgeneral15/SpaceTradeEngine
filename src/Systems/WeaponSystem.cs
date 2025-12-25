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
    /// Weapon firing system: spawns projectiles from single weapons or slot groups, consuming energy/shields if present.
    /// </summary>
    public class WeaponSystem : ECS.System
    {
        private readonly EntityManager _entityManager;
        private readonly SpatialPartitioningSystem _spatialSystem;

        public WeaponSystem(EntityManager entityManager, SpatialPartitioningSystem spatialSystem)
        {
            _entityManager = entityManager;
            _spatialSystem = spatialSystem;
        }

        protected override bool ShouldProcess(Entity entity)
        {
            return entity.HasComponent<WeaponComponent>() || entity.HasComponent<WeaponSlotComponent>();
        }

        public override void Update(float deltaTime)
        {
            foreach (var e in _entities)
            {
                if (!e.IsActive) continue;

                var transform = e.GetComponent<TransformComponent>();
                var targeting = e.GetComponent<TargetingComponent>();
                var faction = e.GetComponent<FactionComponent>();
                var energy = e.GetComponent<EnergyComponent>();
                if (transform == null) continue;

                foreach (var entry in EnumerateWeapons(e))
                {
                    var weapon = entry.Weapon;
                    weapon.CooldownRemaining = Math.Max(0f, weapon.CooldownRemaining - deltaTime);
                    if (weapon.CooldownRemaining > 0f) continue;
                    if (!weapon.AutoFire) continue;

                    // Decide aim point
                    Vector2? targetPos = null;
                    if (targeting != null && targeting.CurrentTarget != null && targeting.IsInRange && targeting.HasLineOfSight)
                    {
                        targetPos = targeting.LeadPosition;
                    }
                    else if (weapon.FireWithoutTarget)
                    {
                        var forward = Facing(transform.Rotation + entry.FacingOffsetRadians);
                        targetPos = transform.Position + forward * weapon.Range;
                    }

                    if (targetPos == null) continue;
                    if (energy != null && !energy.Consume(weapon.EnergyCost)) continue;

                    FireProjectiles(e, weapon, faction, transform, entry.LocalOffset, entry.FacingOffsetRadians, targetPos.Value);
                }
            }
        }

        private IEnumerable<(WeaponComponent Weapon, Vector2 LocalOffset, float FacingOffsetRadians)> EnumerateWeapons(Entity e)
        {
            var slotComp = e.GetComponent<WeaponSlotComponent>();
            if (slotComp != null && slotComp.Slots.Count > 0)
            {
                foreach (var slot in slotComp.Slots)
                {
                    if (slot.Enabled)
                        yield return (slot.Weapon, slot.LocalOffset, slot.FacingOffsetRadians);
                }
                yield break;
            }

            var single = e.GetComponent<WeaponComponent>();
            if (single != null)
            {
                yield return (single, Vector2.Zero, 0f);
            }
        }

        private void FireProjectiles(Entity owner, WeaponComponent weapon, FactionComponent? faction, TransformComponent transform, Vector2 localOffset, float facingOffset, Vector2 targetPos)
        {
            var muzzle = transform.Position + Rotate(localOffset, transform.Rotation);
            var baseDir = Vector2.Normalize(targetPos - muzzle);
            if (baseDir == Vector2.Zero) baseDir = Facing(transform.Rotation + facingOffset);

            int count = Math.Max(1, weapon.ProjectilesPerShot);
            float spreadRad = MathHelper.ToRadians(weapon.SpreadDegrees);

            for (int i = 0; i < count; i++)
            {
                var dir = ApplySpread(baseDir, spreadRad, i, count);
                SpawnProjectile(owner, faction, weapon, muzzle, dir);
            }

            weapon.CooldownRemaining = weapon.Cooldown;
        }

        private void SpawnProjectile(Entity owner, FactionComponent? faction, WeaponComponent weapon, Vector2 origin, Vector2 direction)
        {
            var proj = _entityManager.CreateEntity("Projectile");
            proj.AddComponent(new TransformComponent { Position = origin });
            proj.AddComponent(new VelocityComponent { LinearVelocity = direction * weapon.ProjectileSpeed });
            proj.AddComponent(new CollisionComponent { Radius = weapon.ProjectileRadius, IsTrigger = true });
            proj.AddComponent(new TagComponent("projectile"));
            if (faction != null)
                proj.AddComponent(new FactionComponent(faction.FactionId, faction.FactionName));

            proj.AddComponent(new ProjectileComponent
            {
                Damage = weapon.Damage,
                MaxRange = weapon.Range,
                Speed = weapon.ProjectileSpeed,
                Origin = origin,
                OwnerId = owner.Id,
                TTL = weapon.ProjectileTTL,
                ShieldPierceRatio = weapon.ShieldPierceRatio
            });

            _entityManager.RefreshEntity(proj);
        }

        private static Vector2 Facing(float radians) => new((float)Math.Cos(radians), (float)Math.Sin(radians));

        private static Vector2 Rotate(Vector2 v, float radians)
        {
            float c = (float)Math.Cos(radians);
            float s = (float)Math.Sin(radians);
            return new Vector2(v.X * c - v.Y * s, v.X * s + v.Y * c);
        }

        private static Vector2 ApplySpread(Vector2 baseDir, float spreadRadians, int index, int total)
        {
            if (total == 1 || spreadRadians <= 0.0001f) return Vector2.Normalize(baseDir);
            float t = total == 1 ? 0f : (index / (float)(total - 1));
            float offset = MathHelper.Lerp(-spreadRadians * 0.5f, spreadRadians * 0.5f, t);
            return Vector2.Normalize(Rotate(baseDir, offset));
        }
    }
}
