using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	// 전체 메뉴의 화면 트리. 모양은 FullMenu.uss
	public partial class FullMenuView
	{
		private const string NAV_CLASS = "wm-fullmenu-nav";
		private const string HIDDEN_CLASS = "wm-fullmenu--hidden";
		private const int TILES_PER_ROW = 2;

		// 메뉴 항목 하나. Hotkey 가 있으면 그 단축키가 이 빌드에서 보일 때만 항목을 보이고 키 글자를 옆에 붙임
		private sealed class Entry
		{
			public InputEventType? Hotkey;
			public Action Run;
			public bool KeyboardOnly;
			public bool TouchOnly;
			// 있으면 열 때마다 물어 거짓이면 숨김 (지금 자리에서 할 수 없는 동작)
			public Func<bool> IsAvailable;
			public bool IsTile;
			public Button Button;
		}

		private readonly List<Entry> entries = new();

		private Label nyangLabel;
		private Label placeLabel;
		private Label placeSubLabel;
		private Label timeLabel;
		private Label dollNameLabel;
		private Label dollLevelLabel;
		private VisualElement dollPortrait;
		private Button closeButton;
		private Texture2D glowTexture;
		private MenuBackdrop backdrop;
		private VisualElement tileGrid;
		private Image coreImage;

		private void SetBlurred(bool isBlurred) => backdrop.SetBlurred(isBlurred);

		private void Update() => backdrop.Repaint();

		private void BuildUI()
		{
			// 배경: 흐린 게임 화면. 화면 끝까지라 container 에. UI 연출을 끄면 단색 막 (USS)
			backdrop = new MenuBackdrop(container, Settings.BlurOutput);

			stage = new VisualElement();
			stage.AddToClassList("wm-fullmenu__stage");
			safeContent.Add(stage);
			safeContent.RegisterCallback<GeometryChangedEvent>(_ => FitStage());

			BuildTop();

			VisualElement body = new VisualElement();
			body.AddToClassList("wm-fullmenu__body");
			stage.Add(body);

			VisualElement mainColumn = new VisualElement();
			mainColumn.AddToClassList("wm-fullmenu__main");
			body.Add(mainColumn);
			AddWindowEntry(mainColumn, "wm-fullmenu-main", "인벤토리", "장비와 재료", InputEventType.Inventory);
			AddWindowEntry(mainColumn, "wm-fullmenu-main", "퀘스트", "의뢰와 목표", InputEventType.QuestToggle);
			AddWindowEntry(mainColumn, "wm-fullmenu-main", "업적", "모은 기록", InputEventType.AchievementToggle);
			AddWindowEntry(mainColumn, "wm-fullmenu-main", "마도서", "배운 마법", InputEventType.MagicBookToggle);
			AddWindowEntry(mainColumn, "wm-fullmenu-main", "인형", "편성과 상태", InputEventType.DollToggle);

			BuildCenter(body);

			// 작은 칸은 보이는 것만 두 개씩 줄을 지음 (RefreshEntries). 빌드와 조작 방식에 따라 수가 달라짐
			tileGrid = new VisualElement();
			tileGrid.AddToClassList("wm-fullmenu__tiles");
			body.Add(tileGrid);
			AddWindowEntry(null, "wm-fullmenu-tile", "스탯", null, InputEventType.Status);
			AddWindowEntry(null, "wm-fullmenu-tile", "도감", null, InputEventType.DiscoveryToggle);

			Entry keybind = AddEntry(null, "wm-fullmenu-tile", "단축키", null, Settings.KeybindIcon, () => OpenWindow(InputEventType.KeybindHelpToggle));
			keybind.Hotkey = InputEventType.KeybindHelpToggle;
			keybind.KeyboardOnly = true;
			AddKeyLabel(keybind);

			// 스테이지 안에서만. 예전 설정창 버튼
			AddEntry(null, "wm-fullmenu-tile", "끼임 탈출", null, Settings.UnstuckIcon, AskUnstuck).IsAvailable = HasStage;

			// 폰에만: 키 하나에 매달린 조작과 버튼 자리 옮기기. 예전 폰 창 서랍에 있던 것
			AddWindowEntry(null, "wm-fullmenu-tile", "시점", null, InputEventType.CameraViewCycle).TouchOnly = true;
			AddWindowEntry(null, "wm-fullmenu-tile", "1인칭", null, InputEventType.CameraPerspectiveToggle).TouchOnly = true;
			AddWindowEntry(null, "wm-fullmenu-tile", "건축", null, InputEventType.BuildModeToggle).TouchOnly = true;
			AddEntry(null, "wm-fullmenu-tile", "자리 옮기기", null, Icons.LayoutEdit, BeginLayoutEdit).TouchOnly = true;

			BuildBottom();
		}

		private void BeginLayoutEdit()
		{
			Close();
			GetComponent<MobileControlsView>().BeginLayoutEdit();
		}

		private void BuildTop()
		{
			VisualElement top = new VisualElement();
			top.AddToClassList("wm-fullmenu__top");
			stage.Add(top);

			VisualElement nyang = new VisualElement();
			nyang.AddToClassList("wm-fullmenu-nyang");
			nyang.Add(MakeIcon(Settings.CoinIcon, "wm-fullmenu-nyang__icon"));
			nyangLabel = new Label();
			nyangLabel.AddToClassList("wm-fullmenu-nyang__value");
			nyang.Add(nyangLabel);
			top.Add(nyang);

			VisualElement spacer = new VisualElement { pickingMode = PickingMode.Ignore };
			spacer.AddToClassList("wm-fullmenu__spacer");
			top.Add(spacer);

			// 제목은 양옆 칸 너비와 무관하게 화면 가운데
			Label title = new Label("메뉴") { pickingMode = PickingMode.Ignore };
			title.AddToClassList("wm-fullmenu__title");
			stage.Add(title);

			closeButton = KeyHint.KeyButton("닫기", "ESC", Close);
			closeButton.AddToClassList("wm-fullmenu-close");
			RegisterNav(closeButton);
			top.Add(closeButton);
		}

		private void BuildCenter(VisualElement body)
		{
			VisualElement center = new VisualElement { pickingMode = PickingMode.Ignore };
			center.AddToClassList("wm-fullmenu__center");
			body.Add(center);

			VisualElement core = new VisualElement { pickingMode = PickingMode.Ignore };
			core.AddToClassList("wm-fullmenu-core");
			center.Add(core);

			VisualElement glow = new VisualElement { pickingMode = PickingMode.Ignore };
			glow.AddToClassList("wm-fullmenu-core__glow");
			glowTexture = MenuStage.CreateGlowTexture(Settings.GlowTextureSize, "FullMenuGlow");
			glow.style.backgroundImage = new StyleBackground(glowTexture);
			core.Add(glow);

			// 그림은 처음 열 때 연결 (EnsureCoreStage)
			coreImage = new Image { pickingMode = PickingMode.Ignore, scaleMode = ScaleMode.ScaleToFit };
			coreImage.AddToClassList("wm-fullmenu-core__model");
			core.Add(coreImage);

			// 코어 아래 지금 자리 표시. 동심 타원 셋과 가운데 점
			VisualElement where = MenuStage.CreateWhereRings();
			VisualElement pin = new VisualElement { pickingMode = PickingMode.Ignore };
			pin.AddToClassList("wm-fullmenu-where__pin");
			where.Add(pin);
			center.Add(where);

			placeLabel = new Label { pickingMode = PickingMode.Ignore };
			placeLabel.AddToClassList("wm-fullmenu-place");
			center.Add(placeLabel);

			// 던전 안에서는 큰 글이 던전 이름, 이 줄이 그 던전이 쓰는 스테이지 이름
			placeSubLabel = new Label { pickingMode = PickingMode.Ignore };
			placeSubLabel.AddToClassList("wm-fullmenu-place-sub");
			center.Add(placeSubLabel);

			timeLabel = new Label { pickingMode = PickingMode.Ignore };
			timeLabel.AddToClassList("wm-fullmenu-time");
			center.Add(timeLabel);
		}

		private void BuildBottom()
		{
			VisualElement bottom = new VisualElement();
			bottom.AddToClassList("wm-fullmenu__bottom");
			stage.Add(bottom);

			VisualElement me = new VisualElement { pickingMode = PickingMode.Ignore };
			me.AddToClassList("wm-fullmenu-me");
			dollPortrait = new VisualElement();
			dollPortrait.AddToClassList("wm-fullmenu-me__portrait");
			me.Add(dollPortrait);
			dollNameLabel = new Label();
			dollNameLabel.AddToClassList("wm-fullmenu-me__name");
			me.Add(dollNameLabel);
			dollLevelLabel = new Label();
			dollLevelLabel.AddToClassList("wm-fullmenu-me__level");
			me.Add(dollLevelLabel);
			bottom.Add(me);

			VisualElement system = new VisualElement();
			system.AddToClassList("wm-fullmenu__system");
			bottom.Add(system);
			AddEntry(system, "wm-fullmenu-system", "설정", null, Settings.SettingIcon, OpenSetting);
			// 던전 판 중에만. 예전 설정창 버튼. 메뉴는 월드에서만 열려 로비로를 숨길 일이 없음
			AddEntry(system, "wm-fullmenu-system", "던전 포기", null, Settings.DungeonExitIcon, AskDungeonExit).IsAvailable = () => LobbyManager.IsRunInProgress;
			AddEntry(system, "wm-fullmenu-system", "로비로", null, Settings.LobbyIcon, GoToLobby).Button.tooltip = SettingView.LOBBY_TOOLTIP;
			AddEntry(system, "wm-fullmenu-system", "게임 종료", null, Settings.QuitIcon, AskQuit);
		}

		private Entry AddWindowEntry(VisualElement parent, string ussClass, string label, string description, InputEventType hotkey)
		{
			Entry entry = AddEntry(parent, ussClass, label, description, Icons.For(hotkey), () => OpenWindow(hotkey));
			entry.Hotkey = hotkey;
			AddKeyLabel(entry);
			return entry;
		}

		private Entry AddEntry(VisualElement parent, string ussClass, string label, string description, Texture2D icon, Action run)
		{
			Entry entry = new Entry { Run = run };
			Button button = new Button(() =>
			{
				// 닫히는 중에 들어온 확인 입력은 버림
				if (IsOpen == false)
					return;
				UiSfx.Play(UiSfxEvent.Confirm);
				entry.Run();
			});
			button.AddToClassList(ussClass);
			button.Add(MakeIcon(icon, ussClass + "__icon"));

			VisualElement texts = new VisualElement { pickingMode = PickingMode.Ignore };
			texts.AddToClassList(ussClass + "__texts");
			Label name = new Label(label) { pickingMode = PickingMode.Ignore };
			name.AddToClassList(ussClass + "__label");
			texts.Add(name);
			if (string.IsNullOrEmpty(description) == false)
			{
				Label detail = new Label(description) { pickingMode = PickingMode.Ignore };
				detail.AddToClassList(ussClass + "__desc");
				texts.Add(detail);
			}
			button.Add(texts);

			RegisterNav(button);
			// parent 가 없으면 작은 칸. 줄은 RefreshEntries 가 지음
			parent?.Add(button);
			entry.IsTile = parent == null;
			entry.Button = button;
			entries.Add(entry);
			return entry;
		}

		// 단축키 글자. 손가락 조작에서는 KeyHint 규칙으로 숨음
		private static void AddKeyLabel(Entry entry)
		{
			string path = KeybindRegistry.GetCurrentPath(entry.Hotkey.Value);
			if (string.IsNullOrEmpty(path))
				return;
			Label key = KeyHint.KeyOnly(new Label(path.Substring(path.LastIndexOf('/') + 1).ToUpperInvariant()));
			key.pickingMode = PickingMode.Ignore;
			key.AddToClassList("wm-fullmenu-key");
			entry.Button.Add(key);
		}

		private static VisualElement MakeIcon(Texture2D texture, string ussClass)
		{
			VisualElement icon = new VisualElement { pickingMode = PickingMode.Ignore };
			icon.AddToClassList(ussClass);
			icon.style.backgroundImage = new StyleBackground(texture);
			return icon;
		}

		// 이 빌드에서 숨는 단축키의 항목은 메뉴에서도 숨김. 키보드 전용과 폰 전용 칸은 지금 조작 방식에 맞춰
		private void RefreshEntries()
		{
			bool isTouch = inputManager.IsTouchMode;
			tileGrid.Clear();
			VisualElement row = null;
			foreach (Entry entry in entries)
			{
				bool visible = entry.Hotkey.HasValue == false || KeybindRegistry.IsShownInThisBuild(entry.Hotkey.Value);
				if (entry.KeyboardOnly && isTouch)
					visible = false;
				if (entry.TouchOnly && isTouch == false)
					visible = false;
				if (entry.IsAvailable != null && entry.IsAvailable() == false)
					visible = false;
				entry.Button.EnableInClassList(HIDDEN_CLASS, visible == false);

				if (entry.IsTile == false || visible == false)
					continue;
				if (row == null || row.childCount >= TILES_PER_ROW)
				{
					row = new VisualElement();
					row.AddToClassList("wm-fullmenu__tile-row");
					tileGrid.Add(row);
				}
				row.Add(entry.Button);
			}
		}

		private void FitStage() => MenuStage.Fit(safeContent, stage, Settings.ReferenceHeight, Settings.MinimumWidth);
	}
}
