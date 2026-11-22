using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>화면 진단 표시를 켜고 끄는 탭. 기본은 전부 꺼짐 (2026-11-04).</summary>
	public sealed class DisplayDevMode : IDevMode
	{
		public string Id => "display";
		public string DisplayName => "표시";
		public VisualElement Root { get; } = new VisualElement();

		private readonly Toggle fpsToggle;
		private readonly Toggle buildStampToggle;
		private readonly RadioButtonGroup dungeonPitchGroup;
		private readonly RadioButtonGroup spawnPolicyGroup;

		private static readonly SpawnPlacementPolicy[] SPAWN_POLICIES = { SpawnPlacementPolicy.ForwardCone, SpawnPlacementPolicy.OffscreenRing };
		private static readonly string[] SPAWN_POLICY_LABELS = { "앞쪽 (기본)", "화면 밖 둘레" };

		public DisplayDevMode()
		{
			fpsToggle = new Toggle("FPS 숫자");
			fpsToggle.RegisterValueChangedCallback(changed => UIFPS.IsVisible = changed.newValue);
			Root.Add(fpsToggle);

			buildStampToggle = new Toggle("빌드 정보");
			buildStampToggle.RegisterValueChangedCallback(changed => BuildStampOverlay.SetVisible(changed.newValue));
			Root.Add(buildStampToggle);

			// 던전 카메라 각도. 이번 실행에서만 (저장 안 함). 시야 경계는 각도와 무관하게 그대로
			dungeonPitchGroup = new RadioButtonGroup("던전 카메라 각도");
			dungeonPitchGroup.RegisterValueChangedCallback(changed => SelectDungeonPitch(changed.newValue));
			Root.Add(dungeonPitchGroup);

			// 적 생성 위치. 지금 판에서만, 다음 판은 던전 자산 값 (콘솔 threat spawn 과 같은 길). 어느 쪽이 좋을지 비교용
			spawnPolicyGroup = new RadioButtonGroup("적 생성 위치 (지금 판)", SPAWN_POLICY_LABELS.ToList());
			spawnPolicyGroup.RegisterValueChangedCallback(changed => SelectSpawnPolicy(changed.newValue));
			Root.Add(spawnPolicyGroup);
		}

		public void OnActivate()
		{
			fpsToggle.SetValueWithoutNotify(UIFPS.IsVisible);
			buildStampToggle.SetValueWithoutNotify(BuildStampOverlay.IsVisible);
			RefreshDungeonPitch();
			RefreshSpawnPolicy();
		}

		private void RefreshSpawnPolicy()
		{
			MonsterSpawner spawner = CurrentSpawner();
			spawnPolicyGroup.SetEnabled(spawner != null);
			if (spawner == null)
				return;
			spawnPolicyGroup.SetValueWithoutNotify(System.Array.IndexOf(SPAWN_POLICIES, spawner.ThreatPolicy.SpawnPolicy));
		}

		private static void SelectSpawnPolicy(int index)
		{
			MonsterSpawner spawner = CurrentSpawner();
			if (spawner == null || index < 0 || index >= SPAWN_POLICIES.Length)
				return;
			spawner.SetThreatPolicy(spawner.ThreatPolicy.With(SPAWN_POLICIES[index], spawner.ThreatPolicy.RetirePolicy));
		}

		// 던전 안일 때만. 밖이면 null
		private static MonsterSpawner CurrentSpawner() =>
			DungeonManager.Instance != null && DungeonManager.Instance.IsDungeon ? DungeonManager.Instance.MonsterSpawner : null;

		private void RefreshDungeonPitch()
		{
			CameraManager cameraManager = CameraManager.Instance;
			dungeonPitchGroup.SetEnabled(cameraManager != null);
			if (cameraManager == null)
				return;
			List<float> choices = cameraManager.DungeonPitchChoices.ToList();
			dungeonPitchGroup.choices = choices.Select(pitch => $"{pitch:0}도").ToList();
			int current = choices.FindIndex(pitch => Mathf.Approximately(pitch, cameraManager.DungeonPitch));
			dungeonPitchGroup.SetValueWithoutNotify(current);
		}

		private static void SelectDungeonPitch(int index)
		{
			CameraManager cameraManager = CameraManager.Instance;
			if (cameraManager == null || index < 0 || index >= cameraManager.DungeonPitchChoices.Count)
				return;
			cameraManager.SetDungeonPitch(cameraManager.DungeonPitchChoices[index]);
		}

		public void OnDeactivate() { }
	}
}
