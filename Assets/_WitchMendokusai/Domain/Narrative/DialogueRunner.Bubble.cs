using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer;

namespace WitchMendokusai
{
	// DialogueRunner 의 말풍선과 선택 부분. 같은 클래스의 partial 조각이다. 상태(필드)는 DialogueRunner.cs 를 본다.
	public partial class DialogueRunner : MonoBehaviour
	{
		private Transform bubbleTarget;

		private void HandleStepChanged(DialogueStep step)
		{
			if (step.Kind == DialogueStepKind.Speak)
			{
				Transcript.Record(step.SpeakLine);
				ShowBubble(step.SpeakLine);
				return;
			}
			if (step.Kind == DialogueStepKind.Choice)
			{
				OnChoicesPresented(step.Options);
			}
		}

		private void ShowBubble(DialogueLine line)
		{
			if (line == null)
			{
				return;
			}
			// 말풍선이 사라지는 시간과 대화가 넘어가는 시간은 **같아야 한다** — 다르면 빈 자리가 생기거나
			// 다음 대사가 앞 말풍선 위에 겹친다. 그래서 같은 계산을 쓴다.
			float duration = line.Wait > 0f
				? line.Wait
				: DialogueReadingTime.For(line.Text, readingCharactersPerSecond, minimumLineSeconds, maximumLineSeconds);
			if (duration <= 0f)
			{
				// 재생기에 넘긴 값과 **같은 값**이라야 한다 — 여기만 박아 두면 인스펙터로 조절했을 때
				// 말풍선과 대화가 서로 다른 시간을 쓰게 된다(위 주석이 경계하는 바로 그 어긋남).
				duration = defaultLineSeconds;
			}
			Transform anchor = ResolveLineAnchor(line);
			if (uiManager != null && uiManager.SpeechBubble != null && anchor != null)
			{
				uiManager.SpeechBubble.Show(anchor, line.Text, duration);
			}
		}

		/// <summary>
		/// 이 대사를 **누구 위에** 띄울지. 순서: ① 원고에 쓴 이름으로 등록된 캐릭터
		/// ② 재생할 때 넘겨받은 대상 ③ 카메라(옛 거동).
		///
		/// ①이 없다고 대화가 멈추면 안 된다 — 캐릭터 배선이 아직인 원고도 그냥 읽혀야 한다.
		/// </summary>
		private Transform ResolveLineAnchor(DialogueLine line)
		{
			string speakerName = line.ResolveSpeakerName();
			if (string.IsNullOrEmpty(speakerName) == false
				&& DialogueSpeakerBridge.TryGetAnchor(speakerName, out Transform speakerAnchor))
			{
				return speakerAnchor;
			}
			return bubbleTarget;
		}

		/// <summary>
		/// 선택지가 떴는데 고르는 쪽이 없다 — **선택지 화면이 아직 없어서** 생기는 상황이다.
		/// 조용히 서 있으면 뒤에 줄 선 대화까지 전부 막히므로, 크게 알리고 이 대화만 접는다(줄은 계속 흐른다).
		/// </summary>
		private void HandleChoiceStalled()
		{
			Debug.LogWarning($"[DialogueRunner] 선택지가 {choiceStallSeconds}초째 그대로다 — 고르는 쪽(선택지 화면)이 없다. 이 대화를 접는다.");
			playback?.Stop();
		}

		/// <summary>
		/// 「이 대화에서 무슨 답을 골랐나」를 남긴다.
		///
		/// ★ 왜 재생기가 아니라 여기인가: 재생기는 **대화 번호를 모른다**(그래프만 안다).
		///   번호를 아는 건 무엇을 틀었는지 기억하는 이쪽이다. 끝까지 들었는지를 여기서 남기는 것과 같은 자리.
		///
		/// 도중에 접어도 남긴다 — 접었다고 **한 말이 없던 일이 되지는 않는다.**
		/// (「끝까지 들었나」와는 다른 물음이라 판단도 다르다.)
		/// </summary>
		private void HandleChoiceSelected(string label)
		{
			// 번호 없는 재생(그 자리에서 세운 사슬)은 남길 자리가 없다 — 0 번에 적으면 남의 칸을 더럽힌다.
			if (CurrentDialogueId != DataSO.NONE_ID)
			{
				History.MarkChoice(CurrentDialogueId, label);
			}

			// 로그에도 남긴다 — 되짚는 이유의 절반은 「내가 뭐라고 했더라」다.
			Transcript.RecordChoice(label);
		}

		private static Transform ResolveTarget(Transform speakerTransform)
		{
			if (speakerTransform != null)
			{
				return speakerTransform;
			}
			Camera mainCamera = Camera.main;
			return mainCamera == null ? null : mainCamera.transform;
		}
	}
}
