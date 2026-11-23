using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// 마을 NPC 의 머리 위 퀘스트 표식과 발밑 이름표. UIRoot 가 만들어 HUD 층에 붙임
	/// 대상은 켜져 있는 NPCMarker 전부 (NPCMarker.Active). 투영은 InteractPromptView 와 같은 방식
	/// (WorldToScreenPoint 다음 RuntimePanelUtils.ScreenToPanel). UI Toolkit 층이라 캐릭터 위에 늘 그려짐
	/// 역할 표시는 가까이 갔을 때 InteractPromptView 가 맡음
	/// </summary>
	public class NPCNameplateView : MonoBehaviour
	{
		private const string USS_ROOT = "wm-npc-nameplate";
		// NPC 마다 흔들림 박자를 어긋나게 하는 간격 (황금비의 소수부, 몇 명이 와도 고르게 퍼짐)
		private const float BOB_PHASE_STEP = 0.618034f;

		public NPCNameplateSettings Settings { private get; set; }

		private sealed class Entry
		{
			public Label Name;
			public VisualElement Marker;
			public VisualElement Background;
			public VisualElement Flash;
			public VisualElement Icon;
			public NPC ShownData;
			public NPCQuestMark ShownMark = NPCQuestMark.None;
			public float BobPhase;
		}

		private readonly Dictionary<NPCMarker, Entry> entries = new();
		private readonly List<NPCMarker> staleMarkers = new();
		private VisualElement layer;

		private void OnDisable()
		{
			if (layer != null)
				layer.style.display = DisplayStyle.None;
		}

		private void OnDestroy()
		{
			layer?.RemoveFromHierarchy();
		}

		// UIRoot 재활성화 때 HUD 층이 새로 생기면 다시 붙임
		private bool EnsureBuilt()
		{
			if (UIRoot.Instance == null || UIRoot.Instance.HudLayer == null)
				return false;
			VisualElement hudLayer = UIRoot.Instance.HudLayer;
			if (layer != null && layer.parent == hudLayer)
				return true;

			layer?.RemoveFromHierarchy();
			entries.Clear();
			layer = new VisualElement { name = "NPCNameplates", pickingMode = PickingMode.Ignore };
			layer.AddToClassList(USS_ROOT + "-layer");
			layer.styleSheets.Add(Settings.Style);
			// HUD 층 맨 앞에 넣어 상호작용 표시와 다른 HUD 보다 아래에 그림
			hudLayer.Insert(0, layer);
			return true;
		}

		private void LateUpdate()
		{
			if (EnsureBuilt() == false)
				return;

			Camera camera = Camera.main;
			PlayerObject player = PlayerProvider.Instance != null ? PlayerProvider.Instance.CurrentObject : null;
			if (camera == null || player == null || layer.panel == null || IsHidden())
			{
				layer.style.display = DisplayStyle.None;
				return;
			}
			layer.style.display = DisplayStyle.Flex;

			RemoveStaleEntries();
			Vector3 playerPosition = player.transform.position;
			foreach (NPCMarker marker in NPCMarker.Active)
			{
				// 풀에서 꺼낸 NPC 는 데이터가 켜진 뒤에 들어옴. 들어올 때까지 그리지 않음
				if (marker.NPC.Data == null)
					continue;
				if (entries.TryGetValue(marker, out Entry entry) == false)
				{
					entry = CreateEntry(marker);
					entries.Add(marker, entry);
				}
				UpdateEntry(marker, entry, camera, playerPosition);
			}
		}

		// NPC 메뉴, 전체 화면 창, 일시정지, 장면 전환 중에는 전부 숨김
		private static bool IsHidden()
		{
			if (TimeManager.TryGetExistingInstance(out TimeManager timeManager) && timeManager.IsPaused)
				return true;
			if (UIManager.TryGetExistingInstance(out UIManager uiManager) && uiManager.NPC != null && uiManager.NPC.IsPanelOpen)
				return true;
			return GameConditionBridge.Get(GameConditionType.IsViewingUI)
				|| GameConditionBridge.Get(GameConditionType.IsInTransition);
		}

		private void RemoveStaleEntries()
		{
			staleMarkers.Clear();
			foreach (KeyValuePair<NPCMarker, Entry> pair in entries)
			{
				if (pair.Key == null || NPCMarker.Active.Contains(pair.Key) == false)
					staleMarkers.Add(pair.Key);
			}
			foreach (NPCMarker marker in staleMarkers)
			{
				entries[marker].Name.RemoveFromHierarchy();
				entries[marker].Marker.RemoveFromHierarchy();
				entries.Remove(marker);
			}
		}

		private Entry CreateEntry(NPCMarker marker)
		{
			Entry entry = new()
			{
				Name = new Label { pickingMode = PickingMode.Ignore },
				Marker = new VisualElement { pickingMode = PickingMode.Ignore },
				Background = new VisualElement { pickingMode = PickingMode.Ignore },
				Flash = new VisualElement { pickingMode = PickingMode.Ignore },
				Icon = new VisualElement { pickingMode = PickingMode.Ignore },
				BobPhase = Mathf.Repeat(entries.Count * BOB_PHASE_STEP, 1f),
			};
			entry.Name.AddToClassList(USS_ROOT + "__name");
			entry.Marker.AddToClassList(USS_ROOT + "__marker");
			entry.Background.AddToClassList(USS_ROOT + "__marker-bg");
			entry.Flash.AddToClassList(USS_ROOT + "__marker-flash");
			entry.Icon.AddToClassList(USS_ROOT + "__icon");
			entry.Flash.style.backgroundImage = new StyleBackground(Settings.FlashSilhouette);
			entry.Marker.Add(entry.Background);
			entry.Marker.Add(entry.Flash);
			entry.Marker.Add(entry.Icon);
			layer.Add(entry.Name);
			layer.Add(entry.Marker);
			return entry;
		}

		private void UpdateEntry(NPCMarker marker, Entry entry, Camera camera, Vector3 playerPosition)
		{
			if (marker.NPC.Data != entry.ShownData)
			{
				entry.ShownData = marker.NPC.Data;
				entry.Name.text = entry.ShownData.DisplayName;
			}

			Vector3 foot = marker.NPC.transform.position;
			float distance = Vector3.Distance(playerPosition, foot);
			float alpha = 1f - Mathf.InverseLerp(Settings.FadeStartDistance, Settings.FadeEndDistance, distance);
			bool hasFootPoint = TryProject(camera, foot, out Vector2 footPoint);
			bool hasHeadPoint = TryProject(camera, marker.transform.position, out Vector2 headPoint);
			bool isVisible = alpha > 0f && (hasFootPoint || hasHeadPoint);

			entry.Name.style.display = isVisible && hasFootPoint ? DisplayStyle.Flex : DisplayStyle.None;
			entry.Name.style.opacity = alpha;
			entry.Name.style.left = footPoint.x;
			entry.Name.style.top = footPoint.y + Settings.NameDrop;

			NPCQuestMark mark = marker.Mark;
			bool showMarker = isVisible && hasHeadPoint && mark != NPCQuestMark.None;
			entry.Marker.style.display = showMarker ? DisplayStyle.Flex : DisplayStyle.None;
			if (showMarker == false)
				return;

			if (mark != entry.ShownMark)
			{
				entry.ShownMark = mark;
				entry.Background.style.backgroundImage = new StyleBackground(Settings.GetBackground(mark));
				entry.Icon.style.backgroundImage = new StyleBackground(Settings.GetIcon(mark));
			}

			float time = Time.unscaledTime;
			float bob = Mathf.Sin((time / Settings.BobPeriod + entry.BobPhase) * Mathf.PI * 2f) * Settings.BobAmplitude;
			entry.Marker.style.opacity = alpha;
			entry.Marker.style.left = headPoint.x;
			entry.Marker.style.top = headPoint.y - Settings.MarkerTailTipY - Settings.MarkerLift + bob;
			// 시작 표식만 바탕이 천천히 물들었다 빠짐. 0 에서 시작해 부드럽게 오르내림
			entry.Flash.style.opacity = mark == NPCQuestMark.Start
				? Settings.StartFlashStrength * (0.5f - 0.5f * Mathf.Cos(time / Settings.StartFlashPeriod * Mathf.PI * 2f))
				: 0f;
		}

		// 세계 좌표를 이 층의 패널 좌표로. 카메라 뒤나 화면 밖이면 false
		private bool TryProject(Camera camera, Vector3 world, out Vector2 local)
		{
			Vector3 screen = camera.WorldToScreenPoint(world);
			bool isOnScreen = screen.z > 0f && screen.x >= 0f && screen.x <= Screen.width && screen.y >= 0f && screen.y <= Screen.height;
			Vector2 panelPoint = RuntimePanelUtils.ScreenToPanel(layer.panel, new Vector2(screen.x, Screen.height - screen.y));
			local = layer.WorldToLocal(panelPoint);
			return isOnScreen;
		}
	}
}
