using NUnit.Framework;
using UnityEditor;

namespace WitchMendokusai.Tests
{
	/// <summary> 모든 제약 자산이 효과 글을 만들 수 있는지 (글이 없는 효과 종류나 스탯이면 예외), 이름과 효과 글이 다른지 </summary>
	public sealed class DungeonConstraintEffectTextTest
	{
		[Test]
		public void Every_constraint_has_effect_text_apart_from_name()
		{
			string[] guids = AssetDatabase.FindAssets("t:" + nameof(DungeonConstraint));
			Assert.Greater(guids.Length, 0, "제약 자산 없음");
			foreach (string guid in guids)
			{
				DungeonConstraint constraint = AssetDatabase.LoadAssetAtPath<DungeonConstraint>(AssetDatabase.GUIDToAssetPath(guid));
				string full = DungeonConstraintEffectText.Of(constraint);
				string compact = DungeonConstraintEffectText.Compact(constraint);
				Assert.IsNotEmpty(full, constraint.name + " 효과 글 없음");
				Assert.IsNotEmpty(compact, constraint.name + " 짧은 효과 글 없음");
				Assert.AreNotEqual(constraint.Name, full, constraint.name + " 효과 글이 이름과 같음");
			}
		}

		[Test]
		public void Formats_target_sign_and_unit()
		{
			Assert.AreEqual("적 체력 +10", DungeonConstraintEffectText.Part(DungeonConstraintEffectKind.Stat, UnitStatType.HP_MAX, 10));
			Assert.AreEqual("적 피해 +20%", DungeonConstraintEffectText.Part(DungeonConstraintEffectKind.Stat, UnitStatType.DAMAGE_BONUS, 20));
			Assert.AreEqual("받는 회복 -25%", DungeonConstraintEffectText.Part(DungeonConstraintEffectKind.HealRate, UnitStatType.NONE, -25));
			Assert.AreEqual("카드 선택지 -1장", DungeonConstraintEffectText.Part(DungeonConstraintEffectKind.ChoiceCount, UnitStatType.NONE, -1));
		}
	}
}
