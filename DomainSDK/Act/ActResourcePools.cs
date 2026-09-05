using System;
using System.Collections.Generic;

namespace WitchMendokusai.DomainSDK.Act
{
    /// <summary>
    /// 창고 여럿을 하나로 묶는다 (TASK-WM-410 — 팔기·바치기) — 가방·지갑·창고가 각자 다른 곳에 살아도
    /// 원장은 「창고 하나」만 보면 된다.
    ///
    /// ★ 왜 필요한가: 작물은 가방에 있고 돈은 딴 데(마을 장부) 있다. 그런데 「팔기」는 <b>한 행동</b>이다 —
    ///   작물이 줄고 돈이 느는 것이 <b>전부 되거나 전부 안 되어야</b> 한다. 창고가 갈라져 있으면
    ///   원장의 전무-또는-전부가 깨져 「작물만 사라지고 돈은 안 들어온」 세계가 만들어진다.
    ///
    /// ★ 왜 인터페이스를 안 늘렸나: 「이 자원 네 것이냐」를 창고에게 물으면 모든 창고가 그 질문에
    ///   답해야 한다. 어느 창고가 무엇을 맡는지는 <b>묶는 쪽</b>이 아는 일이라 여기서 판정한다.
    /// </summary>
    public sealed class ActResourcePools : IActResourcePool
    {
        private readonly List<KeyValuePair<Func<ResourceId, bool>, IActResourcePool>> routes = new();

        /// <summary>이 조건에 맞는 자원은 이 창고가 맡는다. 먼저 등록한 쪽이 먼저 걸린다(결정성).</summary>
        public ActResourcePools Route(Func<ResourceId, bool> handles, IActResourcePool pool)
        {
            if (handles != null && pool != null)
            {
                routes.Add(new KeyValuePair<Func<ResourceId, bool>, IActResourcePool>(handles, pool));
            }

            return this;
        }

        public int AmountOf(ResourceId resource)
        {
            IActResourcePool pool = PoolFor(resource);
            return pool == null ? 0 : pool.AmountOf(resource);
        }

        public void Add(ResourceId resource, int amount)
        {
            PoolFor(resource)?.Add(resource, amount);
        }

        private IActResourcePool PoolFor(ResourceId resource)
        {
            for (int i = 0; i < routes.Count; i++)
            {
                if (routes[i].Key(resource))
                {
                    return routes[i].Value;
                }
            }

            return null;
        }
    }
}
