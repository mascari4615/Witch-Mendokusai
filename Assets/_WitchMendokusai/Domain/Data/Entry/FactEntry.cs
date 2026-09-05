using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = nameof(FactEntry), menuName = "WM/Variable/Entry/FactEntry")]
public class FactEntry : BaseEntry
{
    public int Value => value;
    public FactEntryScopeType Scope => scope;
    
    [SerializeField] private int value;
    [SerializeField] private FactEntryScopeType scope;
}

public enum FactEntryScopeType
{
    Global,
    Area,
    Scene,
    Temporary
}