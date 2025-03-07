using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class CutSceneModule : MonoBehaviour
{
    public TextMeshProUGUI Subtitle => subtitle;
    public CanvasGroup FadeCanvasGroup => fadeCanvasGroup;
    
    [SerializeField] private TextMeshProUGUI subtitle;
    [SerializeField] private CanvasGroup fadeCanvasGroup;
}
