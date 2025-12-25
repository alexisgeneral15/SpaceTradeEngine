using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Audio;
using SpaceTradeEngine.ECS;
using SpaceTradeEngine.Events;

#nullable enable
namespace SpaceTradeEngine.Systems
{
    /// <summary>
    /// Audio system using MonoGame SoundEffect and Song; subscribes to game events for audio cues.
    /// </summary>
    public class AudioSystem : ECS.System
    {
        // COMENTADO: AudioSystem no se usa - lazy init para ahorrar ~2-5MB
        private Dictionary<string, SoundEffect>? _soundEffects;
        private Dictionary<string, Song>? _songs;
        private readonly EventSystem _eventSystem;
        // ELIMINADO: _currentMusicInstance (AudioSystem comentado, ahorra warning CS0649)
        private float _masterVolume = 0.8f;
        private float _sfxVolume = 0.8f;
        private float _musicVolume = 0.6f;

        public AudioSystem(EventSystem eventSystem)
        {
            _eventSystem = eventSystem;

            // Subscribe to relevant events for audio cues
            _eventSystem.Subscribe<EntityDamagedEvent>(OnEntityDamaged);
            _eventSystem.Subscribe<CollisionEvent>(OnCollision);
            _eventSystem.Subscribe<EntityDestroyedEvent>(OnEntityDestroyed);
            _eventSystem.Subscribe<TradeCompletedEvent>(OnTradeCompleted);
        }

        protected override bool ShouldProcess(Entity entity)
        {
            // Audio system doesn't process entities directly
            return false;
        }

        public override void Update(float deltaTime)
        {
            // No per-frame updates needed beyond event subscriptions
        }

        public void LoadSoundEffect(string name, SoundEffect soundEffect)
        {
            _soundEffects ??= new();
            _soundEffects[name] = soundEffect;
        }

        public void LoadSong(string name, Song song)
        {
            _songs ??= new();
            _songs[name] = song;
        }

        public void PlaySoundEffect(string name, float volume = 1f, float pitch = 0f, float pan = 0f)
        {
            if (_soundEffects == null || !_soundEffects.TryGetValue(name, out var sfx)) return;
            sfx.Play(_masterVolume * _sfxVolume * volume, pitch, pan);
        }

        public void PlaySoundEffectInstance(string name, float volume = 1f, float pitch = 0f, float pan = 0f)
        {
            if (_soundEffects == null || !_soundEffects.TryGetValue(name, out var sfx)) return;
            var instance = sfx.CreateInstance();
            instance.Volume = _masterVolume * _sfxVolume * volume;
            instance.Pitch = pitch;
            instance.Pan = pan;
            instance.Play();
        }

        public void PlayMusic(string name, bool looping = true)
        {
            if (_songs == null || !_songs.TryGetValue(name, out var song)) return;

            // _currentMusicInstance?.Dispose(); // ELIMINADO
            // Note: MonoGame Song playback is handled via MediaPlayer, not direct instances
            // This is a simplified stub - use MediaPlayer.Play(song) in actual implementation
        }

        public void StopMusic()
        {
            // _currentMusicInstance?.Stop(); // ELIMINADO
        }

        public void SetMasterVolume(float volume) => _masterVolume = Math.Clamp(volume, 0f, 1f);
        public void SetSFXVolume(float volume) => _sfxVolume = Math.Clamp(volume, 0f, 1f);
        public void SetMusicVolume(float volume) => _musicVolume = Math.Clamp(volume, 0f, 1f);

        #region Event Handlers

        private void OnEntityDamaged(EntityDamagedEvent evt)
        {
            if (evt.Damage > 50f)
                PlaySoundEffect("damage_heavy", 0.6f);
            else
                PlaySoundEffect("damage_light", 0.4f);
        }

        private void OnCollision(CollisionEvent evt)
        {
            PlaySoundEffect("collision", 0.5f);
        }

        private void OnEntityDestroyed(EntityDestroyedEvent evt)
        {
            PlaySoundEffect("explosion", 0.8f);
        }

        private void OnTradeCompleted(TradeCompletedEvent evt)
        {
            // Trade events always represent successful transactions
            PlaySoundEffect("trade_success", 0.5f);
        }

        #endregion

        public void Dispose()
        {
            StopMusic();
            // _currentMusicInstance?.Dispose(); // ELIMINADO
            if (_soundEffects != null)
            {
                foreach (var sfx in _soundEffects.Values)
                    sfx.Dispose();
                _soundEffects.Clear();
            }
            if (_songs != null)
            {
                foreach (var song in _songs.Values)
                    song.Dispose();
                _songs.Clear();
            }
        }
    }
}
