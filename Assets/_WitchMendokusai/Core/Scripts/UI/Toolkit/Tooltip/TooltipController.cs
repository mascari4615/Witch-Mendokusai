using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// 글로벌 툴팁 컨트롤러. UIRoot.OverlayLayer에 단일 TooltipView 마운트.
	/// 데이터 타입 → ITooltipBuilder 매핑으로 컨텐츠 위임.
	/// 등록되지 않은 타입은 base type을 거슬러 올라가며 검색.
	/// </summary>
	[DefaultExecutionOrder(-40)]
	public class TooltipController : Singleton<TooltipController>
	{
		private const float OFFSET_X = 16f;
		private const float OFFSET_Y = 16f;
		private const float EDGE_PADDING = 8f;

		private readonly Dictionary<Type, ITooltipBuilder> builders = new();

		private TooltipView view;
		private bool isShowing;

		protected override void Awake()
		{
			base.Awake();

			RegisterBuilder(typeof(ItemData), new ItemTooltipBuilder());
			RegisterBuilder(typeof(Building), new BuildingTooltipBuilder());
		}

		private void OnEnable()
		{
			Mount();
		}

		private void Mount()
		{
			if (view != null)
				return;
			if (UIRoot.TryGetExistingInstance(out UIRoot uiRoot) == false)
				return;
			if (uiRoot.OverlayLayer == null)
				return;

			view = new TooltipView();
			uiRoot.OverlayLayer.Add(view);
			view.SetVisible(false);
		}

		public void RegisterBuilder(Type dataType, ITooltipBuilder builder)
		{
			builders[dataType] = builder;
		}

		public void Show(object data, TooltipMode mode = TooltipMode.Simple)
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
			if (Mouse.current == null)
				return;

			Vector2 screen = Mouse.current.position.ReadValue();
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
