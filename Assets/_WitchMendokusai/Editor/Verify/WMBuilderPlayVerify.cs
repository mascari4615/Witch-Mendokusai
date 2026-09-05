using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
// ★ 좌표는 판정 쪽 (TASK-WM-214) — 검증 스크립트도 게임과 같은 타입으로 말해야 한다.
using Vector3Int = WitchMendokusai.Numerics.Vector3Int;
using Vector3 = WitchMendokusai.Numerics.Vector3;

namespace WitchMendokusai.EditorTools
{
	/// <summary>
	/// 빌더 배치/파괴(TASK-WM-181) PlayMode 자율 behavior-verify — 사용자 0클릭. lifecycle = <see cref="WMPlayVerifyBase"/>.
	///
	/// 빌드모드 진입(GameModeManager.SetMode) → 실 경로 배치(GridData.AddBuildingAt + BuildManager.SpawnBuildingObject)
	/// → 콜라이더 부착 확인 → 카메라를 건물 위로 정렬 + 실 좌클릭 제거 경로(BuildManager.TryRemoveCell: raycast →
	/// GetComponentInParent&lt;BuildingObject&gt; → RemoveBuildingAt → Despawn) 구동 → 제거 확인.
	///
	/// 사적 멤버(SpawnBuildingObject / TryRemoveCell / MouseScreenPosition setter / isPointerOverUI) = reflection
	/// (검증 도구가 production 에 test-seam 안 뚫고 실 코드경로 구동 — 정당). 배치(우클릭 ClickCell)는 기존 user-verified.
	/// </summary>
	[InitializeOnLoad]
	public sealed class WMBuilderPlayVerify : WMPlayVerifyBase
	{
		private const float CAMERA_OVERHEAD = 12f;          // 건물 위 카메라 높이(탑다운 raycast)
		private const float RAY_DISTANCE = 200f;            // control raycast 도달거리(건물까지 ~12 이상이면 충분)
		private const float COLLIDER_MIN_SIZE = 0.0001f;    // 비퇴화 콜라이더 판정 임계
		private const int FREE_CELL_SCAN_START = 500;       // 기존 건물·플레이어서 멀찍이(빈 셀 스캔)
		private const int FREE_CELL_SCAN_END = 560;
		private const string SCREENSHOT_PATH = "Temp/builder-play-verify.png";

		private static readonly WMBuilderPlayVerify Instance = new();
		static WMBuilderPlayVerify() { }

		[MenuItem("WM/Verify/Builder Place-Destroy Play Verify")]
		private static void ArmFromMenu() => Instance.Arm();

		protected override string ArmPref => "WM_BUILDER_PLAYVERIFY_ARMED";
		protected override string Tag => "[BUILD-PLAY-7c1]";

		protected override bool IsReady()
		{
			return SceneIsWorld()
				&& BuildManager.Instance != null
				&& GameModeManager.Instance != null
				&& InputManager.Instance != null
				&& StageManager.TryGetExistingInstance(out StageManager stageManager)
				&& stageManager.CurStage is WorldStage
				&& Camera.main != null;
		}

