using UnityEngine;
using Vector2 = WitchMendokusai.Numerics.Vector2;
using Vector3 = WitchMendokusai.Numerics.Vector3;
using Vector2Int = WitchMendokusai.Numerics.Vector2Int;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	// TowerDefenseHudView 의 매 프레임 갱신 부분. 같은 클래스의 partial 조각이다. 상태(필드)는 TowerDefenseHudView.cs 를 본다.
	public partial class TowerDefenseHudView
	{
		// 본편 UI 복원용 — 숨기기 전 값을 보관(무조건 되돌리면 원래 숨김 상태였던 경우를 깨뜨린다).
		private DisplayStyle baseHudPreviousDisplay = DisplayStyle.Flex;
		private DisplayStyle baseWindowsPreviousDisplay = DisplayStyle.Flex;
		private readonly System.Collections.Generic.List<Canvas> hiddenCanvases = new();


		public void Show(TowerDefenseStageSO stage)
		{
			HideBaseGameUI();
			container.style.display = DisplayStyle.Flex;
			ResetForNewMatch(stage);
		}

		/// <summary>
		/// 새 판 상태로 되돌린다 — 배너 숨김 + 범례/핫바 재구성 + 힌트 복원.
		/// ⚠ 재시작 때 <see cref="Show"/> 를 부르면 안 된다: HideBaseGameUI 가 *이미 숨겨진* 본편 UI 를
		/// 다시 훑어 복원 목록이 빈 채로 덮여, 개척을 나갈 때 본편 UI 가 영영 안 돌아온다.
		/// </summary>
		public void ResetForNewMatch(TowerDefenseStageSO stage)
		{
			if (relicLabel != null)
			{
				relicLabel.style.display = DisplayStyle.None;
				pullButton.style.display = DisplayStyle.None;
			}

			FillLegend(stage);
			FillHotbar(stage, null);
			SetBannerVisible(false);

			// ★ 「지금 설치 모드인가」가 화면 어디에도 없었다(사용자 실증: "확실히 지금이 설치 모드라는
			//   걸 알려줘야함"). 칸을 골랐는지 아닌지로 클릭의 뜻이 통째로 달라지는데, 그걸 손이
			//   기억해야 했다. 안내 줄 맨 앞에 상태를 박는다 — 색까지 바꿔 곁눈으로도 읽히게.
			ApplyHintText(stage);
		}

		public void Hide()
		{
			container.style.display = DisplayStyle.None;
			SetBannerVisible(false);
			RestoreBaseGameUI();
		}

		/// <summary>
		/// 본편 UI 전체 숨김 — 개척은 *다른 게임*이라 본편 UI 가 겹치면 안 된다.
		/// 두 갈래를 모두 덮어야 한다(사용자 실증: 레이어만 껐더니 "체력바·인벤토리가 안 꺼진다"):
		/// ① UI Toolkit 레이어 — HudLayer(핫바/건물바/시계) + WindowsLayer(인벤토리 등 창)
		/// ② 씬의 uGUI 캔버스 — 플레이어 체력바 등은 Toolkit 이 아니라 씬 Canvas 에 있다.
		/// 개척 HUD 는 OverlayLayer(UIDocument) 라 캔버스를 꺼도 살아남는다.
		/// </summary>
		private void HideBaseGameUI()
		{
			if (uiRoot != null)
			{
				if (uiRoot.HudLayer != null)
				{
					baseHudPreviousDisplay = uiRoot.HudLayer.style.display.value;
					uiRoot.HudLayer.style.display = DisplayStyle.None;
				}
				if (uiRoot.WindowsLayer != null)
				{
					baseWindowsPreviousDisplay = uiRoot.WindowsLayer.style.display.value;
					uiRoot.WindowsLayer.style.display = DisplayStyle.None;
				}
			}

			hiddenCanvases.Clear();
			Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude);
			foreach (Canvas canvas in canvases)
			{
				// 루트 캔버스만 — 중첩 캔버스는 부모가 꺼지면 같이 사라진다(이중 처리 시 복원이 꼬임).
				if (canvas.isRootCanvas == false || canvas.enabled == false)
					continue;

				canvas.enabled = false;
				hiddenCanvases.Add(canvas);
			}
		}

		private void RestoreBaseGameUI()
		{
			if (uiRoot != null)
			{
				if (uiRoot.HudLayer != null)
					uiRoot.HudLayer.style.display = baseHudPreviousDisplay;
				if (uiRoot.WindowsLayer != null)
					uiRoot.WindowsLayer.style.display = baseWindowsPreviousDisplay;
			}

			foreach (Canvas canvas in hiddenCanvases)
			{
				if (canvas != null)
					canvas.enabled = true;
			}
			hiddenCanvases.Clear();
		}

		/// <summary> 매 프레임 갱신 — 소유 컨트롤러가 TD 모드 동안 호출. </summary>
		public void Tick(TowerDefenseMatch match, TowerDefenseStageSO stage)
		{
			RefreshDeviceHints();

			if (match == null)
				return;

			// 값이 바뀌었으면(건설 할인 카드) 칸을 다시 그린다 — 화면이 옛 값을 계속 말하면 안 된다.
			// ★ 값뿐 아니라 *칸 수*도 서명에 넣는다 — 연구로 새 칸이 열렸는데 값이 그대로면
			//   화면이 옛 목록을 계속 그린다(해금이 눈에 안 보이면 연구를 왜 했는지 알 수 없다).
			int costSignature = match.CostOf(TowerDefensePlaceableKind.Tower)
				+ match.CostOf(TowerDefensePlaceableKind.Harvester) * 1000
				+ match.AvailableSlots.Count * 1000000;
			if (costSignature != lastHotbarCostSignature)
			{
				lastHotbarCostSignature = costSignature;
				FillHotbar(stage, match);
			}

			resourceValue.text = match.Resource.ToString();

			bool endless = stage == null || stage.Rules.IsEndless;
			waveValue.text = endless
				? (match.WaveIndex + 1).ToString()
				: (match.WaveIndex + 1) + " / " + stage.Rules.WaveCount;

			bool preparing = match.Phase == TowerDefensePhase.Prepare;

			// ★ 실시간에는 「국면」이 없다 — 이 자리가 말해야 할 것은 *지금 판이 어떤 상태인가*다.
			//   압력(시간이 올린 마수 강도)과 적응(내가 한 수단에 기댄 결과)은 판을 바꾸는데도 화면 어디에도
			//   안 나와 있었다 — 안 보이는 규칙은 없는 규칙이다(개선 목록 23번).
			string pressureText = "마수 강도 x" + match.Pressure.ToString("0.0");
			string adaptationText = TowerDefenseAdaptation.Describe(match.Adaptation);
			string heroText = match.HeroRespawnIn > 0f
				? "  ·  영웅 " + Mathf.CeilToInt(match.HeroRespawnIn) + "초 뒤 복귀"
				: string.Empty;

			phaseValue.text = pressureText
				+ (string.IsNullOrEmpty(adaptationText) ? string.Empty : "  ·  " + adaptationText)
				+ heroText;

			// 실시간에는 국면이 없다 — 늘 교전 중이라 이 조건은 항상 참이었다(페이즈제 잔재).
			enemyValue.text = match.AliveEnemyCount.ToString();

			// 「기본 + 채집 N기」로 쪼개 보여준다 — 총액만 보이면 그 숫자가 어디서 왔는지 알 수 없다.
			// 끊긴 채집이 있으면 그 사실이 수입 옆에 붙어야 한다 — 안 그러면 「왜 수입이 줄었지」가 미스터리가 된다.
			string supplyNote = match.DisconnectedHarvesters > 0
				? "  ⚠ 보급 끊김 " + match.DisconnectedHarvesters
				: string.Empty;

			// ★ 「채집 N기」는 *실제로 버는* 수여야 한다. 지은 수를 말하면 다섯 채 중 둘만 일해도
			//   다섯이라 하고, 그러면 「왜 수입이 이것밖에 안 되지」가 영영 안 풀린다.
			//   지은 수와 다르면 둘 다 보여준다 — 「몇 채가 놀고 있나」가 곧 다음에 할 일이다.
			string harvesterNote = match.HarvesterCount > 0
				? (match.WorkingHarvesters == match.HarvesterCount
					? " (기본 " + stage.Rules.BaseWaveIncome + " + 채집 " + match.WorkingHarvesters + "기)"
					: " (기본 " + stage.Rules.BaseWaveIncome + " + 채집 " + match.WorkingHarvesters + "/" + match.HarvesterCount + "기)")
				: string.Empty;
			incomeValue.text = match.NextWaveIncome + harvesterNote;
			incomeValue.text += supplyNote;

			livesValue.text = match.UsesLives ? match.Lives.ToString() : "-";
			livesTopValue.text = livesValue.text; // 같은 값, 보이는 자리만 다르다.
			essenceValue.text = match.NextWaveEssence > 0
				? match.Essence + " (+" + match.NextWaveEssence + ")"
				: match.Essence.ToString();
			nextWaveValue.text = BuildWavePreview(match);
			// ★ 월드에 붙는 것(이름표·체력바·광맥 배수)은 여기서 그리지 않는다 — TickWorldAnchored 가
			//   *카메라가 움직인 뒤에* 그린다. 아래 주석 참고.
			worldTickMatch = match;
			worldTickStage = stage;
			minimap?.Tick(match, stage);
			minimap?.RefreshTerrain(match.MapLayout, stage); // 작은 지도에도 땅이 보여야 한다.
			if (minimapClickBound == false && match != null)
			{
				minimap?.EnableClickToLook(match);
				minimapClickBound = true;
			}
			mapPanel?.Tick(match, stage);

			string boonSummary = match.BoonSummary;
			boonSummaryLabel.text = boonSummary;
			boonSummaryLabel.style.display = string.IsNullOrEmpty(boonSummary) ? DisplayStyle.None : DisplayStyle.Flex;

			// ★ 판이 끝나면 조작 손잡이도 같이 죽어야 한다. 라이브에서 봤다 — 결말 화면이 떠 있는데
			//   멈춤·배속·진행·핫바가 전부 눌릴 것처럼 살아 있었다. 눌러도 아무 일이 없는 스위치는
			//   「안 되는구나」를 알려주는 대신 *바꿨다고 믿게* 만든다(이 판에서 이미 한 번 당한 병이다).
			bool over = match.Outcome != TowerDefenseOutcome.InProgress;
			selectionBar.Root.SetEnabled(over == false);
			waveModeButton.SetEnabled(over == false);

			bool paused = match.SpeedScale <= 0f;
			pauseButton.text = paused ? "▶ 재개" : "⏸ 멈춤";
			pauseButton.SetEnabled(over == false);
			speedButton.text = "배속 ×" + Mathf.Max(1f, match.SpeedScale).ToString("0");
			speedButton.SetEnabled(over == false && paused == false);

			waveModeButton.text = match.AutoAdvanceWaves ? "진행: 자동" : "진행: 수동";
			if (difficultyButton != null)
				difficultyButton.text = "난이도: " + TowerDefenseDifficulty.NameOf(match.Difficulty) + " (다음 판)";
			// ★ 실시간에는 「건설 국면」이 없다 — 그 조건에 걸어두면 이 버튼이 *영원히* 안 눌린다.
			//   규칙은 언제든 부를 수 있게 돼 있는데(RequestNextWave) 화면만 막고 있었다.
			//   기다리는 것이 벌칙이 되지 않게 하려고 넣은 손잡이가, 정작 한 번도 못 쓰였다.
			//   이미 예약했으면 다시 못 누르게 한다 — 예약은 1회성이다.
			nextWaveButton.SetEnabled(match.Outcome == TowerDefenseOutcome.InProgress
				&& match.IsNextWaveRequested == false);
			nextWaveButton.text = match.IsNextWaveRequested ? "곧 온다" : "다음 웨이브 ▶";
		}

		/// <summary>
		/// 자원 노드마다 「×1.9」 벌이 배수를 띄운다 — 노드가 다 똑같아 보이면 「어디로 넓힐까」가 판단이 안 된다.
		/// 화면 밖으로 나간 노드는 감춘다(뒤쪽 노드가 화면 가장자리에 눌어붙는 것 방지).
		/// </summary>
		private TowerDefenseMatch worldTickMatch;
		private TowerDefenseStageSO worldTickStage;
	}
}
