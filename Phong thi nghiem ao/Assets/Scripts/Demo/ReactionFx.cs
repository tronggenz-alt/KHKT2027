using System.Collections.Generic;
using Khkt.Interaction;
using UnityEngine;

namespace Khkt.Demo
{
    /// <summary>
    /// Bọt khí nổi lên trong dung dịch khi có khí thoát ra, và làn khí màu bốc lên miệng bình
    /// (NO₂ nâu đỏ, Cl₂ vàng lục, hơi brom nâu...). Dùng chung một bể quả cầu nhỏ, không cần hệ hạt.
    /// </summary>
    public class ReactionFx : MonoBehaviour
    {
        public Material bubbleMaterial;
        public Material fumeMaterial;
        public int poolSize = 220;

        class Particle
        {
            public Transform t;
            public Renderer r;
            public LabContainer owner;
            public bool fume;
            public float age, life, speed, size;
            public Vector3 drift;
        }

        readonly List<Particle> pool = new List<Particle>();
        readonly List<Particle> live = new List<Particle>();
        readonly Dictionary<LabContainer, float> emitAccum = new Dictionary<LabContainer, float>();
        readonly List<LabContainer> containers = new List<LabContainer>();
        MaterialPropertyBlock mpb;
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        float lastScan = -10f;

        void Start()
        {
            mpb = new MaterialPropertyBlock();
            var mesh = Resources.GetBuiltinResource<Mesh>("New-Sphere.fbx");
            for (int i = 0; i < poolSize; i++)
            {
                var go = new GameObject("Hạt hiệu ứng", typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(transform, false);
                go.GetComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.GetComponent<MeshRenderer>();
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                go.SetActive(false);
                pool.Add(new Particle { t = go.transform, r = r });
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (Time.time - lastScan > 1.5f)
            {
                lastScan = Time.time;
                containers.Clear();
                foreach (var c in Khkt.Rendering.ZoneRoot.FindAll<LabContainer>()) if (c.reactive) containers.Add(c);
            }
            foreach (var c in containers)
            {
                if (c == null || c.GasRate <= 0.0004f) continue;
                emitAccum.TryGetValue(c, out float acc);
                acc += Mathf.Min(40f, c.GasRate * 2500f) * dt;
                while (acc >= 1f)
                {
                    acc -= 1f;
                    if (c.VolumeMl > 0.5f) Spawn(c, false);
                    // miệng bình đã đậy nút / nối ống: khí đi theo ống, khói hiện ở đầu ống ra (FlowFx)
                    if (FumeColor(c.GasId, out _) && Random.value < 0.35f && !Khkt.Assembly.FluidNetwork.MouthClosed(c)) Spawn(c, true);
                }
                emitAccum[c] = acc;
            }

            for (int i = live.Count - 1; i >= 0; i--)
            {
                var p = live[i];
                p.age += dt;
                bool dead = p.age >= p.life || p.owner == null;
                if (!dead)
                {
                    if (p.fume)
                    {
                        p.t.position += (Vector3.up * p.speed + p.drift) * dt;
                        float s = p.size * (1f + 2.5f * p.age / p.life);
                        p.t.localScale = Vector3.one * s;
                        FumeColor(p.owner.GasId, out Color col);
                        col.a *= 1f - p.age / p.life;
                        Tint(p, col);
                    }
                    else
                    {
                        p.t.position += Vector3.up * p.speed * dt;
                        float top = LiquidTopY(p.owner);
                        if (p.t.position.y >= top) dead = true;
                    }
                }
                if (dead)
                {
                    p.t.gameObject.SetActive(false);
                    p.owner = null;
                    live.RemoveAt(i);
                    pool.Add(p);
                }
            }
        }

        void Spawn(LabContainer c, bool fume)
        {
            if (pool.Count == 0) return;
            var p = pool[pool.Count - 1];
            pool.RemoveAt(pool.Count - 1);
            p.owner = c;
            p.fume = fume;
            p.age = 0;
            Vector2 d = Random.insideUnitCircle * c.bottomRadiusM * 0.75f;
            if (fume)
            {
                Vector3 m = c.mouth != null ? c.mouth.position : c.transform.position + Vector3.up * 0.1f;
                p.t.position = m + new Vector3(d.x * 0.4f, 0.005f, d.y * 0.4f);
                p.life = 2.2f + Random.value;
                p.speed = 0.06f + Random.value * 0.05f;
                p.size = 0.012f + Random.value * 0.01f;
                p.drift = new Vector3(Random.value - 0.5f, 0, Random.value - 0.5f) * 0.03f;
                p.r.sharedMaterial = fumeMaterial;
            }
            else
            {
                float bottom = c.transform.position.y + 0.004f;
                p.t.position = new Vector3(c.transform.position.x + d.x, bottom, c.transform.position.z + d.y);
                p.life = 4f;
                p.speed = 0.05f + Random.value * 0.06f;
                p.size = 0.0015f + Random.value * 0.0025f;
                p.t.localScale = Vector3.one * p.size;
                p.r.sharedMaterial = bubbleMaterial;
                Tint(p, new Color(1f, 1f, 1f, 0.6f));
            }
            p.t.gameObject.SetActive(true);
            live.Add(p);
        }

        static float LiquidTopY(LabContainer c)
        {
            if (c.fill != null) return c.fill.transform.TransformPoint(new Vector3(0, c.fill.LevelM, 0)).y;
            return (c.mouth != null ? c.mouth.position.y : c.transform.position.y + 0.08f) - 0.01f;
        }

        void Tint(Particle p, Color c)
        {
            p.r.GetPropertyBlock(mpb);
            mpb.SetColor(BaseColorId, c);
            p.r.SetPropertyBlock(mpb);
        }

        /// <summary>Khí có màu hoặc khói thấy được.</summary>
        public static bool FumeColor(string gas, out Color c)
        {
            switch (gas)
            {
                case "NO2": case "NO": c = new Color(0.62f, 0.25f, 0.1f, 0.55f); return true;
                case "Cl2": c = new Color(0.75f, 0.85f, 0.35f, 0.45f); return true;
                case "SO2": case "HCN": case "H2S": case "NH3": c = new Color(0.92f, 0.94f, 0.95f, 0.18f); return true;
                case "C2H2": c = new Color(0.9f, 0.9f, 0.92f, 0.12f); return true;
                default: c = Color.clear; return false;
            }
        }
    }
}
