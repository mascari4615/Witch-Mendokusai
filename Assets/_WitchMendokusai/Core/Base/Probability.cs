using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace WitchMendokusai
{
	public class Probability<T>
	{
		private readonly List<ProbabilityElement> probabilityList = new();
		private readonly bool shouldFill100Percent = false;

		public Probability(bool shouldFill100Percent = false)
		{
			this.shouldFill100Percent = shouldFill100Percent;
		}

		private class ProbabilityElement
		{
			public readonly T Target;
			public readonly float Probability;

			public ProbabilityElement(T target, float probability)
			{
				Target = target;
				Probability = probability;
			}
		}

		public void Add(T target, float probability)
		{
			probabilityList.Add(new ProbabilityElement(target, probability));
		}

		public T Get()
		{
			float totalProbability = probabilityList.Sum(probabilityElement => probabilityElement.Probability);

			if (shouldFill100Percent && (totalProbability < 100))
			{
				float remainPercent = 100 - totalProbability;
				probabilityList.Add(new ProbabilityElement(default, remainPercent));
				totalProbability = 100;
			}

			float pick = Random.value * totalProbability;
			foreach (ProbabilityElement probabilityElement in probabilityList)
			{
				if (pick < probabilityElement.Probability)
					return probabilityElement.Target;
				pick -= probabilityElement.Probability;
			}

			return default;
		}
	}
}