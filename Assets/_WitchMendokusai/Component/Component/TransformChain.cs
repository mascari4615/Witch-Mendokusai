using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace WitchMendokusai
{
	public class TransformChain : MonoBehaviour
	{
		// DOTween을 이용하여 이동 체인을 구현합니다.
		// 이동 체인은 인스펙터에서 설정할 수 있어야 합니다.

		[System.Serializable]
		public class TransformChainData
		{
			public Vector3 EndPosition;
			public Vector3 EndScale = Vector3.one;
			public bool IsLocal;
			public bool IsRelative;
			public float Duration;
			public Ease EaseType;

			public TransformChainData(Vector3 endPosition, Vector3 endScale, float duration, Ease easeType, bool isRelative, bool isLocal)
			{
				EndPosition = endPosition;
				EndScale = endScale;
				Duration = duration;
				EaseType = easeType;
				IsRelative = isRelative;
				IsLocal = isLocal;
			}
		}

		[SerializeField] private List<TransformChainData> transformChainData;

		private void OnEnable()
		{
			StartCoroutine(RunTransformChain());
		}

		private IEnumerator RunTransformChain()
		{
			for (int i = 0; i < transformChainData.Count; i++)
			{
				TransformChainData data = transformChainData[i];
				DoTransform(data);
				yield return new WaitForSeconds(data.Duration);
			}
		}

		private void DoTransform(TransformChainData data)
		{
			if (data.Duration == 0)
			{
				transform.localScale = data.EndScale;
		
				if (data.IsLocal)
				{
					transform.localPosition = data.EndPosition;
				}
				else
				{
					transform.position = data.EndPosition;
				}
				return;
			}

			transform.DOScale(data.EndScale, data.Duration)
				.SetEase(data.EaseType)
				.SetRelative(data.IsRelative);

			if (data.IsLocal)
				transform.DOLocalMove(data.EndPosition, data.Duration)
					.SetEase(data.EaseType)
					.SetRelative(data.IsRelative);
			else
				transform.DOMove(data.EndPosition, data.Duration)
					.SetEase(data.EaseType)
					.SetRelative(data.IsRelative);
		}
	}
}