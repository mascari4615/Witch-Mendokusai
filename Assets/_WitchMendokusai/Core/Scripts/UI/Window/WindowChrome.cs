using UnityEngine;
using UnityEngine.UI;

namespace WitchMendokusai
{
	public class WindowChrome : MonoBehaviour
	{
		[SerializeField] private RectTransform header;
		[SerializeField] private Button closeButton;

		public RectTransform Header => header;
		public Button CloseButton => closeButton;
	}
}
