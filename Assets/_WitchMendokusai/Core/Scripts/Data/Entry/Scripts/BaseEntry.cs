using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class BaseEntry : ScriptableObject
{
    public int ID => id;
    public string Key => key;

    [SerializeField] private int id;
    [SerializeField] private string key;
}
