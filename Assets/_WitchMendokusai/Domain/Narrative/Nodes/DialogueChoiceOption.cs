using System;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 선택지 한 칸 (TASK-WM-052). 라벨 + *보일 조건*.
	///
	/// 조건이 비어 있으면 항상 보인다(지금까지의 거동). 조건이 있으면 그 조건이 맞을 때만 뜬다 —
	/// 「열쇠를 가진 사람에게만 보이는 대답」 같은 것. 조건 표현은 <see cref="Criteria"/> 를 그대로 쓴다
	/// (분기 노드와 같은 것 — 대화 안에 조건 언어가 둘 생기지 않게).
	///
	/// ★ 라벨과 조건을 *한 칸* 으로 묶은 이유: 라벨 목록과 조건 목록을 따로 두면 순서가 어긋나는 순간
	///   조용히 엉뚱한 선택지가 잠긴다. 한 칸이면 어긋날 수가 없다.
	/// </summary>
	[Serializable]
	public class DialogueChoiceOption
	{
		[SerializeField] private string label;
		[SerializeReference] private Criteria condition;

		public string Label { get => label; set => label = value; }
		public Criteria Condition { get => condition; set => condition = value; }

		public DialogueChoiceOption()
		{
		}

		public DialogueChoiceOption(string label, Criteria condition = null)
		{
			this.label = label;
			this.condition = condition;
		}

		/// <summary>조건이 없거나 조건이 맞으면 보인다.</summary>
		public bool IsAvailable() => condition == null || condition.Evaluate();

		/// <summary>조건 없는 선택지를 라벨만으로 — 목록 초기화(`{ "A", "B" }`)를 그대로 쓰기 위한 것.</summary>
		public static implicit operator DialogueChoiceOption(string label) => new(label);
	}
}
