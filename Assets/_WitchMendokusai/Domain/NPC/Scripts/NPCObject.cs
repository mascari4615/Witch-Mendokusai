using VContainer;

namespace WitchMendokusai
{
	public class NPCObject : UnitObject, IInteractable
	{
		public NPC Data => UnitData as NPC;

		private UIManager uiManager;

		[Inject]
		public void Construct(UIManager uiManager, TimeManager timeManager, UnitStatCalculator unitStatCalculator,
			ObjectPoolManager objectPoolManager, PlayerProvider playerProvider)
		{
			this.uiManager = uiManager;
			SetBaseDeps(timeManager, unitStatCalculator, objectPoolManager, playerProvider);
		}

		public void OnInteract()
		{
			uiManager.NPC.SetPanel(NPCPanelType.NPC, this);
		}
	}
}
