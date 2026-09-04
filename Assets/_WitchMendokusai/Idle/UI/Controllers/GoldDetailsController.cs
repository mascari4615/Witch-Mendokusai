using System;
using UnityEngine.UIElements;
using WitchMendokusai.DomainSDK.Idle;
using BigNumberText = WitchMendokusai.Numerics.BigNumberText;

namespace WitchMendokusai.Idle.UI
{
	public sealed class GoldDetailsController
	{
		private readonly VisualElement popup;
		private readonly ModalController modalController;
		private readonly UIContentSO content;
		private readonly Label amount;
		private readonly Label income;

		public GoldDetailsController(VisualElement popup, ModalController modalController, UIContentSO content)
		{
			this.popup = popup;
			this.modalController = modalController;
			this.content = content;
			amount = popup.Q<Label>("gold-amount");
			income = popup.Q<Label>("gold-income");
			modalController.Register(popup, Close);
			popup.Q<Button>("gold-close").clicked += Close;
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
			amount.text = content.GoldAmountText(BigNumberText.Format(snapshot.Resource));
			income.text = content.GoldIncomeText(BigNumberText.Format(snapshot.IncomePerSecond));
		}
	}
}
