using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using TMPro;

namespace WitchMendokusai
{
	public class UIFPS : MonoBehaviour
	{
		// 개발자 창 표시 탭에서 켬. 씬이 바뀌어도 유지
		public static bool IsVisible { get; set; }

		[SerializeField] private TextMeshProUGUI text;
		private float deltaTime;

		private void Update()
		{
			text.enabled = IsVisible;
			if (IsVisible == false)
				return;

			deltaTime += (Time.deltaTime - deltaTime) * 0.1f;
			float fps = 1.0f / deltaTime;

			text.text = Mathf.Ceil(fps).ToString();
		}
	}
}