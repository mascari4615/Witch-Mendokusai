using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 쓰러지면 주인공의 상태이상 하나를 지움. 보스가 부르는 해제 구슬 (낙인 지우기). 정본 memo wm/design/boss-arena-20261126
	// 시키는 행동: 겹이 쌓이면 구슬부터 부수기
	[CreateAssetMenu(fileName = "AF_Cleanse", menuName = "WM/Monster/Affix/Cleanse")]
	public sealed class CleanseAffix : MonsterAffix
	{
		[SerializeField, Tooltip("지울 상태이상")] private UnitStatusType status = UnitStatusType.Brand;
		[SerializeField, Tooltip("지우는 순간 주인공 자리 그림. 비우면 없음")] private CombatFx cleanseFx;
		[SerializeField, Min(0.1f)] private float cleanseFxRadius = 1.2f;

		public override string KindName => "쓰러지면 해제";

		public UnitStatusType Status => status;

		public override void OnDeath(MonsterObject monster, AffixMemory memory, Vector3 position)
		{
			PlayerObject player = CurrentPlayer;
			if (player == null || player.IsAlive == false || UnitStatus.Has(player, status) == false)
				return;
			UnitStatus.Consume(player, status);
			if (cleanseFx != null)
				cleanseFx.Play(new CombatFxContext { Position = player.transform.position, Follow = player.transform, Radius = cleanseFxRadius });
		}

		public override void Validate(Monster owner, List<string> errors)
		{
			if (owner.Type == MonsterType.Boss)
				errors.Add($"{name}: 해제 몸이 보스면 잡는 순간 싸움이 끝나 해제가 뜻이 없음");
		}
	}
}
