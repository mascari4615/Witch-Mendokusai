using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;

namespace WitchMendokusai
{
	public class Portal : MonoBehaviour
	{
		[field: SerializeField] public Transform TpPos { get; private set; }
		[field: SerializeField] public Stage TargetStage { get; private set; }
		[field: SerializeField] private int targetPortalIndex = -1;

		private UIManager uiManager;
		private StageManager stageManager;

		[Inject]
		public void Construct(UIManager uiManager, StageManager stageManager)
		{
			this.uiManager = uiManager;
			this.stageManager = stageManager;
		}

		public void OnTriggerEnter(Collider other)
		{
			if (other.CompareTag("Player"))
			{
				uiManager.Transition.Transition(
					aDuringTransition: () =>
					{
						stageManager.LoadStage(TargetStage, targetPortalIndex);
					},
					aWhenEnd: () =>
					{
						uiManager.StagePopup(TargetStage);
					}).Forget();
			}
		}

		public void Active()
		{
			gameObject.layer = 0;
		}
	}
}
