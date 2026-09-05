using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace WitchMendokusai
{
	// DialogueGraphPreviewWindow 의 그리기 부분. 같은 클래스의 partial 조각이다. 상태(필드)는 DialogueGraphPreviewWindow.cs 를 본다.
	public partial class DialogueGraphPreviewWindow : EditorWindow
	{
		/// <summary>
		/// 지나온 대사 — 한 스텝씩 걸으면 앞 대사는 화면에서 사라진다. 어느 길로 왔는지 안 보이면
		/// 분기를 확인하는 의미가 절반이다. 접어 두고 필요할 때 펴게 한다.
		/// </summary>
		private void DrawTranscript()
		{
			if (transcript.Count == 0)
			{
				return;
			}

			EditorGUILayout.Space(6f);
			transcriptFolded = EditorGUILayout.Foldout(transcriptFolded, $"지나온 대사 {transcript.Count}", true);
			if (transcriptFolded == false)
			{
				return;
			}

			IReadOnlyList<DialogueTranscript.Entry> entries = transcript.Entries;
			for (int i = 0; i < entries.Count; i++)
			{
				if (entries[i].IsChoice)
				{
					// 고른 답은 남의 대사와 **눈에 띄게** 갈라 둔다 — 되짚을 때 제일 먼저 찾는 게 이것이다.
					// (게임 화면의 모양은 화면 쪽이 정한다. 여기 화살표는 이 도구 안에서만 쓰는 표시다.)
					EditorGUILayout.LabelField($"{i + 1}. ▸ {entries[i].Text}", EditorStyles.wordWrappedMiniLabel);
					continue;
				}

				string speaker = string.IsNullOrEmpty(entries[i].Speaker) ? "(나레이션)" : entries[i].Speaker;
				EditorGUILayout.LabelField($"{i + 1}. {speaker}: {entries[i].Text}", EditorStyles.wordWrappedMiniLabel);
			}
		}

		private void DrawStep()
		{
			switch (step.Kind)
			{
				case DialogueStepKind.Speak:
					DrawSpeak(step.SpeakLine);
					break;

				case DialogueStepKind.Choice:
					DrawChoice();
					break;

				case DialogueStepKind.Wait:
					EditorGUILayout.HelpBox("대기 — " + step.WaitKind + " / " + step.WaitSeconds + "초", MessageType.None);
					break;

				case DialogueStepKind.Effect:
					DrawEffect();
					break;

				case DialogueStepKind.End:
					EditorGUILayout.HelpBox("대화 끝.", MessageType.Info);
					break;
			}
		}

		private void DrawSpeak(DialogueLine line)
		{
			if (line == null)
			{
				EditorGUILayout.HelpBox("이 말하기 노드에 DialogueLine 이 안 꽂혀 있다.", MessageType.Warning);
				return;
			}

			EditorGUILayout.LabelField("말하는 이", line.ResolveSpeakerName() is string speakerName && string.IsNullOrEmpty(speakerName) == false
				? speakerName
				: SpeakerName(line));

			Sprite portrait = line.Portrait;
			if (portrait != null && portrait.texture != null)
			{
				Rect rect = GUILayoutUtility.GetRect(96f, 96f, GUILayout.ExpandWidth(false));
				GUI.DrawTexture(rect, portrait.texture, ScaleMode.ScaleToFit);
			}

			if (string.IsNullOrEmpty(line.StageDirection) == false)
			{
				EditorGUILayout.LabelField("지문", line.StageDirection);
			}

			EditorGUILayout.LabelField("대사", EditorStyles.boldLabel);
			EditorGUILayout.SelectableLabel(line.Text, EditorStyles.wordWrappedLabel, GUILayout.MinHeight(48f));

			if (line.Sfx != null)
			{
				EditorGUILayout.LabelField("효과음", line.Sfx.name);
			}

			if (line.Wait > 0f)
			{
				EditorGUILayout.LabelField("대기", line.Wait + "초");
			}

			EditorGUILayout.Space(4f);
			if (GUILayout.Button("이 줄에 해당하는 에셋 선택"))
			{
				Selection.activeObject = line;
				EditorGUIUtility.PingObject(line);
			}

			DrawPlayInGameButton(line);
		}

		/// <summary>
		/// 효과 스텝 — **미리보기에서는 실제로 일어나지 않는다.** 이 창은 순수 순회기만 쓰고
		/// 효과를 일으키는 것은 재생기(<see cref="DialoguePlayback"/>) 쪽이라, 여기서 걸어본다고
		/// 물건이 생기지 않는다. 그래서 「무엇이 일어날 예정인지」만 적어 보여준다.
		/// </summary>
		private void DrawEffect()
		{
			EditorGUILayout.LabelField("여기서 일어나는 것", EditorStyles.boldLabel);

			if (step.Effects != null)
			{
				for (int i = 0; i < step.Effects.Count; i++)
				{
					EffectInfo effect = step.Effects[i];
					EditorGUILayout.LabelField($"· {effect.Type} — {(effect.Data == null ? "(자산 없음)" : effect.Data.name)} x{effect.Value}");
				}
			}
			if (step.EffectData != null)
			{
				for (int i = 0; i < step.EffectData.Count; i++)
				{
					EffectInfoData effect = step.EffectData[i];
					EditorGUILayout.LabelField($"· {effect.Type} — 번호 {effect.DataSoID} x{effect.Value}");
				}
			}

			EditorGUILayout.HelpBox("미리보기에서는 실제로 일어나지 않는다(물건이 생기지 않는다).", MessageType.None);
		}

		private void DrawChoice()
		{
			EditorGUILayout.LabelField("물음", EditorStyles.boldLabel);
			EditorGUILayout.SelectableLabel(step.Prompt, EditorStyles.wordWrappedLabel, GUILayout.MinHeight(32f));

			if (step.Options == null || step.Options.Count == 0)
			{
				EditorGUILayout.HelpBox("선택지가 비어 있다.", MessageType.Warning);
				return;
			}

			EditorGUILayout.LabelField("선택지", EditorStyles.boldLabel);
			for (int i = 0; i < step.Options.Count; i++)
			{
				if (GUILayout.Button(i + 1 + ". " + step.Options[i]))
				{
					// 고른 답도 지나온 기록에 남긴다 — 게임 로그와 같은 판단이다.
					// 안 남기면 갈래를 여럿 걸어 본 뒤 「내가 어디서 뭘 골랐더라」를 못 되짚는다.
					string chosenLabel = step.Options[i];
					if (traversal.SelectChoice(i))
					{
						transcript.RecordChoice(chosenLabel);
						step = traversal.Next();
						stepCount++;
						RecordStep();
					}
				}
			}
		}

		// Play 중일 때만 — 실제 게임 연출(버블/typewriter/sfx)로 태워본다.
		// 원고를 고른 경우엔 **통째로** 재생한다(그게 실제로 게임에서 일어날 일이다).
		// 그래프만 고른 경우엔 이 줄 하나만 태운다(옛 거동).
		private void DrawPlayInGameButton(DialogueLine line)
		{
			if (Application.isPlaying == false)
			{
				return;
			}

			EditorGUILayout.Space(4f);
			bool hasRunner = DialogueRunner.TryGetExistingInstance(out DialogueRunner runner);
			using (new EditorGUI.DisabledScope(hasRunner == false))
			{
				if (scriptSource != null)
				{
					if (GUILayout.Button("게임 화면에서 이 원고 재생"))
					{
						runner.Play(scriptSource);
					}
				}
				else if (GUILayout.Button("게임 화면에서 이 줄 재생"))
				{
					runner.Play(line);
				}

				using (new EditorGUI.DisabledScope(runner == null || runner.IsPlaying == false))
				{
					if (GUILayout.Button("멈춤(게임 화면)"))
					{
						runner.Stop();
					}
				}
			}

			if (hasRunner == false)
			{
				EditorGUILayout.HelpBox("씬에 DialogueRunner 가 아직 없다.", MessageType.None);
				return;
			}

			DrawLiveChoiceButtons(runner);
		}

		/// <summary>
		/// **게임 화면에서 도는 대화의 선택지를 여기서 고른다.**
		///
		/// ★ 왜: 선택지 화면(게임 UI)이 아직 없다(사용자 결정 대기). 그래서 지금은 선택지가 뜨면
		///   아무도 못 고르고 15초 뒤 접힌다 — **선택지 있는 원고를 실제 화면에서 한 번도 못 본다.**
		///   창에서라도 고를 수 있게 해 두면, 화면이 생기기 전에 원고를 끝까지 태워볼 수 있다.
		///   (게임 UI 의 대체가 아니라 **작가·개발자용 임시 손잡이**다.)
		/// </summary>
		private static void DrawLiveChoiceButtons(DialogueRunner runner)
		{
			IReadOnlyList<string> choices = runner.CurrentChoices;
			if (choices == null || choices.Count == 0)
			{
				return;
			}

			EditorGUILayout.Space(4f);
			EditorGUILayout.LabelField("게임에서 지금 뜬 선택지", EditorStyles.boldLabel);
			for (int i = 0; i < choices.Count; i++)
			{
				if (GUILayout.Button($"▸ {choices[i]}"))
				{
					runner.SubmitChoice(i);
				}
			}
			EditorGUILayout.HelpBox("선택지 화면이 생기기 전까지 쓰는 임시 손잡이다.", MessageType.None);
		}

		private static string SpeakerName(DialogueLine line)
		{
			if (line.Speaker == null)
			{
				return "(없음)";
			}

			return string.IsNullOrEmpty(line.Speaker.Name) ? line.Speaker.name : line.Speaker.Name;
		}

		private static string KindLabel(DialogueStepKind kind)
		{
			switch (kind)
			{
				case DialogueStepKind.Speak: return "말하기";
				case DialogueStepKind.Choice: return "선택지";
				case DialogueStepKind.Wait: return "대기";
				default: return "끝";
			}
		}
	}
}
