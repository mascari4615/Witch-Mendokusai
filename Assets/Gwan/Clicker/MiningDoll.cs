using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;

public class MiningDoll : MonoBehaviour
{
    // public enum CaveDollState
    // {
    //     Idle,
    //     Move,
    //     Mining
    // }
    //
    // private Vector3 _touchPos;
    //
    // [SerializeField] private SpriteRenderer _spriteRenderer;
    //
    // [SerializeField] private Animator targetMark;
    // [SerializeField] private new Rigidbody2D rigidbody2D;
    // [SerializeField] private float moveSpeed = 1;
    //
    // private bool _isTargetingUnit = false;
    //
    // private CaveDollState _curState = CaveDollState.Idle;
    // private Coroutine miningLoop;
    //
    // private void Update()
    // {
    //     UpdateState();
    //
    //     switch (_curState)
    //     {
    //         case CaveDollState.Move:
    //             Move();
    //             break;
    //         case CaveDollState.Mining:
    //             // Mining();
    //             break;
    //         case CaveDollState.Idle:
    //             break;
    //         default:
    //             throw new ArgumentOutOfRangeException();
    //     }
    // }
    //
    // private void UpdateState()
    // {
    //     CheckInput();
    //     if (_curState == CaveDollState.Move)
    //         CheckDistance();
    //
    //     if (_curState != CaveDollState.Mining)
    //         if (miningLoop != null)
    //         {
    //             StopCoroutine(miningLoop);
    //             miningLoop = null;
    //         }
    //
    //     void CheckInput()
    //     {
    //         if (Application.platform == RuntimePlatform.Android)
    //         {
    //             if (Input.touchCount == 0)
    //                 return;
    //
    //             if (EventSystem.current
    //                 .IsPointerOverGameObject(Input.GetTouch(0).fingerId))
    //                 return;
    //
    //             var touch = Input.GetTouch(0);
    //             if (touch.phase is not (TouchPhase.Stationary or TouchPhase.Began))
    //                 return;
    //
    //             SetTargetPos(Camera.main.ScreenToWorldPoint(touch.position), false);
    //         }
    //         else
    //         {
    //             if (!Input.GetMouseButtonDown(0))
    //                 return;
    //
    //             if (EventSystem.current.IsPointerOverGameObject())
    //                 return;
    //
    //             SetTargetPos(Camera.main.ScreenToWorldPoint(Input.mousePosition), false);
    //         }
    //
    //         var ray = new Ray2D(_touchPos, Vector2.zero);
    //         const float distance = Mathf.Infinity;
    //         var hit = Physics2D.Raycast(ray.origin, ray.direction, distance, 1 << LayerMask.NameToLayer("Unit"));
    //
    //         if (!hit)
    //             return;
    //
    //         if (!hit.transform.parent.TryGetComponent(out _targetStoneObject))
    //             return;
    //
    //         SetTargetPos(hit.transform.position, true);
    //     }
    //
    //     void CheckDistance()
    //     {
    //         if (_isTargetingUnit)
    //         {
    //             if (Vector3.Distance(_touchPos, rigidbody2D.transform.position) <= 1)
    //             {
    //                 rigidbody2D.velocity = Vector2.zero;
    //                 SetState(CaveDollState.Mining);
    //             }
    //         }
    //         else
    //         {
    //             if (Vector3.Distance(_touchPos, rigidbody2D.transform.position) <= .05f)
    //             {
    //                 rigidbody2D.transform.position = _touchPos;
    //                 rigidbody2D.velocity = Vector2.zero;
    //                 SetState(CaveDollState.Idle);
    //             }
    //         }
    //     }
    // }
    //
    // private void SetTargetPos(Vector3 targetPos, bool isTargetingUnit)
    // {
    //     _touchPos = targetPos;
    //     _touchPos.z = 0;
    //     SetState(CaveDollState.Move);
    //     _isTargetingUnit = isTargetingUnit;
    //
    //     targetMark.transform.position = _touchPos;
    //     targetMark.SetTrigger("TARGET_ON");
    // }
    //
}