using System;
using UnityEngine;
using VContainer;

namespace WitchMendokusai
{
    /// <summary>
    /// TASK-WM-118 I1 — 조립 self-check 가드.
    ///
    /// RootLifetimeScope / SceneLifetimeScope 의 eager Resolve 는 *손-유지 명령형
    /// 순서 리스트* ("순서 = caller 의존 정합"). 이 손-순서가 진짜 [Inject] 위상과
    /// 어긋나거나 순환/미해결이면 — 지금까지는 *조용한 NRE 가 게임플레이까지 잠복*
    /// (WM-115 R1~R6 처럼 던전 진입 통째 깨짐을 늦게 발견). I1 = 그 실패를
    /// *부팅 시점에 시끄럽게·타입 귀속해 명시 차단* 으로 격상.
    ///
    /// FastFail 정합: 증상 은폐 아님 — 더 일찍·더 명확히 실패시켜 init-order
    /// 버그를 *부팅 차단 + 원인 타입 명시* 로 만든다. 성공 경로는 동작 무변경
    /// (Resolve 결과 그대로 반환). 최저 블라스트 — 가드만 추가.
    /// </summary>
    public static class BootGuard
    {
        public static T EagerResolve<T>(IObjectResolver container, string scope)
        {
            try
            {
                return container.Resolve<T>();
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    $"[BOOT-GUARD] {scope}: eager Resolve<{typeof(T).Name}> 실패 — "
                    + "조립 init-order/의존 가드 발동. 손-순서 eager 리스트가 진짜 [Inject] 위상과 "
                    + "불일치하거나, 의존 미해결/순환. (TASK-WM-118 I1) "
                    + $"원인: {ex.GetType().Name}: {ex.Message}");
                throw;
            }
        }
    }
}
