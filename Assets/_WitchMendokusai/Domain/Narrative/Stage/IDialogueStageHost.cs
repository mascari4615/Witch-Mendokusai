using System;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 선택지 한 칸의 화면용 값. 잠긴 칸은 SubmitIndex -1
	public readonly struct DialogueChoiceRow
	{
		public const int LOCKED_INDEX = -1;

		public string Label { get; }
		public string LockReason { get; }
		public int SubmitIndex { get; }
		public bool IsLocked => SubmitIndex == LOCKED_INDEX;

		public DialogueChoiceRow(string label, int submitIndex, string lockReason = null)
		{
			Label = label;
			SubmitIndex = submitIndex;
			LockReason = lockReason;
		}
	}

	// 표시 어댑터가 러너에 넘기는 조작
	public interface IDialogueStageControls
	{
		bool CanAdvanceLine { get; }
		void AdvanceLine();
		int Skip();
		DialogueTranscript Transcript { get; }
	}

	// 표시 어댑터 포트. UI Toolkit과 카메라 구현은 Domain/Chat/Stage
	public interface IDialogueStageHost
	{
		bool IsHoldingInput { get; }
		void Begin(IDialogueStageControls controls);
		void ApplyLine(DialogueStageDelta delta, string speakerName, string text);
		void ApplyCues(DialogueStageDelta delta);
		void PresentChoices(DialogueStageDelta delta, string speakerName, string prompt, IReadOnlyList<DialogueChoiceRow> rows, Action<int> submit);
		void Finish();
		bool TryResolveWorldAnchor(string actorName, out Transform anchor);
	}

	public static class DialogueStageBridge
	{
		public static IDialogueStageHost Current { get; private set; }

		public static bool IsHoldingInput => Current != null && Current.IsHoldingInput;

		public static void Register(IDialogueStageHost host) => Current = host;

		public static void Clear(IDialogueStageHost host)
		{
			if (Current == host)
			{
				Current = null;
			}
		}
	}
}
