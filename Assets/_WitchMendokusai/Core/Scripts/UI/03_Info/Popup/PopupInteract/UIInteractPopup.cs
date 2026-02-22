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
			canvasGroup.SetVisible(false);
			TimeManager.Instance.RegisterCallback(UpdatePopup);
		}

		public void UpdatePopup()
		{
			Vector3 playerPos = Player.Instance.transform.position;
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