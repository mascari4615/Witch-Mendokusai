using System;

namespace WitchMendokusai
{
	// 바닥 아이템 판정의 정본. 엔진 무관
	public static class FloorItemRules
	{
		// 화면 청소 대상. 보스와 정예 제외
		public static bool ClearsTarget(bool isBoss, bool isElite) => isBoss == false && isElite == false;
	}
}
