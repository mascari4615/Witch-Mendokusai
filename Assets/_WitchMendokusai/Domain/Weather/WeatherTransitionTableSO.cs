using System;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 시간대 (4 hour bucket: Dawn/Morning/Afternoon/Night) 별 weather 가중치 행렬.
	// 매 시간 (WorldClock.OnHourChanged) 마다 RollNext 호출 → weighted random.
	// Magical = 가중치 0 (ForceTriggerMagical 만 발동 — sub-G 의식 호출).
	// 계절 축 (Spring/Summer/Autumn/Winter) 은 D 머지 + 사용자 컨펌 후 후속 단계.
	// (TASK-WM-054-D D2)
	[CreateAssetMenu(fileName = nameof(WeatherTransitionTableSO), menuName = "WM/Weather/WeatherTransitionTableSO")]
	public class WeatherTransitionTableSO : ScriptableObject
	{
		[Serializable]
		public struct WeatherWeight
		{
			public WeatherType Type;
			[Range(0f, 1f)] public float Weight;
		}

		[Serializable]
		public struct HourBucketProfile
		{
			[Tooltip("0=Dawn (4-7) / 1=Morning (7-12) / 2=Afternoon (12-17) / 3=Night (17-24+0-4)")]
			[Range(0, 3)] public int HourBucket;
			public List<WeatherWeight> Weights;
		}

		[field: Header("_" + nameof(WeatherTransitionTableSO))]
		[field: Tooltip("hour bucket 4개 × weather 7개 가중치. Bootstrap 이 모동숲 톤 default 박음.")]
		[field: SerializeField] public List<HourBucketProfile> Profiles { get; private set; } = new List<HourBucketProfile>();

		// hour 0~23 → bucket 0~3
		public static int HourToBucket(int hour)
		{
			if (hour >= 4 && hour < 7)
				return 0;
			if (hour >= 7 && hour < 12)
				return 1;
			if (hour >= 12 && hour < 17)
				return 2;
			return 3;
		}

		// hour bucket 의 가중치 합 기반 weighted random.
		// 가중치 합 0 또는 profile 미존재 시 fallback = Clear.
		public WeatherType RollNext(int hour)
		{
			int bucket = HourToBucket(hour);
			HourBucketProfile profile = FindProfile(bucket);

			if (profile.Weights == null || profile.Weights.Count == 0)
				return WeatherType.Clear;

			float totalWeight = 0f;
			foreach (WeatherWeight entry in profile.Weights)
				totalWeight += entry.Weight;

			if (totalWeight <= 0f)
				return WeatherType.Clear;

			float roll = UnityEngine.Random.Range(0f, totalWeight);
			float cursor = 0f;
			foreach (WeatherWeight entry in profile.Weights)
			{
				cursor += entry.Weight;
				if (roll <= cursor)
					return entry.Type;
			}

			return WeatherType.Clear;
		}

		// dominant (최대 가중치) weather — 일기예보 (sub-D D5 PreviewNext) 에 사용.
		public WeatherType DominantAt(int hour)
		{
			int bucket = HourToBucket(hour);
			HourBucketProfile profile = FindProfile(bucket);

			if (profile.Weights == null || profile.Weights.Count == 0)
				return WeatherType.Clear;

			WeatherType dominant = WeatherType.Clear;
			float maxWeight = -1f;
			foreach (WeatherWeight entry in profile.Weights)
			{
				if (entry.Weight > maxWeight)
				{
					maxWeight = entry.Weight;
					dominant = entry.Type;
				}
			}

			return dominant;
		}

		private HourBucketProfile FindProfile(int bucket)
		{
			foreach (HourBucketProfile profile in Profiles)
			{
				if (profile.HourBucket == bucket)
					return profile;
			}
			return default;
		}
	}
}
