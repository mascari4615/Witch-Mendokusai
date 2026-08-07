using System;
using System.Collections.Generic;

namespace WitchMendokusai
{
	/// <summary>어느 시점을 묻는가 — 「말이라도 걸어 봤나」와 「끝까지 들었나」는 다른 이야기다.</summary>
	public enum DialogueSeenKind
	{
		/// <summary>한 번이라도 시작한 적 있다(도중에 끊었어도 포함).</summary>
		Started = 0,

		/// <summary>끝까지 갔다(중간에 끊은 것은 안 센다).</summary>
		Completed = 1,
	}

	/// <summary>
	/// 「이 대화를 본 적 있나」 기록 (TASK-WM-052).
	///
	/// ★ 왜 필요한가: 조건 분기·조건 선택지가 생겼는데 정작 **대화 자신의 이력**을 물을 수가 없었다.
	///   「처음 만났을 때만 하는 인사」 같은, 서사에서 제일 자주 쓰는 조건이 표현 불가였다.
	///
	/// 순수 POCO — Unity 의존 0. 저장은 <see cref="ToSaveData"/> / <see cref="FromSaveData"/> 로
	/// 목록만 주고받는다(`RuntimeXxxSaveData` 선례).
	/// </summary>
	public sealed class DialogueHistory
	{
		private readonly HashSet<int> started = new();
		private readonly HashSet<int> completed = new();

		public void MarkStarted(int dialogueId) => started.Add(dialogueId);

		/// <summary>끝까지 간 대화 — 시작 기록도 같이 남는다(끝났으면 시작한 것이므로).</summary>
		public void MarkCompleted(int dialogueId)
		{
			started.Add(dialogueId);
			completed.Add(dialogueId);
		}

		public bool HasSeen(int dialogueId, DialogueSeenKind kind) =>
			kind == DialogueSeenKind.Completed ? completed.Contains(dialogueId) : started.Contains(dialogueId);

		public DialogueHistorySaveData ToSaveData() => new()
		{
			StartedDialogueIds = new List<int>(started),
			CompletedDialogueIds = new List<int>(completed),
		};

		public void FromSaveData(DialogueHistorySaveData saveData)
		{
			started.Clear();
			completed.Clear();

			if (saveData.StartedDialogueIds != null)
			{
				for (int i = 0; i < saveData.StartedDialogueIds.Count; i++)
				{
					started.Add(saveData.StartedDialogueIds[i]);
				}
			}
			if (saveData.CompletedDialogueIds == null)
			{
				return;
			}
			for (int i = 0; i < saveData.CompletedDialogueIds.Count; i++)
			{
				// 저장본이 「끝났다」만 들고 있어도 「시작했다」로도 세야 앞뒤가 맞는다.
				started.Add(saveData.CompletedDialogueIds[i]);
				completed.Add(saveData.CompletedDialogueIds[i]);
			}
		}
	}

	[Serializable]
	public struct DialogueHistorySaveData
	{
		public List<int> StartedDialogueIds;
		public List<int> CompletedDialogueIds;
	}

	/// <summary>
	/// 대화 이력 static accessor — `SharedBrewChannelBridge` 동형(Bridge 패턴).
	/// 조건(<see cref="DialogueSeenCriteria"/>)은 자산에 박혀 태어나므로 생성자로 무엇도 못 받는다.
	/// 그래서 「지금 이 게임의 이력」은 여기로 찾아온다. <see cref="DialogueRunner"/> 가 등록한다.
	/// </summary>
	public static class DialogueHistoryBridge
	{
		private static DialogueHistory history;

		public static void Register(DialogueHistory dialogueHistory) => history = dialogueHistory;

		/// <summary>자기 자신만 해제 — 다른 인스턴스가 이미 갱신했으면 안 건드린다(race 안전).</summary>
		public static void Clear(DialogueHistory dialogueHistory)
		{
			if (history == dialogueHistory)
			{
				history = null;
			}
		}

		/// <summary>등록된 이력. 아직 없으면 null — 부르는 쪽이 판단한다.</summary>
		public static DialogueHistory Current => history;
	}
}
