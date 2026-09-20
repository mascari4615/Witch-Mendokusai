using System;
using UnityEngine.UIElements;
using WitchMendokusai.DomainSDK.Idle;
using BigNumberText = WitchMendokusai.Numerics.BigNumberText;

namespace WitchMendokusai.Idle.UI
{
	/// <summary>
	/// 던전 한 판이 끝났을 때 결과 (changes/idle-dungeon-run). 돌아온 보상 팝업과 같은 꼴.
	/// 던전 안에서 뜨고, 나가기를 눌러야 본판 (사용자 2026-09-20). 그래서 닫기가 곧 나가기
	/// </summary>
	public static class DungeonResultPresenter
	{
		private static Action leaveBound;

		public static void Bind(VisualElement popup, IdleDungeonResult result, UIContentSO content, Action leave)
		{
			popup.style.display = DisplayStyle.Flex;
			popup.RegisterCallback<PointerDownEvent>(moment => moment.StopPropagation());
			popup.RequireQ<Label>("dungeon-result-title").text = content.DungeonCellText(result.Kind, result.Difficulty, result.Stage);
			popup.RequireQ<Label>("dungeon-result-status").text = content.DungeonResultStatusText(result.Cleared);
			popup.RequireQ<Label>("dungeon-result-span").text = content.DescribeSpan(result.SecondsSpent);
			popup.RequireQ<Label>("kills-value").text = content.GainText(BigNumberText.Format(result.Kills));
			popup.RequireQ<Label>("gold-value").text = content.GainText(BigNumberText.Format(result.Gold));
			popup.RequireQ<Label>("shards-value").text = content.GainText(BigNumberText.Format(result.Shards));
			popup.RequireQ<Label>("gear-value").text = content.GainText(BigNumberText.Format(result.Gear));
			Button close = popup.RequireQ<Button>("dungeon-result-close");
			close.text = content.DungeonLeaveText;
			if (leaveBound != null)
			{
				close.clicked -= leaveBound;
			}
			leaveBound = leave;
			close.clicked += leaveBound;
		}
	}
}
