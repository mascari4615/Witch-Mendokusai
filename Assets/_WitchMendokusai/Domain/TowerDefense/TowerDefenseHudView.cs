using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// 특수시공 개척(TD) HUD — 자원/웨이브/국면 + 조작 힌트 + 결과 배너.
	///
	/// ★ 두 가지가 이 클래스의 존재 이유이고 둘 다 사용자 실증에서 나왔다:
	/// 1. **본편 HUD 를 끈다.** 개척은 *다른 게임*인데 핫바·건물바·시계가 그대로 떠 있으면 화면이
	///    두 게임의 UI 가 겹친 상태가 된다("기존 HUD는 꺼야지 다른 게임이니까").
	///    본편 HUD 는 전부 `UIRoot.HudLayer` 에 붙으므로(HotbarView/BuildingBarView/WorldClockView)
	///    그 레이어를 통째 숨기고, 개척 HUD 는 한 단 위 `OverlayLayer` 에 붙어 살아남는다.
	/// 2. **한 줄을 꽉 채우지 않는다.** 전폭 바 + 긴 한 줄 힌트는 읽히지 않는다("텍스트 한줄도 다
	///    채우지 말고"). 좌상단 컴팩트 스탯 + 하단 중앙 짧은 조작 힌트로 분리.
	///
	/// MonoBehaviour 가 아닌 평범한 클래스 — VContainer [Inject] 메서드는 타입당 1개 제약이 있고
	/// 컨트롤러가 이미 Construct 를 쓰므로 소유자가 UIRoot 를 넘겨 생성·구동한다.
	///
	/// ⚠ 비주얼 톤(색·폰트·여백)은 기능 가독성 우선의 v0 임시안 — 정식 톤은 사용자 확정 후.
	/// </summary>
	public class TowerDefenseHudView
	{
		private readonly UIRoot uiRoot;
		private readonly VisualElement container;
		private readonly Label resourceValue;
		private readonly Label waveValue;
		private readonly Label phaseValue;
		private readonly Label hintLabel;
		private readonly Label bannerLabel;

		// 본편 HUD 복원용 — 숨기기 전 값을 보관(무조건 Flex 로 되돌리면 원래 숨김 상태였던 경우를 깨뜨린다).
		private DisplayStyle baseHudPreviousDisplay = DisplayStyle.Flex;

		public TowerDefenseHudView(UIRoot uiRoot)
		{
			this.uiRoot = uiRoot;

			container = new VisualElement { name = nameof(TowerDefenseHudView) };
			container.style.position = Position.Absolute;
			container.style.left = 0;
			container.style.right = 0;
			container.style.top = 0;
			container.style.bottom = 0;
			container.style.display = DisplayStyle.None;
			container.pickingMode = PickingMode.Ignore;

			container.Add(BuildStatPanel(out resourceValue, out waveValue, out phaseValue));
			container.Add(BuildHintBar(out hintLabel));
			container.Add(BuildBanner(out bannerLabel));

			// 본편 HUD(HudLayer)를 숨겨도 개척 HUD 는 살아있어야 하므로 한 단 위 레이어에 붙인다.
			uiRoot.OverlayLayer.Add(container);
		}

		// 좌상단 컴팩트 스탯 — 폭을 내용에 맞춰 좁게(전폭 바 금지).
		private static VisualElement BuildStatPanel(out Label resource, out Label wave, out Label phase)
		{
			VisualElement panel = new VisualElement { name = "StatPanel" };
			panel.style.position = Position.Absolute;
			panel.style.left = 24;
			panel.style.top = 24;
			panel.style.paddingLeft = 14;
			panel.style.paddingRight = 18;
			panel.style.paddingTop = 10;
			panel.style.paddingBottom = 10;
			panel.style.backgroundColor = new Color(0.04f, 0.05f, 0.08f, 0.72f);
			panel.style.borderTopLeftRadius = 6;
			panel.style.borderTopRightRadius = 6;
			panel.style.borderBottomLeftRadius = 6;
			panel.style.borderBottomRightRadius = 6;
			panel.pickingMode = PickingMode.Ignore;

			panel.Add(MakeStatRow("자원", out resource, new Color(1f, 0.86f, 0.35f, 1f)));
			panel.Add(MakeStatRow("웨이브", out wave, new Color(1f, 0.6f, 0.55f, 1f)));
			panel.Add(MakeStatRow("상태", out phase, new Color(0.72f, 0.88f, 1f, 1f)));
			return panel;
		}

		// 「라벨   값」 한 줄 — 라벨은 흐리게, 값은 크고 밝게(스캔이 값으로 먼저 가게).
		private static VisualElement MakeStatRow(string caption, out Label valueLabel, Color valueColor)
		{
			VisualElement row = new VisualElement();
			row.style.flexDirection = FlexDirection.Row;
			row.style.alignItems = Align.Center;
			row.style.marginBottom = 2;
			row.pickingMode = PickingMode.Ignore;

			Label captionLabel = new Label(caption);
			captionLabel.style.fontSize = 12;
			captionLabel.style.color = new Color(0.62f, 0.66f, 0.74f, 1f);
			captionLabel.style.width = 52;
			captionLabel.pickingMode = PickingMode.Ignore;

			valueLabel = new Label(string.Empty);
			valueLabel.style.fontSize = 20;
			valueLabel.style.color = valueColor;
			valueLabel.pickingMode = PickingMode.Ignore;

			row.Add(captionLabel);
			row.Add(valueLabel);
			return row;
		}

		// 하단 중앙 조작 힌트 — 짧게. 전폭으로 늘이지 않는다.
		private static VisualElement BuildHintBar(out Label hint)
		{
			VisualElement wrapper = new VisualElement();
			wrapper.style.position = Position.Absolute;
			wrapper.style.left = 0;
			wrapper.style.right = 0;
			wrapper.style.bottom = 26;
			wrapper.style.alignItems = Align.Center;
			wrapper.pickingMode = PickingMode.Ignore;

			hint = new Label(string.Empty);
			hint.style.fontSize = 13;
			hint.style.color = new Color(0.80f, 0.86f, 0.94f, 1f);
			hint.style.backgroundColor = new Color(0.04f, 0.05f, 0.08f, 0.62f);
			hint.style.paddingLeft = 14;
			hint.style.paddingRight = 14;
			hint.style.paddingTop = 6;
			hint.style.paddingBottom = 6;
			hint.style.unityTextAlign = TextAnchor.MiddleCenter;
			hint.pickingMode = PickingMode.Ignore;

			wrapper.Add(hint);
			return wrapper;
		}

		private static VisualElement BuildBanner(out Label banner)
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

			banner = new Label(string.Empty);
			banner.style.fontSize = 34;
			banner.style.color = new Color(1f, 0.85f, 0.4f, 1f);
			banner.style.backgroundColor = new Color(0.04f, 0.05f, 0.08f, 0.8f);
			banner.style.paddingLeft = 24;
			banner.style.paddingRight = 24;
			banner.style.paddingTop = 12;
			banner.style.paddingBottom = 12;
			banner.pickingMode = PickingMode.Ignore;

			wrapper.Add(banner);
			return wrapper;
		}

		public void Show(TowerDefenseStageSO stage)
		{
			// 본편 HUD(핫바·건물바·시계) 숨김 — 개척은 다른 게임이므로 UI 가 겹치면 안 된다.
			if (uiRoot != null && uiRoot.HudLayer != null)
			{
				baseHudPreviousDisplay = uiRoot.HudLayer.style.display.value;
				uiRoot.HudLayer.style.display = DisplayStyle.None;
			}

			container.style.display = DisplayStyle.Flex;
			SetBannerVisible(false);

			hintLabel.text = stage == null
				? string.Empty
				: $"좌클릭 채집 {stage.HarvesterCost}   ·   우클릭 포탑 {stage.TowerCost}   ·   X 나가기";
		}

		public void Hide()
		{
			container.style.display = DisplayStyle.None;
			SetBannerVisible(false);

			// 본편 HUD 복원 — 개척 진입 전 상태로 되돌린다.
			if (uiRoot != null && uiRoot.HudLayer != null)
				uiRoot.HudLayer.style.display = baseHudPreviousDisplay;
		}

		/// <summary> 매 프레임 갱신 — 소유 컨트롤러가 TD 모드 동안 호출. </summary>
		public void Tick(TowerDefenseMatch match, TowerDefenseStageSO stage)
		{
			if (match == null)
				return;

			resourceValue.text = match.Resource.ToString();

			bool endless = stage == null || stage.Rules.IsEndless;
			waveValue.text = endless
				? (match.WaveIndex + 1).ToString()
				: (match.WaveIndex + 1) + " / " + stage.Rules.WaveCount;

			phaseValue.text = match.Phase switch
			{
				TowerDefensePhase.Prepare => "건설 " + Mathf.CeilToInt(match.PrepareRemaining) + "초",
				TowerDefensePhase.Assault => "방어 중",
				_ => "종료",
			};
		}

		/// <summary> 매치 종료 배너. 무한 모드 패배 = 버틴 웨이브 수가 곧 점수. </summary>
		public void ShowOutcome(TowerDefenseOutcome outcome, int wavesCleared)
		{
			SetBannerVisible(true);
			bannerLabel.text = outcome == TowerDefenseOutcome.Victory
				? "개척 성공"
				: wavesCleared + " 웨이브까지 버팀";
		}

		private void SetBannerVisible(bool visible)
		{
			VisualElement wrapper = bannerLabel.parent;
			if (wrapper != null)
				wrapper.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
		}
	}
}