		protected override void RunVerify()
		{
			BuildManager buildManager = BuildManager.Instance;
			GameModeManager gameModeManager = GameModeManager.Instance;
			InputManager inputManager = InputManager.Instance;
			StageManager.TryGetExistingInstance(out StageManager stageManager);
			WorldStage worldStage = stageManager.CurStage as WorldStage;

			// ── 1. 빌드모드 진입 (ApplyMode → Click0/Click1 핸들러 등록 + 그리드 활성) ──
			gameModeManager.SetMode(GameMode.Build);
			bool buildModeOn = gameModeManager.IsBuildMode;

			// ── 2. 테스트 건물 = 연구소(콜라이더 없던 worst-case) 우선, 없으면 첫 Building ──
			Building building = PickTestBuilding();
			if (building == null)
			{
				Log("FAIL — Building SO 미로드 (SOManager 텅)");
				return;
			}

			// ── 3. 빈 셀 골라 실 경로 배치 (ClickCell 의 두 단계: AddBuildingAt + SpawnBuildingObject) ──
			Vector3Int cell = FindFreeCell(buildManager);
			worldStage.GridData.AddBuildingAt(cell, new BuildingInstanceData(building.ID));
			BuildingInstanceData data = worldStage.GridData.BuildingData[cell];
			InvokePrivate(buildManager, "SpawnBuildingObject", new object[] { cell, data });

			bool placed = buildManager.BuildingObjectsByPos.TryGetValue(cell, out BuildingObject buildingObject)
				&& buildingObject != null;
			BoxCollider collider = placed ? buildingObject.GetComponent<BoxCollider>() : null;
			bool hasCollider = collider != null
				&& collider.size.x > COLLIDER_MIN_SIZE && collider.size.y > COLLIDER_MIN_SIZE && collider.size.z > COLLIDER_MIN_SIZE;

			// ── 4. 카메라를 건물 위로 정렬 + 마우스 화면중앙 → 실 좌클릭 제거(TryRemoveCell) 구동 ──
			bool controlRayHit = false;
			string hitName = "-";
			bool removed = false;
			if (placed && hasCollider)
			{
				Camera camera = Camera.main;
				Vector3 buildingCenter = collider.bounds.center.ToSim();
				camera.transform.position = (buildingCenter + new Vector3(0f, CAMERA_OVERHEAD, 0f)).ToUnity();
				// 탑다운: 시선 아래(down), 화면 상단 = +Z (LookRotation 의 up 인자에 forward 전달).
				camera.transform.rotation = Quaternion.LookRotation(Vector3.down.ToUnity(), Vector3.forward.ToUnity());
				Physics.SyncTransforms();

				Vector2 screenCenter = new(Screen.width * 0.5f, Screen.height * 0.5f);
				SetPrivateProperty(inputManager, "MouseScreenPosition", screenCenter);
				SetPrivateField(inputManager, "isPointerOverUI", false); // UI 가드 비결정성 제거

				// 컨트롤 raycast(진단): TryRemoveCell 과 동일 ray 가 정말 이 건물을 맞나
				Ray ray = camera.ScreenPointToRay(screenCenter);
				if (Physics.Raycast(ray, out RaycastHit hit, RAY_DISTANCE, ~0, QueryTriggerInteraction.Ignore))
				{
					hitName = hit.collider.name;
					BuildingObject resolved = hit.collider.GetComponentInParent<BuildingObject>();
					controlRayHit = resolved == buildingObject;
				}

				// 실 제거 경로 구동
				InvokePrivate(buildManager, "TryRemoveCell", null);
				removed = buildManager.BuildingObjectsByPos.ContainsKey(cell) == false;
			}

			bool loopOk = buildModeOn && placed && hasCollider && controlRayHit && removed;
			Log((loopOk ? "LOOP OK ✅" : "LOOP FAIL ❌")
				+ " building=" + building.name + " cell=" + cell
				+ " buildModeOn=" + buildModeOn + " placed=" + placed + " hasCollider=" + hasCollider
				+ " controlRayHit=" + controlRayHit + " hitName=" + hitName + " removed=" + removed);

			ScreenCapture.CaptureScreenshot(SCREENSHOT_PATH);
			Log("screenshot → " + SCREENSHOT_PATH);
		}

		private static Building PickTestBuilding()
		{
			if (SOManagerBridge.HasInstance == false)
				return null;
			if (SOManagerBridge.DataSOs.TryGetValue(typeof(Building), out Dictionary<int, DataSO> buildings) == false || buildings.Count == 0)
				return null;

			Building first = null;
			foreach (DataSO dataSO in buildings.Values)
			{
				if (dataSO is Building building == false)
					continue;
				if (first == null)
					first = building;
				if (building.name.Contains("연구소") && building.Prefab != null)
					return building; // worst-case (콜라이더 없던 건물) 우선
			}
			return first;
		}

		// 기존 건물·플레이어 영역 회피: 멀찍이 떨어진 셀에서 빈 칸 탐색 (z=0 = 지면 레벨).
		private static Vector3Int FindFreeCell(BuildManager buildManager)
		{
			for (int x = FREE_CELL_SCAN_START; x < FREE_CELL_SCAN_END; x++)
			{
				Vector3Int cell = new(x, 0, 0);
				if (buildManager.BuildingObjectsByPos.ContainsKey(cell) == false)
					return cell;
			}
			return new Vector3Int(FREE_CELL_SCAN_START, 0, 0);
		}

		private static void InvokePrivate(object target, string methodName, object[] args)
		{
			MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
			if (method == null)
				throw new MissingMethodException(target.GetType().Name + "." + methodName);
			method.Invoke(target, args);
		}

		private static void SetPrivateProperty(object target, string propertyName, object value)
		{
			PropertyInfo property = target.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
			MethodInfo setter = property?.GetSetMethod(true);
			if (setter == null)
				throw new MissingMemberException(target.GetType().Name + "." + propertyName + " setter");
			setter.Invoke(target, new object[] { value });
		}

		private static void SetPrivateField(object target, string fieldName, object value)
		{
			FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
			if (field == null)
				throw new MissingFieldException(target.GetType().Name + "." + fieldName);
			field.SetValue(target, value);
		}
	}
}
