using UnityEngine;
// ★ 초점 좌표는 판정 쪽 (TASK-WM-214).
using Vector3 = WitchMendokusai.Numerics.Vector3;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// 펼치는 지도 (TASK-WM-194) — 미니맵은 곁눈질용이고, 이건 *들여다보는* 지도다.
	///
	/// ★ 사용자 지시: "미니맵만 두지말고, 맵을 UI 열 수 있게. 맵에 범례가 들어가는게 맞음.
	///   맵에 이제 각 요소에 마우스 올리면 그거에 대한 정보가 나오고. 그런식으로."
	///
	/// ★ 왜 범례가 여기로 오나: 범례는 *판을 보면서* 읽는 글이 아니라 「저게 뭐였지」 할 때 찾는 글이다.
	///   판 옆에 상시로 펼쳐 두면 화면의 4분의 1을 먹으면서도 정작 급할 땐 안 읽힌다(사용자 실증:
	///   "범례 자체도 너무 많은 정보가 포함되어 있고"). 지도를 여는 행위가 곧 「알아보자」라서 짝이 맞는다.
	/// </summary>
	public sealed class TowerDefenseMapPanel
	{
		// ★ 사용자 지시: "지도 너무 작아. 화면 꽉차게." 지도는 곁눈질이 아니라 *들여다보는* 것이라
		//   화면을 채워야 한다 — 세로에 맞춰 정사각으로 키운다(가로로 늘리면 판이 찌그러진다).
		private const float SCREEN_MARGIN = 96f;

		private readonly VisualElement root;
		private readonly TowerDefenseMinimapView map;
		private readonly VisualElement legendHost;

		private bool clickBound;

		public VisualElement Root => root;
		public bool IsOpen { get; private set; }

		/// <summary> 지도를 눌렀다 — 그 자리로 시점을 옮기라는 뜻(지도는 그대로 열려 있다). </summary>
		public event System.Action<Vector3> LookAtRequested = delegate { };

		public TowerDefenseMapPanel(VisualElement legend)
		{
			root = new VisualElement { name = "MapPanel" };
			root.style.position = Position.Absolute;
			root.style.left = 0;
			root.style.right = 0;
			root.style.top = 0;
			root.style.bottom = 0;
			root.style.alignItems = Align.Center;
			root.style.justifyContent = Justify.Center;
			root.style.backgroundColor = new Color(0.02f, 0.03f, 0.05f, 0.82f);
			root.style.display = DisplayStyle.None;
			// 지도가 열려 있는 동안의 클릭은 지도 것이다 — 뒤쪽 땅에 건물이 서면 안 된다.
			root.pickingMode = PickingMode.Position;

			VisualElement card = new VisualElement();
			card.style.flexDirection = FlexDirection.Row;
			card.style.backgroundColor = new Color(0.05f, 0.06f, 0.09f, 0.96f);
			card.style.alignItems = Align.Center;
			card.style.paddingLeft = 18;
			card.style.paddingRight = 18;
			card.style.paddingTop = 16;
			card.style.paddingBottom = 16;
			card.style.borderTopLeftRadius = 12;
			card.style.borderTopRightRadius = 12;
			card.style.borderBottomLeftRadius = 12;
			card.style.borderBottomRightRadius = 12;
			root.Add(card);

			VisualElement left = new VisualElement();
			left.style.alignItems = Align.Center;
			card.Add(left);

			Label title = new Label("지도");
			title.style.fontSize = 20;
			title.style.color = new Color(0.92f, 0.94f, 0.98f, 1f);
			title.style.marginBottom = 8;
			left.Add(title);

			float mapSize = Mathf.Max(320f, Screen.height - SCREEN_MARGIN - 120f);
			map = new TowerDefenseMinimapView(mapSize, floating: false) { ShowTooltips = true };
			map.Clicked += focus => LookAtRequested(focus);
			left.Add(map.Root);

			Label hint = new Label("M 또는 「지도」 버튼으로 닫는다");
			hint.style.fontSize = 12;
			hint.style.color = new Color(0.6f, 0.66f, 0.76f, 1f);
			hint.style.marginTop = 8;
			left.Add(hint);

			// 범례는 지도 오른쪽에 붙는다 — 지도를 보면서 바로 대조할 수 있어야 뜻이 있다.
			legendHost = new VisualElement();
			legendHost.style.marginLeft = 18;
			legendHost.style.maxHeight = mapSize + 60f;
			if (legend != null)
			{
				// ★ 범례는 원래 화면 구석에 *절대 좌표*로 박혀 있었다 — 그대로 넣으면 카드 밖으로
				//   삐져나간다(실측). 지도 안에서는 부모가 자리를 정하도록 흐름에 되돌린다.
				legend.style.position = Position.Relative;
				legend.style.left = StyleKeyword.Auto;
				legend.style.right = StyleKeyword.Auto;
				legend.style.top = StyleKeyword.Auto;
				legend.style.bottom = StyleKeyword.Auto;
				legendHost.Add(legend);
			}
			card.Add(legendHost);
		}

		public void SetOpen(bool open)
		{
			IsOpen = open;
			root.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
		}

		public void Toggle() => SetOpen(IsOpen == false);

		/// <summary> 열려 있을 때만 그린다 — 닫힌 지도를 매 프레임 다시 그릴 이유가 없다. </summary>
		public void Tick(TowerDefenseMatch match, TowerDefenseStageSO stage)
		{
			if (IsOpen == false || match == null)
				return;

			if (clickBound == false)
			{
				map.EnableClickToLook(match);
				clickBound = true;
			}
			map.RefreshTerrain(match.MapLayout, stage);
			map.Tick(match, stage);
		}
	}
}
