using VContainer;

namespace WitchMendokusai
{
	public class NPCObject : UnitObject, IInteractable
	{
		public NPC Data => UnitData as NPC;

		private UIManager uiManager;

		[Inject]
		public void Construct(UIManager uiManager)
		{
			this.uiManager = uiManager;
		}

		public void OnInteract()
		{
			uiManager.NPC.SetPanel(NPCPanelType.NPC, this);
		}
	}
}
