using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 사라졌다 나타나기. 예고 -> 몸이 사라짐 (떠오르거나 연기) -> 자리를 고정하고 그 자리들 예고 -> 나타나며 한꺼번에 침 -> 숨 고르기
	// 자리 하나에 쫓는 초가 있으면 그림자로 쫓아와 떨어짐. 자리가 여럿이고 진짜가 있으면 진짜 자리만 안 때리고 거기서 나타남
	// 사라진 동안은 안 맞고 부딪히지 않음 (MonsterHideaway)
	[CreateAssetMenu(fileName = "MA_Hide", menuName = "WM/Variable/MonsterAttack/" + nameof(HideMonsterAttack))]
	public class HideMonsterAttack : MonsterAttack
	{
		public override string KindName => "사라졌다 나타나기";

		[field: Header("사라짐")]
		[field: SerializeField, Tooltip("떠오르는 높이 (m). 0 이면 그 자리에서 사라짐"), Min(0f)] public float RiseHeight { get; private set; }
		[field: SerializeField, Tooltip("떠오르는 초"), Min(0.05f)] public float RiseSeconds { get; private set; } = 0.4f;
		[field: SerializeField, Tooltip("떨어지는 초. 때리는 순간 땅에 닿음"), Min(0.05f)] public float FallSeconds { get; private set; } = 0.28f;
		[field: SerializeField, Tooltip("땅에 닿는 것을 때리는 순간보다 이만큼 앞당김 (초). 피해가 그림보다 먼저 오지 않게"), Min(0f)] public float LandLeadSeconds { get; private set; } = 0.03f;
		[field: SerializeField, Tooltip("떠오를 때 그림")] public Sprite RiseSprite { get; private set; }
		[field: SerializeField, Tooltip("떨어질 때 그림")] public Sprite FallSprite { get; private set; }
		[field: SerializeField, Tooltip("사라지는 순간 이펙트. 비우면 없음")] public CombatFx LeaveFx { get; private set; }
		[field: SerializeField, Min(0.1f)] public float LeaveFxRadius { get; private set; } = 1.4f;
		[field: SerializeField, Tooltip("사라진 동안 몸 자리 바닥의 그림자. 비우면 없음")] public CombatFx ShadowFx { get; private set; }

		[field: Header("쫓음과 자리")]
		[field: SerializeField, Tooltip("사라진 뒤 자리를 고정하기까지 (초). 떠오르는 초 뒤부터 쫓음"), Min(0f)] public float ChaseSeconds { get; private set; }
		[field: SerializeField, Tooltip("쫓는 빠르기 (m/s). 0 이면 안 쫓음"), Min(0f)] public float ChaseSpeed { get; private set; } = 3.4f;
		[field: SerializeField, Tooltip("자리를 고정하고 때리기까지 (초). 피할 틈"), Min(0.2f)] public float LockSeconds { get; private set; } = 1f;
		[field: SerializeField, Tooltip("자리 수. 1 이면 쫓아간 자리, 여럿이면 주인공 둘레 고리"), Min(1)] public int Count { get; private set; } = 1;
		[field: SerializeField, Tooltip("고리 반지름 (m)"), Min(0f)] public float Distance { get; private set; } = 2.9f;
		[field: SerializeField, Tooltip("자리 하나는 진짜. 안 때리고 거기서 나타남")] public bool HasSafeSpot { get; private set; }
		[field: SerializeField, Tooltip("나타날 때 자리보다 이만큼 바깥 (m). 안전한 자리에 선 주인공과 안 겹치게"), Min(0f)] public float EmergeOffset { get; private set; }

		[field: Header("타격")]
		[field: SerializeField, Tooltip("자리마다 반지름 (m)"), Min(0.3f)] public float Radius { get; private set; } = 2.4f;
		[field: SerializeField, Min(1)] public int Damage { get; private set; } = 6;
		[field: SerializeField, Tooltip("피해가 실제로 들어간 순간 주인공에게 거는 것")] public MonsterHitEffect HitEffect { get; private set; } = new();
		[field: SerializeField, Tooltip("진짜 자리의 예고 물체")] public CombatFx SafeFx { get; private set; }
		[field: SerializeField, Tooltip("진짜 자리와 옆 원 사이 최소 틈 (m). 주인공 몸 반지름과 여유, 검사용"), Min(0f)] public float SafeMargin { get; private set; } = 0.5f;

		protected override bool PlaysWindupFx => true;
		protected override bool PlaysStrikeFx => true;

		public float HiddenSeconds => ChaseSeconds + LockSeconds;

		public override BTRunner CreateBehaviour(UnitObject unitObject, PlayerProvider playerProvider) =>
			new BT_Hide(unitObject, playerProvider, this);

		public override void Interrupt(BTRunner behaviour) => ((BT_Hide)behaviour).Interrupt();

		// 자리 계산만. 하나면 중심, 여럿이면 중심 둘레 고리
		public static void Arrange(Vector3 center, int count, float distance, float startAngle, List<Vector3> results)
		{
			results.Clear();
			if (count == 1)
			{
				results.Add(center);
				return;
			}
			for (int i = 0; i < count; i++)
				results.Add(center + Quaternion.Euler(0f, startAngle + 360f / count * i, 0f) * Vector3.forward * distance);
		}

		// 때리는 자리 가운데 하나라도 닿는가. safeIndex 는 안 때리는 자리, 없으면 음수
		public static bool Hits(IReadOnlyList<Vector3> spots, int safeIndex, float radius, Vector3 point, float bodyRadius)
		{
			for (int i = 0; i < spots.Count; i++)
			{
				if (i != safeIndex && SlimeKingPatternMath.IsWithin(spots[i], point, radius + bodyRadius))
					return true;
			}
			return false;
		}

		public int PickSafeIndex() => HasSafeSpot ? Random.Range(0, Count) : -1;

		public override void Validate(Monster owner, List<string> errors)
		{
			base.Validate(owner, errors);
			if (WindupSeconds <= 0f)
				errors.Add($"{name}: 사라지기 전 예고가 있어야 함 ({nameof(WindupSeconds)} 0)");
			if (Pose.IsEmpty)
				errors.Add($"{name}: 사라지고 나타나는 자세가 비어 있음");
			if (LandLeadSeconds + FallSeconds > LockSeconds)
				errors.Add($"{name}: 떨어지는 초 {FallSeconds} 가 피할 틈 {LockSeconds} 보다 김");
			if (RiseHeight > 0f && (RiseSprite == null || FallSprite == null))
				errors.Add($"{name}: 떠오르는데 뜨고 떨어지는 그림이 비어 있음");
			if (HasSafeSpot)
				ValidateSafeSpot(errors);
			HitEffect.Validate(name, errors);
		}

		private void ValidateSafeSpot(List<string> errors)
		{
			if (Count < 2 || SafeFx == null || WindupFx == null)
				errors.Add($"{name}: 진짜 자리는 자리 둘 이상과 진짜, 가짜 물체가 있어야 함");
			if (Distance > Radius)
				errors.Add($"{name}: 가운데 서 있으면 안 맞음 (고리 {Distance}, 반지름 {Radius})");
			if (2f * Distance * Mathf.Sin(Mathf.PI / Count) < Radius + SafeMargin)
				errors.Add($"{name}: 진짜 자리까지 옆 자리가 때림 (고리 {Distance}, 반지름 {Radius}, {Count}개)");
			if (ChaseSeconds > 0f)
				errors.Add($"{name}: 진짜 자리가 있으면 쫓지 않음 ({nameof(ChaseSeconds)})");
		}
	}
}
