using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Deadhaul.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Deadhaul
{
    /// <summary>
    /// Laadt chunks in een ring rond de speler: genereren en meshen gebeurt op achtergrondthreads,
    /// de hoofdthread zet alleen de meshes in de scène. Gewijzigde chunks worden direct opnieuw gemesht.
    /// </summary>
    public sealed class ChunkManager : MonoBehaviour
    {
        sealed class ChunkView
        {
            public int Cx, Cz;
            public GameObject Go;
            public Mesh Mesh;
        }

        sealed class Result
        {
            public int Cx, Cz;
            public byte[] Padded;
            public Vector3[] Pos, Nrm;
            public Vector2[] Uv;
            public int[] Opaque, Glass;
            public Exception Error;
        }

        public WorldGen Gen { get; private set; }
        public VoxelStore Store { get; private set; }
        public int ViewRadius = 10;              // in chunks (16 m)
        public int MaxUploadsPerFrame = 3;

        readonly Dictionary<long, ChunkView> views = new Dictionary<long, ChunkView>();
        readonly HashSet<long> pending = new HashSet<long>();
        readonly ConcurrentQueue<Result> done = new ConcurrentQueue<Result>();
        readonly List<(int cx, int cz, int d)> wanted = new List<(int, int, int)>();
        Material voxelMat, glassMat;
        int maxJobs;
        Vector2Int lastCenter = new Vector2Int(int.MinValue, 0);
        readonly MeshData remeshBuffer = new MeshData();

        /// <summary>Wordt aangeroepen als een chunk (opnieuw) in de wereld staat.</summary>
        public event Action<int, int> ChunkLoaded;

        public void Init(WorldGen gen, VoxelStore store)
        {
            Gen = gen; Store = store;
            voxelMat = VoxelAssets.VoxelMaterial;
            glassMat = VoxelAssets.GlassMaterial;
            maxJobs = Mathf.Max(2, SystemInfo.processorCount - 2);
        }

        public bool IsLoaded(Vector3 worldPos)
        {
            int vx = Mathf.FloorToInt(worldPos.x / World.VoxelSize), vz = Mathf.FloorToInt(worldPos.z / World.VoxelSize);
            return Store.IsLoaded(VoxelStore.ChunkOf(vx), VoxelStore.ChunkOf(vz));
        }

        public int PendingCount => pending.Count;

        public void Tick(Vector3 playerPos)
        {
            int pcx = VoxelStore.ChunkOf(Mathf.FloorToInt(playerPos.x / World.VoxelSize));
            int pcz = VoxelStore.ChunkOf(Mathf.FloorToInt(playerPos.z / World.VoxelSize));
            var center = new Vector2Int(pcx, pcz);
            if (center != lastCenter)
            {
                lastCenter = center;
                RebuildWanted(pcx, pcz);
                Unload(pcx, pcz);
            }
            // nieuwe taken starten, dichtstbijzijnde eerst
            for (int i = 0; i < wanted.Count && pending.Count < maxJobs; i++)
            {
                var (cx, cz, _) = wanted[i];
                long k = VoxelStore.Key(cx, cz);
                if (views.ContainsKey(k) || pending.Contains(k)) continue;
                Schedule(cx, cz);
            }
            // resultaten verwerken
            int uploads = 0;
            while (uploads < MaxUploadsPerFrame && done.TryDequeue(out var r))
            {
                long k = VoxelStore.Key(r.Cx, r.Cz);
                pending.Remove(k);
                if (r.Error != null) { Debug.LogException(r.Error); continue; }
                int d = Mathf.Max(Mathf.Abs(r.Cx - pcx), Mathf.Abs(r.Cz - pcz));
                if (d > ViewRadius + 1) continue;          // intussen te ver weg
                Store.Add(r.Cx, r.Cz, r.Padded);
                Upload(r.Cx, r.Cz, r.Pos, r.Nrm, r.Uv, r.Opaque, r.Glass);
                ChunkLoaded?.Invoke(r.Cx, r.Cz);
                uploads++;
            }
        }

        void RebuildWanted(int pcx, int pcz)
        {
            wanted.Clear();
            int R = ViewRadius;
            for (int dz = -R; dz <= R; dz++)
                for (int dx = -R; dx <= R; dx++)
                {
                    int d2 = dx * dx + dz * dz;
                    if (d2 > (R + 0.5f) * (R + 0.5f)) continue;
                    wanted.Add((pcx + dx, pcz + dz, d2));
                }
            wanted.Sort((a, b) => a.d.CompareTo(b.d));
        }

        void Unload(int pcx, int pcz)
        {
            var remove = new List<long>();
            foreach (var kv in views)
            {
                var v = kv.Value;
                float dx = v.Cx - pcx, dz = v.Cz - pcz;
                if (dx * dx + dz * dz > (ViewRadius + 2.5f) * (ViewRadius + 2.5f)) remove.Add(kv.Key);
            }
            foreach (var k in remove)
            {
                var v = views[k];
                Destroy(v.Mesh);
                Destroy(v.Go);
                Store.Remove(v.Cx, v.Cz);
                views.Remove(k);
            }
        }

        void Schedule(int cx, int cz)
        {
            long k = VoxelStore.Key(cx, cz);
            pending.Add(k);
            var edits = Store.EditsAround(cx, cz);      // kopie, op de hoofdthread gemaakt
            var gen = Gen;
            Task.Run(() =>
            {
                var r = new Result { Cx = cx, Cz = cz };
                try
                {
                    var pad = gen.GenerateChunk(cx, cz);
                    VoxelStore.ApplyEdits(pad, cx, cz, edits);
                    var m = new MeshData();
                    ChunkMesher.Build(pad, cx * World.ChunkSize, cz * World.ChunkSize, m);
                    r.Padded = pad;
                    Convert(m, out r.Pos, out r.Nrm, out r.Uv);
                    r.Opaque = m.Opaque.ToArray();
                    r.Glass = m.Glass.ToArray();
                }
                catch (Exception e) { r.Error = e; }
                done.Enqueue(r);
            });
        }

        static void Convert(MeshData m, out Vector3[] pos, out Vector3[] nrm, out Vector2[] uv)
        {
            int n = m.VertexCount;
            pos = new Vector3[n]; nrm = new Vector3[n]; uv = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                pos[i] = new Vector3(m.Positions[i * 3], m.Positions[i * 3 + 1], m.Positions[i * 3 + 2]);
                nrm[i] = new Vector3(m.Normals[i * 3], m.Normals[i * 3 + 1], m.Normals[i * 3 + 2]);
                uv[i] = new Vector2(m.Uvs[i * 2], m.Uvs[i * 2 + 1]);
            }
        }

        void Upload(int cx, int cz, Vector3[] pos, Vector3[] nrm, Vector2[] uv, int[] opaque, int[] glass)
        {
            long k = VoxelStore.Key(cx, cz);
            if (!views.TryGetValue(k, out var v))
            {
                v = new ChunkView { Cx = cx, Cz = cz };
                v.Go = new GameObject($"Chunk {cx},{cz}");
                v.Go.transform.SetParent(transform, false);
                v.Go.transform.position = new Vector3(cx * World.ChunkSize * World.VoxelSize, 0, cz * World.ChunkSize * World.VoxelSize);
                v.Go.isStatic = false;
                v.Mesh = new Mesh { name = v.Go.name };
                v.Mesh.MarkDynamic();
                v.Go.AddComponent<MeshFilter>().sharedMesh = v.Mesh;
                var mr = v.Go.AddComponent<MeshRenderer>();
                mr.sharedMaterials = new[] { voxelMat, glassMat };
                mr.shadowCastingMode = ShadowCastingMode.On;
                mr.receiveShadows = true;
                views[k] = v;
            }
            var mesh = v.Mesh;
            mesh.Clear();
            mesh.indexFormat = pos.Length > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.vertices = pos;
            mesh.normals = nrm;
            mesh.uv = uv;
            mesh.subMeshCount = 2;
            mesh.SetTriangles(opaque, 0, false);
            mesh.SetTriangles(glass, 1, false);
            mesh.RecalculateBounds();
        }

        /// <summary>Zet een blok en mesht de getroffen chunks meteen opnieuw.</summary>
        public void SetBlock(int x, int y, int z, byte b)
        {
            foreach (var (cx, cz) in Store.Set(x, y, z, b)) Remesh(cx, cz);
        }

        public void Remesh(int cx, int cz)
        {
            var pad = Store.GetPadded(cx, cz);
            if (pad == null) return;
            ChunkMesher.Build(pad, cx * World.ChunkSize, cz * World.ChunkSize, remeshBuffer);
            Convert(remeshBuffer, out var pos, out var nrm, out var uv);
            Upload(cx, cz, pos, nrm, uv, remeshBuffer.Opaque.ToArray(), remeshBuffer.Glass.ToArray());
        }

        /// <summary>Alle chunks weggooien en opnieuw laden (na het laden van een save).</summary>
        public void ReloadAll()
        {
            foreach (var v in views.Values) { Destroy(v.Mesh); Destroy(v.Go); Store.Remove(v.Cx, v.Cz); }
            views.Clear();
            lastCenter = new Vector2Int(int.MinValue, 0);
        }
    }
}
