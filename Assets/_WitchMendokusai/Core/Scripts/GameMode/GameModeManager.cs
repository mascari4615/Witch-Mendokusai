using System;

namespace WitchMendokusai
{
	public enum GameMode
	{
		Default = 0,
		Build = 1,
	}

	public class GameModeManager : Singleton<GameModeManager>
	{
		public event Action<GameMode> OnModeChanged = delegate { };

		public GameMode CurrentMode { get; private set; } = GameMode.Default;

		public bool IsBuildMode => CurrentMode == GameMode.Build;

		public void SetMode(GameMode newMode)
		{
			if (CurrentMode == newMode)
				return;

			CurrentMode = newMode;
			OnModeChanged(newMode);
		}

		public void ToggleBuildMode()
		{
			SetMode(IsBuildMode ? GameMode.Default : GameMode.Build);
		}
	}
}
