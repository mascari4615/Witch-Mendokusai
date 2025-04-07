using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	public class LookAtScreenCenter : MonoBehaviour
	{
		// 스크린 스페이스 중앙을 바라보도록 한다.
		// 이때 회전값은 정반대 방향으로 이루어진다.

		private void Update()
		{
			transform.LookAt(Camera.main.transform.position);
			transform.Rotate(0, 180, 0);
		}
	}
}