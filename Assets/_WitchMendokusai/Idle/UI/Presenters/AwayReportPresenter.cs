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
			popup.Q<Label>("away-span").text = content.AwaySpanText(report.CreditedSeconds);
			popup.Q<Label>("gold-value").text = content.GainText(BigNumberText.Format(report.ResourceGained));
			popup.Q<Label>("kills-value").text = content.GainText(BigNumberText.Format(report.KillsGained));
			popup.Q<Label>("stages-value").text = content.GainText(BigNumberText.Format(report.StagesGained));
			popup.Q<Label>("items-value").text = content.GainText(BigNumberText.Format(report.ItemsGained));

			Label warning = popup.Q<Label>("away-warning");
			if (report.HitCap)
			{
				warning.text = content.AwayWarningText(report.CapSeconds, report.LostSeconds);
				warning.style.display = DisplayStyle.Flex;
			}

			popup.Q<Button>("away-close").clicked += () => popup.style.display = DisplayStyle.None;
		}
	}
}
