using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace WitchMendokusai
{
	public class UILayoutInitializer : MonoBehaviour
	{
		[SerializeField] private List<ContentSizeFitter> contentSizeFitters = new();
		[SerializeField] private List<LayoutElement> layoutElements = new();

		private void Awake()
		{
			EnableLayoutComponents();
		}

		[ContextMenu(nameof(SetupLayoutComponents))]
		public void SetupLayoutComponents()
		{
			contentSizeFitters = GetComponentsInChildren<ContentSizeFitter>(true).ToList();
			layoutElements = GetComponentsInChildren<LayoutElement>(true).ToList();
		}

		// 활성화
		[ContextMenu(nameof(EnableLayoutComponents))]
		public void EnableLayoutComponents()
		{
			foreach (ContentSizeFitter fitter in contentSizeFitters)
				fitter.enabled = true;

			foreach (LayoutElement element in layoutElements)
				element.enabled = true;
		}

		// 비활성화
		[ContextMenu(nameof(DisableLayoutComponents))]
		public void DisableLayoutComponents()
		{
			foreach (ContentSizeFitter fitter in contentSizeFitters)
				fitter.enabled = false;

			foreach (LayoutElement element in layoutElements)
				element.enabled = false;
		}
	}
}