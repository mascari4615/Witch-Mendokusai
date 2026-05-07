using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// DialogueLine 미리보기 EditorWindow. 디자이너가 SO 골라 텍스트/포트레이트/Choices 즉시 검증.
	/// Play 모드에서는 DialogueRunner.Instance 에 위임 — Runner 의 첫 호출처 (데드 인터페이스 회피).
	/// Edit 모드에서는 정적 데이터 view 만 (런타임 코루틴 X).
	///
	/// 메뉴: WitchMendokusai/Narrative/Dialogue Runner Preview
	/// </summary>
	public class DialogueRunnerEditorWindow : EditorWindow
	{
		[MenuItem("WitchMendokusai/Narrative/Dialogue Runner Preview")]
		public static void Open()
		{
			DialogueRunnerEditorWindow window = GetWindow<DialogueRunnerEditorWindow>();
			window.titleContent = new GUIContent("Dialogue Preview");
			window.minSize = new Vector2(420, 320);
			window.Show();
		}

		private DialogueLine selectedLine;

		private ObjectField lineField;
		private VisualElement linePreviewContainer;
		private VisualElement runtimeStatusContainer;
		private Label runtimeStateLabel;
		private Label currentLineLabel;
		private VisualElement choicesContainer;
		private Button playButton;
		private Button advanceButton;
		private Button stopButton;

		private void CreateGUI()
		{
			rootVisualElement.style.flexGrow = 1;
			rootVisualElement.style.paddingLeft = 8;
			rootVisualElement.style.paddingRight = 8;
			rootVisualElement.style.paddingTop = 8;
			rootVisualElement.style.paddingBottom = 8;

			BuildLineSelector();
			BuildLinePreview();
			BuildRuntimeStatus();
			BuildControls();

			RefreshAll();
		}

		private void OnEnable()
		{
			EditorApplication.playModeStateChanged += OnPlayModeChanged;
		}

		private void OnDisable()
		{
			EditorApplication.playModeStateChanged -= OnPlayModeChanged;
			UnsubscribeFromRunner();
		}

		private void OnPlayModeChanged(PlayModeStateChange change)
		{
			if (change == PlayModeStateChange.EnteredPlayMode)
				SubscribeToRunnerWhenAvailable();
			else if (change == PlayModeStateChange.ExitingPlayMode)
				UnsubscribeFromRunner();

			RefreshAll();
		}

		private void BuildLineSelector()
		{
			lineField = new ObjectField("DialogueLine")
			{
				objectType = typeof(DialogueLine),
				allowSceneObjects = false,
			};
			lineField.style.marginBottom = 6;
			lineField.RegisterValueChangedCallback(evt =>
			{
				selectedLine = evt.newValue as DialogueLine;
				RefreshAll();
			});
			rootVisualElement.Add(lineField);
		}

		private void BuildLinePreview()
		{
			linePreviewContainer = new VisualElement();
			linePreviewContainer.style.borderTopWidth = 1;
			linePreviewContainer.style.borderTopColor = new Color(0f, 0f, 0f, 0.3f);
			linePreviewContainer.style.paddingTop = 6;
			linePreviewContainer.style.paddingBottom = 6;
			rootVisualElement.Add(linePreviewContainer);
		}

		private void BuildRuntimeStatus()
		{
			runtimeStatusContainer = new VisualElement();
			runtimeStatusContainer.style.borderTopWidth = 1;
			runtimeStatusContainer.style.borderTopColor = new Color(0f, 0f, 0f, 0.3f);
			runtimeStatusContainer.style.paddingTop = 6;
			runtimeStatusContainer.style.paddingBottom = 6;
			rootVisualElement.Add(runtimeStatusContainer);

			runtimeStateLabel = new Label();
			runtimeStateLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
			runtimeStatusContainer.Add(runtimeStateLabel);

			currentLineLabel = new Label();
			currentLineLabel.style.whiteSpace = WhiteSpace.Normal;
			currentLineLabel.style.marginTop = 4;
			runtimeStatusContainer.Add(currentLineLabel);

			choicesContainer = new VisualElement();
			choicesContainer.style.marginTop = 4;
			runtimeStatusContainer.Add(choicesContainer);
		}

		private void BuildControls()
		{
			VisualElement controls = new();
			controls.style.flexDirection = FlexDirection.Row;
			controls.style.marginTop = 8;
			rootVisualElement.Add(controls);

			playButton = new Button(OnPlayClicked) { text = "Play" };
			playButton.style.flexGrow = 1;
			controls.Add(playButton);

			advanceButton = new Button(OnAdvanceClicked) { text = "Advance" };
			advanceButton.style.flexGrow = 1;
			advanceButton.style.marginLeft = 4;
			controls.Add(advanceButton);

			stopButton = new Button(OnStopClicked) { text = "Stop" };
			stopButton.style.flexGrow = 1;
			stopButton.style.marginLeft = 4;
			controls.Add(stopButton);
		}

		private void OnPlayClicked()
		{
			if (Application.isPlaying == false)
			{
				EditorUtility.DisplayDialog("Dialogue Preview", "Play 모드 진입 후 사용 (DialogueRunner Singleton 이 씬에 필요).", "OK");
				return;
			}

			if (selectedLine == null)
				return;

			SubscribeToRunnerWhenAvailable();
			DialogueRunner.Instance.Play(selectedLine);
			RefreshAll();
		}

		private void OnAdvanceClicked()
		{
			if (Application.isPlaying == false)
				return;
			if (DialogueRunner.TryGetExistingInstance(out DialogueRunner runner) == false)
				return;

			runner.Advance();
		}

		private void OnStopClicked()
		{
			if (Application.isPlaying == false)
				return;
			if (DialogueRunner.TryGetExistingInstance(out DialogueRunner runner) == false)
				return;

			runner.Stop();
			RefreshAll();
		}

		private bool subscribed;

		private void SubscribeToRunnerWhenAvailable()
		{
			if (Application.isPlaying == false)
				return;
			if (subscribed)
				return;
			if (DialogueRunner.TryGetExistingInstance(out DialogueRunner runner) == false)
				return;

			runner.OnLineStart += OnRunnerLineStart;
			runner.OnLineEnd += OnRunnerLineEnd;
			runner.OnDialogueComplete += OnRunnerComplete;
			runner.OnChoicesPresented += OnRunnerChoicesPresented;
			subscribed = true;
		}

		private void UnsubscribeFromRunner()
		{
			if (subscribed == false)
				return;
			if (DialogueRunner.TryGetExistingInstance(out DialogueRunner runner))
			{
				runner.OnLineStart -= OnRunnerLineStart;
				runner.OnLineEnd -= OnRunnerLineEnd;
				runner.OnDialogueComplete -= OnRunnerComplete;
				runner.OnChoicesPresented -= OnRunnerChoicesPresented;
			}
			subscribed = false;
		}

		private void OnRunnerLineStart(DialogueLine line) => RefreshRuntimeStatus();

		private void OnRunnerLineEnd(DialogueLine line) => RefreshRuntimeStatus();

		private void OnRunnerComplete() => RefreshRuntimeStatus();

		private void OnRunnerChoicesPresented(IReadOnlyList<DialogueLine> choices) => RefreshRuntimeStatus();

		private void RefreshAll()
		{
			RefreshLinePreview();
			RefreshRuntimeStatus();
			RefreshControls();
		}

		private void RefreshLinePreview()
		{
			linePreviewContainer.Clear();

			if (selectedLine == null)
			{
				Label hint = new("DialogueLine 미선택");
				hint.style.color = new Color(0.7f, 0.7f, 0.7f);
				linePreviewContainer.Add(hint);
				return;
			}

			Label header = new("Static Preview");
			header.style.unityFontStyleAndWeight = FontStyle.Bold;
			linePreviewContainer.Add(header);

			AddPreviewRow("Speaker", selectedLine.Speaker != null ? selectedLine.Speaker.name : "(none)");
			AddPreviewRow("Text", string.IsNullOrEmpty(selectedLine.Text) ? "(empty)" : selectedLine.Text);
			AddPreviewRow("Portrait", selectedLine.Portrait != null ? selectedLine.Portrait.name : "(none)");
			AddPreviewRow("Sfx", selectedLine.Sfx != null ? selectedLine.Sfx.name : "(none)");
			AddPreviewRow("Wait", selectedLine.Wait <= 0f ? "Advance 대기" : $"{selectedLine.Wait:0.##}s 자동 진행");
			AddPreviewRow("Choices", $"{selectedLine.Choices.Count}개");
		}

		private void AddPreviewRow(string label, string value)
		{
			VisualElement row = new();
			row.style.flexDirection = FlexDirection.Row;
			row.style.marginTop = 2;
			linePreviewContainer.Add(row);

			Label key = new($"{label}: ");
			key.style.minWidth = 70;
			key.style.unityFontStyleAndWeight = FontStyle.Bold;
			row.Add(key);

			Label val = new(value);
			val.style.flexGrow = 1;
			val.style.whiteSpace = WhiteSpace.Normal;
			row.Add(val);
		}

		private void RefreshRuntimeStatus()
		{
			choicesContainer.Clear();

			if (Application.isPlaying == false)
			{
				runtimeStateLabel.text = "Runtime: (Edit 모드 — Play 진입 후 미리보기 가능)";
				currentLineLabel.text = string.Empty;
				return;
			}

			if (DialogueRunner.TryGetExistingInstance(out DialogueRunner runner) == false)
			{
				runtimeStateLabel.text = "Runtime: DialogueRunner 인스턴스 없음 (씬 또는 Resources/Singletons 에 prefab 필요)";
				currentLineLabel.text = string.Empty;
				return;
			}

			runtimeStateLabel.text = runner.IsPlaying ? "Runtime: ▶ Playing" : "Runtime: ⏹ Stopped";

			if (runner.CurrentLine != null)
				currentLineLabel.text = $"현재 라인: \"{runner.CurrentLine.Text}\" ({runner.CurrentLine.name})";
			else
				currentLineLabel.text = string.Empty;

			if (runner.CurrentChoices != null && runner.CurrentChoices.Count > 0)
			{
				Label choicesLabel = new("선택지:");
				choicesLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
				choicesContainer.Add(choicesLabel);

				foreach (DialogueLine choice in runner.CurrentChoices)
				{
					DialogueLine captured = choice;
					Button choiceButton = new(() =>
					{
						runner.SubmitChoice(captured);
						RefreshRuntimeStatus();
					})
					{
						text = string.IsNullOrEmpty(captured.Text) ? captured.name : captured.Text,
					};
					choiceButton.style.marginTop = 2;
					choicesContainer.Add(choiceButton);
				}
			}
		}

		private void RefreshControls()
		{
			bool isPlaying = Application.isPlaying;
			bool runnerExists = isPlaying && DialogueRunner.TryGetExistingInstance(out _);

			playButton.SetEnabled(isPlaying && selectedLine != null);
			advanceButton.SetEnabled(runnerExists);
			stopButton.SetEnabled(runnerExists);
		}

		private void OnInspectorUpdate()
		{
			if (Application.isPlaying)
			{
				RefreshRuntimeStatus();
				RefreshControls();
			}
		}
	}
}
