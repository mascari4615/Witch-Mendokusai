using UnityEngine;
using Vector2 = WitchMendokusai.Numerics.Vector2;
// ★ 좌표는 판정 쪽 (TASK-WM-214) — 엔진으로 나갈 땐 자동, 엔진에서 받을 땐 캐스트.
using Vector3 = WitchMendokusai.Numerics.Vector3;
using Vector2Int = WitchMendokusai.Numerics.Vector2Int;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	// TowerDefenseHudView 의 Boon 부분. 같은 클래스의 partial 조각이다. 상태(필드)는 TowerDefenseHudView.cs 를 본다.
	public partial class TowerDefenseHudView
	{
		/// <summary>
		/// 카드 한 장 — 이름·설명만. 숫자를 더 얹으면 세 장을 비교하는 데 시간이 걸린다.
		///
		/// ★ 이 모양을 코어 레벨업 선택이 그대로 쓴다. 예전엔 「화면 한가운데 예쁜 카드」와
		///   「선택창의 수수한 버튼」이 따로 있었고, 정작 *뜨는 쪽은 수수한 버튼*이었다
		///   (예쁜 쪽은 부르는 데가 없어 한 번도 안 떴다). 한 벌로 합쳐 뜨는 쪽이 예쁜 것을 쓴다.
		/// ★ compact = 선택창처럼 좁은 자리에 들어갈 때. 같은 카드가 크기만 줄어든다
		///   (모양을 두 벌로 만들면 또 갈라진다).
		/// </summary>
		private VisualElement MakeBoonCard(TowerDefenseBoon boon, System.Action onChosen, bool compact)
		{
			Button card = new Button(() => onChosen());
			card.style.width = compact ? 118 : 200;
			card.style.height = compact ? 96 : 132;
			card.style.marginLeft = compact ? 3 : 10;
			card.style.marginRight = compact ? 3 : 10;
			card.style.backgroundColor = new Color(0.10f, 0.12f, 0.18f, 0.96f);
			card.style.alignItems = Align.Center;
			card.style.justifyContent = Justify.Center;
			SetRadius(card, 10);
			card.style.borderLeftWidth = 2;
			card.style.borderRightWidth = 2;
			card.style.borderTopWidth = 2;
			card.style.borderBottomWidth = 2;
			Color accent = BoonColor(boon.Kind);
			card.style.borderLeftColor = accent;
			card.style.borderRightColor = accent;
			card.style.borderTopColor = accent;
			card.style.borderBottomColor = accent;
			card.pickingMode = PickingMode.Position;

			card.Add(TowerDefenseIcon.Make(BoonIcon(boon.Kind), accent, compact ? 22 : 34));

			Label name = new Label(boon.DisplayName);
			name.style.fontSize = compact ? 13 : 18;
			name.style.color = new Color(0.96f, 0.97f, 1f, 1f);
			name.style.marginTop = compact ? 5 : 10;
			name.pickingMode = PickingMode.Ignore;
			card.Add(name);

			Label note = new Label(boon.Note);
			note.style.fontSize = compact ? 10 : 13;
			note.style.color = accent;
			note.style.marginTop = compact ? 3 : 6;
			note.pickingMode = PickingMode.Ignore;
			card.Add(note);

			return card;
		}

		// 종류마다 색·아이콘이 갈려야 세 장이 한눈에 구분된다(글자를 읽어야 알면 그건 목록이지 카드가 아니다).
		private static Color BoonColor(TowerDefenseBoonKind kind)
		{
			return kind switch
			{
				TowerDefenseBoonKind.Firepower => new Color(1f, 0.55f, 0.45f, 1f),
				TowerDefenseBoonKind.Income => new Color(0.42f, 0.92f, 0.68f, 1f),
				TowerDefenseBoonKind.Bounty => new Color(1f, 0.86f, 0.35f, 1f),
				TowerDefenseBoonKind.Life => new Color(1f, 0.62f, 0.9f, 1f),
				TowerDefenseBoonKind.Essence => new Color(0.7f, 0.6f, 1f, 1f),
				_ => new Color(0.62f, 0.82f, 1f, 1f),
			};
		}

		private static TowerDefenseIcon.Kind BoonIcon(TowerDefenseBoonKind kind)
		{
			return kind switch
			{
				TowerDefenseBoonKind.Firepower => TowerDefenseIcon.Kind.Burst,
				TowerDefenseBoonKind.Income => TowerDefenseIcon.Kind.Ring,
				TowerDefenseBoonKind.Bounty => TowerDefenseIcon.Kind.Diamond,
				TowerDefenseBoonKind.Life => TowerDefenseIcon.Kind.Core,
				TowerDefenseBoonKind.Essence => TowerDefenseIcon.Kind.Snow,
				_ => TowerDefenseIcon.Kind.Leaf,
			};
		}

		private VisualElement unlockPathBox;

		/// <summary>
		/// 연구 길을 편다 — 「이미 연 것 · 다음에 열릴 것 · 그 뒤」 (WM-200).
		///
		/// ★ 표는 규칙층이 준 것 그대로다. 여기서 다시 계산하면 창이 약속한 것과 실제로 열리는 것이
		///   어긋나고, 그건 자원을 잘못 쓰게 만드는 거짓말이 된다.
		/// </summary>
		public void ShowUnlockPath(System.Collections.Generic.IReadOnlyList<TowerDefenseUnlockEntry> path, int researchLevel)
		{
			if (unlockPathBox == null)
				return;

			if (path == null || path.Count == 0)
			{
				unlockPathBox.style.display = DisplayStyle.None;
				return;
			}

			unlockPathBox.style.display = DisplayStyle.Flex;
			unlockPathBox.Clear();

			Label heading = new Label("연구로 열리는 것");
			heading.style.fontSize = 15;
			heading.style.color = new Color(0.62f, 0.68f, 0.78f, 1f);
			heading.style.marginBottom = 8;
			heading.pickingMode = PickingMode.Ignore;
			unlockPathBox.Add(heading);

			int nextLevel = researchLevel + 1;
			for (int i = 0; i < path.Count; i++)
			{
				TowerDefenseUnlockEntry entry = path[i];
				bool opened = entry.Level <= researchLevel;
				bool isNext = entry.Level == nextLevel;

				VisualElement row = new VisualElement();
				row.style.flexDirection = FlexDirection.Row;
				row.style.alignItems = Align.Center;
				row.style.paddingTop = 3;
				row.style.paddingBottom = 3;
				row.style.paddingLeft = 8;
				row.style.paddingRight = 8;
				row.pickingMode = PickingMode.Ignore;
				// 다음에 열릴 줄만 배경으로 띄운다 — 지금 값을 치르면 얻는 것이 그것뿐이라서.
				if (isNext)
				{
					row.style.backgroundColor = new Color(0.16f, 0.28f, 0.48f, 0.75f);
					SetRadius(row, 6);
				}

				Label mark = new Label(opened ? "✔" : (isNext ? "▶" : "·"));
				mark.style.width = 22;
				mark.style.fontSize = 14;
				mark.style.color = opened
					? new Color(0.45f, 0.82f, 0.5f, 1f)
					: (isNext ? new Color(1f, 0.86f, 0.42f, 1f) : new Color(0.45f, 0.5f, 0.6f, 1f));
				mark.pickingMode = PickingMode.Ignore;
				row.Add(mark);

				Label stepLabel = new Label(entry.Level == 0 ? "처음부터" : entry.Level + "단계");
				stepLabel.style.width = 76;
				stepLabel.style.fontSize = 14;
				stepLabel.style.color = opened
					? new Color(0.6f, 0.66f, 0.74f, 1f)
					: new Color(0.72f, 0.78f, 0.88f, 1f);
				stepLabel.pickingMode = PickingMode.Ignore;
				row.Add(stepLabel);

				Label nameLabel = new Label(UnlockName(entry));
				nameLabel.style.fontSize = 15;
				nameLabel.style.color = opened
					? new Color(0.58f, 0.64f, 0.72f, 1f)
					: new Color(0.94f, 0.96f, 1f, 1f);
				nameLabel.pickingMode = PickingMode.Ignore;
				row.Add(nameLabel);

				unlockPathBox.Add(row);
			}
		}

		private static string UnlockName(TowerDefenseUnlockEntry entry)
		{
			return entry.Kind switch
			{
				TowerDefensePlaceableKind.Harvester => "채집 인형",
				TowerDefensePlaceableKind.Wall => "벽",
				TowerDefensePlaceableKind.Trap => "함정",
				TowerDefensePlaceableKind.Outpost => "전초기지",
				TowerDefensePlaceableKind.Generator => "발전 인형",
				TowerDefensePlaceableKind.Tower => "포탑 인형 " + (entry.TowerIndex + 1) + "종",
				_ => "?",
			};
		}
	}
}
