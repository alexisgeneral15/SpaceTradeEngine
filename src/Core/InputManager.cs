using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Input;

namespace SpaceTradeEngine.Core
{
    /// <summary>
    /// Handles all input (keyboard, mouse, gamepad)
    /// </summary>
    public class InputManager
    {
        private KeyboardState _currentKeyState;
        private KeyboardState _previousKeyState;
        
        private MouseState _currentMouseState;
        private MouseState _previousMouseState;
        
        private GamePadState _currentGamePadState;
        private GamePadState _previousGamePadState;

        public void Update()
        {
            _previousKeyState = _currentKeyState;
            _currentKeyState = Keyboard.GetState();
            
            _previousMouseState = _currentMouseState;
            _currentMouseState = Mouse.GetState();
            
            _previousGamePadState = _currentGamePadState;
            _currentGamePadState = GamePad.GetState(Microsoft.Xna.Framework.PlayerIndex.One);
        }

        // Keyboard input
        public bool IsKeyDown(Keys key) => _currentKeyState.IsKeyDown(key);
        
        public bool IsKeyPressed(Keys key) =>
            _currentKeyState.IsKeyDown(key) && !_previousKeyState.IsKeyDown(key);
        
        public bool IsKeyReleased(Keys key) =>
            !_currentKeyState.IsKeyDown(key) && _previousKeyState.IsKeyDown(key);

        // Mouse input
        public int MouseX => _currentMouseState.X;
        public int MouseY => _currentMouseState.Y;
        
        public bool IsMouseLeftDown => _currentMouseState.LeftButton == ButtonState.Pressed;
        public bool IsMouseLeftClicked => 
            _currentMouseState.LeftButton == ButtonState.Pressed && 
            _previousMouseState.LeftButton == ButtonState.Released;
        
        public bool IsMouseRightDown => _currentMouseState.RightButton == ButtonState.Pressed;
        public bool IsMouseRightClicked =>
            _currentMouseState.RightButton == ButtonState.Pressed && 
            _previousMouseState.RightButton == ButtonState.Released;

        public int MouseScrollDelta => _currentMouseState.ScrollWheelValue - _previousMouseState.ScrollWheelValue;

        // GamePad input
        public bool IsGamePadConnected => _currentGamePadState.IsConnected;
        
        public Microsoft.Xna.Framework.Vector2 GetGamePadLeftStick() =>
            _currentGamePadState.ThumbSticks.Left;
        
        public Microsoft.Xna.Framework.Vector2 GetGamePadRightStick() =>
            _currentGamePadState.ThumbSticks.Right;
        
        public float GetGamePadLeftTrigger() => _currentGamePadState.Triggers.Left;
        public float GetGamePadRightTrigger() => _currentGamePadState.Triggers.Right;
    }
}
