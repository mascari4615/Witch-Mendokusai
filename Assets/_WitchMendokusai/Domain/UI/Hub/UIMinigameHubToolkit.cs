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

		private List<MinigameEntrySO> entries = new();
		private int selectedIndex;

		private MinigameEntrySO CurrentEntry =>
			(entries != null && selectedIndex >= 0 && selectedIndex < entries.Count) ? entries[selectedIndex] : null;

		protected override void BuildUI(VisualElement root)
		{
			root.style.flexGrow = 1;
			root.style.flexDirection = FlexDirection.Column;
			root.style.paddingLeft = 40;
			root.style.paddingRight = 40;
			root.style.paddingTop = 30;
			root.style.backgroundColor = new Color(0.05f, 0.06f, 0.10f, 0.94f);

			Label header = new Label("티메토 시뮬레이션 콘솔");
			header.style.fontSize = 26;
			header.style.color = new Color(0.85f, 0.9f, 1f, 1f);
			header.style.marginBottom = 16;
			root.Add(header);

			VisualElement body = new VisualElement();
			body.style.flexGrow = 1;
			body.style.flexDirection = FlexDirection.Row;
			root.Add(body);

			// 좌: 목록
			entryListParent = new VisualElement { name = "EntryList" };
			entryListParent.style.flexDirection = FlexDirection.Column;
			entryListParent.style.width = 260;
			entryListParent.style.marginRight = 24;
			body.Add(entryListParent);

			// 우: 상세
			VisualElement detail = new VisualElement { name = "Detail" };
			detail.style.flexGrow = 1;
			detail.style.flexDirection = FlexDirection.Column;
			body.Add(detail);

			titleLabel = new Label(string.Empty);
			titleLabel.style.fontSize = 22;
			titleLabel.style.color = Color.white;
			titleLabel.style.marginBottom = 10;
			detail.Add(titleLabel);

			descriptionLabel = new Label(string.Empty);
			descriptionLabel.style.fontSize = 15;
			descriptionLabel.style.color = new Color(0.78f, 0.82f, 0.9f, 1f);
			descriptionLabel.style.whiteSpace = WhiteSpace.Normal;
			descriptionLabel.style.flexGrow = 1;
			detail.Add(descriptionLabel);

			enterButton = new Button(EnterSelected) { name = "EnterButton", text = "접속" };
			enterButton.style.height = 44;
			enterButton.style.fontSize = 18;
			detail.Add(enterButton);
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

		public override void UpdateUI() => RefreshDetail();

		private void SelectEntry(int index)
		{
			selectedIndex = index;
			RefreshDetail();
		}

		private void RefreshDetail()
		{
			MinigameEntrySO entry = CurrentEntry;
			if (entry == null)
			{
				titleLabel.text = "등록된 시뮬레이션 없음";
				descriptionLabel.text = "티메토 NPC 데이터의 PanelInfos 에 MinigameEntrySO 를 추가해야 목록에 뜬다.";
				enterButton.SetEnabled(false);
				return;
			}

			enterButton.SetEnabled(true);
			titleLabel.text = entry.Name;
			descriptionLabel.text = string.IsNullOrEmpty(entry.Tagline)
				? entry.Description
				: entry.Tagline + "\n\n" + entry.Description;
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
				button.style.height = 40;
				button.style.fontSize = 16;
				button.style.marginBottom = 6;
				entryButtons.Add(button);
				entryListParent.Add(button);
			}
		}
	}
}
