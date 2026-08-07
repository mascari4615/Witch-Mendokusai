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

		[field: SerializeField]
		[field: Tooltip("Speaker 자산이 없을 때 쓸 이름 — 대본에서 만들어진 줄이 여기 이름만 들고 온다.")]
		public string SpeakerName { get; private set; }
		[field: SerializeField, TextArea(3, 10)] public string Text { get; private set; }
		[field: SerializeField] public Sprite Portrait { get; private set; }
		[field: SerializeField] public AudioClip Sfx { get; private set; }
		[field: SerializeField] public float Wait { get; private set; } = 0f;
		[field: SerializeField] public List<DialogueLine> Choices { get; private set; } = new();

		/// <summary>
		/// 대본에서 만들어지는 줄 (TASK-WM-052 — `DialogueScriptGraphBuilder`).
		/// 자산이 아니라 메모리에만 사는 줄이라 파일이 안 생긴다(모드·UGC 가 글만으로 대화를 넣을 길).
		/// 이 통로 말고 다른 데서 값을 바꾸지 말 것 — 나머지는 인스펙터가 정본이다.
		/// </summary>
		public static DialogueLine CreateRuntime(string speakerName, string text, float wait = 0f)
		{
			DialogueLine line = CreateInstance<DialogueLine>();
			line.SpeakerName = speakerName;
			line.Text = text;
			line.Wait = wait;
			line.name = string.IsNullOrEmpty(speakerName) ? "DialogueLine_" : $"DialogueLine_{speakerName}";
			return line;
		}

		/// <summary>말하는 이의 표시 이름 — 자산이 있으면 그 이름, 없으면 대본이 준 이름.</summary>
		public string ResolveSpeakerName()
		{
			if (Speaker == null)
			{
				return SpeakerName;
			}
			return string.IsNullOrEmpty(Speaker.Name) ? Speaker.name : Speaker.Name;
		}
	}
}
