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

		// 특수시공 개척(TASK-WM-194): 실시간 타워디펜스 + 전초기지 건설 모드. append-only.
		TowerDefense = 6,
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

		// TASK-WM-165 item9 — 투기장 관전 모드 게이트 (IsBuildMode 대칭). IsSpectating 조건이 이 값 파생.
		public bool IsArenaMode => CurrentMode == GameMode.Arena;

		// TASK-WM-194 — 특수시공 개척(TD) 모드 게이트 (IsArenaMode 대칭). IsTowerDefenseMode 조건이 이 값 파생.
		public bool IsTowerDefenseMode => CurrentMode == GameMode.TowerDefense;

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

		// TASK-WM-165 item9 — 투기장 진입/이탈 토글 (ToggleBuildMode 대칭). 인게임 진입점이 호출.
		public void ToggleArenaMode()
		{
			SetMode(IsArenaMode ? GameMode.Default : GameMode.Arena);
		}

		// TASK-WM-194 — 특수시공 개척(TD) 진입/이탈 토글 (ToggleArenaMode 대칭). 인게임 진입점이 호출.
		public void ToggleTowerDefenseMode()
		{
			SetMode(IsTowerDefenseMode ? GameMode.Default : GameMode.TowerDefense);
		}
	}
}
