using UnityEngine;
using Vector2 = WitchMendokusai.Numerics.Vector2;
// ★ 좌표는 판정 쪽 (TASK-WM-214) — 엔진으로 나갈 땐 자동, 엔진에서 받을 땐 캐스트.
using Vector3 = WitchMendokusai.Numerics.Vector3;
using Vector2Int = WitchMendokusai.Numerics.Vector2Int;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	// TowerDefenseHudView 의 Outcome 부분. 같은 클래스의 partial 조각이다. 상태(필드)는 TowerDefenseHudView.cs 를 본다.
	public partial class TowerDefenseHudView
	{
		private VisualElement runStatsPanel; // 결과 화면의 기록판 — 판이 끝나야 펴진다.
		private readonly Label bannerLabel;
		private Label relicLabel;
		// 웨이브 사이 드래프트 — 카드가 걸리면 화면 한가운데를 막는다(고르기 전엔 아무것도 못 한다).
		private Label boonSummaryLabel;

		/// <summary> 지금까지 고른 것 — 자원 띠 바로 아래. 안 보이면 「내가 뭘 골랐더라」가 판 내내 미궁이 된다. </summary>
		private static VisualElement BuildBoonSummary(out Label summary)
		{
			VisualElement wrapper = new VisualElement { name = "BoonSummary" };
			wrapper.style.position = Position.Absolute;
			wrapper.style.top = 62;
			wrapper.style.left = 0;
			wrapper.style.right = 0;
			wrapper.style.alignItems = Align.Center;
			wrapper.pickingMode = PickingMode.Ignore;

			summary = new Label(string.Empty);
			summary.style.fontSize = 13;
			summary.style.color = new Color(1f, 0.88f, 0.5f, 0.92f);
			summary.style.display = DisplayStyle.None;
			summary.pickingMode = PickingMode.Ignore;

			wrapper.Add(summary);
			return wrapper;
		}

		private VisualElement BuildBanner(out Label banner, out Button restartButton)
		{
			VisualElement wrapper = new VisualElement();
			wrapper.style.position = Position.Absolute;
			wrapper.style.left = 0;
			wrapper.style.right = 0;
			wrapper.style.top = 200;
			wrapper.style.alignItems = Align.Center;
			wrapper.style.display = DisplayStyle.None;
			wrapper.name = "BannerWrapper";
			wrapper.pickingMode = PickingMode.Ignore;
			bannerWrapper = wrapper;

			// ★ 결말은 *한 덩어리*로 읽혀야 한다. 예전엔 제목에만 어두운 판이 깔리고 요약·유물은
			//   맨바닥에 떠서 지도·인형과 겹쳤다 — 판이 끝난 이유를 되짚으라고 만든 글이 정작
			//   가장 안 읽히는 글이 됐다. 제목·요약·유물을 한 장의 카드에 담아 배경과 갈라놓는다.
			//   (버튼은 카드 밖 — 누르는 것과 읽는 것은 다른 일이다.)
			outcomeCard = new VisualElement();
			outcomeCard.style.backgroundColor = new Color(0.04f, 0.05f, 0.08f, 0.88f);
			outcomeCard.style.paddingLeft = 28;
			outcomeCard.style.paddingRight = 28;
			outcomeCard.style.paddingTop = 16;
			outcomeCard.style.paddingBottom = 18;
			outcomeCard.style.alignItems = Align.Center;
			outcomeCard.style.maxWidth = 720;
			SetRadius(outcomeCard, 10);
			outcomeCard.pickingMode = PickingMode.Ignore;

			banner = new Label(string.Empty);
			banner.style.fontSize = 34;
			banner.style.color = new Color(1f, 0.85f, 0.4f, 1f);
			banner.style.unityTextAlign = TextAnchor.MiddleCenter;
			banner.style.whiteSpace = WhiteSpace.Normal;
			banner.pickingMode = PickingMode.Ignore;

			// 판 밖에 남는 것 — 이번에 번 유물과 보유량. 끝나는 화면에서 바로 보여야 다음 판 이유가 된다.
			relicLabel = new Label(string.Empty);
			relicLabel.style.fontSize = 17;
			relicLabel.style.color = new Color(0.85f, 0.78f, 1f, 1f);
			relicLabel.style.marginTop = 10;
			relicLabel.style.display = DisplayStyle.None;
			relicLabel.pickingMode = PickingMode.Ignore;

			VisualElement buttons = new VisualElement();
			buttons.style.flexDirection = FlexDirection.Row;
			buttons.style.marginTop = 14;
			buttons.pickingMode = PickingMode.Ignore;

			pullButton = MakeActionButton("인형 뽑기", fontSize: 16, () => PullRequested());
			pullButton.style.marginRight = 8;
			pullButton.style.display = DisplayStyle.None;

			// 끝났는데 다음 행동이 화면에 없으면 게임이 아니라 정지 화면이 된다 — 배너 바로 아래 재시작.
			restartButton = MakeActionButton("다시 도전", fontSize: 18, () => RestartRequested());

			buttons.Add(pullButton);
			buttons.Add(restartButton);

			outcomeCard.Add(banner);
			wrapper.Add(outcomeCard);

			summaryLabel = new Label(string.Empty);
			summaryLabel.style.fontSize = 14;
			summaryLabel.style.color = new Color(0.8f, 0.85f, 0.95f, 1f);
			summaryLabel.style.marginTop = 10;
			summaryLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
			summaryLabel.style.whiteSpace = WhiteSpace.Normal;
			summaryLabel.style.display = DisplayStyle.None;
			summaryLabel.pickingMode = PickingMode.Ignore;
			outcomeCard.Add(summaryLabel);

			// 기록판 — 판이 끝나야 펴진다(판이 도는 동안엔 이 숫자들이 화면에 없다).
			runStatsPanel = new VisualElement { name = "RunStats" };
			runStatsPanel.style.marginTop = 14;
			runStatsPanel.style.display = DisplayStyle.None;
			outcomeCard.Add(runStatsPanel);

			outcomeCard.Add(relicLabel);
			wrapper.Add(buttons);
			return wrapper;
		}

		/// <summary>
		/// 매치 종료 배너. 무한 모드 패배 = 버틴 웨이브 수가 곧 점수 —
		/// 기록 갱신 여부까지 말해야 「다시 도전」이 이유를 갖는다.
		/// </summary>
		public void ShowOutcome(TowerDefenseOutcome outcome, int survivedSeconds, int nestsDestroyed, int score, int best,
			int lairsAwakened, int lairsCleared,
			bool isNewRecord, int relicsGained, int relicBalance, bool canPull, string summary = null)
		{
			SetBannerVisible(true);
			SetBestRecord(best);
			ShowRelicResult(relicsGained, relicBalance, canPull);

			// 문구 규칙은 화면 밖(TowerDefenseOutcomeText)에 있다 — 화면 없이도 시험할 수 있어야
			// 「무슨 일이 일어났는지 제대로 말하나」를 자동으로 물을 수 있다.
			bannerLabel.text = TowerDefenseOutcomeText.Build(
				outcome, FormatDuration(survivedSeconds), nestsDestroyed, score, best, isNewRecord);
			// ★ 이겼을 때도 요약은 붙어야 한다 — 예전엔 여기서 빠져나가 이긴 판을 되짚을 수단이 없었다.
			ShowSummary(summary);
			ShowRunStats(survivedSeconds, nestsDestroyed, score, best, lairsAwakened, lairsCleared);
		}

		/// <summary>
		/// 판 요약 — 「왜 졌는지」를 되짚을 유일한 자리. 없으면 매 판이 같은 실수의 반복이 된다.
		/// 배너 아래에 조용히 붙인다(결말 문구를 밀어내지 않게).
		/// </summary>
		/// <summary>
		/// 판이 끝난 뒤에야 펴는 기록판 — 버틴 시간·웨이브·처치·기록.
		///
		/// ★ 판이 도는 동안에는 이 숫자들을 화면에 안 붙인다 (사용자 지시: "런타임에 표시 안했으면
		///   좋겠음 … 게임 끝났을때 통계처럼 공개하는게 맞음. Like RiskofRain 2").
		///   계산은 내내 돌고 있었다 — *보여주는 시점*만 끝으로 옮긴 것이다.
		/// </summary>
		private void ShowRunStats(int survivedSeconds, int nestsDestroyed, int score, int best, int lairsAwakened, int lairsCleared)
		{
			if (runStatsPanel == null)
				return;

			runStatsPanel.Clear();
			AddRunStat("버틴 시간", FormatDuration(survivedSeconds));
			AddRunStat("넘긴 웨이브", waveValue != null ? waveValue.text : "-");
			AddRunStat("부순 둥지", nestsDestroyed.ToString());
			// ★ 세기만 하고 아무 데도 안 나오던 값 — 「얼마나 파고들었나」를 말하는 자리가 여기다.
			AddRunStat("깨운 서식지", lairsAwakened.ToString());
			AddRunStat("쓸어낸 서식지", lairsCleared.ToString());
			AddRunStat("남은 마수", enemyValue != null ? enemyValue.text : "-");
			AddRunStat("점수", score.ToString());
			AddRunStat("최고 기록", best.ToString());
			runStatsPanel.style.display = DisplayStyle.Flex;
		}

		private void AddRunStat(string caption, string value)
		{
			VisualElement row = new VisualElement();
			row.style.flexDirection = FlexDirection.Row;
			row.style.justifyContent = Justify.SpaceBetween;
			row.style.marginTop = 4;
			row.style.minWidth = 260;

			Label captionLabel = new Label(caption);
			captionLabel.style.fontSize = 14;
			captionLabel.style.color = new Color(0.72f, 0.78f, 0.9f, 1f);

			Label valueLabel = new Label(value);
			valueLabel.style.fontSize = 16;
			valueLabel.style.color = new Color(0.96f, 0.98f, 1f, 1f);

			row.Add(captionLabel);
			row.Add(valueLabel);
			runStatsPanel.Add(row);
		}

		private void ShowSummary(string summary)
		{
			if (summaryLabel == null)
				return;

			summaryLabel.text = summary ?? string.Empty;
			summaryLabel.style.display = string.IsNullOrEmpty(summary) ? DisplayStyle.None : DisplayStyle.Flex;
		}

		private Label summaryLabel;

		/// <summary> 결말 한 덩어리 — 제목·요약·유물이 같은 판 위에 앉는다(버튼은 밖). </summary>
		private VisualElement outcomeCard;

		/// <summary> 결말 화면 껍데기 — 보임/숨김의 주인(안쪽 구조와 무관하게 이것만 토글한다). </summary>
		private VisualElement bannerWrapper;

		/// <summary> 초 → 「3분 20초」. 숫자만 던지면 몇 분인지 사람이 암산해야 한다. </summary>
		private static string FormatDuration(int seconds)
		{
			if (seconds < 60)
				return seconds + "초";
			return seconds / 60 + "분 " + seconds % 60 + "초";
		}

		/// <summary>
		/// 결말 화면의 유물·뽑기 — 「이번 판에서 무엇을 얻었고, 그걸로 무엇을 할 수 있나」가
		/// 끝나는 화면 안에서 닫혀야 다시 도전할 이유가 생긴다.
		/// </summary>
		private void ShowRelicResult(int relicsGained, int relicBalance, bool canPull)
		{
			if (relicLabel == null)
				return;

			relicLabel.text = "유물 +" + relicsGained + "  (보유 " + relicBalance + ")";
			relicLabel.style.display = DisplayStyle.Flex;
			pullButton.SetEnabled(canPull);
			pullButton.style.display = DisplayStyle.Flex;
			pullButton.text = "인형 뽑기";
		}

		/// <summary> 뽑기 결과 — 무엇이 나왔는지 그 자리에서 말한다. </summary>
		public void ShowPullResult(TowerDefenseTowerArchetype pulled, int relicBalance, bool canPull)
		{
			if (relicLabel == null)
				return;

			relicLabel.text = (pulled != null ? "「" + pulled.DisplayName + "」 획득" : "인형 획득")
				+ "  (유물 " + relicBalance + ")";
			pullButton.SetEnabled(canPull);
		}

		/// <summary> 최고 기록 — 점수(초 환산). 기록 없으면 「-」. </summary>
		public void SetBestRecord(int bestScore)
		{
			bestValue.text = bestScore > 0 ? FormatDuration(bestScore) : "-";
		}

		private void SetBannerVisible(bool visible)
		{
			// ★ 「제목의 부모」로 잡으면 안 된다 — 실제로 그렇게 짜여 있다가, 제목·요약을 카드 한 장에
			//   묶는 순간 부모가 카드로 바뀌어 *바깥 껍데기가 영영 숨은 채*로 남았다(결말 화면이 통째로
			//   안 떴다). 껍데기는 이름으로 잡는다 — 안쪽 구조를 바꿔도 안 흔들린다.
			if (bannerWrapper != null)
				bannerWrapper.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
		}
	}
}
