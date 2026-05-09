using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 한 라인의 대사 데이터. Speaker 가 Text 를 말하고, Portrait/Sfx 로 연출.
	/// Wait 후 다음 라인 (DialogueRunner — Phase 1.3 예정) 또는 Choices 분기.
	/// Choices 는 Phase 2 (DialogueGraph 노드 통합) 에서 ChoiceNode 로 대체될 placeholder.
	/// </summary>
	[CreateAssetMenu(fileName = "DialogueLine_", menuName = "WM/Narrative/DialogueLine")]
	public class DialogueLine : DataSO
	{
		[field: Header("_" + nameof(DialogueLine))]
		[field: SerializeField] public DataSO Speaker { get; private set; }
		[field: SerializeField, TextArea(3, 10)] public string Text { get; private set; }
		[field: SerializeField] public Sprite Portrait { get; private set; }
		[field: SerializeField] public AudioClip Sfx { get; private set; }
		[field: SerializeField] public float Wait { get; private set; } = 0f;
		[field: SerializeField] public List<DialogueLine> Choices { get; private set; } = new();
	}
}
