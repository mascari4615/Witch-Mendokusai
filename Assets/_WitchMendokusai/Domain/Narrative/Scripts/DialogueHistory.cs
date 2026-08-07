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

		// 「그 대화에서 무슨 답을 골랐나」. 대화 하나에 답이 여럿 남을 수 있어 집합이다.
		private readonly Dictionary<int, HashSet<string>> chosenLabels = new();

		public void MarkStarted(int dialogueId) => started.Add(dialogueId);

		/// <summary>끝까지 간 대화 — 시작 기록도 같이 남는다(끝났으면 시작한 것이므로).</summary>
		public void MarkCompleted(int dialogueId)
		{
			started.Add(dialogueId);
			completed.Add(dialogueId);
		}

		/// <summary>
		/// 「그 대화에서 이 답을 골랐다」를 남긴다.
		///
		/// ★ 왜 「봤다」로는 부족한가: 서사에서 되짚는 건 **본 적**이 아니라 **한 말**이다.
		///   「그때 거절했잖아」는 대화를 봤는지가 아니라 무엇을 골랐는지를 묻는다.
		///   본 적만 남기면 어느 가지로 갔든 똑같은 기록이라, 지난 선택을 아는 대사를 쓸 수가 없다.
		///
		/// 어느 선택지 묶음이었는지는 안 남긴다 — 원고 쓰는 사람이 세어야 하는 번호를 만들면 안 쓴다.
		/// 라벨이 곧 그 사람이 한 말이고, 한 묶음 안 라벨 겹침은 검사가 이미 막는다.
		/// </summary>
		public void MarkChoice(int dialogueId, string label)
		{
			if (string.IsNullOrEmpty(label))
			{
				return;
			}
			started.Add(dialogueId);
			if (chosenLabels.TryGetValue(dialogueId, out HashSet<string> labels) == false)
			{
				labels = new HashSet<string>(StringComparer.Ordinal);
				chosenLabels[dialogueId] = labels;
			}
			labels.Add(label);
		}

		/// <summary>그 대화에서 이 답을 고른 적 있나. 라벨은 **원고에 쓴 그대로** 비교한다.</summary>
		public bool HasChosen(int dialogueId, string label) =>
			string.IsNullOrEmpty(label) == false
			&& chosenLabels.TryGetValue(dialogueId, out HashSet<string> labels)
			&& labels.Contains(label);

		public bool HasSeen(int dialogueId, DialogueSeenKind kind) =>
			kind == DialogueSeenKind.Completed ? completed.Contains(dialogueId) : started.Contains(dialogueId);

		public DialogueHistorySaveData ToSaveData()
		{
			List<DialogueChoiceRecord> records = new();
			foreach (KeyValuePair<int, HashSet<string>> pair in chosenLabels)
			{
				foreach (string label in pair.Value)
				{
					records.Add(new DialogueChoiceRecord { DialogueId = pair.Key, Label = label });
				}
			}

			return new DialogueHistorySaveData
			{
				StartedDialogueIds = new List<int>(started),
				CompletedDialogueIds = new List<int>(completed),
				Choices = records,
			};
		}

		public void FromSaveData(DialogueHistorySaveData saveData)
		{
			started.Clear();
			completed.Clear();
			chosenLabels.Clear();

			if (saveData.Choices != null)
			{
				for (int i = 0; i < saveData.Choices.Count; i++)
				{
					// 고른 적 있으면 시작한 적도 있는 것이다 — 저장본이 한쪽만 들고 있어도 앞뒤가 맞아야 한다.
					MarkChoice(saveData.Choices[i].DialogueId, saveData.Choices[i].Label);
				}
			}

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

	/// <summary>저장 파일에 남는 「그 대화에서 고른 답」 한 줄.</summary>
	[Serializable]
	public struct DialogueChoiceRecord
	{
		public int DialogueId;
		public string Label;
	}

	[Serializable]
	public struct DialogueHistorySaveData
	{
		public List<int> StartedDialogueIds;
		public List<int> CompletedDialogueIds;

		/// <summary>이 칸이 생기기 전 저장본에는 없다 — 비어 온다(옛 저장이 안 열리는 게 제일 큰 사고다).</summary>
		public List<DialogueChoiceRecord> Choices;
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

		/// <summary>
		/// 저장할 것을 꺼낸다. 이력이 아직 없으면 **빈 것**을 준다 —
		/// 저장 시점에 대화 시스템이 안 떠 있을 수 있는데(로비 등), 그때 저장을 건너뛰면
		/// 다음 저장이 옛 기록을 덮어써서 **봤던 대화가 통째로 사라진다.**
		/// </summary>
		public static DialogueHistorySaveData CaptureSaveData() =>
			history == null
				? new DialogueHistorySaveData
				{
					StartedDialogueIds = new List<int>(),
					CompletedDialogueIds = new List<int>(),
					Choices = new List<DialogueChoiceRecord>(),
				}
				: history.ToSaveData();

		/// <summary>불러온 것을 되돌린다. 이력이 아직 없으면 아무 일도 안 한다(등록 뒤 다시 부르면 된다).</summary>
		public static void RestoreSaveData(DialogueHistorySaveData saveData) => history?.FromSaveData(saveData);
	}
}
