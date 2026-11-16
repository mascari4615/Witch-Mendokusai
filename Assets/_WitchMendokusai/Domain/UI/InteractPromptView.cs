using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// 가까운 상호작용 대상 옆에 뜨는 한 줄 표시 (키, 구분선, 이름). 서브컬처 게임의 선택지 줄 모양
	/// 대상의 머리 높이를 화면에 투영해 그 오른쪽에. 손가락 모드에서는 줄을 눌러도 상호작용
	/// UIRoot 가 만들어 HUD 층에 붙임. 모양은 InteractPrompt.uss
	/// </summary>
	public class InteractPromptView : MonoBehaviour
	{
		private const string USS_ROOT = "wm-interact-prompt";
		private const string USS_VISIBLE = USS_ROOT + "--visible";
		private const string KEY_LABEL = "Z";
		private const string TOUCH_LABEL = "›";

		[Tooltip("대상 발밑에서 표시 기준점까지 높이 (m)")]
		[SerializeField, Min(0f)] private float anchorHeight = 1.4f;
		[Tooltip("대상 화면 위치에서 오른쪽으로 띄우는 거리 (패널 픽셀)")]
		[SerializeField, Min(0f)] private float offsetX = 36f;

		public StyleSheet Style { private get; set; }

		private VisualElement row;
		private Label keyLabel;
		private Label nameLabel;
		private Label roleLabel;
		private VisualElement layer;
		private Transform playerTransform;
		private InteractiveObject current;
		private InputManager inputManager;

		private void Awake()
		{
			EventBusBridge.Subscribe<PlayerSpawnedEvent>(OnPlayerSpawned);
			EventBusBridge.Subscribe<PlayerDespawnedEvent>(OnPlayerDespawned);
		}

		private void OnDestroy()
		{
			EventBusBridge.Unsubscribe<PlayerSpawnedEvent>(OnPlayerSpawned);
			EventBusBridge.Unsubscribe<PlayerDespawnedEvent>(OnPlayerDespawned);
			row?.RemoveFromHierarchy();
		}

		private void OnPlayerSpawned(PlayerSpawnedEvent evt) => playerTransform = evt.Transform;
		private void OnPlayerDespawned(PlayerDespawnedEvent evt) => playerTransform = null;

		private bool EnsureBuilt()
		{
			if (row != null)
				return true;
			if (UIRoot.Instance == null || UIRoot.Instance.HudLayer == null)
				return false;

			layer = UIRoot.Instance.HudLayer;
			if (Style != null && layer.styleSheets.Contains(Style) == false)
				layer.styleSheets.Add(Style);

			row = new VisualElement { name = "InteractPrompt", pickingMode = PickingMode.Position };
			row.AddToClassList(USS_ROOT);
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
			row.RegisterCallback<ClickEvent>(_ => OnRowClicked());
			layer.Add(row);
			return true;
		}

		private void OnRowClicked()
		{
			if (current != null && IsTouchMode())
				current.Interact();
		}

		private bool IsTouchMode()
		{
			if (inputManager == null)
				InputManager.TryGetExistingInstance(out inputManager);
			return inputManager != null && inputManager.IsTouchMode;
		}

		private void LateUpdate()
		{
			if (EnsureBuilt() == false)
				return;

			current = FindTarget();
			Camera camera = Camera.main;
			if (current == null || camera == null || row.panel == null)
			{
				row.RemoveFromClassList(USS_VISIBLE);
				return;
			}

			Vector3 screen = camera.WorldToScreenPoint(current.transform.position + Vector3.up * anchorHeight);
			if (screen.z <= 0f)
			{
				row.RemoveFromClassList(USS_VISIBLE);
				return;
			}

			Vector2 panelPoint = RuntimePanelUtils.ScreenToPanel(row.panel, new Vector2(screen.x, Screen.height - screen.y));
			Vector2 local = layer.WorldToLocal(panelPoint);
			float height = row.resolvedStyle.height;
			float width = row.resolvedStyle.width;
			Rect bounds = layer.contentRect;
			float left = Mathf.Clamp(local.x + offsetX, 0f, Mathf.Max(0f, bounds.width - width));
			float top = Mathf.Clamp(local.y - height * 0.5f, 0f, Mathf.Max(0f, bounds.height - height));
			row.style.left = left;
			row.style.top = top;

			keyLabel.text = IsTouchMode() ? TOUCH_LABEL : KEY_LABEL;
			// 데이터 이름 "이름_역할" 은 이름과 흐린 역할로 나눔
			string displayName = GetDisplayName(current);
			int split = displayName.IndexOf('_');
			nameLabel.text = split > 0 ? displayName.Substring(0, split) : displayName;
			roleLabel.text = split > 0 ? displayName.Substring(split + 1) : string.Empty;
			roleLabel.style.display = split > 0 ? DisplayStyle.Flex : DisplayStyle.None;
			row.AddToClassList(USS_VISIBLE);
		}

		private InteractiveObject FindTarget()
		{
			if (playerTransform == null || GameConditionBridge.Get(GameConditionType.IsViewingUI))
				return null;
			return InteractiveObject.GetNearest(playerTransform.position, PlayerInteraction.InteractionDistance);
		}

		// NPC 는 데이터 이름 (한국어), 그 밖은 오브젝트 이름
		private static string GetDisplayName(InteractiveObject target)
		{
			NPCObject npc = target.GetComponent<NPCObject>();
			if (npc != null && npc.Data != null)
				return npc.Data.Name;
			return target.name;
		}
	}
}
