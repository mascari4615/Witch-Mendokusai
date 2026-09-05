using System;
using System.Collections.Generic;
using UnityEngine.UIElements;
using WitchMendokusai.DomainSDK.Idle;
using BigNumberText = WitchMendokusai.Numerics.BigNumberText;

namespace WitchMendokusai.Idle.UI
{
	/// <summary>
	/// 던전 탭 (layout.md 표 3). 4종 줄. 줄마다 보상, 입장권 n/n, 입장, 소탕
	///
	/// ★ 규칙은 한 줄도 없음. 보상 수치와 여닫힘은 전부 사진에서 읽고, 누르면 세션에 보냄
	/// </summary>
	public sealed class DungeonPageController
	{
		private sealed class Row
		{
			public Label Name;
			public Label Ticket;
			public Label Reward;
			public Label Refill;
			public Button Enter;
			public Button Sweep;
		}

		private readonly IdleSession session;
		private readonly UIContentSO content;
		private readonly Action writeDown;
		private readonly Action requestRender;
		private readonly Action<string, float> showFeedback;
		private readonly float feedbackSeconds;
		private readonly List<Row> rows = new List<Row>();

		public DungeonPageController(VisualElement page, IdleSession session, UIContentSO content,
			VisualTreeAsset rowAsset, Action writeDown, Action requestRender,
			Action<string, float> showFeedback, float feedbackSeconds)
		{
			this.session = session;
			this.content = content;
			this.writeDown = writeDown;
			this.requestRender = requestRender;
			this.showFeedback = showFeedback;
			this.feedbackSeconds = feedbackSeconds;

			VisualElement host = page.Q<VisualElement>("dungeon-rows");
			for (int index = 0; index < IdleDungeons.COUNT; index++)
			{
				IdleDungeonKind kind = (IdleDungeonKind)index;
				TemplateContainer tree = rowAsset.Instantiate();
				VisualElement made = tree.Q<VisualElement>("dungeon-row");
				made.RemoveFromHierarchy();
				host.Add(made);

				Row row = new Row
				{
					Name = made.Q<Label>("dungeon-name"),
					Ticket = made.Q<Label>("dungeon-ticket"),
					Reward = made.Q<Label>("dungeon-reward"),
					Refill = made.Q<Label>("dungeon-refill"),
					Enter = made.Q<Button>("dungeon-enter"),
					Sweep = made.Q<Button>("dungeon-sweep"),
				};
				row.Enter.clicked += () => Enter(kind);
				row.Sweep.clicked += () => Sweep(kind);
				rows.Add(row);
			}
		}

		public void Render(IdleSnapshot snapshot)
		{
			string span = content.DescribeSpan(snapshot.TicketRefillSeconds);

			for (int index = 0; index < rows.Count; index++)
			{
				IdleDungeonKind kind = (IdleDungeonKind)index;
				Row row = rows[index];
				long left = index < snapshot.Tickets.Length ? snapshot.Tickets[index] : 0L;
				bool open = IdleDungeons.IsOpen(kind);

				row.Name.text = content.DungeonName(kind);
				row.Ticket.text = content.DungeonTicketText(left, snapshot.TicketsPerDay);
				row.Reward.text = open ? RewardText(kind, snapshot) : content.DungeonClosedText;
				row.Refill.text = left < snapshot.TicketsPerDay ? content.DungeonRefillText(span) : string.Empty;
				row.Enter.text = content.DungeonEnterText;
				row.Sweep.text = content.DungeonSweepText(left);
				row.Enter.SetEnabled(open && left > 0L);
				row.Sweep.SetEnabled(open && left > 0L);
			}
		}

		/// <summary>그 던전이 한 판에 주는 것. 수치는 사진이 실어 온다</summary>
		private string RewardText(IdleDungeonKind kind, IdleSnapshot snapshot)
		{
			switch (kind)
			{
				case IdleDungeonKind.Gold:
					return content.DungeonGoldRewardText(BigNumberText.Format(snapshot.DungeonGold));
				case IdleDungeonKind.Boss:
					return content.DungeonBossRewardText(
						snapshot.DungeonBossShards, snapshot.DungeonBossGear, snapshot.DungeonGearTier);
				case IdleDungeonKind.Gear:
					return content.DungeonGearRewardText(snapshot.DungeonGearCount, snapshot.DungeonGearTier);
				default:
					return content.DungeonClosedText;
			}
		}

		private void Enter(IdleDungeonKind kind)
		{
			if (session.TryEnterDungeon(kind, out IdleDungeonReward reward))
			{
				Say(reward);
			}
		}

		private void Sweep(IdleDungeonKind kind)
		{
			if (session.TrySweepDungeon(kind, out IdleDungeonReward reward))
			{
				Say(reward);
			}
		}

		private void Say(IdleDungeonReward reward)
		{
			string got;
			switch (reward.Kind)
			{
				case IdleDungeonKind.Gold:
					got = content.DungeonGoldRewardText(BigNumberText.Format(reward.Gold));
					break;
				case IdleDungeonKind.Boss:
					got = content.DungeonBossRewardText(reward.Shards, reward.Gear, 0);
					break;
				default:
					got = content.DungeonGearRewardText(reward.Gear, 0);
					break;
			}

			showFeedback(content.DungeonFeedbackText(content.DungeonName(reward.Kind), reward.Runs, got), feedbackSeconds);
			writeDown();
			requestRender();
		}
	}
}
