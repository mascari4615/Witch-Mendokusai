using System.Collections.Generic;
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
	public partial class DialogueGraphPreviewWindow : EditorWindow
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

		// 지금까지 지나온 대사 — 한 스텝씩 걸으면 앞 대사가 화면에서 사라져서 「어디로 왔는지」가 안 보인다.
		// 게임과 같은 기록 클래스를 쓴다(창 전용으로 따로 만들면 둘이 다르게 군다).
		private readonly DialogueTranscript transcript = new(30);
		private bool transcriptFolded = true;

		// 조건 흉내내기 — 조건은 게임 상태(이력·가방·의뢰)를 보는데 이 창은 게임을 안 켠다.
		// 그래서 「봤다 치고 / 가졌다 치고」 걸어볼 수 있게 가짜를 끼운다(Play 중엔 안 쓴다 — 진짜가 있으니까).
		private bool pretendFolded = true;
		private string pretendSeenIds = string.Empty;
		private string pretendItemIds = string.Empty;
		private string pretendDoneQuestIds = string.Empty;

		private void OnGUI()
		{
			DrawSourcePicker();

			if (graph == null)
			{
				EditorGUILayout.HelpBox("원고(글로 쓴 대화)나 대화 그래프를 골라라. Play 를 켜지 않아도 스텝을 밟을 수 있다.", MessageType.Info);
				return;
			}

			DrawControls();
			DrawPretend();
			EditorGUILayout.Space(6f);

			scroll = EditorGUILayout.BeginScrollView(scroll);
			if (walking == false)
			{
				EditorGUILayout.HelpBox("「처음부터」를 눌러 시작.", MessageType.None);
			}
			else
			{
				DrawStep();
				DrawTranscript();
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
				ApplyPretend();
				traversal = new DialogueGraphTraversal(graph);
				transcript.Clear();
				step = traversal.Start();
				walking = true;
				stepCount = 1;
				RecordStep();
			}

			// 선택지 스텝에서는 「다음」이 아니라 선택지 버튼으로 진행한다(미선택 Next = End 라 오해 유발).
			bool canAdvance = walking && step.Kind != DialogueStepKind.End && step.Kind != DialogueStepKind.Choice;
			using (new EditorGUI.DisabledScope(canAdvance == false))
			{
				if (GUILayout.Button("다음"))
				{
					step = traversal.Next();
					stepCount++;
					RecordStep();
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

		/// <summary>
		/// 「봤다 치고 / 가졌다 치고 / 끝냈다 치고」 — 조건이 걸린 가지를 게임 없이 걸어보기 위한 손잡이.
		///
		/// ★ 왜: 조건은 게임 상태를 본다. 이 창은 게임을 안 켜므로 **조건이 늘 거짓**이고,
		///   조건부 가지는 **한 번도 안 밟힌다.** 원고를 쓰는 사람이 제일 보고 싶은 게 그 가지인데도.
		///
		/// Play 중에는 안 끼운다 — 그땐 진짜 상태가 있고, 가짜로 덮으면 게임 쪽 판단까지 흔든다.
		/// </summary>
		private void DrawPretend()
		{
			if (Application.isPlaying)
			{
				return;
			}

			pretendFolded = EditorGUILayout.Foldout(pretendFolded, "조건 흉내내기 (게임 없이 가지 밟아보기)", true);
			if (pretendFolded == false)
			{
				return;
			}

			pretendSeenIds = EditorGUILayout.TextField("본 대화 번호", pretendSeenIds);
			pretendItemIds = EditorGUILayout.TextField("가진 물건 번호", pretendItemIds);
			pretendDoneQuestIds = EditorGUILayout.TextField("끝낸 의뢰 번호", pretendDoneQuestIds);
			EditorGUILayout.LabelField("쉼표로 여러 개. 「처음부터」를 누르면 반영된다.", EditorStyles.miniLabel);
		}

		/// <summary>적어 둔 번호들을 가짜 상태로 끼운다 — 걷기 시작할 때 한 번.</summary>
		private void ApplyPretend()
		{
			if (Application.isPlaying)
			{
				return;
			}

			// 무엇을 참으로 칠지는 이 창이 안 정한다 — 화면 없이 시험되는 쪽에 있다.
			DialoguePretendState.From(pretendSeenIds, pretendItemIds, pretendDoneQuestIds).Register();
		}

		private void RecordStep()
		{
			if (step.Kind == DialogueStepKind.Speak)
			{
				transcript.Record(step.SpeakLine);
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
