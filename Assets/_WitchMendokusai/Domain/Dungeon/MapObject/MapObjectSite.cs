using System;
using UnityEngine;

namespace WitchMendokusai
{
	// 맵 오브젝트를 돌리는 쪽 (MonsterSpawner). 비용, 효과, 판 안 상태를 앎
	public interface IMapObjectHost
	{
		// 비용과 시작 효과. 성공하면 true
		bool TryActivate(MapObjectSite site);

		// 지금 비용을 낼 수 있는지. 상호작용 표시의 비용 글자 색
		bool CanAfford(MapObjectSite site);

		// 상호작용 표시의 비용 글자. 머무름 중이면 진행
		string DescribeCost(MapObjectSite site);

		// 상호작용 표시가 글자를 다시 쓸지 판단하는 값
		int PromptStamp(MapObjectSite site);
	}

	public enum MapObjectSiteState
	{
		Ready = 0,
		Holding = 1,
		Spent = 2,
	}

	// 배치된 맵 오브젝트 하나. 기존 상호작용 입력 (InteractiveObject, 1.5m) 으로 켜짐
	// 비용과 효과는 소유자 (IMapObjectHost) 가 처리. 이 컴포넌트는 사용 횟수, 머무름 진행, 발견 여부를 앎
	public sealed class MapObjectSite : MonoBehaviour, IInteractable
	{
		private IMapObjectHost host;
		private LineRenderer progressLine;
		private float markerRadius;
		private float holdProgress;
		private float holdAge;

		public MapObjectData Data { get; private set; }
		public int UsedCount { get; private set; }
		public MapObjectSiteState State { get; private set; }
		public bool IsSpent => State == MapObjectSiteState.Spent;
		public bool IsHolding => State == MapObjectSiteState.Holding;
		// 작은 지도와 화면 밖 화살표에 보이는지. 가까이 가거나 스캐너로 드러남
		public bool IsKnown { get; private set; }
		// 작은 지도와 화면 밖 화살표가 같은 판정. 발견했고, 신호 범주가 있고, 아직 남은 것
		public bool IsSignaled => IsSpent == false && IsKnown && Data.Signal != MapObjectSignal.None;
		// 머무름은 찼는데 완료 효과를 못 내는 중 (적 상한, 체력 가득). 반응 글자를 한 번만
		public bool IsCompletionBlocked { get; private set; }
		public float HoldProgress => holdProgress;
		// 상호작용 표시가 글자를 다시 쓸지 판단. 상태가 바뀔 때마다 증가
		public int Version { get; private set; }

		// 다음 사용 비용 배율. 재사용마다 CostGrowth 곱
		public float CostMultiplier => MapObjectRules.CostMultiplier(Data.CostGrowth, UsedCount);

		public void Init(MapObjectData data, IMapObjectHost host, float markerRadius)
		{
			Data = data;
			this.host = host;
			this.markerRadius = markerRadius;
			UsedCount = 0;
			State = MapObjectSiteState.Ready;
			IsKnown = false;
			holdProgress = 0f;
			holdAge = 0f;
			Version++;
		}

		public bool CanAfford() => host.CanAfford(this);

		public string DescribeCost() => host.DescribeCost(this);

		public int PromptStamp() => host.PromptStamp(this);

		public void OnInteract()
		{
			if (host == null)
				throw new InvalidOperationException($"{name}: {nameof(Init)} 없이 상호작용");
			if (State != MapObjectSiteState.Ready)
				return;
			if (host.TryActivate(this) == false)
				return;
			if (Data.HasHold)
			{
				State = MapObjectSiteState.Holding;
				holdProgress = 0f;
				holdAge = 0f;
				SetProgressVisible(true);
				UpdateProgressLine();
				Version++;
				return;
			}
			FinishUse();
		}

		public void Reveal()
		{
			if (IsKnown)
				return;
			IsKnown = true;
			Version++;
		}

