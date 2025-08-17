using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using Random = UnityEngine.Random;

namespace WitchMendokusai
{
	public enum MovePosType
	{
		Manual,
		Random
	}

	public enum MoveEaseType
	{
		Ease,
		Parabola
	}

	[Flags]
	public enum TransformFlag
	{
		None = 0,
		DoPosition = 1 << 0,
		DoRotation = 1 << 1,
		DoScale = 1 << 2
	}

	[Serializable]
	public class TransformChainData
	{
		public TransformFlag TransformFlags;
		public Vector3 EndPosition;
		public Vector3 EndScale;
		public bool IsLocal;
		public bool IsRelative;
		public float Duration;
		public Ease EaseType;
		public MoveEaseType MoveEaseType;
		public MovePosType MovePosType;
		public float RandomRange;

		public TransformChainData(TransformFlag transformFlags, Vector3 endPosition, Vector3 endScale, float duration, Ease easeType, bool isRelative, bool isLocal, MoveEaseType moveEaseType, MovePosType movePosType, float randomRange)
		{
			TransformFlags = transformFlags;
			EndPosition = endPosition;
			EndScale = endScale;
			Duration = duration;
			EaseType = easeType;
			MoveEaseType = moveEaseType;
			MovePosType = movePosType;
			IsRelative = isRelative;
			IsLocal = isLocal;
			RandomRange = randomRange;
		}
	}

	public class TransformChain : MonoBehaviour
	{
		// DOTween을 이용하여 이동 체인을 구현합니다.
		// 이동 체인은 인스펙터에서 설정할 수 있어야 합니다.

		[SerializeField] private List<TransformChainData> transformChainData;

		private void OnEnable()
		{
			DOTween.Kill(transform);
			StopAllCoroutines();

			StartCoroutine(RunTransformChain());
		}

		private void OnDisable()
		{
			DOTween.Kill(transform);
			StopAllCoroutines();
		}

		private IEnumerator RunTransformChain()
		{
			for (int i = 0; i < transformChainData.Count; i++)
			{
				yield return StartCoroutine(DoTransform(transformChainData[i]));
			}
		}

		private IEnumerator DoTransform(TransformChainData data)
		{
			if (data.TransformFlags.HasFlag(TransformFlag.DoPosition))
				StartCoroutine(DoPosition(data));
			if (data.TransformFlags.HasFlag(TransformFlag.DoRotation))
				StartCoroutine(DoRotation(data));
			if (data.TransformFlags.HasFlag(TransformFlag.DoScale))
				StartCoroutine(DoScale(data));

			yield return new WaitForSeconds(data.Duration);
		}

		private IEnumerator DoPosition(TransformChainData data)
		{
			if (data.Duration == 0)
			{
				if (data.IsLocal)
				{
					transform.localPosition = data.EndPosition;
				}
				else
				{
					transform.position = data.EndPosition;
				}
			}
			else
			{
				switch (data.MoveEaseType)
				{
					case MoveEaseType.Ease:
						yield return StartCoroutine(Move(data));
						break;
					case MoveEaseType.Parabola:
						yield return StartCoroutine(ParabolaMove(data));
						break;
				}
			}

			yield break;
		}

		private IEnumerator DoRotation(TransformChainData data)
		{
			// 회전 로직을 여기에 추가할 수 있습니다.
			yield break;
		}

		private IEnumerator DoScale(TransformChainData data)
		{
			if (data.Duration == 0)
			{
				transform.localScale = data.EndScale;
			}
			else
			{
				transform.DOScale(data.EndScale, data.Duration)
					.SetEase(data.EaseType)
					.SetRelative(data.IsRelative);
			}
			yield break;
		}

		private IEnumerator Move(TransformChainData data)
		{
			if (data.IsLocal)
				transform.DOLocalMove(data.EndPosition, data.Duration)
					.SetEase(data.EaseType)
					.SetRelative(data.IsRelative);
			else
				transform.DOMove(data.EndPosition, data.Duration)
					.SetEase(data.EaseType)
					.SetRelative(data.IsRelative);

			yield return new WaitForSeconds(data.Duration);
		}

		private IEnumerator ParabolaMove(TransformChainData data)
		{
			Vector3 startPosition;
			if (data.IsLocal)
				startPosition = transform.localPosition;
			else
				startPosition = transform.position;

			Vector3 endPosition;
			switch (data.MovePosType)
			{
				case MovePosType.Manual:
					endPosition = data.EndPosition;
					break;
				case MovePosType.Random:
					Vector3 randomPosition = new(
						Random.Range(-data.RandomRange, data.RandomRange),
						0,
						Random.Range(-data.RandomRange, data.RandomRange)
					);
					endPosition = startPosition + randomPosition;
					break;
				default:
					endPosition = data.EndPosition;
					break;
			}

			float elapsedTime = 0;

			while (elapsedTime < data.Duration)
			{
				float t = elapsedTime / data.Duration;
				float height = Mathf.Sin(t * Mathf.PI) * 2; // Parabola height
				SetPosition(Vector3.Lerp(startPosition, endPosition, t) + Vector3.up * height);

				elapsedTime += Time.deltaTime;
				yield return null;
			}

			SetPosition(endPosition);

			void SetPosition(Vector3 position)
			{
				if (data.IsLocal)
					transform.localPosition = position;
				else
					transform.position = position;
			}
		}
	}
}