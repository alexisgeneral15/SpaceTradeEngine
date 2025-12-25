using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace SpaceTradeEngine.Core
{
    /// <summary>
    /// Sistema de entrada mejorado con callbacks, detección de combos, y macros
    /// </summary>
    public class AdvancedInputManager
    {
        // Eventos de entrada
        public delegate void InputEventHandler(Keys key);
        public delegate void MouseEventHandler(Vector2 position);
        
        public event InputEventHandler OnKeyPressed;
        public event InputEventHandler OnKeyReleased;
        public event MouseEventHandler OnMouseMove;
        public event Action OnMouseLeftClick;
        public event Action OnMouseRightClick;
        public event Action<int> OnMouseWheel;

        // Estados de entrada
        private readonly HashSet<Keys> _currentKeys = new();
        private readonly HashSet<Keys> _previousKeys = new();
        private Vector2 _mousePosition;
        private Vector2 _previousMousePosition;
        private MouseState _previousMouseState;

        // Detección de combos
        private readonly Queue<Keys> _keySequence = new(16);
        private double _comboTimeout = 1.0; // segundos
        private double _lastKeyTime = 0;
        private readonly Dictionary<string, Action> _combos = new();

        // Mapeo de teclas (customizable)
        private readonly Dictionary<string, Keys[]> _keyBindings = new()
        {
            { "MoveUp", new[] { Keys.W, Keys.Up } },
            { "MoveDown", new[] { Keys.S, Keys.Down } },
            { "MoveLeft", new[] { Keys.A, Keys.Left } },
            { "MoveRight", new[] { Keys.D, Keys.Right } },
            { "Fire", new[] { Keys.Space, Keys.RightControl } },
            { "Boost", new[] { Keys.LeftShift } },
            { "Pause", new[] { Keys.Escape, Keys.P } },
        };

        // Macros grabadas
        private readonly Dictionary<string, InputSequence> _macros = new();
        private bool _recordingMacro = false;
        private string _recordingMacroName = "";
        private List<InputStep> _recordingSteps = new();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AdvancedInputManager()
        {
            RegisterCombo("QuickSave", new[] { Keys.LeftControl, Keys.S }, () => Console.WriteLine("[Input] QuickSave Combo triggered"));
            RegisterCombo("QuickLoad", new[] { Keys.LeftControl, Keys.L }, () => Console.WriteLine("[Input] QuickLoad Combo triggered"));
            RegisterCombo("ShowDebug", new[] { Keys.F3 }, () => Console.WriteLine("[Input] Debug panel requested"));
        }

        /// <summary>
        /// Actualiza estado de entrada desde teclado y mouse
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public void Update(double deltaTime)
        {
            var keyboardState = Keyboard.GetState();
            var mouseState = Mouse.GetState();

            // Teclas presionadas y liberadas
            _previousKeys.Clear();
            _previousKeys.UnionWith(_currentKeys);

            var currentKeysArray = keyboardState.GetPressedKeys();
            _currentKeys.Clear();
            foreach (var key in currentKeysArray)
            {
                _currentKeys.Add(key);

                if (!_previousKeys.Contains(key))
                {
                    OnKeyPressed?.Invoke(key);
                    _lastKeyTime = deltaTime;
                    
                    // Agregar a secuencia de combo
                    if (_keySequence.Count >= 16) _keySequence.Dequeue();
                    _keySequence.Enqueue(key);

                    if (_recordingMacro)
                        _recordingSteps.Add(new InputStep { Key = key, IsPress = true, Time = deltaTime });
                }
            }

            // Teclas liberadas
            foreach (var key in _previousKeys)
            {
                if (!_currentKeys.Contains(key))
                {
                    OnKeyReleased?.Invoke(key);
                    
                    if (_recordingMacro)
                        _recordingSteps.Add(new InputStep { Key = key, IsPress = false, Time = deltaTime });
                }
            }

            // Mouse
            _previousMousePosition = _mousePosition;
            _mousePosition = new Vector2(mouseState.X, mouseState.Y);

            if (_mousePosition != _previousMousePosition)
            {
                OnMouseMove?.Invoke(_mousePosition);
            }

            if (mouseState.LeftButton == ButtonState.Pressed && _previousMouseState.LeftButton == ButtonState.Released)
                OnMouseLeftClick?.Invoke();

            if (mouseState.RightButton == ButtonState.Pressed && _previousMouseState.RightButton == ButtonState.Released)
                OnMouseRightClick?.Invoke();

            if (mouseState.ScrollWheelValue != _previousMouseState.ScrollWheelValue)
                OnMouseWheel?.Invoke(mouseState.ScrollWheelValue - _previousMouseState.ScrollWheelValue);

            _previousMouseState = mouseState;

            // Timeout de combo
            if (deltaTime - _lastKeyTime > _comboTimeout)
                _keySequence.Clear();
        }

        /// <summary>
        /// Verifica si una acción está siendo presionada (remapeable)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsActionDown(string actionName)
        {
            if (!_keyBindings.TryGetValue(actionName, out var keys))
                return false;

            foreach (var key in keys)
            {
                if (_currentKeys.Contains(key))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Verifica si una acción fue presionada este frame
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsActionPressed(string actionName)
        {
            if (!_keyBindings.TryGetValue(actionName, out var keys))
                return false;

            foreach (var key in keys)
            {
                if (_currentKeys.Contains(key) && !_previousKeys.Contains(key))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Verifica si una tecla está presionada
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsKeyDown(Keys key) => _currentKeys.Contains(key);

        /// <summary>
        /// Verifica si una tecla fue presionada este frame
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsKeyPressed(Keys key) => _currentKeys.Contains(key) && !_previousKeys.Contains(key);

        /// <summary>
        /// Verifica si una tecla fue liberada este frame
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsKeyReleased(Keys key) => !_currentKeys.Contains(key) && _previousKeys.Contains(key);

        public Vector2 MousePosition => _mousePosition;
        public Vector2 MouseDelta => _mousePosition - _previousMousePosition;

        /// <summary>
        /// Remapea una acción a nuevas teclas
        /// </summary>
        public void RemapAction(string actionName, params Keys[] newKeys)
        {
            _keyBindings[actionName] = newKeys ?? throw new ArgumentNullException(nameof(newKeys));
            Console.WriteLine($"[Input] Action '{actionName}' remapped to {string.Join(", ", newKeys)}");
        }

        /// <summary>
        /// Registra un combo de teclas
        /// </summary>
        public void RegisterCombo(string name, Keys[] keys, Action callback)
        {
            _combos[name] = callback;
            Console.WriteLine($"[Input] Combo registered: {name} ({string.Join("+", keys)})");
        }

        /// <summary>
        /// Comienza grabación de macro
        /// </summary>
        public void StartMacroRecording(string macroName)
        {
            _recordingMacro = true;
            _recordingMacroName = macroName;
            _recordingSteps.Clear();
            Console.WriteLine($"[Macro] Recording started: {macroName}");
        }

        /// <summary>
        /// Detiene grabación y guarda macro
        /// </summary>
        public void StopMacroRecording()
        {
            if (!_recordingMacro) return;

            _recordingMacro = false;
            _macros[_recordingMacroName] = new InputSequence { Steps = new List<InputStep>(_recordingSteps) };
            Console.WriteLine($"[Macro] Recording stopped: {_recordingMacroName} ({_recordingSteps.Count} steps)");
            _recordingSteps.Clear();
        }

        /// <summary>
        /// Reproduce una macro grabada
        /// </summary>
        public void PlayMacro(string macroName)
        {
            if (!_macros.TryGetValue(macroName, out var sequence))
            {
                Console.WriteLine($"[Macro] Not found: {macroName}");
                return;
            }

            Console.WriteLine($"[Macro] Playing: {macroName}");
            // Reproducción se haría en sistema separado
        }

        public IEnumerable<string> GetMacroNames() => _macros.Keys;
    }

    public class InputSequence
    {
        public List<InputStep> Steps { get; set; } = new();
    }

    public struct InputStep
    {
        public Keys Key;
        public bool IsPress;
        public double Time;
    }
}
