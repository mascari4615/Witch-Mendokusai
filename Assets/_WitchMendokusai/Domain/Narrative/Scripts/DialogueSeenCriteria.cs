using System;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 「그 대화를 본 적 있나」 조건 (TASK-WM-052). 분기 노드·선택지 조건에 그대로 꽂힌다
	/// (기존 <see cref="Criteria"/> 계열이라 새 조건 언어가 아니다).
	///
	/// 쓰임새: 처음 만났을 때만 하는 인사 / 이미 들은 이야기는 건너뛰기 /
	/// 「그 얘기 아까 했잖아」 같은 반응.
	///
	/// ★ 호출처가 코드에 없는 게 정상이다 — 이 타입은 **자산에 박혀서 태어난다**(`[SerializeReference]`).
	///   그래서 생성자로 이력을 못 받고 <see cref="DialogueHistoryBridge"/> 로 찾아간다.
	///   (「아무도 안 부르는 층」 감사에 걸리면 이 문단을 근거로 보라 — 부르는 것은 코드가 아니라 데이터다.)
	///
	/// 이력이 아직 등록 전이면 **못 봤다**로 친다. 여기서 터뜨리면 저장을 안 불러온 첫 프레임에
	/// 대화가 통째로 죽는데, 그건 「처음 보는 것으로 취급」보다 훨씬 나쁜 결말이다.
	/// </summary>
	[Serializable]
	public class DialogueSeenCriteria : Criteria
	{
		[SerializeField] private int dialogueId;
		[SerializeField] private DialogueSeenKind kind = DialogueSeenKind.Completed;

		[Tooltip("체크 해제하면 「아직 안 본 경우」가 참이 된다 — 처음 만났을 때만 하는 인사에 쓴다.")]
		[SerializeField] private bool expectedSeen = true;

		public int DialogueId { get => dialogueId; set => dialogueId = value; }
		public DialogueSeenKind Kind { get => kind; set => kind = value; }
		public bool ExpectedSeen { get => expectedSeen; set => expectedSeen = value; }

		public override int GetCurValue() => HasSeen() ? 1 : 0;

		public override int GetTargetValue() => expectedSeen ? 1 : 0;

		public override bool Evaluate() => HasSeen() == expectedSeen;

		private bool HasSeen()
		{
			DialogueHistory history = DialogueHistoryBridge.Current;
			return history != null && history.HasSeen(dialogueId, kind);
		}
	}
}
