using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using WitchMendokusai.Presentation;

namespace WitchMendokusai
{
	[DefaultExecutionOrder(200)]
	public class FloatingTextView : MonoBehaviour
	{
		private const int INITIAL_POOL_SIZE = 16;
		private const int MAX_VISIBLE_TEXTS = 96;
		private const int STACK_LANES = 4;
		private readonly Stack<TextInstance> pool = new Stack<TextInstance>();
		private readonly List<ActiveText> active = new List<ActiveText>(MAX_VISIBLE_TEXTS);
		private UIRoot uiRoot;
		private InputManager inputManager;
		private Canvas canvas;
		public DamageTextSkin ActiveSkin { get; private set; }

		private sealed class TextInstance
		{
			public GameObject Prefab;
			public GameObject Object;
			public RectTransform Rect;
			public TextMeshProUGUI Label;
			// 상태이상 틱 숫자 옆 아이콘. 평소에는 꺼짐
			public Image Icon;
		}

		private struct ActiveText
		{
			public TextInstance Instance;
			public DamageTextSkin Skin;
			public Vector3 WorldPosition;
			public bool HasWorldPosition;
			public float Age;
			public int Lane;
			public ulong TargetId;
			// 상태이상 틱이면 그 종류 줄, 아니면 null
			public UnitStatusStyle Status;
			public float Phase;
		}

		[Inject]
		public void Construct(UIRoot uiRoot, InputManager inputManager)
		{
			this.uiRoot = uiRoot;
			this.inputManager = inputManager;
			ActiveSkin = uiRoot.DamageTextSkin;
		}

