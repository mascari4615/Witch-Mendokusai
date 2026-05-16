using VContainer;

namespace WitchMendokusai
{
	public class NPCObject : UnitObject, IInteractable
	{
		public NPC Data => UnitData as NPC;

		private UIManager uiManager;

		[Inject]
		public void Construct(UIManager uiManager, TimeManager timeManager, UnitStatCalculator unitStatCalculator)
		{
			this.uiManager = uiManager;
			SetBaseDeps(timeManager, unitStatCalculator);
		}

		public void OnInteract()
		{
			uiManager.NPC.SetPanel(NPCPanelType.NPC, this);
		}
	}
}
