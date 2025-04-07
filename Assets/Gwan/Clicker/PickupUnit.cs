using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using UnityEngine;
using Random = UnityEngine.Random;

public class PickupUnit : MonoBehaviour
{
    [SerializeField] private Vector3[] initPos;
    [SerializeField] private Camera _camera;
    private float pickTime;
    private bool nowPicking;

    private Vector3 GetMousePoint()
    {
        var v = _camera.ScreenToWorldPoint(Input.mousePosition);
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