using System;
using System.Collections.Generic;
using UnityEngine.UIElements;
using WitchMendokusai.DomainSDK.Idle;

namespace WitchMendokusai.Idle.UI
{
	/// <summary>맵 팝업의 생명주기와 구역 선택 목록을 맡는다.</summary>
	public sealed class MapSelectionController
	{
		private readonly VisualElement popup;
		private readonly VisualElement rows;
		private readonly VisualTreeAsset rowAsset;
		private readonly ModalController modalController;
		private readonly UIContentSO content;
		private readonly Func<int, bool> canGoToStage;
		private readonly Action<int> selectStage;
		private readonly List<Button> buttons = new List<Button>();
		private int rowTop = int.MinValue;

		public MapSelectionController(
			VisualElement popup,
			VisualTreeAsset rowAsset,
			ModalController modalController,
			UIContentSO content,
			Func<int, bool> canGoToStage,
			Action<int> selectStage)
		{
			this.popup = popup;
			this.rowAsset = rowAsset;
			this.modalController = modalController;
			this.content = content;
			this.canGoToStage = canGoToStage;
			this.selectStage = selectStage;
			rows = popup.RequireQ<VisualElement>("map-rows");

			modalController.Register(popup, Close);
			popup.RequireQ<Button>("map-close").clicked += Close;
		}

		public bool IsOpen => popup.style.display == DisplayStyle.Flex;

		public void Toggle(Action beforeOpen)
		{
			if (IsOpen)
			{
				Close();
				return;
			}

			beforeOpen();
			modalController.Show(popup);
		}

		public void Close()
		{
			modalController.Hide(popup);
		}

		public void Render(IdleSnapshot snapshot)
		{
			int top = snapshot.BestStage;
			int bottom = top - 7 < 1 ? 1 : top - 7;
			int count = top - bottom + 1;

			EnsureRows(count, top);

			for (int index = 0; index < buttons.Count; index++)
			{
				int target = top - index;
				bool here = target == snapshot.Stage;
				buttons[index].text = content.MapStageText(
					target, here, target == snapshot.BestFarmingStage);
				buttons[index].SetEnabled(here == false && canGoToStage(target));
				buttons[index].EnableInClassList("idle-row-button--strong", here);
			}
		}

		private void EnsureRows(int count, int top)
		{
			if (buttons.Count == count && rowTop == top)
			{
				return;
			}

			rows.Clear();
			buttons.Clear();
			rowTop = top;

			for (int index = 0; index < count; index++)
			{
				int target = top - index;
				TemplateContainer tree = rowAsset.Instantiate();
				Button button = tree.RequireQ<Button>("row");
				button.RemoveFromHierarchy();
				button.clicked += () => selectStage(target);
				rows.Add(button);
				buttons.Add(button);
			}
		}
	}
}
