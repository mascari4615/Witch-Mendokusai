using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// 특수시공 개척(TD) HUD — 자원/웨이브/국면/준비 카운트다운 + 조작 힌트 + 결과 배너.
	/// v0 UI: 코드-빌드 VisualElement 를 `UIRoot.HudLayer`(상시 HUD 레이어)에 부착
	/// (SettingView/CauldronMapController 와 동일 관례, 씬 아트·UXML 불요).
	///
	/// MonoBehaviour 가 아닌 **평범한 클래스** — VContainer [Inject] 메서드는 타입당 1개 제약이 있고
	/// 컨트롤러가 이미 Construct 를 쓰므로, 소유자(TowerDefenseModeController)가 UIRoot 를 넘겨
	/// 생성·구동하는 편이 배선이 짧다. 생명주기 = 컨트롤러가 Show/Hide/Tick 호출.
	///
	/// ⚠ 비주얼 톤(색·폰트·여백)은 미확정 — 기능 가독성 우선의 v0 임시안. 사용자 컨펌 후 정식화.
	/// </summary>
	public class TowerDefenseHudView
	{
		private readonly VisualElement container;
		private readonly Label statusLabel;
		private readonly Label hintLabel;
		private readonly Label bannerLabel;

		public TowerDefenseHudView(UIRoot uiRoot)
		{
			container = new VisualElement { name = nameof(TowerDefenseHudView) };
			container.style.position = Position.Absolute;
			container.style.left = 0;
			container.style.right = 0;
			container.style.top = 0;
			container.style.display = DisplayStyle.None;
			container.pickingMode = PickingMode.Ignore;

			VisualElement bar = new VisualElement();
			bar.style.flexDirection = FlexDirection.Column;
			bar.style.alignItems = Align.Center;
			bar.style.paddingTop = 10;
			bar.style.paddingBottom = 10;
			bar.style.backgroundColor = new Color(0f, 0f, 0f, 0.55f);
			bar.pickingMode = PickingMode.Ignore;

			statusLabel = MakeLabel(22, new Color(1f, 1f, 1f, 1f));
			hintLabel = MakeLabel(15, new Color(0.75f, 0.85f, 1f, 1f));
			bar.Add(statusLabel);
			bar.Add(hintLabel);
			container.Add(bar);

			// 결과 배너 — 화면 중앙. 패배(무한 모드) 시 「N 웨이브까지 버팀」.
			bannerLabel = MakeLabel(38, new Color(1f, 0.85f, 0.4f, 1f));
			bannerLabel.style.position = Position.Absolute;
			bannerLabel.style.left = 0;
			bannerLabel.style.right = 0;
			bannerLabel.style.top = 220;
			bannerLabel.style.display = DisplayStyle.None;
			container.Add(bannerLabel);

			uiRoot.HudLayer.Add(container);
		}

		private static Label MakeLabel(int fontSize, Color color)
		{
			Label label = new Label(string.Empty);
			label.style.fontSize = fontSize;
			label.style.color = color;
			label.style.unityTextAlign = TextAnchor.MiddleCenter;
			label.pickingMode = PickingMode.Ignore;
			return label;
		}

		public void Show(TowerDefenseStageSO stage)
		{
			container.style.display = DisplayStyle.Flex;
			bannerLabel.style.display = DisplayStyle.None;
			hintLabel.text = stage == null
				? string.Empty
				: $"좌클릭 = 채집 인형 {stage.HarvesterCost} (자원 노드 위에만)    ·    우클릭 = 포탑 인형 {stage.TowerCost}    ·    X = 나가기";
		}

		public void Hide()
		{
			container.style.display = DisplayStyle.None;
			bannerLabel.style.display = DisplayStyle.None;
		}

		/// <summary> 매 프레임 갱신 — 소유 컨트롤러가 TD 모드 동안 호출. </summary>
		public void Tick(TowerDefenseMatch match, TowerDefenseStageSO stage)
		{
			if (match == null)
				return;

			// 웨이브 표기: 무한이면 총수 없이 「웨이브 N」, 유한이면 「웨이브 N / M」.
			bool endless = stage == null || stage.Rules.IsEndless;
			string waveText = endless
				? $"웨이브 {match.WaveIndex + 1}"
				: $"웨이브 {match.WaveIndex + 1} / {stage.Rules.WaveCount}";

			string phaseText = match.Phase switch
			{
				TowerDefensePhase.Prepare => $"건설 {Mathf.CeilToInt(match.PrepareRemaining)}초",
				TowerDefensePhase.Assault => "방어 중",
				_ => "종료",
			};

			statusLabel.text = $"자원 {match.Resource}      {waveText}      {phaseText}";
		}

		/// <summary> 매치 종료 배너. 무한 모드 패배 = 버틴 웨이브 수가 곧 점수. </summary>
		public void ShowOutcome(TowerDefenseOutcome outcome, int wavesCleared)
		{
			bannerLabel.style.display = DisplayStyle.Flex;
			bannerLabel.text = outcome == TowerDefenseOutcome.Victory
				? "개척 성공"
				: $"개척지 함락 — {wavesCleared} 웨이브까지 버팀";
		}
	}
}
