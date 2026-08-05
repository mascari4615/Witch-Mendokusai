using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// 티메토 시뮬레이션 콘솔 — 「게임 속 게임」 진입 허브 (TASK-WM-195).
	/// 정본 = `memo/wm/design/systems/game-in-game-hub.md`.
	///
	/// 레이아웃 = 사용자 컨펌 A안(목록 + 상세 2단) — `UIDungeonEntranceToolkit` 과 동형 구조.
	/// 그 패널이 이미 게임에서 도는 검증된 모양이라 그대로 따랐다(빠르고 안전 + 설명글 공간이 넓어
	/// 「티메토가 만든 시뮬레이션 세계」 연출을 실을 수 있음).
	///
	/// 입장 = `GameModeManager.SetMode(entry.TargetGameMode)` 한 줄. 실제 진입/이탈(카메라·입력·매치)은
	/// 각 모드 컨트롤러가 OnModeChanged 구독으로 전담하므로 허브는 *트리거만* 쥔다(마도서 '⚔ 투기장'
	/// 진입이 쓰던 것과 동일 계약 — 그래서 이관이 부작용 0).
	///
	/// ⚠ 비주얼 톤(색·폰트·여백·썸네일)은 미확정 — 기능 가독성 우선 v0. 사용자 컨펌 후 정식화.
	/// </summary>
	public class UIMinigameHubToolkit : UIToolkitPanel
	{
		public override string Name => "시뮬레이션 콘솔";
		public override bool IsFullscreen => true;

		private readonly List<Button> entryButtons = new();
		private VisualElement entryListParent;
		private Label titleLabel;
		private Label descriptionLabel;
		private Button enterButton;
		private Label taglineLabel;

		private List<MinigameEntrySO> entries = new();
		private int selectedIndex;

		private MinigameEntrySO CurrentEntry =>
			(entries != null && selectedIndex >= 0 && selectedIndex < entries.Count) ? entries[selectedIndex] : null;

		protected override void BuildUI(VisualElement root)
		{
			// ★ 좌우 이동은 손으로 잡는다. 위아래(목록 안)는 기본 규칙이 알아서 하지만, 목록에서
			//   오른쪽 시작 버튼으로는 넘어가지지 않았다(사용자 실증) — 기본 규칙은 화면상의 위치로
			//   다음 대상을 *추측*하는데, 목록과 버튼이 서로 다른 상자에 담겨 있어 그 추측이 빗나간다.
			//   어디로 갈지가 정해져 있는 창이므로 추측에 맡기지 않고 못 박는다.
			root.RegisterCallback<NavigationMoveEvent>(OnNavigationMove);
			root.style.flexGrow = 1;
			root.style.flexDirection = FlexDirection.Column;
			root.style.paddingLeft = 48;
			root.style.paddingRight = 48;
			root.style.paddingTop = 36;
			// ★ 바닥 여백이 없었다 (실측) — 그래서 시작 버튼이 화면 끝에 붙어 *잘린 채* 떴다.
			//   사용자 실증: "시작 버튼이 이상한데". 창의 가장 중요한 손잡이가 화면 밖으로 나가 있었다.
			root.style.paddingBottom = 36;
			root.style.backgroundColor = new Color(0.04f, 0.05f, 0.08f, 0.985f);
			// ★ 창을 화면 가득 늘리지 않는다 (실측: 글은 위 1/4 에만 있고 아래가 텅 빈 채 시작 버튼만
			//   저 멀리 떨어져 있었다). 내용만 한 덩어리로 묶어 화면 가운데 앉히면 눈이 한 번에 담는다.
			root.style.justifyContent = Justify.Center;
			root.style.alignItems = Align.Center;

			VisualElement window = new VisualElement { name = "HubWindow" };
			window.style.flexDirection = FlexDirection.Column;
			window.style.width = Length.Percent(100);
			window.style.maxWidth = 1400;
			window.style.maxHeight = 660;
			window.style.flexGrow = 0;

			VisualElement headerRow = new VisualElement();
			headerRow.style.flexDirection = FlexDirection.Row;
			headerRow.style.alignItems = Align.Center;
			headerRow.style.marginBottom = 6;
			window.Add(headerRow);

			Label header = new Label("티메토 시뮬레이션 콘솔");
			header.style.fontSize = 26;
			header.style.color = new Color(0.85f, 0.9f, 1f, 1f);
			headerRow.Add(header);

			VisualElement headerSpacer = new VisualElement();
			headerSpacer.style.flexGrow = 1;
			headerRow.Add(headerSpacer);

			Label closeHint = new Label("X 닫기");
			closeHint.style.fontSize = 14;
			closeHint.style.color = new Color(0.5f, 0.56f, 0.66f, 1f);
			headerRow.Add(closeHint);

			// 제목과 내용을 가르는 선 — 창이 하나의 덩어리로 읽히게.
			VisualElement divider = new VisualElement();
			divider.style.height = 2;
			divider.style.marginBottom = 20;
			divider.style.backgroundColor = new Color(1f, 1f, 1f, 0.08f);
			window.Add(divider);

			VisualElement body = new VisualElement();
			body.style.flexGrow = 1;
			body.style.flexDirection = FlexDirection.Row;
			window.Add(body);
			root.Add(window);

			// 좌: 목록
			entryListParent = new VisualElement { name = "EntryList" };
			entryListParent.style.flexDirection = FlexDirection.Column;
			entryListParent.style.width = 300;
			entryListParent.style.marginRight = 24;
			body.Add(entryListParent);

			// 우: 상세 — 눈에 보이는 판으로 만든다. 배경이 없으면 글이 게임 화면 위에 흩뿌려진 것처럼
			//   보여서 「창」으로 안 읽힌다(실측: 목록·설명·버튼이 서로 남남처럼 떨어져 있었다).
			VisualElement detail = new VisualElement { name = "Detail" };
			detail.style.flexGrow = 1;
			detail.style.flexDirection = FlexDirection.Column;
			detail.style.backgroundColor = new Color(0.07f, 0.09f, 0.13f, 1f);
			detail.style.paddingLeft = 28;
			detail.style.paddingRight = 28;
			detail.style.paddingTop = 24;
			detail.style.paddingBottom = 24;
			detail.style.borderTopLeftRadius = 12;
			detail.style.borderTopRightRadius = 12;
			detail.style.borderBottomLeftRadius = 12;
			detail.style.borderBottomRightRadius = 12;
			body.Add(detail);

			titleLabel = new Label(string.Empty);
			titleLabel.style.fontSize = 26;
			titleLabel.style.color = Color.white;
			titleLabel.style.marginBottom = 10;
			detail.Add(titleLabel);

			taglineLabel = new Label(string.Empty);
			taglineLabel.style.fontSize = 15;
			taglineLabel.style.color = new Color(1f, 0.82f, 0.45f, 1f);
			taglineLabel.style.marginBottom = 14;
			taglineLabel.style.whiteSpace = WhiteSpace.Normal;
			detail.Add(taglineLabel);

			descriptionLabel = new Label(string.Empty);
			descriptionLabel.style.fontSize = 15;
			descriptionLabel.style.color = new Color(0.78f, 0.82f, 0.9f, 1f);
			descriptionLabel.style.whiteSpace = WhiteSpace.Normal;
			descriptionLabel.style.flexGrow = 1;
			detail.Add(descriptionLabel);

			// 바닥 줄 — 시작 버튼은 여기 담긴다. 설명이 늘어나도 버튼이 화면 밖으로 밀리지 않게
			// *창 안쪽*에 자리를 잡아 준다(실측: 설명이 자라며 버튼을 화면 끝까지 밀어냈다).
			VisualElement footer = new VisualElement();
			footer.style.flexDirection = FlexDirection.Row;
			footer.style.justifyContent = Justify.FlexEnd;
			footer.style.marginTop = 20;
			detail.Add(footer);

			// 시작 버튼 — 이 창에서 *유일하게 판을 여는* 손잡이라 한눈에 띄어야 한다.
			enterButton = new Button(EnterSelected) { name = "EnterButton", text = "시작하기" };
			enterButton.style.height = 62;
			enterButton.style.width = 260;
			enterButton.style.fontSize = 22;
			enterButton.style.marginTop = 0;
			enterButton.style.backgroundColor = new Color(0.20f, 0.45f, 0.75f, 1f);
			enterButton.style.color = new Color(0.96f, 0.98f, 1f, 1f);
			enterButton.style.borderLeftWidth = 0;
			enterButton.style.borderRightWidth = 0;
			enterButton.style.borderTopWidth = 0;
			enterButton.style.borderBottomWidth = 0;
			enterButton.style.borderTopLeftRadius = 8;
			enterButton.style.borderTopRightRadius = 8;
			enterButton.style.borderBottomLeftRadius = 8;
			enterButton.style.borderBottomRightRadius = 8;
			footer.Add(enterButton);
		}

		public override void SetNPC(NPCObject npc)
		{
			entries = NPCUtil.GetMinigameEntries(npc.Data);
			EnsureButtonCount(entries.Count);

			for (int i = 0; i < entryButtons.Count; i++)
			{
				if (i < entries.Count)
				{
					entryButtons[i].text = entries[i].Name;
					entryButtons[i].style.display = DisplayStyle.Flex;
				}
				else
				{
					entryButtons[i].style.display = DisplayStyle.None;
				}
			}
		}

		protected override void OnOpen()
		{
			SelectEntry(0);
			FocusSelectedEntry();
		}

		/// <summary>
		/// 열자마자 첫 항목에 *키보드 포커스*를 준다 — 고른 것을 색으로만 표시하고 포커스를 안 주면
		/// 마우스를 한 번 잡아야 키보드가 듣는다(사용자 실증: "다른 UI 는 뭐가 포커싱돼서 키보드만으로도
		/// 조작이 가능한데 티메토는 그게 안 된다").
		///
		/// ★ 한 프레임 미뤄서 준다 — 여는 프레임엔 요소가 아직 패널에 안 붙어 있어 포커스가 그냥 버려진다.
		/// </summary>
		private void FocusSelectedEntry()
		{
			if (entryButtons.Count == 0)
				return;

			Button target = entryButtons[Mathf.Clamp(selectedIndex, 0, entryButtons.Count - 1)];
			target.schedule.Execute(() => target.Focus());
		}

		/// <summary>
		/// 좌우 = 목록 ↔ 시작 버튼. 위아래는 손대지 않는다(목록 안 이동은 기본 규칙이 이미 맞게 한다).
		/// 오른쪽으로 갔다가 왼쪽으로 오면 *고르던 그 항목*으로 돌아온다 — 처음으로 되감기면
		/// 「내가 뭘 고르고 있었지」를 다시 찾아야 한다.
		/// </summary>
		private void OnNavigationMove(NavigationMoveEvent evt)
		{
			if (evt.direction == NavigationMoveEvent.Direction.Right)
			{
				if (enterButton == null || enterButton.style.display == DisplayStyle.None)
					return;
				enterButton.Focus();
				evt.StopPropagation();
				return;
			}

			if (evt.direction == NavigationMoveEvent.Direction.Left)
			{
				if (entryButtons.Count == 0)
					return;
				entryButtons[Mathf.Clamp(selectedIndex, 0, entryButtons.Count - 1)].Focus();
				evt.StopPropagation();
			}
		}

		public override void UpdateUI() => RefreshDetail();

		private void SelectEntry(int index)
		{
			selectedIndex = index;
			RefreshEntryHighlight();
			RefreshDetail();
		}

		private void RefreshDetail()
		{
			MinigameEntrySO entry = CurrentEntry;
			if (entry == null)
			{
				titleLabel.text = "등록된 시뮬레이션 없음";
				if (taglineLabel != null)
					taglineLabel.style.display = DisplayStyle.None;
				descriptionLabel.text = "티메토 NPC 데이터의 PanelInfos 에 MinigameEntrySO 를 추가해야 목록에 뜬다.";
				// ★ 「할 수 없다」는 *보이되 꺼져* 있어야 한다 — 버튼이 사라지면 사람은 「시작 버튼이
				//   없는데?」가 된다(사용자 실증). 무엇을 하는 창인지는 늘 보여야 한다.
				enterButton.text = "시작하기";
				enterButton.SetEnabled(false);
				return;
			}

			enterButton.SetEnabled(true);
			titleLabel.text = entry.Name;

			// ★ 한 줄 소개는 *그 줄의 자리*가 따로 있다 — 예전엔 설명 앞에 이어 붙여서 회색 글 뭉치가
			//   됐고, 정작 색을 준 자리는 처음 한 번 숨겨진 뒤 다시 안 켜졌다(죽은 요소).
			//   그래서 창이 밋밋했다: 무엇이 제목이고 무엇이 한 줄 소개인지 눈으로 안 갈렸다.
			bool hasTagline = string.IsNullOrEmpty(entry.Tagline) == false;
			taglineLabel.style.display = hasTagline ? DisplayStyle.Flex : DisplayStyle.None;
			taglineLabel.text = hasTagline ? entry.Tagline : string.Empty;
			descriptionLabel.text = entry.Description;
		}

		private void EnterSelected()
		{
			MinigameEntrySO entry = CurrentEntry;
			if (entry == null)
				return;

			// 트리거만 — 카메라/입력/매치는 대상 모드 컨트롤러가 OnModeChanged 로 전담.
			PanelGroup.ClosePanel();
			GameModeManager.Instance.SetMode(entry.TargetGameMode);
		}

		private void EnsureButtonCount(int count)
		{
			while (entryButtons.Count < count)
			{
				int capturedIndex = entryButtons.Count;
				Button button = new Button(() => SelectEntry(capturedIndex));
				button.style.height = 52;
				button.style.fontSize = 17;
				button.style.marginBottom = 8;
				button.style.unityTextAlign = TextAnchor.MiddleLeft;
				button.style.paddingLeft = 16;
				// 왼쪽에 굵은 띠 — 고른 항목을 색이 아니라 *모양*으로도 가른다(색만으로는 안 읽힌다).
				button.style.borderLeftWidth = 4;
				entryButtons.Add(button);
				entryListParent.Add(button);
			}
		}

		/// <summary>
		/// 고른 항목을 눈에 보이게 — 목록에서 *지금 무엇을 보고 있나*가 안 보이면 오른쪽 설명이
		/// 누구 것인지 알 수 없다(고른 티가 안 나는 목록은 목록이 아니라 나열이다).
		/// </summary>
		private void RefreshEntryHighlight()
		{
			Color activeBar = new Color(1f, 0.78f, 0.35f, 1f);
			Color idleBar = new Color(1f, 1f, 1f, 0.08f);
			Color activeBack = new Color(0.16f, 0.19f, 0.27f, 1f);
			Color idleBack = new Color(0.09f, 0.11f, 0.16f, 1f);

			for (int index = 0; index < entryButtons.Count; index++)
			{
				bool chosen = index == selectedIndex;
				entryButtons[index].style.borderLeftColor = chosen ? activeBar : idleBar;
				entryButtons[index].style.backgroundColor = chosen ? activeBack : idleBack;
				entryButtons[index].style.color = chosen
					? new Color(1f, 0.94f, 0.82f, 1f)
					: new Color(0.78f, 0.82f, 0.9f, 1f);
			}
		}
	}
}
