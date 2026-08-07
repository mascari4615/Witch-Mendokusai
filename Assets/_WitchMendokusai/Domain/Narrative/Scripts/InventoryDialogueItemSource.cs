namespace WitchMendokusai
{
	/// <summary>
	/// 대화의 「그 물건 가졌나」를 **실제 가방**에 묻는 통로 (TASK-WM-052).
	///
	/// 세는 일은 안 한다 — 가방이 이미 세고 있다. 여기서 하는 건 그 답을 넘기는 것뿐이다
	/// (`EffectRunnerDialogueSink` 와 같은 결: 좁은 구멍 + 얇은 어댑터).
	/// </summary>
	public sealed class InventoryDialogueItemSource : IDialogueItemCountSource
	{
		private readonly Inventory inventory;

		public InventoryDialogueItemSource(Inventory itemInventory)
		{
			inventory = itemInventory;
		}

		public int GetItemAmount(int itemId) => inventory.GetItemAmount(itemId);
	}
}
