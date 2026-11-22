using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace WitchMendokusai
{
	// 던전 카메라일 때 전투 시야 경계 (CombatView) 를 켬. 안개는 하늘 설정 (SkyDirector) 그대로, 여기서 안 건드림 (사용자 2026-11-22)
	public partial class CameraManager
	{
		[Header("전투 시야 (던전)")]
		[Tooltip("던전 카메라 앞 이 깊이 (m) 까지만 화면 안으로 침. 적 생성과 반납, 화면 처치, 얼음, 장식 그리기가 같은 경계. 0 이면 끔")]
		[SerializeField, Min(0f)] private float combatViewMaxDepth = 36f;

		[Tooltip("개발자 창 표시 탭에서 고르는 던전 카메라 각도 (도). 기본 각은 Camera.prefab 의 Camera_Dungeon 회전")]
		[SerializeField] private float[] dungeonPitchChoices = { 30f, 40f, 50f };

		public IReadOnlyList<float> DungeonPitchChoices => dungeonPitchChoices;
		public float DungeonPitch => DungeonCamera.BasePitch;
		public void SetDungeonPitch(float pitch) => DungeonCamera.SetBasePitch(pitch);
		private MCamera DungeonCamera => cameras.First(mCamera => mCamera.ContentCameraMode == ContentCameraMode.Dungeon);

		private void UpdateCombatView()
		{
			bool active = CurrentContentMode == ContentCameraMode.Dungeon;
			CombatView.SetMaxDepth(active ? combatViewMaxDepth : 0f);
		}

		private void OnDisable() => CombatView.SetMaxDepth(0f);
	}
}
