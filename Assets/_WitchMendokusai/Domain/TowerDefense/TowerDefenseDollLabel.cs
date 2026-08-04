using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 화면에 뜨는 인형 이름표 한 장(TASK-WM-194) — 「누가 어디 서 있나」.
	///
	/// ★ 왜 매치가 들고 있나: 이름은 *배치 순간*에 정해지고 승급하면 단계가 붙는다. 화면이 매 프레임
	///   유닛에서 캐물으면(GetComponent) 표시와 규칙이 두 경로로 갈라지므로, 매치가 붙인 그대로를 보여준다.
	/// 앵커가 사라지면(팔았다/부서졌다) 이름표도 같이 사라진다 — 없는 아이의 이름이 떠 있으면 안 된다.
	/// </summary>
	public class TowerDefenseDollLabel
	{
		public Transform Anchor { get; }
		public string Name { get; }
		public Color Tint { get; }
		public int Level { get; set; } = 1;

		/// <summary>
		/// 보급이 끊긴 채집 인형 — 캐고는 있지만 코어까지 이어지지 않아 *한 푼도 안 들어온다*.
		/// 이게 화면에 안 보이면 「정수가 왜 0이지」가 판 내내 미궁이 된다(사용자 실증: 정수로만 사는
		/// 연구소·전초기지가 통째로 안 지어짐).
		/// </summary>
		public bool Disconnected { get; set; }

		/// <summary> 전기를 못 받아 멈춘 건물 — 서 있기만 하고 아무 일도 안 한다. </summary>
		public bool Unpowered { get; set; }

		/// <summary>
		/// 이 건물의 「준비됨」 비율(0~1) — 화면의 작은 바가 그린다. 패시브는 늘 1.
		/// 매치가 매 틱 채워 넣는다(화면이 유닛에게 직접 캐물으면 표시와 규칙이 두 경로로 갈라진다).
		/// </summary>
		public float ReadyRatio { get; set; } = 1f;

		/// <summary> 지금 실제로 일하고 있나 — 전기가 끊겼거나 보급이 끊겼으면 false(바가 회색이 된다). </summary>
		public bool Working { get; set; } = true;

		/// <summary> 이 건물의 성장 — 경험치·레벨·아직 안 고른 선택지가 여기 산다. </summary>
		public TowerDefenseBuildingProgress Progress { get; } = new();

		/// <summary> 화면이 이 건물을 가리킬 때 쓰는 고유 번호(선택지 뽑기의 씨앗). </summary>
		public int BuildingId { get; set; }

		/// <summary> 채집인가 — 레벨업 선택지가 갈린다. </summary>
		public bool IsHarvester { get; set; }

		/// <summary>
		/// 사람이 세운 것인가 — 영웅처럼 판이 스스로 만드는 인형과 가른다.
		/// ★ 저장이 이 표를 안 보면 영웅까지 건물로 적어, 이어할 때마다 유령 포탑이 한 채씩 는다.
		/// </summary>
		public bool IsPlacedBuilding { get; set; }

		/// <summary> 무슨 종류로 세웠나(포탑 칸 번호) — 안 적으면 이어할 때 전부 기본형이 된다. </summary>
		public int Variant { get; set; }

		public TowerDefenseDollLabel(Transform anchor, string name, Color tint)
		{
			Anchor = anchor;
			Name = name;
			Tint = tint;
		}

		/// <summary> 화면에 그대로 찍히는 글자 — 1단계면 단계를 안 붙인다(모두에게 「Lv.1」은 잡음이다). </summary>
		public string Text
		{
			get
			{
				string text = Progress.Level > 1 ? Name + " Lv." + Progress.Level : Name;
				if (Level > 1)
					text += " ★" + Level;
				if (Progress.PendingChoices > 0)
					text += "  ✦" + Progress.PendingChoices;

				// ★ 멈춰 있으면 *왜* 멈췄는지를 그 인형 머리 위에서 말해야 한다.
				//   규칙은 이 둘을 이미 갈라 보고 있는데(끊긴 채집은 벌지도 배우지도 않는다) 화면만
				//   전기를 말하지 않았다 — 그러면 「쟤는 왜 아무것도 안 하지」가 짐작으로만 풀린다.
				//   고치는 방법이 서로 다르므로(길을 잇는다 / 발전기를 세운다) 갈라 말한다.
				if (Disconnected)
					text += "\n⚠ 보급 끊김";
				if (Unpowered)
					text += "\n⚡ 전기 없음";
				return text;
			}
		}

		public bool IsAlive => Anchor != null && Anchor.gameObject.activeInHierarchy;
	}
}
