using UnityEngine.UIElements;
using WitchMendokusai.DomainSDK.Idle;

namespace WitchMendokusai.Idle.UI
{
	public sealed class DungeonPageController
	{
		private readonly UIContentSO content;
		private readonly Button[] rows;

		public DungeonPageController(VisualElement page, UIContentSO content)
		{
			this.content = content;
			rows = new Button[IdleDungeons.COUNT];
			for (int index = 0; index < rows.Length; index++)
			{
				rows[index] = page.Q<Button>("dungeon-" + index);
				if (rows[index] != null)
				{
					rows[index].SetEnabled(false);
				}
			}
		}

		public void Render(IdleSnapshot snapshot)
		{
			long hours = (long)(snapshot.TicketRefillSeconds / 3600d);
			long minutes = (long)(snapshot.TicketRefillSeconds / 60d) % 60L;
			for (int index = 0; index < rows.Length; index++)
			{
				if (rows[index] == null)
				{
					continue;
				}

				rows[index].text = content.DungeonRowText(
					content.DungeonName((IdleDungeonKind)index), snapshot.Tickets[index], hours, minutes);
			}
		}
	}
}
