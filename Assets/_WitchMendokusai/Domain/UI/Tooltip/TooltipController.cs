using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;

namespace WitchMendokusai
{
	/// <summary>
	/// 글로벌 툴팁 컨트롤러. UIRoot.TooltipLayer(최상단)에 단일 TooltipView 마운트.
	/// 데이터 타입 → ITooltipBuilder 매핑으로 컨텐츠 위임.
	/// 등록되지 않은 타입은 base type을 거슬러 올라가며 검색.
	/// </summary>
	[DefaultExecutionOrder(-40)]
	public class TooltipController : MonoBehaviour
	{
		// TASK-WM-133 — static Instance/TryGetExistingInstance 삭제. RegisterLeaf
		// prefab + DontDestroyOnLoad 가 단일성 보장(DI 소유), Slot/DevItemSlot 은
		// UIRoot panel-root owner-push 된 UIServices.Tooltip 경유 획득.
		private const float OFFSET_X = 16f;
		private const float OFFSET_Y = 16f;
		private const float EDGE_PADDING = 8f;

		private InputManager inputManager;
		private UIRoot uiRoot;

		[Inject]
		public void Construct(InputManager inputManager, UIRoot uiRoot)
		{
			this.inputManager = inputManager;
			this.uiRoot = uiRoot;
		}

		private readonly Dictionary<Type, ITooltipBuilder> builders = new();

		private TooltipView view;
		private bool isShowing;

		// anchored 모드 (역할② — 고정 인라인 상세, 마우스추종/자동숨김 X). false = hover 팝업(기존).
		private bool isAnchored;
		private Vector2 anchorScreenPos;

		private void Awake()
		{
			RegisterBuilder(typeof(ItemData), new ItemTooltipBuilder());
			RegisterBuilder(typeof(Building), new BuildingTooltipBuilder());
			RegisterBuilder(typeof(SlotData), new SlotDataTooltipBuilder());
		}

		private void OnEnable()
		{
			Mount();
		}

		private void Mount()
		{
			if (view != null)
				return;
			if (uiRoot == null || uiRoot.TooltipLayer == null)
				return;

			view = new TooltipView();
			// 툴팁은 최상단 층 — 무엇 위에 얹히든 가려지면 안 된다(핫바 뒤로 가던 사고).
			uiRoot.TooltipLayer.Add(view);
			view.SetVisible(false);
		}

		public void RegisterBuilder(Type dataType, ITooltipBuilder builder)
		{
			builders[dataType] = builder;
		}

		/// <summary>
		/// hover 팝업 (역할① — 마우스 추종, pointer-exit 시 caller 가 Hide). 기존 동작.
		/// </summary>
		public void Show(object data, TooltipMode mode = TooltipMode.Simple)
		{
			isAnchored = false;
			BuildAndShow(data, mode);
		}

		/// <summary>
		/// anchored (역할② — 고정 화면좌표, 선택 시 갱신되는 인라인 상세 패널).
		/// caller 가 screenPos(스크린 좌표, 좌하 원점) 를 계산해 전달 — uGUI/UIElements 무관.
		/// 마우스 추종/자동숨김 없음. caller 가 명시 Show/Hide 로 수명 제어.
		/// </summary>
		public void ShowAnchored(object data, Vector2 screenPos, TooltipMode mode = TooltipMode.Simple)
		{
			isAnchored = true;
			anchorScreenPos = screenPos;
			BuildAndShow(data, mode);
		}

		private void BuildAndShow(object data, TooltipMode mode)
		{
			if (data == null)
			{
				Hide();
				return;
			}

			Mount();
			if (view == null)
				return;

			ITooltipBuilder builder = ResolveBuilder(data.GetType());
			if (builder == null)
			{
				Hide();
				return;
			}

			view.Clear();
			builder.Build(view, data, mode);
			view.SetVisible(true);
			isShowing = true;
		}

		public void Hide()
		{
			isShowing = false;
			if (view != null)
				view.SetVisible(false);
		}

		private ITooltipBuilder ResolveBuilder(Type dataType)
		{
			for (Type type = dataType; type != null; type = type.BaseType)
			{
				if (builders.TryGetValue(type, out ITooltipBuilder builder))
					return builder;
			}
			return null;
		}

		private void Update()
		{
			if (isShowing == false || view == null || view.panel == null)
				return;

			Vector2 screen;
			if (isAnchored)
			{
				screen = anchorScreenPos;
			}
			else
			{
				if (inputManager.IsMouseAvailable == false)
					return;
				screen = inputManager.MouseScreenPosition;
			}
			screen.y = Screen.height - screen.y;
			Vector2 panelPosition = RuntimePanelUtils.ScreenToPanel(view.panel, screen);

			float panelWidth = view.panel.visualTree.layout.width;
			float panelHeight = view.panel.visualTree.layout.height;
			float viewWidth = view.layout.width;
			float viewHeight = view.layout.height;

			float left = panelPosition.x + OFFSET_X;
			float top = panelPosition.y + OFFSET_Y;

			if (left + viewWidth + EDGE_PADDING > panelWidth)
				left = panelPosition.x - viewWidth - OFFSET_X;
			if (top + viewHeight + EDGE_PADDING > panelHeight)
				top = panelHeight - viewHeight - EDGE_PADDING;
			if (left < EDGE_PADDING)
				left = EDGE_PADDING;
			if (top < EDGE_PADDING)
				top = EDGE_PADDING;

			view.style.left = left;
			view.style.top = top;
		}
	}
}
