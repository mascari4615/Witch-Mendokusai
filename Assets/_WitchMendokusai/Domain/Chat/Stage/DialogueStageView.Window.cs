using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	// DialogueStageView 의 메시지 창, 이름 띠, 글자 표시, 표시 방식 전환 부분
	public partial class DialogueStageView
	{
		private const string USS_WINDOW_FADED = "wm-dlg-window--faded";
		private const string USS_WINDOW_CORNER = "wm-dlg-window__corner";
		private const string USS_BAND_NPC = "wm-dlg-name--npc";
		private const string USS_BAND_HIDDEN = "wm-dlg-name--hidden";
		private const string HIDDEN_TEXT_OPEN = "<color=#00000000>";
		private const string HIDDEN_TEXT_CLOSE = "</color>";

		private readonly DialogueLineAdvancer advancer = new();

		private VisualElement window;
		private VisualElement nameBand;
		private Label nameLabel;
		private Label lineLabel;
		private VisualElement nextMark;

		private int shownCharacters = -1;
		private float blinkElapsed;
		private bool blinkDim;
		private float windowSwitchElapsed = -1f;

		private void BuildWindow()
		{
			window = root.Q<VisualElement>("wm-dlg-window");
			nameBand = root.Q<VisualElement>("wm-dlg-name");
			nameLabel = root.Q<Label>("wm-dlg-name-label");
			lineLabel = root.Q<Label>("wm-dlg-line");
			nextMark = root.Q<VisualElement>("wm-dlg-next");
			nextMark.style.display = DisplayStyle.None;
			lineLabel.enableRichText = true;
			ShopNineSlice corner = new();
			corner.AddToClassList(USS_WINDOW_CORNER);
			window.Add(corner);
		}

		private void SetPresentation(DialogueStagePresentation next)
		{
			presentation = next;
			BeginWindowSwitch();
		}

		private void BeginWindowSwitch()
		{
			if (UIEffects.Enabled == false || shownPresentation == DialogueStagePresentation.World)
			{
				ApplyPresentationClass(presentation);
				return;
			}
			windowSwitchElapsed = 0f;
			window.AddToClassList(USS_WINDOW_FADED);
		}

		private void ApplyPresentationClass(DialogueStagePresentation next)
		{
			root.RemoveFromClassList(USS_ROOT + "--world");
			root.RemoveFromClassList(USS_ROOT + "--novel");
			root.RemoveFromClassList(USS_ROOT + "--feature");
			root.RemoveFromClassList(USS_ROOT + "--field");
			switch (next)
			{
				case DialogueStagePresentation.World:
					root.AddToClassList(USS_ROOT + "--world");
					break;
				case DialogueStagePresentation.Novel:
					root.AddToClassList(USS_ROOT + "--novel");
					break;
				case DialogueStagePresentation.Feature:
					root.AddToClassList(USS_ROOT + "--feature");
					break;
				case DialogueStagePresentation.Field:
					root.AddToClassList(USS_ROOT + "--field");
					break;
			}
			window.style.backgroundImage = next == DialogueStagePresentation.Feature
				? new StyleBackground(GetFadeTexture())
				: new StyleBackground(StyleKeyword.None);
			shownPresentation = next;
		}

		private void ShowLine(string speakerName, string text)
		{
			nextMark.style.display = DisplayStyle.None;
			nameLabel.text = speakerName;
			bool hasName = string.IsNullOrEmpty(speakerName) == false;
			nameBand.EnableInClassList(USS_BAND_HIDDEN, hasName == false);
			DialogueStageSettings.CastEntry entry = hasName ? settings.FindCast(speakerName) : null;
			nameBand.EnableInClassList(USS_BAND_NPC, entry == null || entry.IsPlayable == false);
			advancer.Begin(text);
			if (UIEffects.Enabled == false)
				advancer.Complete();
			shownCharacters = -1;
			RenderLine();
		}

		private void RenderLine()
		{
			if (shownCharacters == advancer.VisibleCharacters)
				return;
			shownCharacters = advancer.VisibleCharacters;
			string text = advancer.Text;
			lineLabel.text = shownCharacters >= text.Length
				? text
				: text.Substring(0, shownCharacters) + HIDDEN_TEXT_OPEN + text.Substring(shownCharacters) + HIDDEN_TEXT_CLOSE;
		}

		private void TickWindow(float deltaTime)
		{
			if (windowSwitchElapsed >= 0f)
			{
				windowSwitchElapsed += deltaTime;
				if (windowSwitchElapsed >= settings.PresentationSwitchSeconds)
				{
					windowSwitchElapsed = -1f;
					ApplyPresentationClass(presentation);
					window.RemoveFromClassList(USS_WINDOW_FADED);
				}
			}
			else if (shownPresentation != presentation)
			{
				ApplyPresentationClass(presentation);
			}

			bool isWorldWithoutChoice = presentation == DialogueStagePresentation.World && isChoosing == false;
			root.EnableInClassList(USS_WINDOW_HIDDEN, isWorldWithoutChoice);

			bool canAdvance = isChoosing == false && advancer.IsTyping == false && controls != null && controls.CanAdvanceLine;
			nextMark.style.display = canAdvance ? DisplayStyle.Flex : DisplayStyle.None;
			if (canAdvance == false || UIEffects.Enabled == false)
				return;

			blinkElapsed += deltaTime;
			if (blinkElapsed >= settings.NextMarkBlinkSeconds)
			{
				blinkElapsed = 0f;
				blinkDim = blinkDim == false;
				nextMark.EnableInClassList("wm-dlg-next--dim", blinkDim);
			}
		}
	}
}
