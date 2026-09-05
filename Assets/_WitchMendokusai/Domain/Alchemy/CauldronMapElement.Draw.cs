using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using WitchMendokusai.DomainSDK.Alchemy;

namespace WitchMendokusai
{
    // CauldronMapElement 의 그리기와 손 조작 부분. 같은 클래스의 partial 조각이다. 상태(필드)는 CauldronMapElement.cs 를 본다.
    public sealed partial class CauldronMapElement : VisualElement
    {
        // 드래그(갈기) 상태.
        private bool dragging;
        private Vector2 dragStartLocal;
        private Vector2 dragCurrentLocal;

        // ── 좌표 변환 (효과공간 ↔ 캔버스 픽셀) ───────────────────────────
        // 캔버스 중앙 = 효과공간 원점. y 위로 = 화면 위로.
        private Vector2 EffectToPixels(BrewVector coord)
        {
            Rect rect = mapCanvas.contentRect;
            Vector2 center = new Vector2(rect.width * 0.5f, rect.height * 0.5f);
            return new Vector2(
                center.x + coord.X * pixelsPerUnit,
                center.y - coord.Y * pixelsPerUnit);
        }

        private BrewVector PixelsToEffect(Vector2 localPixels)
        {
            Rect rect = mapCanvas.contentRect;
            Vector2 center = new Vector2(rect.width * 0.5f, rect.height * 0.5f);
            return new BrewVector(
                (localPixels.x - center.x) / pixelsPerUnit,
                (center.y - localPixels.y) / pixelsPerUnit);
        }

        // ── 지도 렌더 (Painter2D) ────────────────────────────────────────
        private void OnDrawMap(MeshGenerationContext context)
        {
            Painter2D painter = context.painter2D;

            // 위험지대 (붉은 얼룩 — 채움).
            for (int i = 0; i < hazards.Count; i++)
            {
                if (hazards[i].Radius <= 0f)
                {
                    continue;
                }
                FillCircle(painter, EffectToPixels(hazards[i].Center), hazards[i].Radius * pixelsPerUnit, HazardColor);
            }

            // 목표 효과 좌표 (청록 원 + 중심점). 붙어 있으면 세계의 마도서가 정본이다.
            EffectTarget target = ActiveRecipe().Target;
            Vector2 targetPixels = EffectToPixels(target.Position);
            StrokeCircle(painter, targetPixels, Mathf.Max(target.Radius * pixelsPerUnit, 6f), TargetColor, 2f);
            FillCircle(painter, targetPixels, 3f, TargetColor);

            // 누적 경로 (원점 → 각 step 끝점, 보랏빛 잉크 선). 공유 솥이면 동기된 경로(SyncList), 솔로면 로컬
            // = 둘이 같은 *경로*까지 본다. 재료 0개 = 경로 없음(빈 stroke 아티팩트 회피).
            IReadOnlyList<BrewStep> steps = CurrentSteps();
            if (steps.Count > 0)
            {
                BrewVector cursor = BrewVector.Zero;
                painter.strokeColor = PathColor;
                painter.lineWidth = 2.5f;
                painter.lineCap = LineCap.Round;
                painter.lineJoin = LineJoin.Round;
                painter.BeginPath();
                painter.MoveTo(EffectToPixels(cursor));
                for (int i = 0; i < steps.Count; i++)
                {
                    cursor = cursor + steps[i].Direction * steps[i].Grind;
                    painter.LineTo(EffectToPixels(cursor));
                }
                painter.Stroke();
            }

            // 드래그 미리보기 (갈기 방향 점선 느낌 = 실선 회색).
            if (dragging)
            {
                painter.strokeColor = new Color(0.4f, 0.4f, 0.4f, 0.8f);
                painter.lineWidth = 1.5f;
                painter.BeginPath();
                painter.MoveTo(dragStartLocal);
                painter.LineTo(dragCurrentLocal);
                painter.Stroke();
            }

            // 현재 마커 (●) — 공유 솥이면 SyncVar 수신 위치, 솔로면 로컬 세션.
            FillCircle(painter, EffectToPixels(CurrentState().Position), 5f, MarkerColor);
        }

        private static void StrokeCircle(Painter2D painter, Vector2 center, float radius, Color color, float width)
        {
            painter.strokeColor = color;
            painter.lineWidth = width;
            painter.BeginPath();
            painter.Arc(center, radius, new Angle(0f, AngleUnit.Degree), new Angle(360f, AngleUnit.Degree));
            painter.Stroke();
        }

        private static void FillCircle(Painter2D painter, Vector2 center, float radius, Color color)
        {
            painter.fillColor = color;
            painter.BeginPath();
            painter.Arc(center, radius, new Angle(0f, AngleUnit.Degree), new Angle(360f, AngleUnit.Degree));
            painter.Fill();
        }

        // ── 드래그(갈기) 입력 ────────────────────────────────────────────
        private void OnPointerDown(PointerDownEvent evt)
        {
            dragging = true;
            dragStartLocal = evt.localPosition;
            dragCurrentLocal = dragStartLocal;
            mapCanvas.CapturePointer(evt.pointerId);
            mapCanvas.MarkDirtyRepaint();
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (dragging == false)
            {
                return;
            }
            dragCurrentLocal = evt.localPosition;
            mapCanvas.MarkDirtyRepaint();
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (dragging == false)
            {
                return;
            }
            dragging = false;
            mapCanvas.ReleasePointer(evt.pointerId);

            BrewVector from = PixelsToEffect(dragStartLocal);
            BrewVector to = PixelsToEffect(dragCurrentLocal);
            BrewVector delta = to - from;
            float grind = delta.Magnitude;
            if (grind > 0.05f)
            {
                // 드래그 방향 단위벡터 × 거리 = 한 번의 갈기(손맛). 공유 솥이면 ServerRpc 라우팅.
                BrewVector direction = new BrewVector(delta.X / grind, delta.Y / grind);
                AddStepRouted(new BrewStep { Direction = direction, Grind = grind });
            }
            Refresh(); // 드래그 미리보기 해제(+ 솔로 상태 반영). 네트워크 마커는 PollShared 가 갱신.
        }
    }
}
