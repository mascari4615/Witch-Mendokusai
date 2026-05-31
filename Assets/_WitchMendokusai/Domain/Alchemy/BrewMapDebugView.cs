using System.Collections.Generic;
using UnityEngine;
using WitchMendokusai.DomainSDK.Alchemy;

namespace WitchMendokusai
{
    /// <summary>
    /// TASK-WM-174 — 솥 지도 제조 *디버그 뷰* (씬 배선 0, 자가완결 OnGUI).
    /// GameObject 에 이 컴포넌트만 붙이고 Play → 솥 지도 + 재료 버튼이 화면에 뜬다.
    /// 재료 버튼 클릭 = 그 재료를 갈아 넣음 → 마커가 효과 공간을 이동, 경로/목표/위험지대/부작용 시각화.
    /// 정식 UI = UI Toolkit "펼쳐진 마도서"(후속). 본 뷰는 메커니즘 즉시 체감용 throwaway harness.
    /// 모든 더미 데이터·수치 = [SerializeField] 노출(수치노출 룰) — 인스펙터서 놀이처럼 조정.
    /// </summary>
    public sealed class BrewMapDebugView : MonoBehaviour
    {
        [Header("화면 매핑 (효과공간 → 픽셀)")]
        [SerializeField] private Vector2 mapOriginPixels = new Vector2(420f, 320f);
        [SerializeField] private float pixelsPerUnit = 60f;

        [Header("더미 레시피 (목표 효과 좌표)")]
        [SerializeField] private Vector2 targetCoord = new Vector2(4f, 0f);
        [SerializeField] private float targetRadius = 0.5f;

        [Header("더미 위험지대 (저주 폭주)")]
        [SerializeField] private Vector2 hazardCenter = new Vector2(2f, 0f);
        [SerializeField] private float hazardRadius = 1f;
        [SerializeField] private float hazardSeverity = 10f;

        [Header("더미 재료 (방향 + 갈기) — 새 재료 추가 = 이 리스트만")]
        [SerializeField]
        private DummyIngredient[] ingredients =
        {
            new DummyIngredient { label = "동(→)", dirX = 1f, dirY = 0f, grind = 1f },
            new DummyIngredient { label = "북(↑)", dirX = 0f, dirY = 1f, grind = 1f },
            new DummyIngredient { label = "남(↓)", dirX = 0f, dirY = -1f, grind = 1f },
            new DummyIngredient { label = "서(←)", dirX = -1f, dirY = 0f, grind = 1f },
        };

        private readonly BrewSession session = new BrewSession();
        private readonly List<HazardZone> hazards = new List<HazardZone>();

        [System.Serializable]
        private struct DummyIngredient
        {
            public string label;
            public float dirX;
            public float dirY;
            public float grind;
        }

        private void OnEnable()
        {
            RestartSession();
        }

        private void RestartSession()
        {
            hazards.Clear();
            hazards.Add(new HazardZone
            {
                Id = 1,
                Name = "저주-폭주",
                Center = new BrewVector(hazardCenter.x, hazardCenter.y),
                Radius = hazardRadius,
                SeverityPerUnit = hazardSeverity,
            });

            BrewRecipe recipe = new BrewRecipe
            {
                Id = 1,
                EffectName = "더미-효과",
                Target = new EffectTarget
                {
                    Position = new BrewVector(targetCoord.x, targetCoord.y),
                    Radius = targetRadius,
                },
            };

            session.Start(recipe, hazards);
        }

        // 효과공간 좌표 → 화면 픽셀 (y 위로 = 화면 위로).
        private Vector2 ToPixels(BrewVector coord)
        {
            return new Vector2(
                mapOriginPixels.x + coord.X * pixelsPerUnit,
                mapOriginPixels.y - coord.Y * pixelsPerUnit);
        }

        private void OnGUI()
        {
            DrawMap();
            DrawButtons();
            DrawStatus();
        }

        private void DrawMap()
        {
            // 목표 효과 좌표 (원 + 중심점).
            Vector2 targetPixels = ToPixels(session.Recipe.Target.Position);
            DrawCircleLabel(targetPixels, session.Recipe.Target.Radius * pixelsPerUnit, "◎ 목표");

            // 위험지대.
            for (int i = 0; i < hazards.Count; i++)
            {
                Vector2 hazardPixels = ToPixels(hazards[i].Center);
                DrawCircleLabel(hazardPixels, hazards[i].Radius * pixelsPerUnit, "▓ 저주");
            }

            // 누적 경로 (원점 → 각 step 끝점).
            BrewVector cursor = BrewVector.Zero;
            Vector2 fromPixels = ToPixels(cursor);
            IReadOnlyList<BrewStep> steps = session.Steps;
            for (int i = 0; i < steps.Count; i++)
            {
                cursor = cursor + steps[i].Direction * steps[i].Grind;
                Vector2 toPixels = ToPixels(cursor);
                DrawLine(fromPixels, toPixels);
                fromPixels = toPixels;
            }

            // 현재 마커.
            Vector2 markerPixels = ToPixels(session.State.Position);
            GUI.Label(new Rect(markerPixels.x - 8f, markerPixels.y - 12f, 60f, 24f), "● 지금");
        }

        private void DrawButtons()
        {
            float buttonY = Screen.height - 140f;
            GUI.Label(new Rect(20f, buttonY - 24f, 400f, 24f), "재료를 갈아 넣기 (클릭):");
            for (int i = 0; i < ingredients.Length; i++)
            {
                DummyIngredient ingredient = ingredients[i];
                Rect rect = new Rect(20f + i * 110f, buttonY, 100f, 30f);
                if (GUI.Button(rect, ingredient.label))
                {
                    BrewStep step = new BrewStep
                    {
                        Direction = new BrewVector(ingredient.dirX, ingredient.dirY),
                        Grind = ingredient.grind,
                    };
                    session.AddStep(step);
                }
            }

            if (GUI.Button(new Rect(20f, buttonY + 40f, 100f, 30f), "↺ 다시"))
            {
                RestartSession();
            }
        }

        private void DrawStatus()
        {
            string status = session.IsComplete ? "✅ 효과 도달!" : "… 제조 중";
            string text =
                status + "\n"
                + "목표까지 거리: " + session.DistanceToTarget.ToString("0.00") + "\n"
                + "누적 부작용: " + session.AccruedSideEffect.ToString("0.0") + "\n"
                + "넣은 재료 수: " + session.StepCount;
            GUI.Label(new Rect(20f, 20f, 360f, 100f), text);
        }

        private static void DrawCircleLabel(Vector2 center, float radiusPixels, string label)
        {
            // OnGUI 는 원 직접 못 그림 → 라벨 + 사각 경계로 근사(디버그용).
            GUI.Box(new Rect(center.x - radiusPixels, center.y - radiusPixels, radiusPixels * 2f, radiusPixels * 2f), label);
        }

        private static void DrawLine(Vector2 from, Vector2 to)
        {
            // OnGUI 선: 중간 점들을 작은 라벨로 근사(디버그용 — 정식은 UI Toolkit generateVisualContent).
            int segments = 12;
            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                Vector2 p = Vector2.Lerp(from, to, t);
                GUI.Label(new Rect(p.x - 2f, p.y - 2f, 8f, 12f), "·");
            }
        }
    }
}
