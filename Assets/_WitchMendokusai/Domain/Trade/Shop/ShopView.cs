using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	// 1920x1080 고정 무대. 값 표시만 담당, 판정은 UIShopToolkit
	public partial class ShopView : VisualElement
	{
		private const string BLOCK = "wm-shop";

		public event Action<int> CellHovered = delegate { };
		public event Action<int> CellClicked = delegate { };
		public event Action<int> TabClicked = delegate { };
		public event Action<int> CartRowHovered = delegate { };
		public event Action<int> CartRowClicked = delegate { };
		public event Action PayHovered = delegate { };
		public event Action PayClicked = delegate { };

		private readonly ShopUISettings settings;
		private readonly VisualElement stage;

		private VisualElement merchantPicture;
		private ShopOutlinedLabel merchantName;
		private Label freeTag;
		private ShopOutlinedLabel walletValue;
		private VisualElement tabRow;

		private VisualElement cursorTarget;
		private VisualElement cursor;
		private VisualElement cursorCorner;
		private bool cursorBreathing;
		private bool cursorJumpPending;

		private readonly List<VisualElement> tabs = new();
		private readonly List<Label> tabNames = new();
		private readonly List<Label> tabCounts = new();

		public ShopView(ShopUISettings settings)
		{
			this.settings = settings;
			AddToClassList(BLOCK);
			focusable = true;
			tabIndex = 0;
			styleSheets.Add(settings.StyleSheet);

			stage = new ShopGradientPanel();
			stage.AddToClassList(Class("stage"));
			stage.pickingMode = PickingMode.Ignore;
			Add(stage);
			Make("stage-tile", stage);

			BuildHeader();
			BuildGrid();
			BuildInfo();
			BuildCart();
			BuildKeys();
			BuildCursor();

			RegisterCallback<GeometryChangedEvent>(_ => FitStage());
			schedule.Execute(UpdateCursor).Every(16);
			schedule.Execute(ToggleBreath).Every(settings.CursorBreathMilliseconds);
		}

		private static string Class(string element) => $"{BLOCK}__{element}";

		private static VisualElement Make(string element, VisualElement parent, bool picking = false)
		{
			VisualElement made = new();
			made.AddToClassList(Class(element));
			if (picking == false)
				made.pickingMode = PickingMode.Ignore;
			parent.Add(made);
			return made;
		}

		private static Label MakeLabel(string element, VisualElement parent, string text = "")
		{
			Label label = new(text);
			label.AddToClassList(Class("label"));
			label.AddToClassList(Class(element));
			label.pickingMode = PickingMode.Ignore;
			parent.Add(label);
			return label;
		}

		private static ShopOutlinedLabel MakeOutlined(string element, VisualElement parent, string text = "")
		{
			ShopOutlinedLabel label = new();
			label.AddToClassList(Class(element));
			label.text = text;
			parent.Add(label);
			return label;
		}

		private static void SetVisible(VisualElement element, bool visible) =>
			element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

		private static string Number(long value) => value.ToString("N0", CultureInfo.InvariantCulture);

		private static void SetIcon(VisualElement image, Sprite sprite) =>
			image.style.backgroundImage = sprite != null ? new StyleBackground(sprite) : new StyleBackground(StyleKeyword.None);

		private void FitStage()
		{
			float width = stage.resolvedStyle.width;
			float height = stage.resolvedStyle.height;
			if (width <= 0 || height <= 0 || layout.width <= 0 || layout.height <= 0)
				return;
			float scale = Mathf.Min(layout.width / width, layout.height / height);
			stage.style.scale = new Scale(new Vector3(scale, scale, 1));
			stage.style.left = (layout.width - width * scale) / 2;
			stage.style.top = (layout.height - height * scale) / 2;
		}

		private VisualElement MakePanel(string element, VisualElement parent, out VisualElement body)
		{
			ShopGradientPanel panel = new();
			panel.AddToClassList(Class("panel"));
			panel.AddToClassList(Class(element));
			panel.pickingMode = PickingMode.Ignore;
			parent.Add(panel);
			MakeNineSlice("panel-targeting", panel);
			body = Make("panel-body", panel);
			body.AddToClassList(Class($"{element}-body"));
			return panel;
		}

		private static void FinishPanel(VisualElement panel) => MakeNineSlice("panel-corner", panel);

		private static ShopNineSlice MakeNineSlice(string element, VisualElement parent)
		{
			ShopNineSlice frame = new();
			frame.AddToClassList(Class(element));
			parent.Add(frame);
			return frame;
		}

		private void BuildHeader()
		{
			VisualElement who = Make("who", stage);
			VisualElement picture = Make("who-picture", who);
			merchantPicture = Make("who-image", picture);
			merchantName = MakeOutlined("who-name", who);
			freeTag = MakeLabel("who-free", who, "전 품목 무료");

			VisualElement wallet = Make("wallet", stage);
			ShopSlantFill band = new();
			band.AddToClassList(Class("wallet-band"));
			wallet.Add(band);
			VisualElement cap = Make("wallet-cap", wallet);
			Make("wallet-nyang", cap);
			walletValue = MakeOutlined("wallet-value", wallet);

			tabRow = Make("tabs", stage);
		}

		private void BuildKeys()
		{
			VisualElement keys = Make("keys", stage);
			AddKey(keys, "방향키", "이동");
			AddKey(keys, "Z", "담기, 결제");
			AddKey(keys, "X", "빼기, 닫기");
			AddKey(keys, "Tab", "분류");
		}

		private static void AddKey(VisualElement keys, string key, string action)
		{
			VisualElement row = Make("key", keys);
			MakeLabel("key-cap", row, key);
			MakeLabel("key-action", row, action);
		}

		private void BuildCursor()
		{
			cursor = Make("cursor", stage);
			cursor.AddToClassList(Class("cursor--jump"));
			MakeNineSlice("cursor-targeting", cursor);
			cursorCorner = MakeNineSlice("cursor-corner", cursor);
		}

		public void SetMerchant(string name, Sprite picture, bool isFree)
		{
			merchantName.text = name;
			SetIcon(merchantPicture, picture);
			SetVisible(freeTag, isFree);
		}

		public void SetWallet(long wallet) => walletValue.text = $"{Number(wallet)}냥";

		public void SetTabs(IReadOnlyList<ShopTabModel> models, int selectedIndex)
		{
			while (tabs.Count < models.Count)
			{
				int index = tabs.Count;
				VisualElement tab = Make("tab", tabRow, picking: true);
				ShopSlantFill fill = new();
				fill.AddToClassList(Class("tab-fill"));
				tab.Add(fill);
				VisualElement text = Make("tab-text", tab);
				tabNames.Add(MakeLabel("tab-name", text));
				tabCounts.Add(MakeLabel("tab-count", text));
				tab.RegisterCallback<ClickEvent>(_ => TabClicked(index));
				tabs.Add(tab);
			}

			for (int i = 0; i < tabs.Count; i++)
			{
				bool used = i < models.Count;
				SetVisible(tabs[i], used);
				if (used == false)
					continue;
				tabNames[i].text = models[i].Name;
				tabCounts[i].text = models[i].Count.ToString(CultureInfo.InvariantCulture);
				tabs[i].EnableInClassList(Class("tab--sell"), models[i].IsSell);
				tabs[i].EnableInClassList(Class("tab--on"), i == selectedIndex);
			}
		}

		public void SetCursorTarget(VisualElement target, bool jump)
		{
			cursorTarget = target;
			if (jump)
				cursorJumpPending = true;
		}

		private void UpdateCursor()
		{
			if (panel == null || resolvedStyle.display == DisplayStyle.None)
				return;

			if (cursorTarget == null || cursorTarget.panel == null || IsClipped(cursorTarget))
			{
				cursor.style.visibility = Visibility.Hidden;
				return;
			}

			Rect rect = stage.WorldToLocal(cursorTarget.worldBound);
			if (rect.width <= 0 || rect.height <= 0)
				return;

			bool wasHidden = cursor.resolvedStyle.visibility == Visibility.Hidden;
			bool jump = cursorJumpPending || wasHidden;
			cursor.EnableInClassList(Class("cursor--jump"), jump);
			cursorJumpPending = false;
			cursor.style.visibility = Visibility.Visible;
			cursor.style.left = rect.x - settings.CursorOutset;
			cursor.style.top = rect.y - settings.CursorOutset;
			cursor.style.width = rect.width + settings.CursorOutset * 2;
			cursor.style.height = rect.height + settings.CursorOutset * 2;
		}

		private static bool IsClipped(VisualElement target)
		{
			ScrollView scroller = target.GetFirstAncestorOfType<ScrollView>();
			if (scroller == null)
				return false;
			Rect view = scroller.contentViewport.worldBound;
			Rect rect = target.worldBound;
			return rect.yMax < view.yMin || rect.yMin > view.yMax;
		}

		private void ToggleBreath()
		{
			cursorBreathing = cursorBreathing == false;
			cursorCorner.EnableInClassList(Class("cursor-corner--out"), cursorBreathing);
		}

		public void Pop(string text, bool warning)
		{
			ShopOutlinedLabel pop = MakeOutlined("pop", stage, text);
			pop.EnableInClassList(Class("pop--warning"), warning);
			pop.AddToClassList(Class("pop--enter"));

			VisualElement wallet = stage.Q(className: Class("wallet"));
			Rect walletRect = wallet.layout;
			pop.style.right = stage.resolvedStyle.width - walletRect.xMin + settings.PopWalletGap;
			pop.style.top = walletRect.yMin + settings.PopWalletDrop;

			pop.schedule.Execute(() => pop.RemoveFromClassList(Class("pop--enter")));
			pop.schedule.Execute(() => pop.AddToClassList(Class("pop--exit"))).ExecuteLater(settings.PopVisibleMilliseconds);
			pop.schedule.Execute(pop.RemoveFromHierarchy).ExecuteLater(settings.PopVisibleMilliseconds + settings.PopFadeMilliseconds);
		}
	}
}
