using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace SpaceTradeEngine.Core
{
    /// <summary>
    /// Sistema de buffering de entrada para captura precisa sin pérdida.
    /// Permite input lag compensation y secuencias de comandos.
    /// </summary>
    public class InputBuffer
    {
        // Buffer circular de eventos de entrada
        private readonly InputEvent[] _eventBuffer;
        private int _bufferHead;
        private int _bufferTail;
        private const int BufferSize = 128; // Potencia de 2 para máscara rápida
        private const int BufferMask = BufferSize - 1;

        // Estado actual de entrada (post-procesado)
        private readonly HashSet<Keys> _keysDown = new();
        private readonly HashSet<Keys> _keysPressed = new();
        private readonly HashSet<Keys> _keysReleased = new();
        
        private Vector2 _mousePosition;
        private Vector2 _mouseDelta;
        private int _mouseWheelDelta;
        private bool _leftMouseDown;
        private bool _rightMouseDown;
        private bool _leftMousePressed;
        private bool _rightMousePressed;

        // Input prediction para juego en red (reducido a 15 frames para menos memoria)
        private readonly Queue<InputFrame> _inputHistory = new(15);
        private uint _currentFrame;

        public Vector2 MousePosition => _mousePosition;
        public Vector2 MouseDelta => _mouseDelta;
        public int MouseWheelDelta => _mouseWheelDelta;
        public bool IsMouseLeftDown => _leftMouseDown;
        public bool IsMouseRightDown => _rightMouseDown;
        public bool IsMouseLeftPressed => _leftMousePressed;
        public bool IsMouseRightPressed => _rightMousePressed;

        public InputBuffer()
        {
            _eventBuffer = new InputEvent[BufferSize];
            for (int i = 0; i < BufferSize; i++)
                _eventBuffer[i] = new InputEvent();
        }

        /// <summary>
        /// Captura entrada cruda y la agrega al buffer
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CaptureInput(KeyboardState keyboard, MouseState mouse, GamePadState gamepad)
        {
            // Buffer de teclado
            var pressedKeys = keyboard.GetPressedKeys();
            foreach (var key in pressedKeys)
            {
                if (!_keysDown.Contains(key))
                    AddEvent(InputEventType.KeyPress, (int)key);
            }

            // Detectar teclas liberadas
            var releasedKeys = new List<Keys>(_keysDown);
            foreach (var key in pressedKeys)
                releasedKeys.Remove(key);
            
            foreach (var key in releasedKeys)
                AddEvent(InputEventType.KeyRelease, (int)key);

            // Mouse
            var newMousePos = new Vector2(mouse.X, mouse.Y);
            if (newMousePos != _mousePosition)
            {
                AddEvent(InputEventType.MouseMove, mouse.X, mouse.Y);
            }

            if (mouse.LeftButton == ButtonState.Pressed && !_leftMouseDown)
                AddEvent(InputEventType.MouseLeftPress, mouse.X, mouse.Y);
            
            if (mouse.LeftButton == ButtonState.Released && _leftMouseDown)
                AddEvent(InputEventType.MouseLeftRelease, mouse.X, mouse.Y);

            if (mouse.RightButton == ButtonState.Pressed && !_rightMouseDown)
                AddEvent(InputEventType.MouseRightPress, mouse.X, mouse.Y);
            
            if (mouse.ScrollWheelValue != _mouseWheelDelta)
                AddEvent(InputEventType.MouseWheel, mouse.ScrollWheelValue);
        }

        /// <summary>
        /// Procesa todos los eventos del buffer y actualiza el estado
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ProcessBuffer()
        {
            _keysPressed.Clear();
            _keysReleased.Clear();
            _leftMousePressed = false;
            _rightMousePressed = false;
            _mouseDelta = Vector2.Zero;

            var prevMousePos = _mousePosition;

            // Procesar todos los eventos pendientes
            while (_bufferHead != _bufferTail)
            {
                ref var evt = ref _eventBuffer[_bufferTail];
                ProcessEvent(ref evt);
                _bufferTail = (_bufferTail + 1) & BufferMask;
            }

            _mouseDelta = _mousePosition - prevMousePos;

            // Guardar frame para predicción
            RecordInputFrame();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ProcessEvent(ref InputEvent evt)
        {
            switch (evt.Type)
            {
                case InputEventType.KeyPress:
                    var key = (Keys)evt.Data1;
                    _keysDown.Add(key);
                    _keysPressed.Add(key);
                    break;
                
                case InputEventType.KeyRelease:
                    key = (Keys)evt.Data1;
                    _keysDown.Remove(key);
                    _keysReleased.Add(key);
                    break;
                
                case InputEventType.MouseMove:
                    _mousePosition = new Vector2(evt.Data1, evt.Data2);
                    break;
                
                case InputEventType.MouseLeftPress:
                    _leftMouseDown = true;
                    _leftMousePressed = true;
                    break;
                
                case InputEventType.MouseLeftRelease:
                    _leftMouseDown = false;
                    break;
                
                case InputEventType.MouseRightPress:
                    _rightMouseDown = true;
                    _rightMousePressed = true;
                    break;
                
                case InputEventType.MouseRightRelease:
                    _rightMouseDown = false;
                    break;
                
                case InputEventType.MouseWheel:
                    _mouseWheelDelta = evt.Data1;
                    break;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AddEvent(InputEventType type, int data1 = 0, int data2 = 0)
        {
            ref var evt = ref _eventBuffer[_bufferHead];
            evt.Type = type;
            evt.Data1 = data1;
            evt.Data2 = data2;
            evt.Timestamp = DateTime.UtcNow.Ticks;
            
            _bufferHead = (_bufferHead + 1) & BufferMask;
            
            // Evitar overflow
            if (_bufferHead == _bufferTail)
                _bufferTail = (_bufferTail + 1) & BufferMask;
        }

        private void RecordInputFrame()
        {
            var frame = new InputFrame
            {
                FrameNumber = _currentFrame++,
                KeysDown = new HashSet<Keys>(_keysDown),
                MousePosition = _mousePosition,
                LeftMouseDown = _leftMouseDown,
                RightMouseDown = _rightMouseDown
            };

            _inputHistory.Enqueue(frame);
            
            // Mantener solo últimos 60 frames (1 segundo a 60fps)
            if (_inputHistory.Count > 60)
                _inputHistory.Dequeue();
        }

        // API de consulta pública
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsKeyDown(Keys key) => _keysDown.Contains(key);
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsKeyPressed(Keys key) => _keysPressed.Contains(key);
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsKeyReleased(Keys key) => _keysReleased.Contains(key);

        public void Clear()
        {
            _keysDown.Clear();
            _keysPressed.Clear();
            _keysReleased.Clear();
            _bufferHead = 0;
            _bufferTail = 0;
        }

        // Estructuras internas
        private struct InputEvent
        {
            public InputEventType Type;
            public int Data1;
            public int Data2;
            public long Timestamp;
        }

        private struct InputFrame
        {
            public uint FrameNumber;
            public HashSet<Keys> KeysDown;
            public Vector2 MousePosition;
            public bool LeftMouseDown;
            public bool RightMouseDown;
        }

        private enum InputEventType : byte
        {
            None,
            KeyPress,
            KeyRelease,
            MouseMove,
            MouseLeftPress,
            MouseLeftRelease,
            MouseRightPress,
            MouseRightRelease,
            MouseWheel
        }
    }
}
