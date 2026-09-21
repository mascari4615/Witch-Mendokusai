using System;
using UnityEngine.UIElements;
using WitchMendokusai.DomainSDK.Idle;

namespace WitchMendokusai.Idle.UI
{
	/// <summary>확률표 팝업 (layout.md 구현 순서 10). 등급별 확률, 천장, 픽업, 묶음 보장. 수치는 전부 사진에서</summary>
	public sealed class OddsPopupController
	{
		private readonly VisualElement popup;
		private readonly ModalController modalController;
		private readonly UIContentSO content;
		private readonly Label[] rows;
		private readonly Label pity;
		private readonly Label pickup;
		private readonly Label batch;

		public OddsPopupController(VisualElement popup, ModalController modalController, UIContentSO content)
		{
			this.popup = popup;
			this.modalController = modalController;
			this.content = content;
			int grades = Enum.GetValues(typeof(IdleDollGrade)).Length;
			rows = new Label[grades];
			for (int grade = 0; grade < grades; grade++)
			{
				rows[grade] = popup.RequireQ<Label>("odds-row-" + grade);
			}

			pity = popup.RequireQ<Label>("odds-pity");
			pickup = popup.RequireQ<Label>("odds-pickup");
			batch = popup.RequireQ<Label>("odds-batch");
			modalController.Register(popup, Close);
			popup.RequireQ<Button>("odds-close").clicked += Close;
		}

		public void Open(Action beforeOpen)
		{
			beforeOpen();
			modalController.Show(popup);
		}

		public void Close()
		{
			modalController.Hide(popup);
		}

		public void Render(IdleSnapshot snapshot)
		{
			double common = 1d - snapshot.LegendChance - snapshot.EpicChance - snapshot.RareChance;
			SetRow(IdleDollGrade.Legend, snapshot.LegendChance);
			SetRow(IdleDollGrade.Epic, snapshot.EpicChance);
			SetRow(IdleDollGrade.Rare, snapshot.RareChance);
			SetRow(IdleDollGrade.Common, common > 0d ? common : 0d);

			pity.text = content.OddsPityText(snapshot.PullsToPity);
			pickup.text = snapshot.PickupDollId >= 0
				? content.OddsPickupText(IdleDolls.KindOf(snapshot.PickupDollId).Name, snapshot.PickupWeight)
				: content.PickupNoneText;
			batch.text = content.OddsBatchText(snapshot.PullBatchCount, content.GradeName(snapshot.PullBatchFloorGrade));
		}

		private void SetRow(IdleDollGrade grade, double chance)
		{
			Label row = rows[(int)grade];
			if (row != null)
			{
				row.text = content.OddsRowText(content.GradeName(grade), chance);
			}
		}
	}
}
