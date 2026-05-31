using NUnit.Framework;
using WitchMendokusai.DomainSDK.Alchemy;

namespace WitchMendokusai.Tests
{
    /// <summary>
    /// TASK-WM-174 Phase 2 — 제조 세션(BrewSession) stateful 진행. UI(솥 지도) 점진 소비처.
    /// BrewEngine(stateless) 래핑 + 재료 누적 + 도달 판정. R3/MonoBehaviour 의존 0 = EditMode 검증.
    /// </summary>
    [TestFixture]
    public class BrewSessionTest
    {
        private static BrewRecipe RecipeAt(float x, float y, float radius)
        {
            return new BrewRecipe
            {
                Id = 1,
                EffectName = "더미-효과",
                Target = new EffectTarget { Position = new BrewVector(x, y), Radius = radius },
            };
        }

        private static BrewIngredient Dir(float x, float y)
        {
            return new BrewIngredient { Id = 1, Name = "더미", Direction = new BrewVector(x, y), DefaultGrind = 1f };
        }

        [Test]
        public void Start_PlacesMarkerAtOrigin_NoSteps()
        {
            BrewSession session = new BrewSession();

            session.Start(RecipeAt(5f, 0f, 0.5f));

            Assert.AreEqual(0f, session.State.Position.X);
            Assert.AreEqual(0f, session.State.Position.Y);
            Assert.AreEqual(0, session.StepCount);
            Assert.IsFalse(session.IsComplete, "시작 시 목표 미도달");
        }

        [Test]
        public void AddIngredient_MovesMarker_AccumulatesSteps()
        {
            BrewSession session = new BrewSession();
            session.Start(RecipeAt(3f, 0f, 0.5f));

            session.AddIngredient(Dir(1f, 0f), 2f);

            Assert.AreEqual(2f, session.State.Position.X);
            Assert.AreEqual(1, session.StepCount);
        }

        [Test]
        public void Brew_ReachesTarget_AcrossMultipleIngredients()
        {
            BrewSession session = new BrewSession();
            session.Start(RecipeAt(3f, 4f, 0.5f));

            session.AddIngredient(Dir(1f, 0f), 3f);   // (3,0)
            Assert.IsFalse(session.IsComplete, "중간 = 미도달");
            session.AddIngredient(Dir(0f, 1f), 4f);   // (3,4)

            Assert.IsTrue(session.IsComplete, "목표 좌표 도달 = 제조 성공");
            Assert.AreEqual(2, session.StepCount);
        }

        [Test]
        public void DistanceToTarget_Decreases_AsApproaching()
        {
            BrewSession session = new BrewSession();
            session.Start(RecipeAt(10f, 0f, 0.5f));

            float before = session.DistanceToTarget;
            session.AddIngredient(Dir(1f, 0f), 5f);
            float after = session.DistanceToTarget;

            Assert.Less(after, before, "재료 투입으로 목표에 가까워짐");
        }

        [Test]
        public void Reset_ClearsSteps_KeepsRecipe()
        {
            BrewSession session = new BrewSession();
            BrewRecipe recipe = RecipeAt(3f, 4f, 0.5f);
            session.Start(recipe);
            session.AddIngredient(Dir(1f, 0f), 3f);

            session.Reset();

            Assert.AreEqual(0, session.StepCount, "재료 비워짐");
            Assert.AreEqual(0f, session.State.Position.X, "마커 원점 복귀");
            Assert.AreEqual(recipe.Id, session.Recipe.Id, "레시피(목표)는 유지");
        }

        [Test]
        public void AddIngredientDefault_UsesDefaultGrind()
        {
            BrewSession session = new BrewSession();
            session.Start(RecipeAt(5f, 0f, 0.5f));

            BrewIngredient ingredient = new BrewIngredient { Id = 2, Name = "더미", Direction = new BrewVector(1f, 0f), DefaultGrind = 4f };
            session.AddIngredientDefault(ingredient);

            Assert.AreEqual(4f, session.State.Position.X, "기본 갈기량 4 만큼 이동");
        }

        [Test]
        public void Steps_ExposesPathSnapshot_ForReplay()
        {
            BrewSession session = new BrewSession();
            session.Start(RecipeAt(3f, 4f, 0.5f));
            session.AddIngredient(Dir(1f, 0f), 3f);
            session.AddIngredient(Dir(0f, 1f), 4f);

            Assert.AreEqual(2, session.Steps.Count, "경로 스냅샷 = 투입 step 수");
            Assert.AreEqual(3f, session.Steps[0].Grind);
            Assert.AreEqual(4f, session.Steps[1].Grind);
        }
    }
}
