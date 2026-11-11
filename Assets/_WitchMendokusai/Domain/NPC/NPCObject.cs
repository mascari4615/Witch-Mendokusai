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

		protected override void Start()
		{
			// 개발 빌드 전용 NPC (개발 상점 등) 는 릴리스에서 숨김
			if (Data != null && Data.DevBuildOnly && UnityEngine.Debug.isDebugBuild == false)
			{
				gameObject.SetActive(false);
				return;
			}
			base.Start();
		}

		public void OnInteract()
		{
			uiManager.NPC.SetPanel(NPCPanelType.NPC, this);
		}
	}
}
