using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>재생해 달라고 들어온 한 건. 원고 한 편이거나, 옛 방식의 대사 한 줄이다.</summary>
	public readonly struct DialoguePlayRequest
	{
		public DialogueScriptSource Script { get; }
		public DialogueLine Line { get; }

		/// <summary>
		/// 이미 세워진 그래프를 직접 트는 경우(원고 자산 없이). 미리보기 창·시험·직접 호출이 쓴다.
		///
		/// ★ 왜 여기 있어야 하나: 그래프 입구가 **줄을 안 서고** 바로 틀던 시절이 있었다.
		///   그러면 말하는 중에 그래프를 걸면 앞 대화가 그냥 끊기고, 반대로 그래프가 트는 중에
		///   원고를 걸면 그래프 쪽이 끊긴다 — 어느 쪽도 「사라졌다」는 흔적을 안 남긴다.
		/// </summary>
		public DialogueGraph Graph { get; }

		public Transform SpeakerTransform { get; }

		public DialoguePlayRequest(DialogueScriptSource script, DialogueLine line, Transform speakerTransform,
			DialogueGraph graph = null)
		{
			Script = script;
			Line = line;
			SpeakerTransform = speakerTransform;
			Graph = graph;
		}

		public bool IsEmpty => Script == null && Line == null && Graph == null;

		/// <summary>같은 것을 또 넣었는지 — 연타·중복 트리거 거르기용.</summary>
		public bool SameContentAs(DialoguePlayRequest other) =>
			Script == other.Script && Line == other.Line && Graph == other.Graph;
	}

	/// <summary>
	/// 대화 재생 차례 (TASK-WM-052).
	///
	/// ★ 왜 필요한가: 여태 대화가 재생 중일 때 다른 대화를 시작하면 **앞 대화를 그냥 끊었다.**
	///   퀘스트 보상 대사와 NPC 말이 겹치는 순간이 실제로 있는데, 그러면 **한 편이 통째로 사라진다** —
	///   플레이어는 「무슨 말을 하다 말았는지」조차 모른다. 사라지게 두지 말고 차례를 세운다.
	///
	/// 규칙:
	/// <list type="bullet">
	/// <item>들어온 순서대로(FIFO) — 이야기는 순서가 뜻이다.</item>
	/// <item>같은 것이 이미 줄에 있으면 안 넣는다(연타·중복 트리거).</item>
	/// <item>줄이 꽉 차면 **새 것을 버린다**. 오래된 걸 버리면 앞 이야기가 사라져 순서가 깨진다.</item>
	/// </list>
	/// 순수 자료구조 — Unity I/O 0. 그래서 이 규칙이 화면 없이 검증된다.
	/// </summary>
	public sealed class DialoguePlayQueue
	{
		public const int DEFAULT_CAPACITY = 8;

		private readonly List<DialoguePlayRequest> pending = new();
		private readonly int capacity;

		public DialoguePlayQueue(int capacity = DEFAULT_CAPACITY)
		{
			this.capacity = capacity < 1 ? 1 : capacity;
		}

		public int Count => pending.Count;
		public bool IsEmpty => pending.Count == 0;

		/// <summary>줄 세우기. 넣었으면 true — false 는 「빈 요청·중복·꽉 참」 셋 중 하나다.</summary>
		public bool Enqueue(DialoguePlayRequest request)
		{
			if (request.IsEmpty)
			{
				return false;
			}
			for (int i = 0; i < pending.Count; i++)
			{
				if (pending[i].SameContentAs(request))
				{
					return false;
				}
			}
			if (pending.Count >= capacity)
			{
				return false;
			}
			pending.Add(request);
			return true;
		}

		/// <summary>다음 차례를 꺼낸다. 없으면 false(꺼낸 값은 비어 있다).</summary>
		public bool TryDequeue(out DialoguePlayRequest request)
		{
			if (pending.Count == 0)
			{
				request = default;
				return false;
			}
			request = pending[0];
			pending.RemoveAt(0);
			return true;
		}

		/// <summary>줄을 통째로 비운다 — 「지금 대화 그만」이면 기다리던 것도 같이 접는 게 맞다.</summary>
		public void Clear() => pending.Clear();
	}
}
