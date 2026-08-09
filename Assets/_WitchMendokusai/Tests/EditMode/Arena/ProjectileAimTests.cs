using NUnit.Framework;
using UnityEngine;
// ★ 좌표는 판정 쪽 (TASK-WM-214).
using Vector3 = WitchMendokusai.Numerics.Vector3;
using Vector2 = WitchMendokusai.Numerics.Vector2;
using Vector2Int = WitchMendokusai.Numerics.Vector2Int;
using Vector3Int = WitchMendokusai.Numerics.Vector3Int;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// ProjectileAim.Resolve 조준 우선순위 회귀 — 전술 타겟 > 폴백 > forward + y평면 + null 가드.
	/// WM-165 item 6 후속(리뷰 major fix): SkillContext.Target 소비 경로의 테스트 가능 seam.
	/// </summary>
	public class ProjectileAimTests
	{
		[Test]
		public void TargetPresent_AimsAtTarget_IgnoresFallback()
		{
			Vector3 dir = ProjectileAim.Resolve(Vector3.zero, new Vector3(5f, 0f, 0f), new Vector3(0f, 0f, 1f), Vector3.forward);
			Assert.AreEqual(Vector3.right, dir);
		}

		[Test]
		public void NoTarget_UsesFallbackAim()
		{
			Vector3 dir = ProjectileAim.Resolve(Vector3.zero, null, new Vector3(0f, 0f, 3f), Vector3.right);
			Assert.AreEqual(Vector3.forward, dir);
		}

		[Test]
		public void NoTargetNoFallback_UsesFinalForward()
		{
			Vector3 dir = ProjectileAim.Resolve(Vector3.zero, null, null, Vector3.left);
			Assert.AreEqual(Vector3.left, dir);
		}

		[Test]
		public void TargetAtOrigin_FallsThroughToFallback()
		{
			Vector3 dir = ProjectileAim.Resolve(Vector3.zero, Vector3.zero, new Vector3(0f, 0f, 2f), Vector3.right);
			Assert.AreEqual(Vector3.forward, dir);
		}

		[Test]
		public void IgnoresYComponent()
		{
			Vector3 dir = ProjectileAim.Resolve(Vector3.zero, new Vector3(0f, 10f, 4f), null, Vector3.right);
			Assert.AreEqual(Vector3.forward, dir);
		}
	}
}
