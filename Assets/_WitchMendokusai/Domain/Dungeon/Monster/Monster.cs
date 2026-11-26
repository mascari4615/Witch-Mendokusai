using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace WitchMendokusai
{
	public enum MonsterType
	{
		Normal,
		Boss,
	}

	public enum MonsterTag
	{
		Normal,
		Boss,
	}

	public enum MonsterEliteType
	{
		// 불타는, 과부하, 
	}

	[CreateAssetMenu(fileName = nameof(Monster), menuName = "WM/Variable/" + nameof(Unit) + "/" + nameof(Monster))]
	public class Monster : Unit
	{
		[field: Header("_" + nameof(Monster))]
		[PropertyOrder(20)][field: SerializeField] public MonsterType Type { get; private set; }
		[PropertyOrder(21)][field: SerializeField] public List<DataSOWithPercentage> Loots { get; private set; }
		[PropertyOrder(22)][field: SerializeField, Tooltip("정면 외 위협 (원거리, 돌진, 장판, 순서, 단계 등). 비우면 추적만. FSMSlime 이 돌림")] public MonsterAttack Attack { get; private set; }
		[PropertyOrder(24)][field: SerializeField, Min(-1f), Tooltip("피격 무적 초. 맞은 뒤 이 시간 동안 다시 안 맞음. 음수면 종류 기본값 (UnitStatusConfig 의 보스, 일반)")] public float HitInvincibleSeconds { get; private set; } = HitInvincibilityRules.NOT_SET;
		[PropertyOrder(23)][field: SerializeField, Tooltip("그림 색 곱. 같은 프리팹을 쓰는 변종 구분. 정예가 되면 정예 색을 위에 곱함")] public Color Tint { get; private set; } = Color.white;
		[PropertyOrder(28)][field: SerializeField, Tooltip("정예 색을 곱하지 않음. 처음부터 정예로 그린 몬스터")] public bool KeepsTintAsElite { get; private set; }
		[PropertyOrder(25)][field: SerializeField, Tooltip("쓰러진 뒤 바닥에 남는 것 (조각, 얼룩, 웅덩이). 비우면 안 남김")] public RemainsProfile Remains { get; private set; }
		[PropertyOrder(26)][field: SerializeField, Tooltip("몸에 늘 붙는 효과 (빛 번짐, 바닥, 조각). 비우면 없음")] public MonsterBodyFxStyle BodyFx { get; private set; }
		[PropertyOrder(27)][field: SerializeField, Tooltip("특정 인형에게 더 사나움")] public MonsterGrudge Grudge { get; private set; } = new();
		[PropertyOrder(30)][field: SerializeField, Tooltip("얹는 특징 (자국 남김, 죽으면 터짐, 분열, 치유 파동, 분노, 끌어당김). 여럿 가능, 비우면 없음")] public List<MonsterAffix> Affixes { get; private set; } = new();
		[PropertyOrder(29)][field: SerializeField, Tooltip("걷는 성격 (앞질러 막기, 수줍음, 깡충 등). 비우면 곧장 쫓음. 공격이 없을 때와 예고형 공격 (WindupMonsterAttack) 을 기다리는 동안의 걸음. 돌진, 무리 같은 다른 공격의 추적은 아직 안 따름")] public MonsterTemperament Temperament { get; private set; }
	}
}