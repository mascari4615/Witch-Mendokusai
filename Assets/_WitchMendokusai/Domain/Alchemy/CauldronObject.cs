using UnityEngine;

namespace WitchMendokusai
{
    /// <summary>
    /// TASK-WM-174 Phase 5b-5 — 디제틱 솥 트리거. 게임 속 솥(공방) 오브젝트에 다가가 Z(상호작용)로 솥 지도 제조 UI 를 연다.
    /// 같은 GameObject 에 InteractiveObject 컴포넌트 + Collider 필수(WM 상호작용 시스템 규약: InteractiveObject 가 GetComponents&lt;IInteractable&gt; 수집).
    /// 'n' 단축키는 dev/fallback 으로 유지 — 정식 진입은 이 디제틱 트리거. 솥 메시 = Pot.prefab.
    /// </summary>
    public sealed class CauldronObject : MonoBehaviour, IInteractable
    {
        public void OnInteract()
        {
            if (CauldronMapController.Instance != null)
            {
                CauldronMapController.Instance.Open();
            }
        }
    }
}
