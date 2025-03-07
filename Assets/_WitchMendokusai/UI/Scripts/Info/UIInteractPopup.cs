using System.Collections;
using System.Collections.Generic;
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

		private void Awake()
		{
			canvasGroup = GetComponent<CanvasGroup>();
			contentFitterRefresh = GetComponent<ContentFitterRefresh>();
		}

		private void Start()
		{
			canvasGroup.alpha = 0;
			TimeManager.Instance.RegisterCallback(UpdatePopup);
		}

		public void UpdatePopup()
		{
			Vector3 playerPos = Player.Instance.transform.position;
			float interactDistance = PlayerInteraction.InteractionDistance;

			InteractiveObject nearestInteractive = MHelper.GetNearest(InteractiveObject.ActiveInteractives, playerPos, interactDistance);

			if (nearestInteractive == null)
			{
				canvasGroup.alpha = 0;
				return;
			}

			canvasGroup.alpha = 1;
			keyCodeText.text = "Z";
			nameText.text = nearestInteractive.name;

			contentFitterRefresh.RefreshContentFitters();
		}
	}
}