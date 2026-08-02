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

		public TowerDefenseDollLabel(Transform anchor, string name, Color tint)
		{
			Anchor = anchor;
			Name = name;
			Tint = tint;
		}

		/// <summary> 화면에 그대로 찍히는 글자 — 1단계면 단계를 안 붙인다(모두에게 「Lv.1」은 잡음이다). </summary>
		public string Text => Level > 1 ? Name + " ★" + Level : Name;

		public bool IsAlive => Anchor != null && Anchor.gameObject.activeInHierarchy;
	}
}
