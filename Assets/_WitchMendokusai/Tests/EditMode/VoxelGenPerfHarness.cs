using System.Diagnostics;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace WitchMendokusai.Tests
{
    /// <summary>
    /// 복셀 지형 생성 파이프라인 phase 별 결정적 측정 하니스 (process.md § 가설 박기 X — 측정 우선).
    ///
    /// 4 phase 를 청크별 Stopwatch 로 분리: Generate / Mesh / ApplyToMesh / Collider bake.
    /// 결정적 — 고정 seed (TerrainParametersService.Active) + 고정 청크 그리드, render/play 의존 0
    /// (EditMode 에서 Mesh API · Physics.BakeMesh 모두 동작). bisect 후 동일 하니스로 회귀 검증.
    ///
    /// 출력: [VOXPERF] prefix (cleanup 단일 grep). 항상 Pass — 측정 도구지 합격/불합격 게이트 아님.
    /// </summary>
    public sealed class VoxelGenPerfHarness
    {
        private const int GRID_RADIUS = 2;   // renderDistance 2 = 5x5 = 25 청크 (실제 부하 단위)
        private const int WARMUP = 2;        // JIT/캐시 워밍 (측정 제외)

        [Test]
        public void Measure_VoxelGenerationPipeline()
        {
            if (BlockRegistry.IsInitialized == false)
                BlockBootstrap.Reload();

            TerrainParameters tp = TerrainParametersService.Active;
            if (tp == null)
            {
                Assert.Ignore("[VOXPERF] Active TerrainParameters 없음 — 측정 스킵 (Resources 확인).");
                return;
            }
            tp.EnsureHeightmapCache();

            int side = GRID_RADIUS * 2 + 1;
            int total = side * side;

            double genMs = 0, meshMs = 0, applyMs = 0, bakeMs = 0;
            int measured = 0;
            long totalVerts = 0, totalTris = 0;

            // --- sub-bisect: Generate 안 SampleHeight vs SampleBiome 격리 (단일 청크, 측정 path 확정) ---
            {
                int sampleX0 = 0, sampleZ0 = 0;
                Stopwatch ssw = new Stopwatch();
                int n = VoxelConstants.CHUNK_SIZE_X * VoxelConstants.CHUNK_SIZE_Z; // 256 columns
                ssw.Restart();
                for (int k = 0; k < n; k++)
                    TerrainGenerator.SampleHeight(tp, sampleX0 + (k & 15), sampleZ0 + (k >> 4));
                ssw.Stop();
                double shMs = ssw.Elapsed.TotalMilliseconds;
                ssw.Restart();
                for (int k = 0; k < n; k++)
                    TerrainGenerator.SampleBiome(tp, sampleX0 + (k & 15), sampleZ0 + (k >> 4));
                ssw.Stop();
                double sbMs = ssw.Elapsed.TotalMilliseconds;
                Debug.Log($"[VOXPERF] path: HasTerrainGraph={tp.HasTerrainGraph} HasHeightmapCache={tp.HasHeightmapCache} | per-256col SampleHeight={shMs:F2}ms SampleBiome={sbMs:F2}ms");
            }

            // --- 동작 동일성 가드: 같은 (x,z) 는 항상 같은 height (캐시/풀 컨텍스트 리팩터가 출력 안 바꿈) ---
            if (tp.HasTerrainGraph)
            {
                int[] xs = { 0, 13, -47, 200, 16, 255, -256, 1000 };
                int[] zs = { 0, -8, 91, -200, 17, 255, 256, -999 };
                for (int k = 0; k < xs.Length; k++)
                {
                    float a = TerrainGenerator.SampleHeight(tp, xs[k], zs[k]);
                    float b = TerrainGenerator.SampleHeight(tp, xs[k], zs[k]);
                    Assert.AreEqual(a, b, 1e-4f, $"[VOXPERF] SampleHeight 비결정적 @({xs[k]},{zs[k]}): {a} vs {b}");
                    Assert.IsFalse(float.IsNaN(a) || float.IsInfinity(a), $"[VOXPERF] SampleHeight 비정상 @({xs[k]},{zs[k]})={a}");
                }
                Debug.Log("[VOXPERF] determinism guard OK (8 samples stable+finite)");
            }

            Stopwatch sw = new Stopwatch();
            Mesh scratch = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };

            int i = 0;
            for (int cx = -GRID_RADIUS; cx <= GRID_RADIUS; cx++)
            {
                for (int cz = -GRID_RADIUS; cz <= GRID_RADIUS; cz++)
                {
                    bool warm = i < WARMUP;
                    i++;

                    Chunk chunk = new Chunk(new ChunkPosition(cx, cz));

                    sw.Restart();
                    ChunkGenerator.Generate(chunk, tp);
                    sw.Stop();
                    double g = sw.Elapsed.TotalMilliseconds;

                    sw.Restart();
                    ChunkMeshData md = ChunkMesher.GenerateMeshData(chunk, tp);
                    sw.Stop();
                    double m = sw.Elapsed.TotalMilliseconds;

                    sw.Restart();
                    md.ApplyToMesh(scratch);
                    sw.Stop();
                    double a = sw.Elapsed.TotalMilliseconds;

                    sw.Restart();
                    Physics.BakeMesh(scratch.GetEntityId(), false);
                    sw.Stop();
                    double b = sw.Elapsed.TotalMilliseconds;

                    if (warm == false)
                    {
                        genMs += g; meshMs += m; applyMs += a; bakeMs += b;
                        totalVerts += md.Vertices.Length;
                        totalTris += md.Triangles.Length / 3;
                        measured++;
                    }
                }
            }

            Object.DestroyImmediate(scratch);

            double perChunk = (genMs + meshMs + applyMs + bakeMs) / measured;
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"[VOXPERF] grid={side}x{side} measured={measured} (warmup={WARMUP}) chunk={VoxelConstants.CHUNK_SIZE_X}x{VoxelConstants.CHUNK_SIZE_Y}x{VoxelConstants.CHUNK_SIZE_Z}");
            sb.AppendLine($"[VOXPERF] TOTAL  {genMs + meshMs + applyMs + bakeMs,8:F1} ms   perChunk {perChunk,7:F2} ms   verts/chunk {totalVerts / measured}  tris/chunk {totalTris / measured}");
            sb.AppendLine($"[VOXPERF]   Generate     {genMs,8:F1} ms  ({genMs / (genMs + meshMs + applyMs + bakeMs) * 100,5:F1}%)  {genMs / measured,6:F2} ms/chunk  [bg thread]");
            sb.AppendLine($"[VOXPERF]   Mesh         {meshMs,8:F1} ms  ({meshMs / (genMs + meshMs + applyMs + bakeMs) * 100,5:F1}%)  {meshMs / measured,6:F2} ms/chunk  [bg thread]");
            sb.AppendLine($"[VOXPERF]   ApplyToMesh  {applyMs,8:F1} ms  ({applyMs / (genMs + meshMs + applyMs + bakeMs) * 100,5:F1}%)  {applyMs / measured,6:F2} ms/chunk  [MAIN thread hitch]");
            sb.AppendLine($"[VOXPERF]   ColliderBake {bakeMs,8:F1} ms  ({bakeMs / (genMs + meshMs + applyMs + bakeMs) * 100,5:F1}%)  {bakeMs / measured,6:F2} ms/chunk  [MAIN thread hitch]");
            Debug.Log(sb.ToString());

            Assert.Pass("측정 도구 — [VOXPERF] 로그 참조");
        }
    }
}
