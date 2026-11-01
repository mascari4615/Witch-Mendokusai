using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
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
		private readonly Stack<FloatingTextElement> pool = new Stack<FloatingTextElement>();
		private readonly List<ActiveText> active = new List<ActiveText>(MAX_VISIBLE_TEXTS);
		private UIRoot uiRoot;
		private InputManager inputManager;
		public DamageTextSkin ActiveSkin { get; private set; }

		private struct ActiveText
		{
			public FloatingTextElement Element;
			public DamageTextSkin Skin;
			public Vector3 WorldPosition;
			public bool HasWorldPosition;
			public float Age;
			public int Lane;
			public ulong TargetId;
		}

		[Inject]
		public void Construct(UIRoot uiRoot, InputManager inputManager)
		{
			this.uiRoot = uiRoot;
			this.inputManager = inputManager;
			ActiveSkin = uiRoot.DamageTextSkin;
		}

		private void Start()
		{
			for (int i = 0; i < INITIAL_POOL_SIZE; i++)
				pool.Push(CreateItem());
		}

		public void SetSkin(DamageTextSkin skin) => ActiveSkin = skin != null ? skin : uiRoot.DamageTextSkin;

		private FloatingTextElement CreateItem()
		{
			FloatingTextElement item = new FloatingTextElement();
			item.style.display = DisplayStyle.None;
			uiRoot.CombatTextLayer.Add(item);
			return item;
		}

		public IEnumerator AniTextUI(TextType textType, string message, Vector3? worldPosition = null, ulong targetId = 0)
		{
			if (GameConditionBridge.Get(GameConditionType.IsViewingUI))
				yield break;
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
			FloatingTextElement item = pool.Count > 0 ? pool.Pop() : CreateItem();
			item.style.display = DisplayStyle.Flex;
			item.Show(KindOf(textType), message, ActiveSkin);
			active.Add(new ActiveText
			{
				Element = item,
				Skin = ActiveSkin,
				WorldPosition = worldPosition.GetValueOrDefault(),
				HasWorldPosition = worldPosition.HasValue,
				Lane = 0,
				TargetId = targetId,
			});
			yield break;
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
				float lifetime = text.Skin != null ? text.Skin.Lifetime : 0.85f;
				if (text.Age >= lifetime)
				{
					Retire(i);
					continue;
				}
				Vector3 screen = inputManager.MouseScreenPosition;
				if (text.HasWorldPosition)
				{
					if (camera == null)
					{
						Retire(i);
						continue;
					}
					screen = camera.WorldToScreenPoint(text.WorldPosition);
					text.Element.style.display = screen.z > 0f ? DisplayStyle.Flex : DisplayStyle.None;
				}
				float progress = Mathf.Clamp01(text.Age / lifetime);
				float spacing = text.Skin != null ? text.Skin.LaneSpacing : 24f;
				float rise = text.Skin != null ? text.Skin.Rise : 42f;
				float horizontal = text.Lane == 0 ? 0f : (text.Lane % 2 == 0 ? -spacing : spacing);
				Vector2 offset = new Vector2(horizontal, -18f - text.Lane * spacing - rise * progress);
				text.Element.SetScreenPosition(screen, offset);
				text.Element.SetProgress(progress, text.Skin);
				active[i] = text;
			}
		}

		private void Retire(int index)
		{
			FloatingTextElement element = active[index].Element;
			element.Hide();
			pool.Push(element);
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
			foreach (FloatingTextElement element in pool)
				element.RemoveFromHierarchy();
			pool.Clear();
		}

		private static FloatingTextKind KindOf(TextType textType) => textType switch
		{
			TextType.Critical => FloatingTextKind.Critical,
			TextType.Heal => FloatingTextKind.Heal,
			TextType.Exp => FloatingTextKind.Experience,
			TextType.Warning => FloatingTextKind.Warning,
			_ => FloatingTextKind.Normal,
		};
	}
}
