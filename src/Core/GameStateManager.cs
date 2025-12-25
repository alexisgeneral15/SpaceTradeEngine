using System;

namespace SpaceTradeEngine.Core
{
    /// <summary>
    /// Manages game state transitions
    /// </summary>
    public enum GameState
    {
        MainMenu,
        Playing,
        Paused,
        SettingsMenu,
        GameOver,
        Loading
    }

    public class GameStateManager
    {
        public GameState CurrentState { get; private set; } = GameState.MainMenu;
        public GameState PreviousState { get; private set; }

        public event Action<GameState, GameState> OnStateChanged;

        public void ChangeState(GameState newState)
        {
            if (newState == CurrentState)
                return;

            PreviousState = CurrentState;
            CurrentState = newState;
            OnStateChanged?.Invoke(PreviousState, CurrentState);
        }
    }
}
