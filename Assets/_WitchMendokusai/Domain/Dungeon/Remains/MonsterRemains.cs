using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 몬스터가 쓰러진 자리에 남는 것. 몬스터 데이터의 RemainsProfile 을 읽어 조각, 얼룩, 웅덩이를 남김
	/// - 공용 몬스터 프리팹 (MonsterBase) 에 붙음. 잔상 연출 (MonsterLifeFx) 과 따로
	/// - 던전 밖 (RemainsWorld 없음), 흔적 끔, 프로필 없음이면 아무것도 안 함
	/// - 처치가 몰리면 조각은 예산 안에서만, 얼룩은 언제나
	/// - 마지막 타격이 치명타면 조각이 더 많고 멀리 (평소 처치와 다른 종류의 보상)
	/// </summary>
	public class MonsterRemains : MonoBehaviour, IDeathReaction
	{
		[SerializeField, Tooltip("몸 색을 뽑을 그림")] private SpriteRenderer body;
		[SerializeField, Tooltip("쓰러진 몬스터")] private MonsterObject monster;
		[SerializeField, Tooltip("죽인 타격을 읽을 체력")] private UnitHealth health;

		private bool lastHitCritical;
		private Vector3 lastHitAway;

		// 죽인 타격 (UnitHealth 가 사망 알림 전에 남김) 의 치명타 여부와 맞은 방향
		private void ReadKillingBlow()
		{
			DamageInfo killingBlow = health.LastDamage;
			lastHitCritical = killingBlow.type == DamageType.Critical;
			lastHitAway = Vector3.zero;
			if (killingBlow.damageSource is Component source && source != null)
			{
				Vector3 away = transform.position - source.transform.position;
				away.y = 0f;
				if (away.sqrMagnitude > .0001f)
					lastHitAway = away.normalized;
			}
		}

		public void OnDeath()
		{
			RemainsWorld remains = RemainsWorld.Current;
			RemainsProfile profile = monster.UnitData.Remains;
			if (remains == null || remains.Enabled == false || profile == null)
				return;
			ReadKillingBlow();
			Vector3 position = transform.position;
			position.y = remains.GroundHeight;
			remains.Grass.Flatten(position, remains.Settings.GrassKillFlattenRadius);
			if (profile.StainRadius > 0f)
				remains.Stain(position, profile.StainRadius, profile.StainColor);
			if (profile.Spill != null && Random.value < profile.SpillChance)
				remains.Goop.Spill(profile.Spill, position, profile.SpillRadius, 1f);
			// 치명타 처치는 예산과 확률을 건너뜀
			if (lastHitCritical || (Random.value < profile.PieceChance && remains.TryConsumeKillBudget()))
				SpawnPieces(remains, profile, position);
		}

		private void SpawnPieces(RemainsWorld remains, RemainsProfile profile, Vector3 position)
		{
			RemainsSettings settings = remains.Settings;
			Material material = profile.Material != null ? profile.Material : settings.PieceMaterial;
			Mesh mesh = RemainsMeshes.Get(profile.Shape);
			Color bodyColor = BodyColor(settings);
			Vector3 away = lastHitAway != Vector3.zero ? lastHitAway : AwayFromPlayer(position);
			float critScale = lastHitCritical ? settings.CritPieceScale : 1f;
			int count = Mathf.RoundToInt(Random.Range(profile.Count.x, profile.Count.y + 1) * critScale);
			for (int i = 0; i < count; i++)
			{
				Vector2 circle = Random.insideUnitCircle.normalized;
				Vector3 outward = new Vector3(circle.x, 0f, circle.y) + away * profile.AwayBias;
				outward.y = 0f;
				Color tint = profile.Palette.Length > 0 ? profile.Palette[Random.Range(0, profile.Palette.Length)] : bodyColor;
				float size = Random.Range(profile.Size.x, profile.Size.y);
				DebrisSpawn spawn = new()
				{
					Mesh = mesh,
					Material = material,
					Position = position + Vector3.up * size + new Vector3(circle.x, 0f, circle.y) * size,
					Rotation = Random.rotationUniform,
					Velocity = (outward.normalized * Random.Range(profile.Speed.x, profile.Speed.y) + Vector3.up * profile.Lift * Random.Range(1f - profile.LiftVariance, 1f)) * (lastHitCritical ? settings.CritSpeedScale : 1f),
					SpinAxis = Random.onUnitSphere,
					SpinDegrees = profile.Spin,
					Size = size,
					FlyClearance = RemainsMeshes.FlyClearance(profile.Shape),
					RestClearance = RemainsMeshes.RestClearance(profile.Shape),
					Gravity = profile.Gravity,
					Bounce = profile.Bounce,
					GroundHeight = remains.GroundHeight,
					Tint = tint,
					HasTint = true,
					StainRadius = profile.PieceStainScale,
					StainColor = profile.StainColor,
				};
				remains.Debris.Spawn(settings, spawn);
			}
		}

		private Color BodyColor(RemainsSettings settings)
		{
			if (body.sprite != null && SpriteColorSampler.TryGet(body.sprite, settings.ColorSampleCacheLimit, settings.ColorSampleRequestsPerFrame, out Color sampled))
				return sampled * monster.UnitData.Tint;
			return settings.FallbackPieceColor;
		}

		// 주인공 반대쪽으로 쏠리게. 맞은 방향은 사망 알림에 안 실려서 주인공 위치로 대신
		private static Vector3 AwayFromPlayer(Vector3 position)
		{
			PlayerObject player = PlayerProvider.Instance != null ? PlayerProvider.Instance.CurrentObject : null;
			if (player == null)
				return Vector3.zero;
			Vector3 away = position - player.transform.position;
			away.y = 0f;
			return away.sqrMagnitude > .0001f ? away.normalized : Vector3.zero;
		}
	}
}
