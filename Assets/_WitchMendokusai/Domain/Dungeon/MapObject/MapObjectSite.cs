using System;
using UnityEngine;

namespace WitchMendokusai
{
	// 배치된 맵 오브젝트 하나. 기존 상호작용 입력 (InteractiveObject, 1.5m) 으로 켜짐
	// 비용과 효과는 소유자 (MonsterSpawner) 가 처리. 이 컴포넌트는 사용 횟수와 소진만 앎
	public sealed class MapObjectSite : MonoBehaviour, IInteractable
	{
		private Func<MapObjectSite, bool> tryActivate;

		public MapObjectData Data { get; private set; }
		public int UsedCount { get; private set; }
		public bool IsSpent { get; private set; }

		// 다음 사용 비용 배율. 재사용마다 CostGrowth 곱
		public float CostMultiplier => Mathf.Pow(Data.CostGrowth, UsedCount);

		public void Init(MapObjectData data, Func<MapObjectSite, bool> tryActivate)
		{
			Data = data;
			this.tryActivate = tryActivate;
			UsedCount = 0;
			IsSpent = false;
		}

		public void OnInteract()
		{
			if (tryActivate == null)
				throw new InvalidOperationException($"{name}: {nameof(Init)} 없이 상호작용");
			if (IsSpent)
				return;
			if (tryActivate(this) == false)
				return;
			UsedCount++;
			if (Data.Uses > 0 && UsedCount >= Data.Uses)
			{
				IsSpent = true;
				gameObject.SetActive(false);
			}
		}

		// 선 표식. 공유 재질, 개체별 새 재질 없음. 눕힌 자식에 그림, 소유자 회전은 그대로
		public static void BuildMarker(GameObject owner, MapObjectData data, MapObjectSettings settings, float radius)
		{
			GameObject marker = new("Marker");
			marker.transform.SetParent(owner.transform, false);
			// 면 법선 (자식 Z) 이 위
			marker.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
			LineRenderer line = marker.AddComponent<LineRenderer>();
			line.alignment = LineAlignment.TransformZ;
			line.useWorldSpace = false;
			line.loop = true;
			line.sharedMaterial = settings.MarkerMaterial;
			line.startColor = data.MarkerColor;
			line.endColor = data.MarkerColor;
			line.startWidth = data.MarkerWidth;
			line.endWidth = data.MarkerWidth;
			line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
			line.receiveShadows = false;
			switch (data.MarkerShape)
			{
				case MapObjectMarkerShape.Diamond:
					SetPolygon(line, 4, radius, 0f);
					break;
				case MapObjectMarkerShape.Square:
					SetPolygon(line, 4, radius, Mathf.PI * 0.25f);
					break;
				case MapObjectMarkerShape.Ring:
					SetPolygon(line, settings.RingSegments, radius, 0f);
					break;
				default:
					throw new InvalidOperationException($"{data.name}: 표식 모양 {data.MarkerShape} 처리 없음");
			}
		}

		private static void SetPolygon(LineRenderer line, int corners, float radius, float startAngle)
		{
			line.positionCount = corners;
			for (int i = 0; i < corners; i++)
			{
				float angle = startAngle + Mathf.PI * 2f * i / corners;
				// 눕힌 자식의 XY 평면이 월드 XZ 평면
				line.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius);
			}
		}
	}
}
