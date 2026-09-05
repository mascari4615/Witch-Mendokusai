using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Vector3 = WitchMendokusai.Numerics.Vector3;
using Vector2Int = WitchMendokusai.Numerics.Vector2Int;
using Vector3Int = WitchMendokusai.Numerics.Vector3Int;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace WitchMendokusai.EditorTools
{
	// TowerDefensePlayVerify 의 화면과 창 확인 부분. 같은 클래스의 partial 조각이다. 상태(필드)는 TowerDefensePlayVerify.cs 를 본다.
	public static partial class TowerDefensePlayVerify
	{
		private static void LogHudState()
		{
			UIRoot uiRoot = Object.FindAnyObjectByType<UIRoot>();
			// ★ 개척 HUD 는 **ModeHudLayer** 에 붙는다(본편 HUD 를 통째 숨겨도 살아남아야 해서 한 단 위 층).
			//   검사기는 옛 층(OverlayLayer)을 보고 있어서 **매번 HUD-FAIL 을 뱉었다** — 화면엔 멀쩡히
			//   떠 있는데 확인 도구만 못 찾는 상태다. 이런 실패가 상시로 뜨면 사람이 로그를 통째로 무시하게 된다.
			// HudLayer 를 보던 예전 assert 는 그 설계 변경 이후로 항상 실패하는 죽은 검사였다.
			if (uiRoot == null || uiRoot.ModeHudLayer == null)
			{
				Debug.LogError(TAG + " HUD-FAIL UIRoot/ModeHudLayer 없음");
				return;
			}

			VisualElement hud = uiRoot.ModeHudLayer.Q(nameof(TowerDefenseHudView));
			if (hud == null)
			{
				Debug.LogError(TAG + " HUD-FAIL ModeHudLayer 에 TowerDefenseHudView 없음");
				return;
			}

			string statusText = string.Empty;
			foreach (Label label in hud.Query<Label>().ToList())
			{
				if (string.IsNullOrEmpty(label.text) == false)
				{
					statusText = label.text;
					break;
				}
			}
			Debug.Log(TAG + " HUD visible=" + (hud.style.display.value == DisplayStyle.Flex)
				+ " text=\"" + statusText + "\"");
		}

		// 자원 노드 표식 — 안 보이면 채집 인형을 어디 지을지 알 수 없다.
		private static void LogNodeMarkers(Transform stageRoot)
		{
			int markers = 0;
			foreach (Transform child in stageRoot)
			{
				if (child.name == "ResourceNode")
					markers++;
			}
			Debug.Log(TAG + " NODE-MARKERS count=" + markers);
		}

		/// <summary>
		/// 티메토 허브 라이브 확인 (TASK-WM-195) — 씬의 티메토 NPCObject 를 찾아 실제 대화 진입점
		/// `OnInteract()` 를 호출하고, 허브 패널이 열려 미니게임 목록이 렌더됐는지 본다.
		/// 에디터에서 데이터만 보는 건 "말 걸면 뜬다"의 증명이 아니다(사용자가 실제로 못 찾은 사례).
		/// </summary>
		private static void VerifyTimetoHub()
		{
			NPCObject[] npcs = Object.FindObjectsByType<NPCObject>(FindObjectsInactive.Include);
			NPCObject timeto = null;
			foreach (NPCObject npc in npcs)
			{
				if (npc.Data != null && npc.Data.ID == 7)
				{
					timeto = npc;
					break;
				}
			}

			if (timeto == null)
			{
				Debug.LogError(TAG + " HUB-FAIL 씬에 티메토(NPC ID 7) 없음 — 씬 스캔 " + npcs.Length + "명");
				return;
			}

			List<MinigameEntrySO> entries = NPCUtil.GetMinigameEntries(timeto.Data);
			Debug.Log(TAG + " HUB-NPC 티메토 발견 panels=" + string.Join(",", timeto.Data.GetPanelTypeList())
				+ " entries=" + entries.Count);

			// 실제 대화 진입점 — 플레이어가 상호작용했을 때와 동일 경로.
			timeto.OnInteract();

			// 대화 메뉴에 「시뮬레이션 콘솔」 선택지가 실제로 켜졌는지 = 사용자가 도달 가능한가의 핵심.
			// 메뉴 버튼은 NPCPanelType.Count 만큼 동적 생성되고 NPC 의 PanelInfos 로 활성 여부가 갈린다.
			UINPCMenu menu = Object.FindAnyObjectByType<UINPCMenu>(FindObjectsInactive.Include);
			if (menu == null)
			{
				Debug.LogError(TAG + " HUB-MENU UINPCMenu 없음");
			}
			else
			{
				int activeOptions = 0;
				foreach (UISlot slot in menu.GetComponentsInChildren<UISlot>(true))
				{
					if (slot.gameObject.activeSelf && slot.Index == (int)NPCPanelType.Hub)
						activeOptions++;
				}
				Debug.Log(TAG + " HUB-MENU hubOptionActive=" + (activeOptions > 0));
			}

			UIRoot uiRoot = Object.FindAnyObjectByType<UIRoot>();
			VisualElement hubPanel = uiRoot != null && uiRoot.ScreenLayer != null
				? uiRoot.ScreenLayer.Q(nameof(UIMinigameHubToolkit))
				: null;
			Debug.Log(TAG + " HUB-PANEL exists=" + (hubPanel != null)
				+ " buttons=" + (hubPanel != null ? hubPanel.Query<Button>().ToList().Count : -1));

			// 허브를 닫고 원래 흐름(TD 모드 진입)으로 복귀 — 패널이 열린 채면 입력이 UI 에 묶인다.
			if (UIManagerHubCloseSafe(uiRoot) == false)
				Debug.LogWarning(TAG + " HUB 패널 닫기 실패 — 이후 검증이 UI 에 막힐 수 있음");
		}

		private static bool UIManagerHubCloseSafe(UIRoot uiRoot)
		{
			UIManager uiManager = Object.FindAnyObjectByType<UIManager>();
			if (uiManager == null || uiManager.NPC == null)
				return false;
			uiManager.NPC.ClosePanel();
			return true;
		}

		/// <summary>
		/// 카메라 실측 — "GameObject 가 active" 는 *화면에 보인다*의 증명이 아니다(사용자 실증:
		/// camActive=True 였는데 화면은 그대로였음). 실제로 무엇이 렌더되는지는 enabled + depth +
		/// URP renderType(Base/Overlay) + Camera.main 이 함께 결정하므로 전부 찍는다.
		/// </summary>
		private static void DumpCameras(string phase)
		{
			// ⚠ Unity 콘솔 리더는 멀티라인 로그의 *첫 줄만* 준다 → 카메라마다 별도 Debug.Log 로 찍어야
			//   원격 콘솔에서 전부 읽힘. 한 줄로 몰면 진단 통째 유실(실측)
			Camera[] cameras = Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude);
			Debug.Log(TAG + " CAMERAS(" + phase + ") count=" + cameras.Length
				+ " main=" + (Camera.main != null ? Camera.main.name : "NULL"));

			Camera topmost = null;
			foreach (Camera camera in cameras)
			{
				string renderType = "n/a";
				UnityEngine.Rendering.Universal.UniversalAdditionalCameraData urpData =
					camera.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
				if (urpData != null)
					renderType = urpData.renderType.ToString();

				Debug.Log(TAG + " CAM[" + phase + "] " + camera.name
					+ " enabled=" + camera.enabled
					+ " active=" + camera.gameObject.activeInHierarchy
					+ " depth=" + camera.depth
					+ " urpType=" + renderType
					+ " pos=" + camera.transform.position);

				if (camera.enabled == false)
					continue;
				if (topmost == null || camera.depth > topmost.depth)
					topmost = camera;
			}

			// ★ 개척은 이제 **정식 content 카메라**(vcam priority)다 — "카메라를 하나 더 켰나"가 아니라
			//   ① 렌더 카메라가 하나뿐인가 ② content 모드가 개척으로 바뀌었나 ③ 개척 vcam 이 등록·구동 중인가
			//   를 봐야 한다. 예전 assert(ModeCamera 가 최상위인가)는 구조가 바뀌어 무의미해졌다.
			Debug.Log(TAG + " CAM-RENDER[" + phase + "] renderCameraCount=" + cameras.Length
				+ " topmost=" + (topmost != null ? topmost.name + "(depth " + topmost.depth + ")" : "none"));

			CameraManager cameraManager = CameraManager.Instance;
			if (cameraManager == null)
			{
				Debug.LogError(TAG + " CAM-MODE[" + phase + "] CameraManager.Instance NULL — 카메라 리그 자체가 없음.");
				return;
			}

			Debug.Log(TAG + " CAM-MODE[" + phase + "] contentMode=" + cameraManager.CurrentContentMode
				+ " isFreePosition=" + cameraManager.IsFreePositionMode);

			// 개척 vcam 실재/등록 확인 — 없으면 SetContentCameraMode 가 First() 에서 터지거나 무시된다.
			MCamera[] allVcams = Object.FindObjectsByType<MCamera>(FindObjectsInactive.Include);
			bool foundTowerDefenseVcam = false;
			foreach (MCamera vcam in allVcams)
			{
				Debug.Log(TAG + " VCAM[" + phase + "] " + vcam.name
					+ " contentMode=" + vcam.ContentCameraMode
					// priority 는 Cinemachine 타입이라 Editor asmdef 에서 직접 못 읽는다 — 대신 활성/좌표로 판별.
					+ " active=" + vcam.gameObject.activeInHierarchy
					+ " pos=" + vcam.transform.position);
				if (vcam.ContentCameraMode == ContentCameraMode.TowerDefense)
					foundTowerDefenseVcam = true;
			}

			if (foundTowerDefenseVcam == false)
				Debug.LogError(TAG + " VCAM-MISS[" + phase + "] 개척 vcam(ContentCameraMode.TowerDefense) 이 씬에 없음 — Camera 프리팹 자식 Camera_TowerDefense 확인 필요.");
			else if (cameraManager.CurrentContentMode != ContentCameraMode.TowerDefense && phase.Contains("진입"))
				Debug.LogError(TAG + " CAM-MODE-MISS[" + phase + "] 개척 진입인데 contentMode=" + cameraManager.CurrentContentMode + " — 전환 실패.");
		}

		/// <summary>
		/// UI 위 클릭이 설치로 새지 않는지 — 「HUD 버튼을 눌렀는데 그 아래 지면에 건물이 선다」 회귀 방지.
		/// 하네스는 실제 마우스를 못 누르므로 *판정 함수*를 진실의 기준으로 검사한다:
		/// ① HUD 버튼이 차지한 화면 좌표에서 UI 위라고 답하는가 ② 빈 지면 좌표에선 아니라고 답하는가.
		/// 버튼의 화면 좌표는 변환식을 역산하지 않고 화면을 성기게 훑어 구한다(같은 식을 두 번 쓰면
		/// 자기 자신을 검증하는 꼴이라 의미가 없다).
		/// </summary>
		private static void VerifyUiPointerGuard()
		{
			UIRoot uiRoot = Object.FindAnyObjectByType<UIRoot>();
			VisualElement hud = uiRoot != null && uiRoot.ModeHudLayer != null
				? uiRoot.ModeHudLayer.Q(nameof(TowerDefenseHudView))
				: null;
			// ★ 「첫 번째 버튼」으로 고르면 판 상태에 따라 *다른 버튼*이 잡힌다 — 실제로 전체 실행에서만
			//   위쪽 버튼이 잡혀 좌표를 못 찾고 실패했다(배치만 실행에서는 늘 아래쪽 버튼이라 통과).
			//   순서가 아니라 **이름**으로 고른다. 같은 함정을 이 저장소에서 여러 번 밟았다.
			Button button = hud != null ? hud.Q<Button>("RestartButton") : null;
			if (button == null && hud != null)
				button = hud.Q<Button>();
			if (button == null)
			{
				Debug.LogError(TAG + " UIGUARD-FAIL HUD 버튼을 못 찾음 — 판정 검사 불가.");
				return;
			}

			Rect buttonPanelRect = button.worldBound;
			// 어느 버튼을 골랐는지 남긴다 — 못 찾았을 때 「무엇을 재려 했나」가 없으면 원인을 못 짚는다.
			Debug.Log($"{TAG} UIGUARD 대상 버튼 = 「{button.name}」/「{button.text}」 rect={buttonPanelRect}");
			const int SAMPLE_COLUMNS = 96;
			const int SAMPLE_ROWS = 54;
			Vector2 buttonScreenPoint = new Vector2(-1f, -1f);

			for (int column = 0; column <= SAMPLE_COLUMNS && buttonScreenPoint.x < 0f; column++)
			{
				for (int row = 0; row <= SAMPLE_ROWS; row++)
				{
					Vector2 candidate = new Vector2(
						Screen.width * column / (float)SAMPLE_COLUMNS,
						Screen.height * row / (float)SAMPLE_ROWS);
					Vector2 panelPoint = RuntimePanelUtils.ScreenToPanel(
						uiRoot.Root.panel, new Vector2(candidate.x, Screen.height - candidate.y));
					if (buttonPanelRect.Contains(panelPoint))
					{
						buttonScreenPoint = candidate;
						break;
					}
				}
			}

			if (buttonScreenPoint.x < 0f)
			{
				Debug.LogError(TAG + " UIGUARD-FAIL 버튼의 화면 좌표를 못 찾음 buttonRect=" + buttonPanelRect);
				return;
			}

			bool overButton = UIPointer.IsOverInteractive(buttonScreenPoint);
			// 화면 정중앙 = 개척지 한복판. HUD 는 모서리에 있으므로 여기는 반드시 설치 가능해야 한다.
			bool overGround = UIPointer.IsOverInteractive(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));

			string verdict = TAG + " UIGUARD button=" + overButton + " ground=" + overGround
				+ " buttonScreen=" + buttonScreenPoint + " buttonText=" + button.text;

			if (overButton && overGround == false)
				Debug.Log(verdict + " → UI 위는 막고 지면은 통과 ✔");
			else
				Debug.LogError(verdict + " → UI 클릭이 설치로 새거나(button=False) 지면이 막힌다(ground=True).");
		}

		// 화면 어디에 무엇이 놓였나 — 겹치면 안 되는 덩어리들. 전면(배너·드래프트)은 *덮는 것이 일*이라 뺀다.
		//
		// ★ UnitTooltip 은 뺐다 — 그건 HUD 층이 아니라 **TooltipLayer** 에 붙는다(커서를 따라다니며
		//   무엇이든 덮어야 하는 물건이라 층이 다르다). HUD 층에서 찾으니 매번 「조각이 없음」이 떴고,
		//   그 상시 경고가 진짜 신호를 덮는다. 겹침 검사 대상도 아니다 — 덮는 것이 그 물건의 일이다.
		private static readonly string[] HUD_BLOCKS =
		{
			"ResourceBar", "ProgressPanel", "LegendPanel", "TowerDefenseSelectionBar",
			"HintBar", "RestartButton", "BoonSummary", "SelectionPanel", "Minimap",
		};

		/// <summary>
		/// 그 덩어리가 실제로 *차지한* 자리 — 전폭 띠는 껍데기가 화면을 가로지르지만 알맹이는 가운데
		/// 한 줌뿐이다. 껍데기로 재면 「가운데 띠가 좌측 범례를 가린다」 같은 거짓 겹침이 무더기로 나온다.
		/// 그래서 화면 폭을 거의 다 쓰는 껍데기는 *보이는 자식들이 실제로 덮은 범위*로 좁혀 잰다.
		/// </summary>
		private static Rect ContentBound(VisualElement block, float screenWidth)
		{
			Rect bound = block.worldBound;
			if (screenWidth <= 1f || bound.width < screenWidth * 0.95f)
				return bound;

			Rect union = Rect.zero;
			bool any = false;
			foreach (VisualElement child in block.Children())
			{
				if (child.resolvedStyle.display != DisplayStyle.Flex)
					continue;
				Rect childBound = ContentBound(child, screenWidth);
				if (childBound.width <= 1f || childBound.height <= 1f)
					continue;

				union = any ? Rect.MinMaxRect(
					Mathf.Min(union.xMin, childBound.xMin), Mathf.Min(union.yMin, childBound.yMin),
					Mathf.Max(union.xMax, childBound.xMax), Mathf.Max(union.yMax, childBound.yMax)) : childBound;
				any = true;
			}

			return any ? union : bound;
		}

		/// <param name="mustBeUp">
		/// 이 상태에서 *반드시 떠 있어야* 하는 조각. 안 떠 있으면 실패다.
		/// ★ 없으면 「안 뜬 것은 겹칠 수도 없어서 겹침 0 이 「띄워본 적이 없다」를 숨긴다」가 그대로 일어난다 —
		///   실측에서 평상시·건물 선택 중·툴팁·코어 선택 중이 **네 상태 모두 똑같은 5개**를 재고 있었다.
		///   네 번 다 초록불이었지만 잰 것은 한 번뿐이었던 셈이다.
		/// </param>
		private static void VerifyHudLayout(string phase, string mustBeUp = null)
		{
			UIRoot uiRoot = Object.FindAnyObjectByType<UIRoot>();
			VisualElement hud = uiRoot != null && uiRoot.ModeHudLayer != null
				? uiRoot.ModeHudLayer.Q(nameof(TowerDefenseHudView))
				: null;
			if (hud == null)
			{
				Debug.LogError(TAG + " HUD-LAYOUT[" + phase + "] HUD 를 못 찾음");
				return;
			}

			if (mustBeUp != null && hud.Q(mustBeUp) == null)
			{
				Debug.LogError(TAG + " HUD-LAYOUT[" + phase + "]-FAIL 「" + mustBeUp
					+ "」가 안 떠 있다 — 띄운 줄 알고 잰 것이라 이 판정은 무의미하다.");
			}

			List<string> names = new();
			List<Rect> rects = new();
			foreach (string blockName in HUD_BLOCKS)
			{
				VisualElement block = hud.Q(blockName);
				if (block == null)
				{
					Debug.LogError(TAG + " HUD-LAYOUT[" + phase + "] 조각이 없음: " + blockName + " — 이름이 바뀌었거나 안 붙었다.");
					continue;
				}
				if (block.resolvedStyle.display != DisplayStyle.Flex)
					continue; // 지금 안 보이는 것은 겹칠 수도 없다.

				Rect bound = ContentBound(block, hud.worldBound.width);
				if (bound.width <= 1f || bound.height <= 1f)
					continue; // 아직 배치 전(폭 0) — 겹침 판정 대상이 아니다.

				names.Add(blockName);
				rects.Add(bound);
			}

			Rect screen = hud.worldBound;
			int overlaps = 0;
			for (int left = 0; left < rects.Count; left++)
			{
				if (screen.width > 1f && screen.Contains(new Vector2(rects[left].xMin + 1f, rects[left].yMin + 1f)) == false)
					Debug.LogError(TAG + " HUD-OFFSCREEN[" + phase + "] " + names[left] + " " + rects[left] + " 가 화면(" + screen + ") 밖으로 나감");

				for (int right = left + 1; right < rects.Count; right++)
				{
					if (rects[left].Overlaps(rects[right]) == false)
						continue;
					overlaps++;
					Debug.LogError(TAG + " HUD-OVERLAP[" + phase + "] " + names[left] + rects[left]
						+ " ↔ " + names[right] + rects[right]);
				}
			}

			string verdict = TAG + " HUD-LAYOUT[" + phase + "] blocks=" + names.Count + " overlaps=" + overlaps
				+ " [" + string.Join(",", names) + "]";
			if (overlaps == 0)
				Debug.Log(verdict + " → 겹치는 덩어리 없음 ✔");
		}
	}
}