		public void SetCanvas(Canvas source)
		{
			GameObject holder = new GameObject("CombatTextCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
			holder.transform.SetParent(uiRoot.transform, false);
			canvas = holder.GetComponent<Canvas>();
			canvas.renderMode = RenderMode.ScreenSpaceOverlay;
			canvas.overrideSorting = true;
			canvas.sortingOrder = -1;
			canvas.targetDisplay = source.targetDisplay;
			CanvasScaler reference = source.GetComponent<CanvasScaler>();
			CanvasScaler scaler = holder.GetComponent<CanvasScaler>();
			scaler.uiScaleMode = reference.uiScaleMode;
			scaler.referenceResolution = reference.referenceResolution;
			scaler.screenMatchMode = reference.screenMatchMode;
			scaler.matchWidthOrHeight = reference.matchWidthOrHeight;
			scaler.referencePixelsPerUnit = reference.referencePixelsPerUnit;
			scaler.scaleFactor = reference.scaleFactor;
		}

		private void Start()
		{
			for (int i = 0; i < INITIAL_POOL_SIZE; i++)
				pool.Push(CreateItem());
		}

		public void SetSkin(DamageTextSkin skin)
		{
			DamageTextSkin next = skin != null ? skin : uiRoot.DamageTextSkin;
			if (next == null || next.TextPrefab == null || next.TextPrefab.GetComponentInChildren<TextMeshProUGUI>(true) == null)
				throw new System.ArgumentException("Damage skin requires a TMP text prefab", nameof(skin));
			if (ActiveSkin == null || ActiveSkin.TextPrefab != next.TextPrefab)
			{
				foreach (TextInstance item in pool)
					Destroy(item.Object);
				pool.Clear();
			}
			ActiveSkin = next;
		}

		private TextInstance CreateItem()
		{
			GameObject prefab = ActiveSkin.TextPrefab;
			GameObject instance = Instantiate(prefab, canvas.transform, false);
			Animator animator = instance.GetComponent<Animator>();
			if (animator != null)
				animator.enabled = false;
			TextMeshProUGUI label = instance.GetComponentInChildren<TextMeshProUGUI>(true);
			TextMeshProUGUI source = prefab.GetComponentInChildren<TextMeshProUGUI>(true);
			label.rectTransform.localScale = source.rectTransform.localScale;
			label.rectTransform.anchoredPosition = source.rectTransform.anchoredPosition;
			label.raycastTarget = false;
			foreach (CanvasGroup group in instance.GetComponentsInChildren<CanvasGroup>(true))
				group.alpha = 1f;
			GameObject iconObject = new GameObject("Status icon", typeof(RectTransform), typeof(Image));
			iconObject.transform.SetParent(label.rectTransform, false);
			Image icon = iconObject.GetComponent<Image>();
			icon.raycastTarget = false;
			icon.preserveAspect = true;
			icon.enabled = false;
			instance.SetActive(false);
			return new TextInstance { Prefab = prefab, Object = instance, Rect = instance.GetComponent<RectTransform>(), Label = label, Icon = icon };
		}

		public IEnumerator AniTextUI(TextType textType, string message, Vector3? worldPosition = null, ulong targetId = 0)
		{
			Show(textType, message, worldPosition, targetId);
			yield break;
		}

		// 화상, 독 같은 상태이상 틱. 크리티컬처럼 색과 크기를 바꾸고 종류 아이콘을 붙임. 값은 UnitStatusConfig
		public void ShowStatusTick(UnitStatusType statusType, string message, Vector3? worldPosition, ulong targetId)
		{
			UnitStatusConfig config = ResourceManager.Instance.UnitStatus;
			TextInstance item = ShowItem(TextType.Normal, message, worldPosition, targetId);
			if (item == null)
				return;
			UnitStatusStyle style = config.Style(statusType);
			item.Label.color = style.TickTextColor;
			item.Label.fontSize = ActiveSkin.FontSize * style.TickTextScale;
			ActiveText text = active[active.Count - 1];
			text.Status = style;
			text.Phase = Random.value;
			active[active.Count - 1] = text;
			if (style.Icon == null)
				return;
			float fontSize = item.Label.fontSize;
			float iconSize = fontSize * config.TickTextIconSize;
			item.Icon.sprite = style.Icon;
			item.Icon.color = style.IconColor;
			item.Icon.rectTransform.sizeDelta = new Vector2(iconSize, iconSize);
			item.Icon.rectTransform.anchoredPosition = new Vector2(-(item.Label.preferredWidth * 0.5f + fontSize * config.TickTextIconGap + iconSize * 0.5f), 0f);
			item.Icon.enabled = true;
		}

		public void Show(TextType textType, string message, Vector3? worldPosition = null, ulong targetId = 0) => ShowItem(textType, message, worldPosition, targetId);

		private TextInstance ShowItem(TextType textType, string message, Vector3? worldPosition, ulong targetId)
		{
			if (GameConditionBridge.Get(GameConditionType.IsViewingUI))
				return null;
			if (worldPosition.HasValue)
			{
				for (int i = active.Count - 1; i >= 0; i--)
				{
					ActiveText text = active[i];
					bool sameTarget = targetId != 0 ? text.TargetId == targetId
						: text.TargetId == 0 && (text.WorldPosition - worldPosition.Value).sqrMagnitude < 4f;
					if (text.HasWorldPosition == false || sameTarget == false)
						continue;
					text.Lane++;
					if (text.Lane >= STACK_LANES)
						Retire(i);
					else
						active[i] = text;
				}
			}
			if (active.Count >= MAX_VISIBLE_TEXTS)
				Retire(0);
			TextInstance item = pool.Count > 0 ? pool.Pop() : CreateItem();
			item.Label.text = textType == TextType.Critical ? ActiveSkin.CriticalPrefix + message : message;
			item.Label.fontSize = ActiveSkin.FontSize * (textType == TextType.Critical ? ActiveSkin.CriticalScale : 1f);
			item.Label.color = textType switch
			{
				TextType.Critical => ActiveSkin.CriticalColor,
				TextType.Heal => ActiveSkin.HealColor,
				TextType.Warning => ActiveSkin.WarningColor,
				_ => ActiveSkin.NormalColor,
			};
			item.Icon.enabled = false;
			item.Object.SetActive(true);
			active.Add(new ActiveText { Instance = item, Skin = ActiveSkin, WorldPosition = worldPosition.GetValueOrDefault(), HasWorldPosition = worldPosition.HasValue, TargetId = targetId });
			return item;
		}

		private void LateUpdate()
		{
			if (GameConditionBridge.Get(GameConditionType.IsViewingUI))
			{
				Clear();
				return;
			}
			Camera camera = ViewCameraResolver.Current;
			for (int i = active.Count - 1; i >= 0; i--)
			{
				ActiveText text = active[i];
				text.Age += Time.deltaTime;
				if (text.Age >= text.Skin.Lifetime || (text.HasWorldPosition && camera == null))
				{
					Retire(i);
					continue;
				}
				Vector3 screen = text.HasWorldPosition ? camera.WorldToScreenPoint(text.WorldPosition) : (Vector3)inputManager.MouseScreenPosition;
				text.Instance.Object.SetActive(text.HasWorldPosition == false || screen.z > 0f);
				float progress = Mathf.Clamp01(text.Age / text.Skin.Lifetime);
				float spacing = text.Skin.LaneSpacing;
				float horizontal = text.Lane == 0 ? 0f : (text.Lane % 2 == 0 ? -spacing : spacing);
				RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)canvas.transform, screen, null, out Vector2 position);
				float rise = text.Skin.Rise * progress;
				float alpha = 1f - Mathf.Clamp01((progress - 0.65f) / 0.35f);
				UnitStatusStyle status = text.Status;
				if (status != null)
				{
					// 상태이상 틱: 떠오르는 높이, 좌우 흔들림, 깜빡임이 종류마다 다름
					rise *= status.TickTextRiseScale;
					horizontal += status.TickTextSway * Mathf.Sin((text.Age * status.TickTextSwayRate + text.Phase) * Mathf.PI * 2f);
					alpha *= 1f - status.TickTextFlicker * (0.5f + 0.5f * Mathf.Sin((text.Age * status.TickTextFlickerRate + text.Phase) * Mathf.PI * 2f));
					Color iconColor = status.IconColor;
					iconColor.a *= alpha;
					text.Instance.Icon.color = iconColor;
				}
				text.Instance.Rect.anchoredPosition = position + new Vector2(horizontal, 18f + text.Lane * spacing + rise);
				text.Instance.Rect.localScale = Vector3.one * Mathf.Lerp(text.Skin.ImpactScale, 1f, Mathf.Clamp01(progress / 0.18f));
				text.Instance.Label.alpha = alpha;
				active[i] = text;
			}
		}

		private void Retire(int index)
		{
			TextInstance item = active[index].Instance;
			item.Icon.enabled = false;
			item.Object.SetActive(false);
			if (ActiveSkin != null && item.Prefab == ActiveSkin.TextPrefab)
				pool.Push(item);
			else
				Destroy(item.Object);
			active.RemoveAt(index);
		}

		private void Clear()
		{
			for (int i = active.Count - 1; i >= 0; i--)
				Retire(i);
		}

		private void OnDisable() => Clear();
		private void OnDestroy()
		{
			Clear();
			pool.Clear();
			if (canvas != null)
				Destroy(canvas.gameObject);
		}
	}
}
