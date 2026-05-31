using System;
using System.Collections.Generic;

namespace WitchMendokusai.DomainSDK.Alchemy
{
    /// <summary>
    /// 제조 한 판의 stateful 진행 = UI(솥 지도) 점진 소비처. BrewEngine(stateless 함수)을 감싼다.
    /// MonoBehaviour/R3 의존 0(DomainSDK references=[]) — EditMode 테스트 first-use.
    /// R3 ViewModel(Domain)이 이 세션을 얇게 감싸 ReactiveProperty 로 노출(테스트 불요 어댑터).
    /// 흐름: Start(recipe) → AddIngredient(재료 갈아 한 step) ×N → IsComplete 면 성공.
    /// 일시정지 재편집(Undo/재시작)은 후속 — v1 = 전진 + Reset 만.
    /// </summary>
    public sealed class BrewSession
    {
        private readonly List<BrewStep> steps = new List<BrewStep>();
        private IReadOnlyList<HazardZone> hazards;

        public BrewRecipe Recipe { get; private set; }
        public BrewState State { get; private set; }

        /// <summary>현재 경로가 위험지대를 통과하며 누적한 부작용(UI 경고 표시용).</summary>
        public float AccruedSideEffect
        {
            get { return State.AccruedSideEffect; }
        }

        /// <summary>지금까지 투입한 재료 step 수(= UI 경로 점 개수).</summary>
        public int StepCount
        {
            get { return steps.Count; }
        }

        /// <summary>현재 마커가 레시피 목표 효과 좌표에 도달했는가(= 제조 성공).</summary>
        public bool IsComplete
        {
            get { return BrewEngine.IsReached(State, Recipe.Target); }
        }

        /// <summary>현재 마커에서 목표까지 거리(UI 근접도 표시용).</summary>
        public float DistanceToTarget
        {
            get { return BrewEngine.DistanceTo(State, Recipe.Target); }
        }

        /// <summary>레시피(목표 효과 좌표)로 새 제조 세션 시작 — 마커는 솥 중앙(원점). 위험지대 없음.</summary>
        public void Start(BrewRecipe recipe)
        {
            Start(recipe, null);
        }

        /// <summary>레시피 + 위험지대(저주 폭주 구역)로 시작. 경로가 위험지대 통과 시 부작용 누적.</summary>
        public void Start(BrewRecipe recipe, IReadOnlyList<HazardZone> hazardZones)
        {
            Recipe = recipe;
            hazards = hazardZones;
            State = BrewState.Start;
            steps.Clear();
        }

        /// <summary>재료를 grind 만큼 갈아 한 step 투입 → 마커 이동. 갱신된 state 반환(UI 즉시 반영).</summary>
        public BrewState AddIngredient(BrewIngredient ingredient, float grind)
        {
            return AddStep(ingredient.ToStep(grind));
        }

        /// <summary>재료를 기본 갈기량으로 투입.</summary>
        public BrewState AddIngredientDefault(BrewIngredient ingredient)
        {
            return AddStep(ingredient.ToDefaultStep());
        }

        /// <summary>이미 만들어진 step 직접 투입(테스트·재생용). 위험지대 있으면 통과 부작용 누적.</summary>
        public BrewState AddStep(BrewStep step)
        {
            steps.Add(step);
            State = hazards == null ? BrewEngine.Apply(State, step) : BrewEngine.Apply(State, step, hazards);
            return State;
        }

        /// <summary>현재 세션의 투입 경로 스냅샷(UI 경로 그리기·리플레이용, 읽기 전용).</summary>
        public IReadOnlyList<BrewStep> Steps
        {
            get { return steps; }
        }

        /// <summary>같은 레시피·위험지대로 다시 시작(재료만 비우고 목표·위험지대 유지).</summary>
        public void Reset()
        {
            State = BrewState.Start;
            steps.Clear();
        }
    }
}
