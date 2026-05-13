using TMPro;
using UnityEngine;
using VContainer;

namespace WitchMendokusai
{
	[RequireComponent(typeof(CanvasGroup), typeof(ContentFitterRefresh))]
	public class UIInteractPopup : MonoBehaviour
	{
		private CanvasGroup canvasGroup;
		private ContentFitterRefresh contentFitterRefresh;
		[SerializeField] private TextMeshProUGUI keyCodeText;
		[SerializeField] private TextMeshProUGUI nameText;

		private Transform playerTransform;
		private TimeManager timeManager;

		[Inject]
		public void Construct(TimeManager timeManager)
		{
			this.timeManager = timeManager;
		}

		private void Awake()
		{
			canvasGroup = GetComponent<CanvasGroup>();
			contentFitterRefresh = GetComponent<ContentFitterRefresh>();

			EventBusBridge.Subscribe<PlayerSpawnedEvent>(OnPlayerSpawned);
			EventBusBridge.Subscribe<PlayerDespawnedEvent>(OnPlayerDespawned);
		}

		private void Start()
		{
			canvasGroup.SetVisible(false);
			timeManager.RegisterCallback(UpdatePopup);
		}

		private void OnDestroy()
		{
			EventBusBridge.Unsubscribe<PlayerSpawnedEvent>(OnPlayerSpawned);
			EventBusBridge.Unsubscribe<PlayerDespawnedEvent>(OnPlayerDespawned);
			timeManager.RemoveCallback(UpdatePopup);
		}

		private void OnPlayerSpawned(PlayerSpawnedEvent evt) => playerTransform = evt.Transform;
		private void OnPlayerDespawned(PlayerDespawnedEvent evt) => playerTransform = null;

		public void UpdatePopup()
		{
			if (playerTransform == null)
			{
				canvasGroup.SetVisible(false);
				return;
			}

			Vector3 playerPos = playerTransform.position;
			float interactDistance = PlayerInteraction.InteractionDistance;

			InteractiveObject nearestInteractive = WMHelper.GetNearest(InteractiveObject.ActiveInteractive, playerPos, interactDistance);

			if (nearestInteractive == null)
			{
				canvasGroup.SetVisible(false);
				return;
			}

			canvasGroup.SetVisible(true);
			keyCodeText.text = "Z";
			nameText.text = nearestInteractive.name;

			contentFitterRefresh.RefreshContentFitters();
		}
	}
}
