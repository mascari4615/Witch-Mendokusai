using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 자국 남김. 걸어간 자리에 바닥 액체 (기름, 독, 점액) 를 흘림. 기름은 불이 번지고 독은 밟으면 아픔
	// 시키는 행동: 지나간 길을 피해 돌기, 기름이면 불로 태워 무리째 잡기
	[CreateAssetMenu(fileName = "AF_Trail", menuName = "WM/Monster/Affix/Trail")]
	public sealed class TrailAffix : MonsterAffix
	{
		[SerializeField, Tooltip("흘리는 액체")] private GoopKind goop;
		[SerializeField, Tooltip("이만큼 걸을 때마다 한 번 흘림 (m)"), Min(0.1f)] private float spacing = 0.7f;
		[SerializeField, Tooltip("한 번 흘리는 반지름 (m)"), Min(0.1f)] private float radius = 0.55f;
		[SerializeField, Tooltip("한 번 흘리는 양 0~1"), Range(0f, 1f)] private float amount = 0.8f;

		public override string KindName => "자국 남김";

		public static bool ShouldSpill(Vector3 lastSpot, Vector3 position, float spacing)
		{
			Vector3 offset = position - lastSpot;
			offset.y = 0f;
			return offset.sqrMagnitude >= spacing * spacing;
		}

		// 기억의 LastSpot 은 마지막으로 흘린 자리
		public override void Tick(MonsterObject monster, AffixMemory memory, float deltaTime)
		{
			RemainsWorld remains = RemainsWorld.Current;
			// 액체는 놀이라 흔적 표시 설정 (Enabled) 과 무관 (RemainsWorld 주석, BreakableField 와 같게)
			if (goop == null || remains == null)
				return;
			Vector3 position = monster.transform.position;
			if (ShouldSpill(memory.LastSpot, position, spacing) == false)
				return;
			memory.LastSpot = position;
			position.y = remains.GroundHeight;
			remains.Goop.Spill(goop, position, radius, amount);
		}

		public override void Validate(Monster owner, List<string> errors)
		{
			if (goop == null)
				errors.Add($"{name}: 흘릴 액체 ({nameof(goop)}) 비어 있음");
		}
	}
}
