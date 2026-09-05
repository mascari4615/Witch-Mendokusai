using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using TMPro;

namespace WitchMendokusai
{
	public class UIFPS : MonoBehaviour
	{
		[SerializeField] private TextMeshProUGUI text;
		private float deltaTime;

		private void Update()
		{
			deltaTime += (Time.deltaTime - deltaTime) * 0.1f;
			float fps = 1.0f / deltaTime;

			text.text = Mathf.Ceil(fps).ToString();
		}
	}
}