		// 머무름 한 틱. 결과가 Completed 면 소유자가 완료 효과를 낸 뒤 CompleteHold, Failed 면 이미 소진
		public MapObjectHoldResult TickHold(float deltaSeconds, Vector3 playerPosition)
		{
			if (State != MapObjectSiteState.Holding)
				throw new InvalidOperationException($"{name}: 머무름 중이 아닌데 {nameof(TickHold)}");
			Vector3 offset = playerPosition - transform.position;
			offset.y = 0f;
			bool inside = offset.sqrMagnitude <= markerRadius * markerRadius;
			MapObjectHoldResult result = MapObjectRules.StepHold(ref holdProgress, ref holdAge, deltaSeconds, inside, Data.HoldSeconds, Data.HoldOutside, Data.HoldDecayPerSecond, Data.HoldTimeLimit);
			UpdateProgressLine();
			if (result == MapObjectHoldResult.Failed)
				Spend();
			return result;
		}

		// 완료 효과를 낸 뒤. 사용 횟수가 남으면 다시 Ready
		public void CompleteHold()
		{
			if (State != MapObjectSiteState.Holding)
				throw new InvalidOperationException($"{name}: 머무름 중이 아닌데 {nameof(CompleteHold)}");
			State = MapObjectSiteState.Ready;
			holdProgress = 0f;
			IsCompletionBlocked = false;
			SetProgressVisible(false);
			FinishUse();
		}

		// 처음 막힌 틱에만 true
		public bool MarkCompletionBlocked()
		{
			if (IsCompletionBlocked)
				return false;
			IsCompletionBlocked = true;
			return true;
		}

		private void FinishUse()
		{
			UsedCount++;
			Version++;
			if (Data.Uses > 0 && UsedCount >= Data.Uses)
				Spend();
		}

		private void Spend()
		{
			State = MapObjectSiteState.Spent;
			Version++;
			gameObject.SetActive(false);
		}

		private void SetProgressVisible(bool visible)
		{
			if (progressLine != null)
				progressLine.enabled = visible;
		}

		// 표식 원 안쪽 호. 진행 비율만큼 한 바퀴를 채움
		private void UpdateProgressLine()
		{
			if (progressLine == null)
				return;
			float ratio = Data.HoldSeconds > 0f ? Mathf.Clamp01(holdProgress / Data.HoldSeconds) : 0f;
			int segments = progressLine.positionCount;
			float radius = markerRadius * MapObjectSettings.Load().HoldProgressRadiusRatio;
			for (int i = 0; i < segments; i++)
			{
				float angle = Mathf.PI * 0.5f - Mathf.PI * 2f * ratio * i / (segments - 1);
				progressLine.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius);
			}
		}

		// 선 표식. 공유 재질, 개체별 새 재질 없음. 눕힌 자식에 그림, 소유자 회전은 그대로
		// 머무름 오브젝트는 같은 재질의 진행 호를 하나 더
		public void BuildMarker(MapObjectSettings settings)
		{
			LineRenderer line = MakeLine("Marker", settings, Data.MarkerColor, Data.MarkerWidth);
			line.loop = true;
			switch (Data.MarkerShape)
			{
				case MapObjectMarkerShape.Diamond:
					SetPolygon(line, 4, markerRadius, 0f);
					break;
				case MapObjectMarkerShape.Square:
					SetPolygon(line, 4, markerRadius, Mathf.PI * 0.25f);
					break;
				case MapObjectMarkerShape.Ring:
					SetPolygon(line, settings.RingSegments, markerRadius, 0f);
					break;
				default:
					throw new InvalidOperationException($"{Data.name}: 표식 모양 {Data.MarkerShape} 처리 없음");
			}
			if (Data.HasHold == false)
				return;
			progressLine = MakeLine("HoldProgress", settings, settings.HoldProgressColor, Data.MarkerWidth * settings.HoldProgressWidthRatio);
			progressLine.loop = false;
			progressLine.positionCount = settings.RingSegments + 1;
			UpdateProgressLine();
			SetProgressVisible(false);
		}

		private LineRenderer MakeLine(string lineName, MapObjectSettings settings, Color color, float width)
		{
			GameObject marker = new(lineName);
			marker.transform.SetParent(transform, false);
			// 면 법선 (자식 Z) 이 위
			marker.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
			LineRenderer line = marker.AddComponent<LineRenderer>();
			line.alignment = LineAlignment.TransformZ;
			line.useWorldSpace = false;
			line.sharedMaterial = settings.MarkerMaterial;
			line.startColor = color;
			line.endColor = color;
			line.startWidth = width;
			line.endWidth = width;
			line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
			line.receiveShadows = false;
			return line;
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
