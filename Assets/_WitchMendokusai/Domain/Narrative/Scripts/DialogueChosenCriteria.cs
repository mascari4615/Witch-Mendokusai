using System;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 「그 대화에서 이 답을 골랐나」 조건 (TASK-WM-052). 분기 노드·선택지 조건에 그대로 꽂힌다.
	///
	/// 쓰임새: 「그때 거절했잖아」 · 한 번 받은 부탁을 두 번 안 묻기 · 지난 대답에 따라 태도가 달라지는 대사.
	///
	/// ★ <see cref="DialogueSeenCriteria"/> 로는 왜 안 되나: 그쪽은 **본 적**만 안다.
	///   어느 가지로 갔든 기록이 똑같아서, 지난 선택을 아는 대사를 쓸 수가 없다.
	///
	/// ★ 호출처가 코드에 없는 게 정상이다 — 자산에 박혀 태어나므로(`[SerializeReference]`)
	///   생성자로 이력을 못 받고 <see cref="DialogueHistoryBridge"/> 로 찾아간다.
	///
	/// 이력이 아직 등록 전이면 **안 골랐다**로 친다 — 조건부 가지는 *안 열리는* 쪽으로 넘어진다.
	/// 「덜 진행된 쪽으로 넘어진다」는 이 계열 전체의 규칙이다.
	/// </summary>
	[Serializable]
	public class DialogueChosenCriteria : Criteria
	{
		[SerializeField] private int dialogueId;

		[Tooltip("원고에 쓴 선택지 글자 그대로. 한 글자라도 다르면 안 맞는다.")]
		[SerializeField] private string label;

		[Tooltip("체크 해제하면 「그 답을 고른 적 없으면」이 참이 된다.")]
		[SerializeField] private bool expectedChosen = true;

		public int DialogueId { get => dialogueId; set => dialogueId = value; }
		public string Label { get => label; set => label = value; }
		public bool ExpectedChosen { get => expectedChosen; set => expectedChosen = value; }

		public override int GetCurValue() => HasChosen() ? 1 : 0;

		public override int GetTargetValue() => expectedChosen ? 1 : 0;

		public override bool Evaluate() => HasChosen() == expectedChosen;

		private bool HasChosen()
		{
			DialogueHistory history = DialogueHistoryBridge.Current;
			return history != null && history.HasChosen(dialogueId, label);
		}
	}
}
