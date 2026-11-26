using System;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	public enum VolleyShape
	{
		// 온 둘레를 고르게. 물결마다 TurnDegreesPerWave 만큼 엇갈림
		Ring = 0,
		// 주인공 쪽 가운데로 ArcDegrees 만큼 벌어짐
		Fan = 1,
		// 고리를 물결마다 같은 쪽으로 돌림. 부호가 회전 방향, 크기가 빠르기
		Spiral = 2,
		// 갈래마다 빠르기가 다른 탄 PerSpoke 개가 줄을 이룸
		Spokes = 3,
		// 큰 탄 (WindupFx) 이 예고 동안 주인공 자리로 날아가 앉고, 착탄에 그 자리에서 작은 탄 고리
		Split = 4,
	}

	// 나는 탄 하나의 그림. 다른 종의 그린 물체를 빌려 씀 (단색 도형 금지)
	[Serializable]
	public class VolleyBulletLook
	{
		[field: SerializeField, Tooltip("날 때 번갈아 쓰는 그림")] public Sprite[] Fly { get; private set; } = Array.Empty<Sprite>();
		[field: SerializeField, Min(1f)] public float FramesPerSecond { get; private set; } = 10f;
		[field: SerializeField, Tooltip("박혀 남은 그림. 비우면 날던 그림")] public Sprite Planted { get; private set; }
		[field: SerializeField, Tooltip("카메라를 보는 재질")] public Material Material { get; private set; }
		[field: SerializeField, Min(0.05f)] public float Scale { get; private set; } = 0.6f;
		[field: SerializeField, Tooltip("바닥에서 뜬 높이 (m)"), Min(0f)] public float Height { get; private set; } = 0.6f;
		[field: SerializeField, Tooltip("박혀 남은 그림이 바닥에서 뜬 높이 (m)"), Min(0f)] public float PlantedHeight { get; private set; } = 0.15f;
		[field: SerializeField, Tooltip("박힌 탄이 아플 때 깜빡이는 색")] public Color HotTint { get; private set; } = new(1f, 0.6f, 0.8f, 1f);
		[field: SerializeField, Tooltip("박힌 탄이 식어 안 아픈 색")] public Color CoolTint { get; private set; } = new(0.45f, 0.42f, 0.42f, 1f);
		[field: SerializeField, Min(0f)] public float HotPulseSpeed { get; private set; } = 7f;
		[field: SerializeField, Tooltip("사라질 때 줄어드는 초"), Min(0.01f)] public float FadeSeconds { get; private set; } = 0.15f;
	}

	// 탄막. 몸동작과 짧은 바닥 표시로 예고한 뒤 탄 묶음을 쏨 (고리, 부채, 나선, 방사줄, 분열). 탄은 VolleyField 가 날리고 판정함
	// 탄의 생애: 날기 -> 사거리 끝에서 앉기 -> Planted 가 있으면 박혀 남음 (닿으면 아픔, LingerField), 없으면 사라짐
	[CreateAssetMenu(fileName = "MA_Volley", menuName = "WM/Variable/MonsterAttack/" + nameof(VolleyMonsterAttack))]
	public class VolleyMonsterAttack : WindupMonsterAttack
	{
		public override string KindName => "탄막";

		[field: Header("탄막 모양")]
		[field: SerializeField] public VolleyShape Shape { get; private set; } = VolleyShape.Ring;
		[field: SerializeField, Tooltip("한 물결의 탄 수. 방사줄은 갈래 수"), Min(1)] public int Count { get; private set; } = 12;
		[field: SerializeField, Tooltip("부채만. 벌어진 각도 (도)"), Range(0f, 359f)] public float ArcDegrees { get; private set; } = 60f;
		[field: SerializeField, Tooltip("방사줄만. 갈래 하나의 탄 수"), Min(1)] public int PerSpoke { get; private set; } = 1;
		[field: SerializeField, Tooltip("물결 수"), Min(1)] public int Waves { get; private set; } = 1;
		[field: SerializeField, Tooltip("물결 사이 초"), Min(0f)] public float WaveInterval { get; private set; } = 0.4f;
		[field: SerializeField, Tooltip("물결마다 돌아가는 각도 (도). 나선은 부호가 회전 방향")] public float TurnDegreesPerWave { get; private set; }
		[field: SerializeField, Tooltip("이 물결 뒤로 반대로 돎 (방향 반전). 0 이면 안 꺾음"), Min(0)] public int ReverseAtWave { get; private set; }

		[field: Header("탄")]
		[field: SerializeField, Tooltip("첫 탄의 빠르기 (m/s)"), Min(0.1f)] public float Speed { get; private set; } = 4f;
		[field: SerializeField, Tooltip("같은 물결에서 뒤 탄일수록 줄어드는 빠르기 (m/s). 방사줄은 갈래 안 차례"), Min(0f)] public float SpeedStep { get; private set; }
		[field: SerializeField, Tooltip("나는 거리 (m). 끝에서 앉음. 분열은 작은 탄의 거리이자 예고 반지름"), Min(0.5f)] public float Range { get; private set; } = 8f;
		[field: SerializeField, Tooltip("탄 판정 반지름 (m)"), Min(0.05f)] public float BulletRadius { get; private set; } = 0.25f;
		[field: SerializeField, Tooltip("쏘는 자리에서 이만큼 앞에서 나옴 (m)"), Min(0f)] public float SpawnOffset { get; private set; } = 0.5f;
		[field: SerializeField, Tooltip("탄에 맞았을 때 기본 피해"), Min(1)] public int Damage { get; private set; } = 3;
		[field: SerializeField] public MonsterHitEffect HitEffect { get; private set; } = new();
		[field: SerializeField] public VolleyBulletLook Look { get; private set; } = new();
		[field: SerializeField, Tooltip("탄이 맞거나 박힐 때 이펙트. 비우면 없음")] public CombatFx PopFx { get; private set; }
		[field: SerializeField, Min(0.1f)] public float PopFxRadius { get; private set; } = 0.5f;
		[field: SerializeField, Tooltip("사거리 끝에서 박혀 남는 탄. 초가 0 이면 안 남음")] public LingerSettings Planted { get; private set; } = new();
		[field: SerializeField, Tooltip("씬 전체에 동시에 있을 수 있는 탄 상한. 넘으면 새 탄을 안 냄"), Min(1)] public int FieldCap { get; private set; } = 160;

		[field: Header("예고 표시")]
		[field: SerializeField, Tooltip("분열이 아닐 때 시전자 둘레 바닥 표시 반지름 (m)"), Min(0.1f)] public float TelegraphRadius { get; private set; } = 2f;

		public int ShotsPerWave => VolleyPatternMath.ShotCount(Shape, Count, PerSpoke);

		// 물결을 다 내보내는 데 걸리는 초 (첫 물결은 실행 순간)
		public float VolleySeconds => (Waves - 1) * WaveInterval;

		protected override bool TargetsArePositions => Shape == VolleyShape.Split;

		public override CombatFxContext WindupContext(UnitObject unitObject, Vector3 target, int index)
		{
			if (Shape == VolleyShape.Split)
				return new CombatFxContext { Position = target, Radius = Range, Seconds = WindupSeconds };
			Vector3 caster = unitObject.transform.position;
			return new CombatFxContext
			{
				Position = caster,
				Follow = unitObject.transform,
				Direction = AimOf(caster, target),
				Radius = TelegraphRadius,
				ArcDegrees = Shape == VolleyShape.Fan ? ArcDegrees : 360f,
				Seconds = WindupSeconds,
			};
		}

		public override bool Execute(UnitObject unitObject, PlayerObject player, Vector3 target, int index)
		{
			VolleyField.Instance.Fire(this, unitObject, target);
			// 탄은 날아가서 때림. 이 순간 맞은 것은 없음
			return false;
		}

		// 수평으로 주인공 쪽. 겹쳐 있으면 앞
		public static Vector3 AimOf(Vector3 from, Vector3 to)
		{
			Vector3 offset = to - from;
			offset.y = 0f;
			return offset.sqrMagnitude < 0.0001f ? Vector3.forward : offset.normalized;
		}

		public override void Validate(Monster owner, List<string> errors)
		{
			base.Validate(owner, errors);
			Planted.Validate(name, errors);
			HitEffect.Validate(name, errors);
			if (Linger.Seconds > 0f)
				errors.Add($"{name}: 탄막은 목표 자리에 남기지 않음. 박히는 탄은 {nameof(Planted)}");
			if (WindupSeconds <= 0f)
				errors.Add($"{name}: 탄막은 예고가 있어야 함 ({nameof(WindupSeconds)} 0)");
			if (Look.Fly == null || Look.Fly.Length == 0 || Look.Material == null)
				errors.Add($"{name}: 탄 그림 ({nameof(Look)}) 이 비어 있음");
			if (Waves > 1 && WaveInterval <= 0f)
				errors.Add($"{name}: 물결 둘 이상인데 물결 사이 초가 0 이라 한 프레임에 겹쳐 나감");
			if (SpawnOffset >= Range)
				errors.Add($"{name}: 나오는 자리 {SpawnOffset} m 가 나는 거리 {Range} m 이상");
			if (Planted.BurstRadius > 0f)
				errors.Add($"{name}: 박힌 탄은 터지지 않음 (탄이 걷힐 때 금 간 예고를 치우지 않음)");
			if (Waves > 1 && RecoverySeconds < VolleySeconds)
				errors.Add($"{name}: 숨 고르기 {RecoverySeconds} 초가 물결 길이 {VolleySeconds} 초보다 짧아 쏘는 중에 걸어 다님");
			int order = (Shape == VolleyShape.Spokes ? PerSpoke : Count) - 1;
			if (VolleyPatternMath.SpeedOf(Speed, SpeedStep, order) <= 0f)
				errors.Add($"{name}: 마지막 탄의 빠르기가 0 이하 ({nameof(SpeedStep)} 이 큼)");
			if (ReverseAtWave > 0 && (ReverseAtWave >= Waves - 1 || TurnDegreesPerWave == 0f))
				errors.Add($"{name}: 방향 반전 물결 {ReverseAtWave} 뒤에 물결이 둘 이상 남고 도는 각도가 있어야 함");
			if (FieldCap < ShotsPerWave)
				errors.Add($"{name}: 탄 상한 {FieldCap} 이 한 물결 {ShotsPerWave} 발보다 작음");
			switch (Shape)
			{
				case VolleyShape.Fan when ArcDegrees <= 0f || Count < 2:
					errors.Add($"{name}: 부채는 각도와 탄 둘 이상이 있어야 함");
					break;
				case VolleyShape.Spiral when TurnDegreesPerWave == 0f || Waves < 2:
					errors.Add($"{name}: 나선은 물결 둘 이상과 도는 각도가 있어야 함");
					break;
				case VolleyShape.Spokes when PerSpoke < 2 || SpeedStep <= 0f:
					errors.Add($"{name}: 방사줄은 갈래마다 빠르기가 다른 탄 둘 이상");
					break;
				case VolleyShape.Split when WindupFx == null:
					errors.Add($"{name}: 분열은 날아가는 큰 탄 ({nameof(WindupFx)}) 이 있어야 함");
					break;
			}
		}
	}
}
