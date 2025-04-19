namespace WitchMendokusai
{
	public class NPCObject : UnitObject, IInteractable
	{
		public NPC Data => UnitData as NPC;

		public void OnInteract()
		{
			UIManager.Instance.SetPanel(PanelType.NPC, this);
		}
	}
}