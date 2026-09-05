using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using UnityEngine;
using WitchMendokusai;
using Random = UnityEngine.Random;

public class PickupUnit : MonoBehaviour
{
    [SerializeField] private Vector3[] initPos;
    [SerializeField] private Camera _camera;
    [SerializeField] private InputManager inputManager;
    private float pickTime;
    private bool nowPicking;

    private Vector3 GetMousePoint()
    {
        Vector2 mouseScreen = inputManager.MouseScreenPosition;
        Vector3 v = _camera.ScreenToWorldPoint(new Vector3(mouseScreen.x, mouseScreen.y, 0f));
        v.z = v.y;
        return v;
    }

    private void LateUpdate()
    {
        if (nowPicking)
        {
            transform.position = GetMousePoint();
        }
    }

    private void OnMouseDrag()
    {
        pickTime += Time.deltaTime;

        if (pickTime >= .5f)
        {
            nowPicking = true;
        }
    }

    private void OnMouseUp()
    {
        pickTime = 0;
        nowPicking = false;

        transform.position = initPos[Random.Range(0, initPos.Length)];
    }
}