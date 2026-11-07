using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	// DialogueStageView 의 선택지 목록. 오른쪽 작은 세로 목록, 키보드와 패드와 마우스
	public partial class DialogueStageView
	{
		private const string USS_ROW = "wm-dlg-choice";
		private const string USS_ROW_IN = USS_ROW + "--in";
		private const string USS_ROW_FOCUS = USS_ROW + "--focus";
		private const string USS_ROW_LOCKED = USS_ROW + "--locked";
		private const string USS_ROW_LABEL = USS_ROW + "__label";
		private const string USS_ROW_REASON = USS_ROW + "__reason";

		private VisualElement choiceList;
		private readonly List<Button> choiceRows = new();
		private readonly List<int> selectableRows = new();
		private IReadOnlyList<DialogueChoiceRow> currentRows;
		private Action<int> choiceSubmit;
		private int focusedSelectable;
		private bool isChoosing;
		private int ignoreInputUntilFrame = -1;

		private void BuildChoices()
		{
			choiceList = root.Q<VisualElement>("wm-dlg-choices");
			choiceList.RegisterCallback<NavigationMoveEvent>(OnChoiceNavigationMove, TrickleDown.TrickleDown);
		}

		public void PresentChoices(DialogueStageDelta delta, string speakerName, string prompt, IReadOnlyList<DialogueChoiceRow> rows, Action<int> submit)
		{
			EnsureSessionVisible();
			isChoosing = true;
			ApplyDelta(delta);

			if (presentation == DialogueStagePresentation.World || isSkipping)
				ShowLine(speakerName, prompt);
			advancer.Complete();
			RenderLine();

			currentRows = rows;
			choiceSubmit = submit;
			BuildChoiceRows(rows);
			root.AddToClassList(USS_CHOOSING);
			choiceList.style.display = DisplayStyle.Flex;
			FocusSelectable(0, playSound: false);
			RefreshOpenState();
		}

		private void BuildChoiceRows(IReadOnlyList<DialogueChoiceRow> rows)
		{
			choiceList.Clear();
			choiceRows.Clear();
			selectableRows.Clear();

			for (int i = 0; i < rows.Count; i++)
			{
				int rowIndex = i;
				DialogueChoiceRow row = rows[i];
				Button button = new(() => OnChoiceClicked(rowIndex));
				button.AddToClassList(USS_ROW);

				Label label = new(row.Label) { pickingMode = PickingMode.Ignore };
				label.AddToClassList(USS_ROW_LABEL);
				button.Add(label);

				if (row.IsLocked)
				{
					button.AddToClassList(USS_ROW_LOCKED);
					button.focusable = false;
					Label reason = new(row.LockReason) { pickingMode = PickingMode.Ignore };
					reason.AddToClassList(USS_ROW_REASON);
					button.Add(reason);
				}
				else
				{
					int selectableIndex = selectableRows.Count;
					selectableRows.Add(rowIndex);
					button.RegisterCallback<PointerEnterEvent>(_ => FocusSelectable(selectableIndex, playSound: true));
				}

				choiceList.Add(button);
				choiceRows.Add(button);

				if (UIEffects.Enabled)
				{
					button.style.transitionDelay = new List<TimeValue> { new(settings.ChoiceRowDelaySeconds * i, TimeUnit.Second) };
					button.schedule.Execute(() => button.AddToClassList(USS_ROW_IN)).ExecuteLater((long)(settings.ChoiceAppearDelaySeconds * 1000f));
				}
				else
				{
					button.AddToClassList(USS_ROW_IN);
				}
			}
		}

		private void OnChoiceNavigationMove(NavigationMoveEvent evt)
		{
			if (isChoosing == false || selectableRows.Count == 0)
				return;

			if (evt.direction == NavigationMoveEvent.Direction.Up)
				FocusSelectable((focusedSelectable + selectableRows.Count - 1) % selectableRows.Count, playSound: true);
			else if (evt.direction == NavigationMoveEvent.Direction.Down)
				FocusSelectable((focusedSelectable + 1) % selectableRows.Count, playSound: true);
			else
				return;

			evt.StopPropagation();
		}

		private void FocusSelectable(int selectableIndex, bool playSound)
		{
			if (selectableRows.Count == 0)
				return;

			bool isSame = selectableIndex == focusedSelectable;
			focusedSelectable = selectableIndex;
			for (int i = 0; i < selectableRows.Count; i++)
				choiceRows[selectableRows[i]].EnableInClassList(USS_ROW_FOCUS, i == selectableIndex);

			choiceRows[selectableRows[selectableIndex]].Focus();
			if (playSound && isSame == false)
				UiSfx.Play(UiSfxEvent.FocusMove);
		}

		private void OnChoiceClicked(int rowIndex)
		{
			if (isChoosing == false)
				return;

			DialogueChoiceRow row = currentRows[rowIndex];
			if (row.IsLocked)
			{
				UiSfx.Play(UiSfxEvent.Blocked);
				return;
			}
			SubmitChoiceRow(row);
		}

		private void SubmitFocusedChoice()
		{
			if (selectableRows.Count > 0)
				SubmitChoiceRow(currentRows[selectableRows[focusedSelectable]]);
		}

		private void SubmitChoiceRow(DialogueChoiceRow row)
		{
			Action<int> submit = choiceSubmit;
			isChoosing = false;
			choiceSubmit = null;
			ignoreInputUntilFrame = Time.frameCount + 1;
			HideChoiceList();
			UiSfx.Play(UiSfxEvent.Confirm);
			submit?.Invoke(row.SubmitIndex);
			if (isSessionOpen)
				RefreshOpenState();
		}

		private void HideChoiceList()
		{
			isChoosing = false;
			root.RemoveFromClassList(USS_CHOOSING);
			choiceList.style.display = DisplayStyle.None;
			choiceList.Clear();
			choiceRows.Clear();
			selectableRows.Clear();
			focusedSelectable = 0;
			root.panel?.focusController?.focusedElement?.Blur();
		}
	}
}
