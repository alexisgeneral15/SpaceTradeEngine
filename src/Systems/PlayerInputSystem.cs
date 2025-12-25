using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using SpaceTradeEngine.ECS;
using SpaceTradeEngine.ECS.Components;
using SpaceTradeEngine.Core;

namespace SpaceTradeEngine.Systems
{
    /// <summary>
    /// Handles player keyboard and mouse input for ship movement, rotation, and combat.
    /// Implements WASD throttle control, mouse aiming, and spacebar firing.
    /// </summary>
    public class PlayerInputSystem : ECS.System
    {
        private readonly InputManager _inputManager;
        private readonly RenderingSystem _renderingSystem;

        public PlayerInputSystem(InputManager inputManager, RenderingSystem renderingSystem)
        {
            _inputManager = inputManager;
            _renderingSystem = renderingSystem;
        }

        protected override bool ShouldProcess(Entity entity)
        {
            // Only process entities with player control component
            return entity.HasComponent<PlayerControlComponent>();
        }

        public override void Update(float deltaTime)
        {
            foreach (var entity in _entities)
            {
                ProcessPlayerInput(entity, deltaTime);
            }
        }

        private void ProcessPlayerInput(Entity entity, float deltaTime)
        {
            var playerControl = entity.GetComponent<PlayerControlComponent>();
            var transform = entity.GetComponent<TransformComponent>();
            var velocity = entity.GetComponent<VelocityComponent>();
            var weapon = entity.GetComponent<WeaponComponent>();

            if (playerControl == null || transform == null || velocity == null)
                return;

            // 1. Handle rotation with A/D keys or mouse
            HandleRotation(transform, playerControl, deltaTime);

            // 2. Handle acceleration with W/S keys
            HandleThrottle(transform, velocity, playerControl, deltaTime);

            // 3. Handle firing with Space or Left Mouse Button
            HandleFiring(entity, weapon, deltaTime);

            // 4. Handle camera follow toggle
            if (_inputManager.IsKeyPressed(Keys.F))
            {
                // Will be handled by GameEngine's camera system
            }
        }

        private void HandleRotation(TransformComponent transform, PlayerControlComponent playerControl, float deltaTime)
        {
            float rotationDelta = 0f;

            // Keyboard rotation (A/D) - direct control
            if (_inputManager.IsKeyDown(Keys.A))
                rotationDelta -= playerControl.RotationSpeed * deltaTime;
            if (_inputManager.IsKeyDown(Keys.D))
                rotationDelta += playerControl.RotationSpeed * deltaTime;

            // Mouse-based rotation: only if NOT using keyboard rotation
            if (rotationDelta == 0f)
            {
                var mouseScreen = new Vector2(_inputManager.MouseX, _inputManager.MouseY);
                var mouseWorld = _renderingSystem.ScreenToWorld(mouseScreen);
                var directionToMouse = mouseWorld - transform.Position;
                if (directionToMouse.LengthSquared() > 1f) // Only if mouse is away from ship
                {
                    float targetRotation = (float)Math.Atan2(directionToMouse.Y, directionToMouse.X);
                    rotationDelta = LerpAngle(transform.Rotation, targetRotation, playerControl.RotationSpeed * deltaTime * 2f) - transform.Rotation;
                }
            }

            transform.Rotation += rotationDelta;
        }

        private void HandleThrottle(TransformComponent transform, VelocityComponent velocity, PlayerControlComponent playerControl, float deltaTime)
        {
            float throttle = 0f;

            // Forward thrust (W)
            if (_inputManager.IsKeyDown(Keys.W))
                throttle = 1f;

            // Reverse thrust (S)
            if (_inputManager.IsKeyDown(Keys.S))
                throttle = -0.5f;

            // Apply acceleration in ship's forward direction
            var shipForward = new Vector2((float)Math.Cos(transform.Rotation), (float)Math.Sin(transform.Rotation));
            var acceleration = shipForward * playerControl.Acceleration * throttle;

            velocity.LinearVelocity += acceleration * deltaTime;

            // Apply speed limit
            float speed = velocity.LinearVelocity.Length();
            if (speed > playerControl.MaxSpeed)
            {
                velocity.LinearVelocity = Vector2.Normalize(velocity.LinearVelocity) * playerControl.MaxSpeed;
            }

            // Apply drag (friction)
            velocity.LinearVelocity *= 0.98f;
        }

        private void HandleFiring(Entity entity, WeaponComponent weapon, float deltaTime)
        {
            if (weapon == null)
                return;

            // Reduce cooldown
            weapon.CooldownRemaining = Math.Max(0, weapon.CooldownRemaining - deltaTime);

            // Fire when spacebar is pressed or left mouse is held
            bool shouldFire = _inputManager.IsKeyDown(Keys.Space) || _inputManager.IsMouseLeftDown;

            if (shouldFire && weapon.CooldownRemaining <= 0f)
            {
                var transform = entity.GetComponent<TransformComponent>();
                if (transform != null)
                {
                    weapon.CooldownRemaining = weapon.Cooldown;
                }
            }
        }

        /// <summary>
        /// Smoothly interpolate between two angles
        /// </summary>
        private float LerpAngle(float from, float to, float amount)
        {
            float diff = to - from;
            while (diff > MathHelper.Pi) diff -= MathHelper.TwoPi;
            while (diff < -MathHelper.Pi) diff += MathHelper.TwoPi;
            return from + diff * Math.Min(amount, 1f);
        }
    }
}
