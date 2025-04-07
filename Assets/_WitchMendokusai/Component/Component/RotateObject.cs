using DG.Tweening;
using UnityEngine;

public class RotateObject : MonoBehaviour
{
	private void Start()
	{
		// 오브젝트를 Y축을 기준으로 360도 회전시키고, 무한 루프로 반복
		transform.DORotate(new Vector3(0, 360, 0), 2, RotateMode.FastBeyond360)
				 .SetLoops(-1, LoopType.Restart)
				 .SetEase(Ease.Linear);
	}
}