using System.Collections.Generic;
using NUnit.Framework;
using WitchMendokusai.DomainSDK.Alchemy;

namespace WitchMendokusai.Tests
{
    /// <summary>
    /// TASK-WM-174 Phase 1 — 데이터 주도 제조("커스텀 쉽게").
    /// 재료(BrewIngredient)·레시피(BrewRecipe) POCO 가 BrewEngine 순수 코어를 먹이는지 검증.
    /// 효과/재료 종류는 placeholder 더미(디자인 미확정) — 구조가 데이터 주도임만 잠금.
    /// </summary>
    [TestFixture]
    public class BrewIngredientTest
    {
        // placeholder 더미 재료 — 효과 종류 디자인 미확정, 구조 검증용. 4방위.
        private static BrewIngredient East()
        {
            return new BrewIngredient { Id = 1, Name = "더미-동", Direction = new BrewVector(1f, 0f), DefaultGrind = 2f };
        }

        private static BrewIngredient North()
        {
            return new BrewIngredient { Id = 2, Name = "더미-북", Direction = new BrewVector(0f, 1f), DefaultGrind = 3f };
        }

        [Test]
        public void ToStep_UsesIngredientDirection_AndGivenGrind()
        {
            BrewIngredient ingredient = East();

            BrewStep step = ingredient.ToStep(5f);

            Assert.AreEqual(1f, step.Direction.X);
            Assert.AreEqual(0f, step.Direction.Y);
            Assert.AreEqual(5f, step.Grind);
        }

        [Test]
        public void ToDefaultStep_UsesDefaultGrind()
        {
            BrewIngredient ingredient = North();

            BrewStep step = ingredient.ToDefaultStep();

            Assert.AreEqual(3f, step.Grind, "DefaultGrind 가 step 에 반영");
        }

        [Test]
        public void DataDriven_Brew_ReachesRecipeTarget()
        {
            // 데이터(재료 2개)만으로 제조 → 레시피 목표 도달. 코드에 효과/재료 하드코딩 0.
            List<BrewStep> steps = new List<BrewStep>
            {
                East().ToStep(3f),    // (0,0) -> (3,0)
                North().ToStep(4f),   // (3,0) -> (3,4)
            };

            BrewState end = BrewEngine.Brew(BrewState.Start, steps);

            BrewRecipe recipe = new BrewRecipe
            {
                Id = 100,
                EffectName = "더미-효과",
                Target = new EffectTarget { Position = new BrewVector(3f, 4f), Radius = 0.5f },
            };

            Assert.IsTrue(BrewEngine.IsReached(end, recipe.Target), "데이터 주도 재료열이 레시피 목표 좌표 도달");
        }

        [Test]
        public void DataDriven_WrongIngredients_MissTarget()
        {
            // 다른 재료 조합 = 다른 좌표 = 목표 빗나감(데이터가 결과를 가른다).
            List<BrewStep> steps = new List<BrewStep>
            {
                East().ToStep(1f),    // (1,0)
            };

            BrewState end = BrewEngine.Brew(BrewState.Start, steps);

            BrewRecipe recipe = new BrewRecipe
            {
                Id = 100,
                EffectName = "더미-효과",
                Target = new EffectTarget { Position = new BrewVector(3f, 4f), Radius = 0.5f },
            };

            Assert.IsFalse(BrewEngine.IsReached(end, recipe.Target), "부족한 재료 = 목표 미도달");
        }

        [Test]
        public void AddingNewIngredient_NoCodeChange()
        {
            // "커스텀 쉽게" 회귀 잠금 — 새 재료를 데이터로만 추가해도 엔진이 그대로 소비.
            BrewIngredient custom = new BrewIngredient { Id = 99, Name = "커스텀-남서", Direction = new BrewVector(-1f, -1f), DefaultGrind = 1f };

            BrewState end = BrewEngine.Apply(BrewState.Start, custom.ToStep(2f));

            Assert.AreEqual(-2f, end.Position.X);
            Assert.AreEqual(-2f, end.Position.Y);
        }
    }
}
