using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// 가까운 상호작용 대상 옆에 뜨는 한 줄 표시 (키, 구분선, 이름, 역할). 서브컬처 게임의 선택지 줄 모양
	/// 대상의 머리 높이를 화면에 투영해 그 오른쪽에, 화면 오른쪽 끝이면 왼쪽에. 손가락 모드에서는 줄을 눌러도 상호작용
	/// UIRoot 가 만들어 HUD 층에 붙임. 모양은 InteractPrompt.uss
	/// </summary>
	public class InteractPromptView : MonoBehaviour
	{
		private const string USS_ROOT = "wm-interact-prompt";
		private const string USS_VISIBLE = USS_ROOT + "--visible";
		private const string KEY_LABEL = "Z";
		private const string TOUCH_LABEL = "›";
		private const string CLONE_SUFFIX = "(Clone)";
		private const char ROLE_SEPARATOR = '_';

		[Tooltip("대상 발밑에서 표시 기준점까지 높이 (m)")]
		[SerializeField, Min(0f)] private float anchorHeight = 1.4f;
		[Tooltip("대상 화면 위치에서 옆으로 띄우는 거리 (패널 픽셀)")]
		[SerializeField, Min(0f)] private float offsetX = 36f;

		public StyleSheet Style { private get; set; }

		private VisualElement row;
		private BlurPanel blurPanel;
		private Label keyLabel;
		private Label nameLabel;
		private Label roleLabel;
		// 맵 오브젝트 전용. 보상 예고 칸 (아이콘, 글자) 과 비용 글자 (MapObjectSettings)
		private VisualElement previewChip;
		private VisualElement previewIcon;
		private Label previewLabel;
		private Label costLabel;
		private int shownSiteStamp;
		private VisualElement layer;
		private InteractiveObject current;
		private InteractiveObject shownTarget;
		private bool shownTouchMode;
		private bool isShown;

		// 꺼진 동안 줄과 흐림 요청도 숨김
		private void OnDisable()
		{
			if (row != null)
				Hide();
		}

		private void OnDestroy()
		{
			if (row != null)
				row.RemoveFromHierarchy();
		}

		// UIRoot 재활성화 때 층 새로 생성. 붙은 층이 현재 HUD 층이 아니면 재구성
		private bool EnsureBuilt()
		{
			if (UIRoot.Instance == null || UIRoot.Instance.HudLayer == null)
				return false;
			if (row != null && row.parent == UIRoot.Instance.HudLayer)
				return true;

			row?.RemoveFromHierarchy();
			layer = UIRoot.Instance.HudLayer;
			if (layer.styleSheets.Contains(Style) == false)
				layer.styleSheets.Add(Style);

			row = new VisualElement { name = "InteractPrompt", pickingMode = PickingMode.Ignore };
			row.AddToClassList(USS_ROOT);
			// 뒤 화면 흐림과 오른쪽 꼬리 (레퍼런스 선택지 줄 결). 줄은 투명도로만 숨어 붙은 채라 보일 때만 흐림 요청
			blurPanel = BlurPanel.AttachBehind(row);
			blurPanel.SetActive(false);
			VisualElement tail = new VisualElement { pickingMode = PickingMode.Ignore };
			tail.AddToClassList(USS_ROOT + "__tail");
			row.Add(tail);
			keyLabel = new Label(KEY_LABEL) { pickingMode = PickingMode.Ignore };
			keyLabel.AddToClassList(USS_ROOT + "__key");
			row.Add(keyLabel);
			VisualElement divider = new VisualElement { pickingMode = PickingMode.Ignore };
			divider.AddToClassList(USS_ROOT + "__divider");
			row.Add(divider);
			nameLabel = new Label { pickingMode = PickingMode.Ignore };
			nameLabel.AddToClassList(USS_ROOT + "__name");
			row.Add(nameLabel);
			roleLabel = new Label { pickingMode = PickingMode.Ignore };
			roleLabel.AddToClassList(USS_ROOT + "__role");
			row.Add(roleLabel);
			previewChip = new VisualElement { pickingMode = PickingMode.Ignore };
			previewChip.AddToClassList(USS_ROOT + "__preview");
			previewIcon = new VisualElement { pickingMode = PickingMode.Ignore };
			previewIcon.AddToClassList(USS_ROOT + "__preview-icon");
			previewChip.Add(previewIcon);
			previewLabel = new Label { pickingMode = PickingMode.Ignore };
			previewLabel.AddToClassList(USS_ROOT + "__preview-label");
			previewChip.Add(previewLabel);
			row.Add(previewChip);
			costLabel = new Label { pickingMode = PickingMode.Ignore };
			costLabel.AddToClassList(USS_ROOT + "__cost");
			row.Add(costLabel);
			row.RegisterCallback<ClickEvent>(_ => OnRowClicked());
			layer.Add(row);
			shownTarget = null;
			isShown = false;
			return true;
		}

		// 누른 순간 대상 재탐색, 키 입력과 같은 조건
		private void OnRowClicked()
		{
			if (IsTouchMode() == false)
				return;
			InteractiveObject target = FindTarget();
			if (target != null)
				target.Interact();
		}

		private static bool IsTouchMode() => InputManager.TryGetExistingInstance(out InputManager inputManager) && inputManager.IsTouchMode;

		private void LateUpdate()
		{
			if (EnsureBuilt() == false)
				return;

			current = FindTarget();
			Camera camera = Camera.main;
			if (current == null || camera == null || row.panel == null)
			{
				Hide();
				return;
			}

			Vector3 screen = camera.WorldToScreenPoint(current.transform.position + Vector3.up * anchorHeight);
			bool isOnScreen = screen.z > 0f && screen.x >= 0f && screen.x <= Screen.width && screen.y >= 0f && screen.y <= Screen.height;
			if (isOnScreen == false)
			{
				Hide();
				return;
			}

			bool isTouchMode = IsTouchMode();
			int siteStamp = current.TryGetComponent(out MapObjectSite currentSite) ? currentSite.PromptStamp() : 0;
			if (current != shownTarget || isTouchMode != shownTouchMode || siteStamp != shownSiteStamp)
				ApplyContent(current, isTouchMode, siteStamp);

			Vector2 panelPoint = RuntimePanelUtils.ScreenToPanel(row.panel, new Vector2(screen.x, Screen.height - screen.y));
			Vector2 local = layer.WorldToLocal(panelPoint);
			float width = row.resolvedStyle.width;
			float height = row.resolvedStyle.height;
			if (float.IsNaN(width) || float.IsNaN(height))
				return;
			Rect bounds = layer.contentRect;
			float left = local.x + offsetX;
			if (left + width > bounds.width)
				left = local.x - offsetX - width;
			row.style.left = Mathf.Clamp(left, 0f, Mathf.Max(0f, bounds.width - width));
			row.style.top = Mathf.Clamp(local.y - height / 2f, 0f, Mathf.Max(0f, bounds.height - height));
			row.AddToClassList(USS_VISIBLE);
			if (isShown == false)
			{
				isShown = true;
				blurPanel.SetActive(true);
			}
		}

		private void Hide()
		{
			row.RemoveFromClassList(USS_VISIBLE);
			row.pickingMode = PickingMode.Ignore;
			shownTarget = null;
			if (isShown)
			{
				isShown = false;
				blurPanel.SetActive(false);
			}
		}

		// 대상이나 입력 방식이 바뀔 때만 글자 갱신
		private void ApplyContent(InteractiveObject target, bool isTouchMode, int siteStamp)
		{
			shownTarget = target;
			shownTouchMode = isTouchMode;
			shownSiteStamp = siteStamp;
			previewChip.style.display = DisplayStyle.None;
			costLabel.style.display = DisplayStyle.None;
			keyLabel.text = isTouchMode ? TOUCH_LABEL : KEY_LABEL;
			// 숨긴 동안과 마우스 모드는 클릭을 잡지 않음 (월드 클릭 보호)
			row.pickingMode = isTouchMode ? PickingMode.Position : PickingMode.Ignore;

			NPCObject npc = target.GetComponent<NPCObject>();
			if (npc != null && npc.Data != null)
			{
				// NPC 데이터 이름 "이름_역할" 은 이름과 흐린 역할로
				string dataName = npc.Data.Name;
				int split = dataName.IndexOf(ROLE_SEPARATOR);
				nameLabel.text = split > 0 ? dataName.Substring(0, split) : dataName;
				roleLabel.text = split > 0 ? dataName.Substring(split + 1) : string.Empty;
			}
			else if (target.TryGetComponent(out MapObjectSite site))
			{
				// 맵 오브젝트는 데이터의 이름과 설명
				nameLabel.text = site.Data.DisplayName;
				roleLabel.text = site.Data.Role;
				ApplyMapObjectContent(site);
			}
			else
			{
				nameLabel.text = target.name.Replace(CLONE_SUFFIX, string.Empty).Trim();
				roleLabel.text = string.Empty;
			}
			roleLabel.style.display = roleLabel.text.Length > 0 ? DisplayStyle.Flex : DisplayStyle.None;
		}

		// 보상 예고 (수락 전 범주) 와 비용. 못 내는 비용은 다른 색
		private void ApplyMapObjectContent(MapObjectSite site)
		{
			MapObjectSettings settings = MapObjectSettings.Load();
			if (site.IsHolding == false && settings.TryGetRewardPreview(site.Data.RewardPreview, out MapObjectRewardPreviewStyle preview))
			{
				previewIcon.style.backgroundImage = new StyleBackground(preview.Icon);
				previewLabel.text = preview.Label;
				previewChip.style.backgroundColor = preview.Color;
				previewChip.style.display = DisplayStyle.Flex;
			}
			string cost = site.DescribeCost();
			if (cost.Length == 0)
				return;
			costLabel.text = cost;
			costLabel.style.color = site.IsHolding || site.CanAfford() ? settings.CostAffordableColor : settings.CostShortColor;
			costLabel.style.display = DisplayStyle.Flex;
		}

		// 키 입력이 막히는 상황 (일시정지, 글 입력, 전체 화면 창, 시전, 사망, 건설, 전환) 에는 표시도 숨김
		private static InteractiveObject FindTarget()
		{
			if (GameConditionBridge.Get(GameConditionType.IsViewingUI)
				|| GameConditionBridge.Get(GameConditionType.IsPaused)
				|| GameConditionBridge.Get(GameConditionType.IsTyping)
				|| GameConditionBridge.Get(GameConditionType.IsPlayerCasting)
				|| GameConditionBridge.Get(GameConditionType.IsDied)
				|| GameConditionBridge.Get(GameConditionType.IsBuilding)
				|| GameConditionBridge.Get(GameConditionType.IsInTransition))
				return null;
			PlayerObject player = PlayerProvider.Instance != null ? PlayerProvider.Instance.CurrentObject : null;
			if (player == null)
				return null;
			return InteractiveObject.GetNearest(player.transform.position, PlayerInteraction.InteractionDistance);
		}
	}
}
