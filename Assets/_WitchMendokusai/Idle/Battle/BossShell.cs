using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai.Idle
{
	/// <summary>
	/// 보스 몸 둘레의 껍질 조각. 체력이 줄면 조각이 떨어지고 남은 조각이 밖으로 벌어짐
	///
	/// ★ 조각은 그 도형의 면 하나를 얇게 뜬 것. 몸과 같은 재질이라 한 덩어리로 읽힘
	/// </summary>
	internal sealed class BossShell
	{
		private readonly Transform pivot;
		private readonly Material skin;
		private readonly BattleEntityPresenter.Settings settings;
		private readonly List<Transform> shards = new List<Transform>();

		public BossShell(Transform modelPivot, Material skin, BattleEntityPresenter.Settings settings)
		{
			this.skin = skin;
			this.settings = settings;

			GameObject shell = new GameObject("Shell");
			shell.transform.SetParent(modelPivot, false);
			pivot = shell.transform;
		}

		/// <summary>도형이 바뀌면 조각을 다시 뜬다. 조각 수는 면 수를 넘지 않음</summary>
		public void Rebuild(Geometry.Shape shape)
		{
			foreach (Transform shard in shards)
			{
				if (shard != null)
				{
					BattleVisualFactory.Kill(shard.gameObject);
				}
			}

			shards.Clear();
			int faces = Geometry.FaceCountOf(shape);
			int count = Mathf.Clamp(settings.BossShardCount, 1, faces);

			for (int at = 0; at < count; at++)
			{
				GameObject shard = new GameObject("Shard" + at);
				shard.transform.SetParent(pivot, false);

				MeshFilter mesh = shard.AddComponent<MeshFilter>();
				mesh.sharedMesh = Geometry.FaceShard(
					shape,
					settings.FoeRadius * settings.BossShardRadiusScale,
					at * Mathf.Max(1, faces / count),
					settings.BossShardThickness);
				MeshRenderer renderer = shard.AddComponent<MeshRenderer>();
				renderer.sharedMaterial = skin;

				float angle = at * Mathf.PI * 2f / count;
				shard.transform.localPosition = new Vector3(Mathf.Cos(angle), LiftOf(at), Mathf.Sin(angle));
				shard.transform.localRotation = Quaternion.Euler(settings.BossShardEulerStep * at);
				shards.Add(shard.transform);
			}
		}

		/// <summary>체력 몫만큼 조각을 남기고 벌린다. 다친 만큼 반지름이 커짐</summary>
		public void Spread(float healthRatio, float delta)
		{
			if (shards.Count == 0)
			{
				return;
			}

			float health = Mathf.Clamp01(healthRatio);
			float hurt = 1f - health;
			float radius = settings.BossShellRadius + settings.BossShellSpread * hurt;
			int alive = Mathf.Clamp(Mathf.CeilToInt(shards.Count * health), 1, shards.Count);

			for (int at = 0; at < shards.Count; at++)
			{
				Transform shard = shards[at];
				bool onShell = at < alive;

				if (shard.gameObject.activeSelf != onShell)
				{
					shard.gameObject.SetActive(onShell);
				}

				if (onShell == false)
				{
					continue;
				}

				float angle = at * Mathf.PI * 2f / alive;
				Vector3 want = new Vector3(Mathf.Cos(angle), LiftOf(at), Mathf.Sin(angle)) * radius;
				shard.localPosition = Vector3.Lerp(shard.localPosition, want,
					BattleMotion.CatchUp(settings.PositionCatchUp * settings.BossShellCatchUpShare, delta));
				shard.Rotate(
					Vector3.up,
					settings.BossShellSpinDegrees * settings.BossShardSpinShare * delta,
					Space.Self);
			}

			pivot.Rotate(Vector3.up, settings.BossShellSpinDegrees * delta, Space.Self);
		}

		/// <summary>조각을 세 높이로 흩어 놓음. 한 줄로 서면 고리로만 읽힘</summary>
		private float LiftOf(int at)
		{
			return ((at % 3) - 1) * settings.BossShardLift;
		}
	}
}
