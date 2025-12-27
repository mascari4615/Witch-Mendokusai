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
		public TransformFlag IsRelative;
		public float Duration;
		public Ease EaseType;
		public MoveEaseType MoveEaseType;
		public MovePosType MovePosType;
		public float RandomRange;

		public TransformChainData(TransformFlag transformFlags, Vector3 endPosition, Vector3 endScale, float duration, Ease easeType, TransformFlag isRelative, bool isLocal, MoveEaseType moveEaseType, MovePosType movePosType, float randomRange)
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
		[SerializeField] private List<TransformChainData> transformChainData;
		[SerializeField] private bool autoRun = true; // Target을 외부에서 설정해줘야하는 경우가 있을 듯

		private void OnEnable()
		{
			DOTween.Kill(transform);
			StopAllCoroutines();

			if (autoRun)
			{
				StartCoroutine(RunTransformChain());
			}
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
				DoPosition(data);
			if (data.TransformFlags.HasFlag(TransformFlag.DoRotation))
				DoRotation(data);
			if (data.TransformFlags.HasFlag(TransformFlag.DoScale))
				DoScale(data);

			yield return new WaitForSeconds(data.Duration);
		}

		private void DoPosition(TransformChainData data)
		{
			if (data.Duration == 0)
			{
				if (data.IsLocal)
				{
					transform.localPosition = CalcEndPosition(data);
				}
				else
				{
					transform.position = CalcEndPosition(data);
				}
			}
			else
			{
				StartCoroutine(Move(data));
			}
		}

		private void DoRotation(TransformChainData data)
		{
			// 회전 로직을 여기에 추가할 수 있습니다.
		}

		private void DoScale(TransformChainData data)
		{
			if (data.Duration == 0)
			{
				transform.localScale = data.EndScale;
			}
			else
			{
				transform.DOScale(data.EndScale, data.Duration)
					.SetEase(data.EaseType)
					.SetRelative(data.IsRelative.HasFlag(TransformFlag.DoScale));
			}
		}

		private IEnumerator Move(TransformChainData data)
		{
			Vector3 startPosition = data.IsLocal ? transform.localPosition : transform.position;
			Vector3 endPosition = CalcEndPosition(data);

			if (data.IsRelative.HasFlag(TransformFlag.DoPosition))
			{
				endPosition += startPosition;
			}

			for (float elapsedTime = 0; elapsedTime < data.Duration; elapsedTime += Time.deltaTime)
			{
				float t = elapsedTime / data.Duration;

				Vector3 newPos;
				switch (data.MoveEaseType)
				{
					case MoveEaseType.Ease:
						float easedT = DOVirtual.EasedValue(0, 1, t, data.EaseType);
						newPos = Vector3.Lerp(startPosition, endPosition, easedT);
						break;
					case MoveEaseType.Parabola:
						float height = Mathf.Sin(t * Mathf.PI) * 2;
						newPos = Vector3.Lerp(startPosition, endPosition, t) + Vector3.up * height;
						break;
					default:
						newPos = Vector3.Lerp(startPosition, endPosition, t);
						break;
				}

				SetPosition(newPos);
				yield return null;
			}

			void SetPosition(Vector3 position)
			{
				if (data.IsLocal)
					transform.localPosition = position;
				else
					transform.position = position;
			}
		}

		public Vector3 CalcEndPosition(TransformChainData data)
		{
			Vector3 endPosition;
			switch (data.MovePosType)
			{
				case MovePosType.Manual:
					endPosition = data.EndPosition;
					break;
				case MovePosType.Random:
					endPosition = new(
						Random.Range(-data.RandomRange, data.RandomRange),
						0,
						Random.Range(-data.RandomRange, data.RandomRange)
					);
					break;
				default:
					endPosition = data.EndPosition;
					break;
			}
			return endPosition;
		}
	}
}