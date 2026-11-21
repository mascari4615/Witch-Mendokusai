using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 부품 흔들림과 회전. 오브젝트 하나에 하나
	public class MapObjectModelMotion : MonoBehaviour
	{
		private struct Mover
		{
			public Transform Target;
			public Vector3 BasePosition;
			public Quaternion BaseRotation;
			public float Spin;
			public float BobHeight;
			public float BobPeriod;
			public float Phase;
		}

		private readonly List<Mover> movers = new();

		public void Add(Transform target, float spin, float bobHeight, float bobPeriod, float phase) =>
			movers.Add(new Mover { Target = target, BasePosition = target.localPosition, BaseRotation = target.localRotation, Spin = spin, BobHeight = bobHeight, BobPeriod = bobPeriod, Phase = phase });

		private void Update()
		{
			float time = Time.time;
			foreach (Mover mover in movers)
			{
				float bob = mover.BobHeight > 0f ? Mathf.Sin((time / mover.BobPeriod + mover.Phase) * Mathf.PI * 2f) * mover.BobHeight : 0f;
				mover.Target.localPosition = mover.BasePosition + Vector3.up * bob;
				mover.Target.localRotation = Quaternion.Euler(0f, mover.Spin * time, 0f) * mover.BaseRotation;
			}
		}
	}
}
