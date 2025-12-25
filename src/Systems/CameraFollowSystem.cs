using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using SpaceTradeEngine.ECS;
using SpaceTradeEngine.ECS.Components;
using SpaceTradeEngine.Core;
using System;

namespace SpaceTradeEngine.Systems
{
    /// <summary>
    /// Smooth camera follow system for player-controlled ships.
    /// Provides RTS-style camera panning with mouse drag support.
    /// </summary>
    public class CameraFollowSystem : ECS.System
    {
        private readonly RenderingSystem _renderingSystem;
        private readonly InputManager _inputManager;
        private Entity? _targetEntity;
        private Vector2 _targetPosition;
        private float _followSpeed = 150f; // Units per second
        private float _zoomTarget = 1f;
        private float _zoomSpeed = 2f;
        private bool _enabled = true;
        
        // Mouse panning
        private bool _isDraggingCamera = false;
        private Vector2 _lastMouseWorld;
        private Vector2 _cameraVelocity = Vector2.Zero;

        public Entity? TargetEntity => _targetEntity;
        public bool IsFollowing => _enabled && _targetEntity != null;

        public CameraFollowSystem(RenderingSystem renderingSystem, InputManager inputManager)
        {
            _renderingSystem = renderingSystem ?? throw new ArgumentNullException(nameof(renderingSystem));
            _inputManager = inputManager ?? throw new ArgumentNullException(nameof(inputManager));
        }

        public void SetTarget(Entity? entity)
        {
            _targetEntity = entity;
            if (entity != null)
            {
                var transform = entity.GetComponent<TransformComponent>();
                if (transform != null)
                {
                    _targetPosition = transform.Position;
                    _renderingSystem.SetCameraPosition(_targetPosition);
                }
            }
        }

        /// <summary>
        /// Toggle camera follow mode on/off.
        /// </summary>
        public void ToggleFollow() => _enabled = !_enabled;

        /// <summary>
        /// Set camera follow speed (units per second for interpolation).
        /// </summary>
        public void SetFollowSpeed(float speed) => _followSpeed = Math.Max(speed, 10f);

        /// <summary>
        /// Set target zoom level with smooth interpolation.
        /// </summary>
        public void SetZoomTarget(float zoom)
        {
            _zoomTarget = MathHelper.Clamp(zoom, 0.5f, 3f);
        }

        protected override bool ShouldProcess(Entity entity)
        {
            // This system doesn't process entities; it's just a manager
            return false;
        }

        public override void Update(float deltaTime)
        {
            HandleMousePanning(deltaTime);

            if (!_enabled || _targetEntity == null)
                return;

            // Get target position
            var transform = _targetEntity.GetComponent<TransformComponent>();
            if (transform == null)
                return;

            _targetPosition = transform.Position;

            // Smoothly move camera toward target
            Vector2 cameraPos = _renderingSystem.CameraPosition;
            Vector2 direction = _targetPosition - cameraPos;
            float distance = direction.Length();

            if (distance > 0.1f)
            {
                float maxMove = _followSpeed * deltaTime;
                if (distance <= maxMove)
                {
                    cameraPos = _targetPosition;
                }
                else
                {
                    cameraPos += Vector2.Normalize(direction) * maxMove;
                }
                _renderingSystem.SetCameraPosition(cameraPos);
            }

            // Smooth zoom interpolation
            float currentZoom = _renderingSystem.CameraZoom;
            if (Math.Abs(currentZoom - _zoomTarget) > 0.01f)
            {
                float newZoom = MathHelper.Lerp(currentZoom, _zoomTarget, _zoomSpeed * deltaTime);
                _renderingSystem.SetCameraZoom(newZoom);
            }
        }

        /// <summary>
        /// Handle mouse drag panning (RMB or MMB)
        /// </summary>
        private void HandleMousePanning(float deltaTime)
        {
            var mouseScreen = new Vector2(_inputManager.MouseX, _inputManager.MouseY);
            var mouseWorld = _renderingSystem.ScreenToWorld(mouseScreen);
            
            // Start dragging on right mouse button
            if (_inputManager.IsMouseRightDown)
            {
                if (!_isDraggingCamera)
                {
                    _isDraggingCamera = true;
                    _lastMouseWorld = mouseWorld;
                    _enabled = false; // Disable auto-follow while panning
                }
                else
                {
                    // Calculate drag delta in world space
                    Vector2 delta = _lastMouseWorld - mouseWorld;
                    Vector2 cameraPos = _renderingSystem.CameraPosition;
                    _renderingSystem.SetCameraPosition(cameraPos + delta);
                    
                    // Track velocity for momentum
                    _cameraVelocity = delta / deltaTime;
                    
                    // Update last position for next frame
                    _lastMouseWorld = _renderingSystem.ScreenToWorld(mouseScreen);
                }
            }
            else
            {
                // Stop dragging
                if (_isDraggingCamera)
                {
                    _isDraggingCamera = false;
                    // Apply momentum
                    if (_cameraVelocity.LengthSquared() > 10f)
                    {
                        Vector2 cameraPos = _renderingSystem.CameraPosition;
                        _renderingSystem.SetCameraPosition(cameraPos + _cameraVelocity * deltaTime * 0.2f);
                    }
                    _cameraVelocity *= 0.8f; // Decay
                }
            }
        }

        /// <summary>
        /// Get offset from camera to allow leading the camera ahead of fast-moving objects.
        /// Can be used for prediction-based following.
        /// </summary>
        public Vector2 GetLeadOffset(Entity? entity = null)
        {
            entity ??= _targetEntity;
            if (entity == null)
                return Vector2.Zero;

            var velocity = entity.GetComponent<VelocityComponent>();
            if (velocity == null)
                return Vector2.Zero;

            // Lead by 0.2 seconds of movement
            return velocity.LinearVelocity * 0.2f;
        }
    }
}
