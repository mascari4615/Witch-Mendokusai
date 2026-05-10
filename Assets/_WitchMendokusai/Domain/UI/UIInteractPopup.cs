using TMPro;
using UnityEngine;

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

		private void Awake()
		{
			canvasGroup = GetComponent<CanvasGroup>();
			contentFitterRefresh = GetComponent<ContentFitterRefresh>();

			EventBus eventBus = EventBus.Instance;
			eventBus.Subscribe<PlayerSpawnedEvent>(OnPlayerSpawned);
			eventBus.Subscribe<PlayerDespawnedEvent>(OnPlayerDespawned);
		}

		private void Start()
		{
			canvasGroup.SetVisible(false);
			TimeManager.Instance.RegisterCallback(UpdatePopup);
		}

		private void OnDestroy()
		{
			if (EventBus.TryGetExistingInstance(out EventBus eventBus))
			{
				eventBus.Unsubscribe<PlayerSpawnedEvent>(OnPlayerSpawned);
				eventBus.Unsubscribe<PlayerDespawnedEvent>(OnPlayerDespawned);
			}
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
