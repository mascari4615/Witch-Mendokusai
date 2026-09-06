using UnityEngine.UIElements;
using WitchMendokusai.DomainSDK.Idle;
using BigNumberText = WitchMendokusai.Numerics.BigNumberText;

namespace WitchMendokusai.Idle.UI
{
	public static class AwayReportPresenter
	{
		public static void Bind(VisualElement popup, IdleAwayReport report, UIContentSO content)
		{
			popup.style.display = DisplayStyle.Flex;
			popup.RegisterCallback<PointerDownEvent>(moment => moment.StopPropagation());
			popup.RequireQ<Label>("away-span").text = content.AwaySpanText(report.CreditedSeconds);
			popup.RequireQ<Label>("gold-value").text = content.GainText(BigNumberText.Format(report.ResourceGained));
			popup.RequireQ<Label>("kills-value").text = content.GainText(BigNumberText.Format(report.KillsGained));
			popup.RequireQ<Label>("stages-value").text = content.GainText(BigNumberText.Format(report.StagesGained));
			popup.RequireQ<Label>("items-value").text = content.GainText(BigNumberText.Format(report.ItemsGained));

			Label warning = popup.RequireQ<Label>("away-warning");
			if (report.HitCap)
			{
				warning.text = content.AwayWarningText(report.CapSeconds, report.LostSeconds);
				warning.style.display = DisplayStyle.Flex;
			}

			popup.RequireQ<Button>("away-close").clicked += () => popup.style.display = DisplayStyle.None;
		}
	}
}
