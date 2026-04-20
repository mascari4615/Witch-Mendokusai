using System.Collections;
using FMODUnity;
using UnityEngine;

namespace WitchMendokusai
{
	public class FishingSpotObject : MonoBehaviour, IInteractable
	{
		[Header("_" + nameof(FishingSpotObject))]
		[SerializeField] private FishingSpotData data;
		[SerializeField] private string castEventPath = "event:/SFX/Fishing/Cast";

		private IFishingMiniGame miniGame;
		private bool isFishing;

		private void Awake()
		{
			miniGame = GetComponent<IFishingMiniGame>();
		}

		public void OnInteract()
		{
			if (isFishing)
				return;

			if (!SOManager.Instance.ItemInventory.HasEquipment(EquipmentType.FishingRod))
			{
				UIManager.Instance.SpeechBubble.Show(transform, "낚싯대가 필요합니다.");
				return;
			}

			StartCoroutine(FishingRoutine());
		}

		private IEnumerator FishingRoutine()
		{
			isFishing = true;

			if (!string.IsNullOrEmpty(castEventPath))
				RuntimeManager.PlayOneShot(castEventPath, transform.position);

			bool caught = false;
			FishingContext context = new() { Fisherman = Player.Instance.transform, Data = data };
			yield return StartCoroutine(miniGame.Play(context, result => caught = result));

			if (caught)
				GameLogic.SpawnLootItem(data.Loots, transform.position);

			isFishing = false;
		}
	}
}
