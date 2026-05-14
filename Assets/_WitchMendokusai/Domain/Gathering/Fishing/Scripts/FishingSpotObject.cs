using System.Collections;
using FMODUnity;
using UnityEngine;
using VContainer;

namespace WitchMendokusai
{
	public class FishingSpotObject : MonoBehaviour, IInteractable
	{
		[Header("_" + nameof(FishingSpotObject))]
		[SerializeField] private FishingSpotData data;
		[SerializeField] private string castEventPath = "event:/SFX/Fishing/Cast";

		private IFishingMiniGame miniGame;
		private bool isFishing;
		private UIManager uiManager;
		private SOManager soManager;
		private PlayerProvider playerProvider;

		[Inject]
		public void Construct(UIManager uiManager, SOManager soManager, PlayerProvider playerProvider)
		{
			this.uiManager = uiManager;
			this.soManager = soManager;
			this.playerProvider = playerProvider;
		}

		private void Awake()
		{
			miniGame = GetComponent<IFishingMiniGame>();
		}

		private void OnDisable()
		{
			StopAllCoroutines();
			isFishing = false;
		}

		public void OnInteract()
		{
			if (isFishing)
				return;

			if (soManager.ItemInventory.HasEquipment(EquipmentType.FishingRod) == false)
			{
				uiManager.SpeechBubble.Show(transform, "낚싯대가 필요합니다.");
				return;
			}

			StartCoroutine(FishingRoutine());
		}

		private IEnumerator FishingRoutine()
		{
			isFishing = true;

			if (miniGame == null)
			{
				Debug.LogError("[FishingSpotObject] IFishingMiniGame 컴포넌트가 없습니다.", this);
				isFishing = false;
				yield break;
			}

			if (string.IsNullOrEmpty(castEventPath) == false)
				RuntimeManager.PlayOneShot(castEventPath, transform.position);

			bool caught = false;
			FishingContext context = new() { Fisherman = playerProvider.Current.transform, Data = data };
			yield return StartCoroutine(miniGame.Play(context, result => caught = result));

			if (caught)
				GameLogic.SpawnLootItem(data.Loots, transform.position);

			isFishing = false;
		}
	}
}
