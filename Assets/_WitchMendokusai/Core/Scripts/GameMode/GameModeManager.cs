using System;
using UnityEngine;

namespace WitchMendokusai
{
	public enum GameMode
	{
		Default = 0,
		Build = 1,

		// SimCity Phase 1 (TASK-WM-164): 도시 빌더 모드. append-only (slot C WM-165 Arena 와 enum 값
		// 합의 — session-bus 2026-05-30 21:00. Arena 는 그 다음 값).
		Zone = 2,
		Road = 3,

		// 마계 투기장 (TASK-WM-165): 전술코딩 오토배틀러 관전 모드. (session-bus 합의 = 4)
		Arena = 4,

		// SimCity Phase 3 (TASK-WM-176): 발전소(전력원) 페인트 모드.
		Power = 5,
	}

	public class GameModeManager : MonoBehaviour
	{
		public static GameModeManager Instance { get; private set; }

		public static bool TryGetExistingInstance(out GameModeManager mgr)
		{
			mgr = Instance;
			return mgr != null;
		}

		private void Awake()
		{
			if (Instance != null && Instance != this)
			{
				Destroy(gameObject);
				return;
			}
			Instance = this;
		}

		private void OnDestroy()
		{
			if (Instance == this)
				Instance = null;
		}

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
