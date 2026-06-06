using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WitchMendokusai.EditorTools
{
	// 빌더 배치/파괴(TASK-WM-181) PlayMode 자율 behavior-verify — 사용자 0클릭. WMGreenhousePlayVerify 동형.
	// WM Play 는 MCP 브리지를 wedge 시키므로 이 하네스가 *에디터 안에서* 스스로: Play 진입 → World 준비 대기 →
	// 빌드모드 진입(GameModeManager.SetMode) → 실 경로 배치(GridData.AddBuildingAt + BuildManager.SpawnBuildingObject) →
	// 콜라이더 부착 확인 → 카메라를 건물 위로 정렬 + 실 좌클릭 제거 경로(BuildManager.TryRemoveCell, raycast→
	// GetComponentInParent<BuildingObject>→RemoveBuildingAt→Despawn) 구동 → 제거 확인 → 스크린샷 → auto-exit.
	// Editor.log 가 ground-truth. [[wm-playmode-autoverify-bootready-gate]] 패턴. 하드 타임아웃으로 공유 에디터 보호.
	//
	// 사적 멤버(SpawnBuildingObject / TryRemoveCell / MouseScreenPosition setter / isPointerOverUI) = reflection
	// (검증 도구 — 프로덕션 코드 미오염). 배치 로직(우클릭 ClickCell)은 기존 user-behavior-verified.
	[InitializeOnLoad]
	public static class WMBuilderPlayVerify
	{
		private const string ARM_PREF = "WM_BUILDER_PLAYVERIFY_ARMED";
		private const string TAG = "[BUILD-PLAY-7c1]";
		private const double SETTLE_SECONDS = 2.0;
		private const double HARD_TIMEOUT = 45.0;

		private static double playStart;
		private static double readyAt = -1.0;
		private static bool ran;

		static WMBuilderPlayVerify()
		{
			EditorApplication.playModeStateChanged += OnPlayModeChanged;
		}

		[MenuItem("WM/Verify/빌더 배치파괴 Play 자율검증")]
		public static void Arm()
		{
			EditorPrefs.SetBool(ARM_PREF, true);
			Debug.Log(TAG + " armed — Play 진입");
			EditorApplication.EnterPlaymode();
		}

		private static void OnPlayModeChanged(PlayModeStateChange change)
		{
			if (change == PlayModeStateChange.EnteredPlayMode && EditorPrefs.GetBool(ARM_PREF, false))
			{
				EditorPrefs.SetBool(ARM_PREF, false);
				playStart = EditorApplication.timeSinceStartup;
				readyAt = -1.0;
				ran = false;
				EditorApplication.update += Tick;
				Debug.Log(TAG + " EnteredPlayMode — World 대기 시작");
			}
		}

		private static void Tick()
		{
			double now = EditorApplication.timeSinceStartup;

			if (now - playStart > HARD_TIMEOUT)
			{
				Debug.LogError(TAG + " TIMEOUT — World 미준비 또는 행. Play 강제 종료.");
				Finish();
				return;
			}

			if (ran)
				return;

			// World + 매니저 준비 게이트.
			Scene active = SceneManager.GetActiveScene();
			bool worldReady = active.IsValid() && active.name == "World" && active.isLoaded;
			bool managersReady = BuildManager.Instance != null
				&& GameModeManager.Instance != null
				&& InputManager.Instance != null
				&& StageManager.TryGetExistingInstance(out StageManager stageManager)
				&& stageManager.CurStage is WorldStage
				&& Camera.main != null;

			if (worldReady == false || managersReady == false)
				return;

			if (readyAt < 0.0)
			{
				readyAt = now;
				return;
			}
			if (now - readyAt < SETTLE_SECONDS)
				return;

			ran = true;
			RunVerify();
		}

		private static void RunVerify()
		{
			try
			{
				BuildManager buildManager = BuildManager.Instance;
				GameModeManager gameModeManager = GameModeManager.Instance;
				InputManager inputManager = InputManager.Instance;
				StageManager.TryGetExistingInstance(out StageManager stageManager);
				WorldStage worldStage = stageManager.CurStage as WorldStage;

				// ── 1. 빌드모드 진입 (ApplyMode → Click0/Click1 핸들러 등록 + 그리드 활성) ──
				gameModeManager.SetMode(GameMode.Build);
				bool buildModeOn = gameModeManager.IsBuildMode;

				// ── 2. 테스트 건물 선택 = 연구소(콜라이더 없던 worst-case) 우선, 없으면 첫 Building ──
				Building building = PickTestBuilding();
				if (building == null)
				{
					Debug.LogError(TAG + " FAIL — Building SO 미로드 (SOManager 텅)");
					Finish();
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
					&& collider.size.x > 0.0001f && collider.size.y > 0.0001f && collider.size.z > 0.0001f;

				// ── 4. 카메라를 건물 위로 정렬 + 마우스 화면중앙 → 실 좌클릭 제거(TryRemoveCell) 구동 ──
				bool controlRayHit = false;
				string hitName = "-";
				bool removed = false;
				if (placed && hasCollider)
				{
					Camera camera = Camera.main;
					Vector3 buildingCenter = collider.bounds.center;
					camera.transform.position = buildingCenter + new Vector3(0f, 12f, 0f);
					camera.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
					Physics.SyncTransforms();

					Vector2 screenCenter = new(Screen.width * 0.5f, Screen.height * 0.5f);
					SetPrivateProperty(inputManager, "MouseScreenPosition", screenCenter);
					SetPrivateField(inputManager, "isPointerOverUI", false); // UI 가드 비결정성 제거

					// 컨트롤 raycast(진단): TryRemoveCell 과 동일 ray 가 정말 이 건물을 맞나
					Ray ray = camera.ScreenPointToRay(screenCenter);
					if (Physics.Raycast(ray, out RaycastHit hit, 200f, ~0, QueryTriggerInteraction.Ignore))
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
				Debug.Log(TAG + (loopOk ? " LOOP OK ✅" : " LOOP FAIL ❌")
					+ " building=" + building.name + " cell=" + cell
					+ " buildModeOn=" + buildModeOn + " placed=" + placed + " hasCollider=" + hasCollider
					+ " controlRayHit=" + controlRayHit + " hitName=" + hitName + " removed=" + removed);

				string shot = "Temp/builder-play-verify.png";
				ScreenCapture.CaptureScreenshot(shot);
				Debug.Log(TAG + " screenshot → " + shot);
			}
			catch (Exception e)
			{
				Debug.LogError(TAG + " EXCEPTION — " + e.GetType().Name + ": " + e.Message);
			}
			finally
			{
				Finish();
			}
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
				if (dataSO is Building b == false)
					continue;
				if (first == null)
					first = b;
				if (b.name.Contains("연구소") && b.Prefab != null)
					return b; // worst-case (콜라이더 없던 건물) 우선
			}
			return first;
		}

		// 기존 건물·플레이어 영역 회피: 멀찍이 떨어진 셀에서 빈 칸 탐색 (z=0 = 지면 레벨).
		private static Vector3Int FindFreeCell(BuildManager buildManager)
		{
			for (int x = 500; x < 560; x++)
			{
				Vector3Int cell = new(x, 0, 0);
				if (buildManager.BuildingObjectsByPos.ContainsKey(cell) == false)
					return cell;
			}
			return new Vector3Int(500, 0, 0);
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

		private static void Finish()
		{
			EditorApplication.update -= Tick;
			if (EditorApplication.isPlaying)
				EditorApplication.ExitPlaymode();
		}
	}
}
