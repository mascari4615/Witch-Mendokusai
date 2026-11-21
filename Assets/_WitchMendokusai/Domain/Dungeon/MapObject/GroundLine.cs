using UnityEngine;

namespace WitchMendokusai
{
	// 바닥에 눕힌 선 하나. 맵 오브젝트 표식, 머무름 진행 호, 판 경계가 같이 씀
	// 공유 재질 (MapObjectSettings.MarkerMaterial), 개체별 새 재질 없음. 자식의 XY 평면이 월드 XZ 평면
	public static class GroundLine
	{
		public static LineRenderer Make(Transform parent, string lineName, Material material, Color color, float width)
		{
			GameObject owner = new(lineName);
			owner.transform.SetParent(parent, false);
			// 면 법선 (자식 Z) 이 위
			owner.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
			LineRenderer line = owner.AddComponent<LineRenderer>();
			line.alignment = LineAlignment.TransformZ;
			line.useWorldSpace = false;
			line.sharedMaterial = material;
			line.startColor = color;
			line.endColor = color;
			line.startWidth = width;
			line.endWidth = width;
			line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
			line.receiveShadows = false;
			return line;
		}

		// 닫힌 정다각형. 꼭짓점이 많으면 원
		public static void SetPolygon(LineRenderer line, int corners, float radius, float startAngle)
		{
			line.loop = true;
			line.positionCount = corners;
			for (int i = 0; i < corners; i++)
			{
				float angle = startAngle + Mathf.PI * 2f * i / corners;
				line.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius);
			}
		}
	}
}
