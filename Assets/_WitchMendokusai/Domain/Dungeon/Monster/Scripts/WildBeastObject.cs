namespace WitchMendokusai
{
	/// <summary>
	/// 마계 야수 런타임 (TASK-WM-182, 생태 트랙③ 사냥). <see cref="MonsterObject"/> 를 상속하되
	/// 사망 시 전리품(마수 고기/가죽/뼈) 드랍을 <c>DungeonManagerBridge.IsDungeon</c> 게이트와
	/// 무관하게 발생시킨다 — 사냥은 던전이 아니라 야외(마계 지대)에서 일어나므로.
	///
	/// (베이스 <see cref="MonsterObject.HandleDeathEffects"/> 는 IsDungeon 일 때만 DropLoot/킬카운트 →
	/// 야외 야수는 드랍 0 이 됨. 여기서 컨텍스트 무관 드랍으로 재정의. 던전 킬카운트·BOSS_KILL 통계는
	/// 야수에 무의미하므로 생략.)
	/// </summary>
	public class WildBeastObject : MonsterObject
	{
		protected override void HandleDeathEffects()
		{
			// 사냥 전리품은 컨텍스트 무관 드랍 (야외 사냥 핵심).
			DropLoot();

			StopAllCoroutines();
			ObjectBufferManager.RemoveObject(ObjectType.Monster, gameObject);
			gameObject.SetActive(false);
		}
	}
}
