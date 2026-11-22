using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace WitchMendokusai
{
	// 던전 카메라일 때 전투 시야 경계 (CombatView) 와 그 경계를 가리는 선형 안개를 켬. 던전을 나가면 안개 방식을 되돌림
	// 안개 색과 하늘은 SkyDirector 그대로. 여기는 방식과 시작, 끝 깊이만 덮어씀 (SkyDirector 의 Update 뒤, 그리기 전)
	public partial class CameraManager
	{
		[Header("전투 시야 (던전)")]
		[Tooltip("던전 카메라 앞 이 깊이 (m) 까지만 화면 안으로 침. 적 생성과 반납, 화면 처치, 얼음, 장식 그리기가 같은 경계. 0 이면 끔")]
		[SerializeField, Min(0f)] private float combatViewMaxDepth = 36f;
		[Tooltip("던전에서 안개가 시작하는 깊이 (m). 캐릭터는 카메라에서 약 14m")]
		[SerializeField, Min(0f)] private float combatFogStartDepth = 22f;
		[Tooltip("던전에서 안개가 다 덮는 깊이 (m). 위 경계보다 짧아야 경계 밖에서 생긴 적이 안 보임")]
		[SerializeField, Min(0f)] private float combatFogEndDepth = 34f;

		[Tooltip("개발자 창 표시 탭에서 고르는 던전 카메라 각도 (도). 기본 각은 Camera.prefab 의 Camera_Dungeon 회전")]
		[SerializeField] private float[] dungeonPitchChoices = { 30f, 40f, 50f };

		public IReadOnlyList<float> DungeonPitchChoices => dungeonPitchChoices;
		public float DungeonPitch => DungeonCamera.BasePitch;
		public void SetDungeonPitch(float pitch) => DungeonCamera.SetBasePitch(pitch);
		private MCamera DungeonCamera => cameras.First(mCamera => mCamera.ContentCameraMode == ContentCameraMode.Dungeon);

		private bool combatViewApplied;
		private bool fogBeforeCombat;
		private FogMode fogModeBeforeCombat;
		private float fogStartBeforeCombat;
		private float fogEndBeforeCombat;

		private void UpdateCombatView()
		{
			// 모드는 블렌드 시작에 바로 바뀜. 블렌드 중에 켜고 끄면 마을 쪽 카메라 위치에서 화면이 안개로 덮이거나, 나갈 때 먼 쪽이 먼저 드러남
			if (cinemachineBrain.IsBlending)
				return;
			bool active = CurrentContentMode == ContentCameraMode.Dungeon && combatViewMaxDepth > 0f;
			if (active)
			{
				if (combatViewApplied == false)
				{
					fogBeforeCombat = RenderSettings.fog;
					fogModeBeforeCombat = RenderSettings.fogMode;
					fogStartBeforeCombat = RenderSettings.fogStartDistance;
					fogEndBeforeCombat = RenderSettings.fogEndDistance;
					combatViewApplied = true;
				}
				CombatView.SetMaxDepth(combatViewMaxDepth);
				RenderSettings.fog = true;
				RenderSettings.fogMode = FogMode.Linear;
				RenderSettings.fogStartDistance = combatFogStartDepth;
				RenderSettings.fogEndDistance = combatFogEndDepth;
				return;
			}
			ClearCombatView();
		}

		private void ClearCombatView()
		{
			if (combatViewApplied == false)
				return;
			combatViewApplied = false;
			CombatView.SetMaxDepth(0f);
			RenderSettings.fog = fogBeforeCombat;
			RenderSettings.fogMode = fogModeBeforeCombat;
			RenderSettings.fogStartDistance = fogStartBeforeCombat;
			RenderSettings.fogEndDistance = fogEndBeforeCombat;
		}

		private void OnDisable() => ClearCombatView();

		private void OnValidate()
		{
			if (combatViewMaxDepth > 0f && combatFogEndDepth >= combatViewMaxDepth)
				Debug.LogWarning($"[CameraManager] 안개 끝 {combatFogEndDepth} 이 전투 시야 경계 {combatViewMaxDepth} 이상. 경계 밖에서 생긴 적이 보임", this);
			if (combatFogStartDepth >= combatFogEndDepth)
				Debug.LogWarning($"[CameraManager] 안개 시작 {combatFogStartDepth} 이 끝 {combatFogEndDepth} 이상", this);
		}
	}
}
