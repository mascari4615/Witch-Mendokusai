using UnityEditor;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 대화 미리보기 EditorWindow — 대화 그래프를 **Play 없이** 한 스텝씩 걸어본다 (TASK-WM-052).
	///
	/// ★ 왜 러너가 아니라 traversal 을 쓰나: 옛 미리보기 창(feature/wm-052-dialogue-sequence,
	///   2026-05-07)은 <see cref="DialogueRunner"/> 의 Advance/CurrentLine/CurrentChoices/
	///   SubmitChoice 를 불렀는데, 그 뒤 러너가 재작성되며 그 표면이 전부 사라졌다(= 이식 불가).
	///   같은 능력이 지금은 <see cref="DialogueGraphTraversal"/> 에 **순수 클래스**로 있다.
	///   러너(MonoBehaviour)에 의존하지 않으니 Play 를 켜지 않고도 분기까지 밟아볼 수 있다 —
	///   옛 창(Play 모드에서만 제어 가능)보다 오히려 루프가 빠르다.
	///
	/// 메뉴: WM/Narrative/Dialogue Graph Preview
	/// </summary>
	public class DialogueGraphPreviewWindow : EditorWindow
	{
		[MenuItem("WM/Narrative/Dialogue Graph Preview")]
		public static void Open()
		{
			DialogueGraphPreviewWindow window = GetWindow<DialogueGraphPreviewWindow>();
			window.titleContent = new GUIContent("대화 미리보기");
			window.minSize = new Vector2(360f, 320f);
			window.Show();
		}

		private DialogueScriptSource scriptSource;
		private ParsedDialogueScript parsedScript;
		private bool issuesFolded;
		private DialogueGraph graph;
		private DialogueGraphTraversal traversal;
		private DialogueStep step;
		private bool walking;
		private Vector2 scroll;
		private int stepCount;

		private void OnGUI()
		{
			DrawSourcePicker();

			if (graph == null)
			{
				EditorGUILayout.HelpBox("원고(글로 쓴 대화)나 대화 그래프를 골라라. Play 를 켜지 않아도 스텝을 밟을 수 있다.", MessageType.Info);
				return;
			}

			DrawControls();
			EditorGUILayout.Space(6f);

			scroll = EditorGUILayout.BeginScrollView(scroll);
			if (walking == false)
			{
				EditorGUILayout.HelpBox("「처음부터」를 눌러 시작.", MessageType.None);
			}
			else
			{
				DrawStep();
			}
			EditorGUILayout.EndScrollView();
		}

		private void DrawSourcePicker()
		{
			EditorGUI.BeginChangeCheck();
			scriptSource = (DialogueScriptSource)EditorGUILayout.ObjectField(
				"원고", scriptSource, typeof(DialogueScriptSource), false);
			if (EditorGUI.EndChangeCheck())
			{
				ResetWalk();
				parsedScript = null;
				graph = null;
				if (scriptSource != null)
				{
					// 창을 여는 동안엔 항상 지금 글자를 읽는다 — 원고를 고치고 바로 확인하는 게 이 창의 쓸모다.
					scriptSource.Invalidate();
					graph = scriptSource.BuildGraph(out parsedScript);
				}
			}

			using (new EditorGUI.DisabledScope(scriptSource != null))
			{
				EditorGUI.BeginChangeCheck();
				DialogueGraph pickedGraph = (DialogueGraph)EditorGUILayout.ObjectField(
					"대화 그래프(직접)", scriptSource != null ? graph : graph, typeof(DialogueGraph), false);
				if (EditorGUI.EndChangeCheck())
				{
					graph = pickedGraph;
					parsedScript = null;
					ResetWalk();
				}
			}

			DrawScriptReport();
		}

		/// <summary>
		/// 원고를 읽은 결과 — **줄 번호와 함께** 보여준다. 이게 이 창의 값어치 절반이다:
		/// 게임을 켜지 않고도 「어느 줄이 잘못됐는지」를 안다.
		/// </summary>
		private void DrawScriptReport()
		{
			if (parsedScript == null)
			{
				return;
			}

			int lineCount = 0;
			foreach (DialogueScriptSection section in parsedScript.Sections)
			{
				lineCount += section.Entries.Count;
			}
			EditorGUILayout.LabelField(
				$"장면 {parsedScript.Sections.Count} · 마디 {lineCount} · 걸림 {parsedScript.Issues.Count} · 안 읽은 인용줄 {parsedScript.SkippedQuoteLines.Count}",
				EditorStyles.miniLabel);

			if (parsedScript.Issues.Count == 0)
			{
				return;
			}

			issuesFolded = EditorGUILayout.Foldout(issuesFolded, $"걸린 곳 {parsedScript.Issues.Count}", true);
			if (issuesFolded == false)
			{
				return;
			}
			for (int i = 0; i < parsedScript.Issues.Count; i++)
			{
				EditorGUILayout.HelpBox($"L{parsedScript.Issues[i].LineNumber}: {parsedScript.Issues[i].Message}", MessageType.Warning);
			}
		}

		private void DrawControls()
		{
			EditorGUILayout.BeginHorizontal();

			if (GUILayout.Button("처음부터"))
			{
				traversal = new DialogueGraphTraversal(graph);
				step = traversal.Start();
				walking = true;
				stepCount = 1;
			}

			// 선택지 스텝에서는 「다음」이 아니라 선택지 버튼으로 진행한다(미선택 Next = End 라 오해 유발).
			bool canAdvance = walking && step.Kind != DialogueStepKind.End && step.Kind != DialogueStepKind.Choice;
			using (new EditorGUI.DisabledScope(canAdvance == false))
			{
				if (GUILayout.Button("다음"))
				{
					step = traversal.Next();
					stepCount++;
				}
			}

			using (new EditorGUI.DisabledScope(walking == false))
			{
				if (GUILayout.Button("멈춤"))
				{
					ResetWalk();
				}
			}

			EditorGUILayout.EndHorizontal();

			if (walking)
			{
				EditorGUILayout.LabelField("스텝 " + stepCount + " — " + KindLabel(step.Kind), EditorStyles.miniLabel);
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
					if (traversal.SelectChoice(i))
					{
						step = traversal.Next();
						stepCount++;
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
			}
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

		private void ResetWalk()
		{
			traversal = null;
			step = DialogueStep.End;
			walking = false;
			stepCount = 0;
		}
	}
}
