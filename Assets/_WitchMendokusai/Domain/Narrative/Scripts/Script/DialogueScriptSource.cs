using System;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 글로 쓴 대화 한 편 (TASK-WM-052). 텍스트 파일을 물고 있다가 필요할 때 그래프로 세운다.
	///
	/// ★ 이것이 「원고 → 게임」의 마지막 칸이다. 지금까지는 읽는 법(<see cref="DialogueScriptParser"/>)과
	///   세우는 법(<see cref="DialogueScriptGraphBuilder"/>)만 있고, **게임이 그 글을 어디서 얻는지**가 없었다.
	///
	/// `DataSO` 를 상속하므로 다른 데이터처럼 ID/이름을 갖는다 — 대화 이력(<see cref="DialogueHistory"/>)이
	/// 그 ID 로 「이 대화를 봤나」를 기억한다.
	///
	/// 세운 그래프는 **한 번만 만들고 재사용**한다(같은 대화를 다시 걸어도 다시 안 세운다).
	/// 대사 줄은 메모리에만 사는 사본이라 자산 파일이 안 생긴다.
	/// </summary>
	[CreateAssetMenu(fileName = "DialogueScript_", menuName = "WM/Narrative/DialogueScript")]
	public class DialogueScriptSource : DataSO
	{
		[field: Header("_" + nameof(DialogueScriptSource))]
		[field: SerializeField]
		[field: Tooltip("설계 문서 원고 그대로. `> 이름: \"대사\"` 줄만 읽고 산문은 넘긴다.")]
		public TextAsset Script { get; private set; }

		[NonSerialized] private DialogueGraph builtGraph;
		[NonSerialized] private ParsedDialogueScript parsedScript;

		/// <summary>글을 읽은 결과(대사·장면·걸린 것). 매번 새로 읽는다 — 검사·미리보기용.</summary>
		public ParsedDialogueScript ParseFresh() => DialogueScriptParser.Parse(Script == null ? null : Script.text);

		/// <summary>재생할 그래프. 처음 부를 때 세우고 그 뒤로는 같은 것을 준다.</summary>
		public DialogueGraph BuildGraph()
		{
			if (builtGraph != null)
			{
				return builtGraph;
			}
			parsedScript = ParseFresh();
			builtGraph = DialogueScriptGraphBuilder.Build(parsedScript);
			return builtGraph;
		}

		/// <summary>세우면서 읽은 결과도 같이 — 걸린 것을 화면·콘솔에 보여줄 때.</summary>
		public DialogueGraph BuildGraph(out ParsedDialogueScript parsed)
		{
			DialogueGraph graph = BuildGraph();
			parsed = parsedScript;
			return graph;
		}

		/// <summary>
		/// 원고를 바꿨을 때 다시 세우게 한다(에디터에서 글을 고치고 바로 확인하는 자리).
		/// 재생 중에 부르면 지금 흐르는 대화는 옛 그래프를 계속 쓴다 — 그게 안전하다(도중에 발밑이 바뀌지 않음).
		/// </summary>
		public void Invalidate()
		{
			builtGraph = null;
			parsedScript = null;
		}
	}
}
