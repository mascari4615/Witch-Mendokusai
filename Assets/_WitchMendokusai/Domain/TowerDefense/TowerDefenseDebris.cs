using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 잔해(TASK-WM-194) — 마수가 죽은 자리에 남아 그 위를 지나는 마수를 느리게 만든다.
	///
	/// ★ 「전투가 판을 바꾼다」: 이벤트 웨이브(D)가 *바깥에서* 성격을 바꾼다면, 잔해는 *내 전투의 결과가*
	///   판을 바꾼다. 많이 죽인 자리는 저절로 늪이 되어 다음 무리가 느려진다 — 길목이 스스로 굳는다.
	/// ★ 통행을 막지 않는다: 막으면 길이 끊길 위험이 생기고(벽과 달리 내가 통제 못 하는 위치),
	///   「죽인 곳이 유리해진다」는 의도엔 둔화만으로 충분하다.
	/// </summary>
	public sealed class TowerDefenseDebris : MonoBehaviour
	{
		private IReadOnlyList<ICombatant> enemyPool;
		private float slowFactor;
		private float radiusSqr;
		private float lifeRemaining;

		public static void Spawn(
			Transform parent,
			Vector3 worldPosition,
			IReadOnlyList<ICombatant> enemies,
			float seconds,
			float slow,
			float radius,
			Color tint)
		{
			if (seconds <= 0f)
				return;

			GameObject debrisObject = TowerDefenseVisuals.Primitive(PrimitiveType.Quad);
			debrisObject.name = "Debris";
			Destroy(debrisObject.GetComponent<Collider>()); // 표시·판정용 — 물리를 끼우면 마수가 걸린다.
			debrisObject.transform.SetParent(parent, false);
			debrisObject.transform.position = worldPosition + new Vector3(0f, 0.04f, 0f);
			debrisObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
			debrisObject.transform.localScale = Vector3.one * radius * 1.6f;

			Renderer debrisRenderer = debrisObject.GetComponent<Renderer>();
			if (debrisRenderer != null)
			{
				Material material = new Material(debrisRenderer.sharedMaterial);
				material.SetFloat("_Surface", 1f);
				material.SetOverrideTag("RenderType", "Transparent");
				material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
				material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
				material.SetInt("_ZWrite", 0);
				material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
				material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
				Color color = tint;
				color.a = 0.55f;
				material.color = color;
				if (material.HasProperty("_BaseColor"))
					material.SetColor("_BaseColor", color);
				debrisRenderer.sharedMaterial = material;
				debrisRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
			}

			TowerDefenseDebris debris = debrisObject.AddComponent<TowerDefenseDebris>();
			debris.enemyPool = enemies;
			debris.slowFactor = slow;
			debris.radiusSqr = radius * radius;
			debris.lifeRemaining = seconds;
		}

		private void Update()
		{
			lifeRemaining -= Time.deltaTime;
			if (lifeRemaining <= 0f)
			{
				Destroy(gameObject);
				return;
			}

			if (enemyPool == null)
				return;

			Vector3 position = transform.position;
			foreach (ICombatant candidate in enemyPool)
			{
				if (candidate == null || candidate.IsAlive == false)
					continue;
				if ((candidate.Position - position.ToSim()).sqrMagnitude > radiusSqr)
					continue;

				MatchCombatant combatant = candidate as MatchCombatant;
				if (combatant == null || combatant.UnitObject == null)
					continue;

				// 짧게 계속 새로 걸어준다 — 밟고 있는 동안만 느리고 벗어나면 곧 회복된다.
				TowerDefenseSlow.Apply(combatant.UnitObject, 1f - slowFactor, 0.4f);
			}
		}
	}
}
