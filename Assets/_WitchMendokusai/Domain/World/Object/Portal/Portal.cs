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
		[Tooltip("이 포털만의 화면 전환. 비우면 UIFxSettings.portalWipe")]
		[SerializeField] private UIWipe wipe;

		private UIManager uiManager;
		private StageManager stageManager;

		[Inject]
		public void Construct(UIManager uiManager, StageManager stageManager)
		{
			this.uiManager = uiManager;
			this.stageManager = stageManager;
		}

		private void OnEnable()
		{
			if (TargetStage != null && TargetStage.DevOnly && Debug.isDebugBuild == false)
				gameObject.SetActive(false);
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
					},
					overrideWipe: wipe).Forget();
			}
		}

		public void Active()
		{
			gameObject.layer = 0;
		}
	}
}
