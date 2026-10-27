using System.Collections.Generic;
using Khkt.Demo;
using Khkt.Interaction;
using UnityEngine;

namespace Khkt.Assembly
{
    /// <summary>
    /// Hình ảnh của mạng dòng chảy (giai đoạn C4), dùng chung một bể quả cầu nhỏ như ReactionFx (không cần hệ hạt):
    ///  • giọt rơi từ đầu nhỏ giọt / đuôi phễu xuống bình hứng; chảy nhanh (trên 1 ml/s) thành dòng liền;
    ///  • bọt khí: đầu ống ngập trong dung dịch, bình rửa khí, kệ chậu nước (bọt dồn lên ống úp), bình đang sôi;
    ///  • hơi nước bốc lên chỗ hơi thoát ra, khói màu của khí độc ở đầu ống ra;
    ///  • giọt ngưng chảy dọc ống trong của sinh hàn;
    ///  • mực nước trong ống úp trên kệ chậu hạ dần khi khí dồn vào; cột nhiệt kế dâng theo nhiệt độ hơi.
    /// Mạng dòng chảy gọi Request(...) mỗi nhịp (0,1 s); một kênh không được gọi lại quá 0,5 s thì tắt (sôi, chảy cập nhật mỗi 0,3 s).
    /// </summary>
    public class FlowFx : MonoBehaviour
    {
        public static FlowFx Instance { get; private set; }
        public int poolSize = 260;
        public int streamCount = 12;

        public enum Kind { Drop, Bubble, Steam, Fume, Condensate }

        class Channel
        {
            public Kind kind;
            public Vector3 from, to;
            public float rate, expire, acc;
            public Color color;
            public Transform stream;
        }

        class Particle
        {
            public Transform t;
            public Renderer r;
            public Kind kind;
            public Vector3 from, to, vel;
            public float age, life, size, hang;
            public Color color;
        }

        readonly Dictionary<(Object, int), Channel> channels = new Dictionary<(Object, int), Channel>();
        readonly List<(Object, int)> dead = new List<(Object, int)>();
        readonly List<Particle> pool = new List<Particle>();
        readonly List<Particle> live = new List<Particle>();
        readonly List<Transform> streams = new List<Transform>();
        Material bubbleMat, fumeMat;
        MaterialPropertyBlock mpb;
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        // ống úp trên kệ chậu, nhiệt kế
        readonly List<(ApparatusPart trough, LabContainer jar)> jars = new List<(ApparatusPart, LabContainer)>();
        readonly Dictionary<LabContainer, Transform> jarWater = new Dictionary<LabContainer, Transform>();
        readonly List<ApparatusPart> thermometers = new List<ApparatusPart>();
        readonly Dictionary<ApparatusPart, (Transform col, Vector3 pos, Vector3 scale)> columns = new Dictionary<ApparatusPart, (Transform, Vector3, Vector3)>();
        float nextScan;

        public int LiveParticles => live.Count;
        public int ActiveChannels => channels.Count;

        void Awake() => Instance = this;

        void Start()
        {
            mpb = new MaterialPropertyBlock();
            var fx = FindAnyObjectByType<ReactionFx>();
            bubbleMat = fx != null ? fx.bubbleMaterial : null;
            fumeMat = fx != null ? fx.fumeMaterial : null;
            if (bubbleMat == null)
            {
                bubbleMat = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { hideFlags = HideFlags.DontSave };
                fumeMat = bubbleMat;
            }
            // bọt, giọt vẽ sau chất lỏng và thủy tinh trong suốt (không bị khối chất lỏng che); hơi, khói: mờ, không bóng
            bubbleMat = new Material(bubbleMat) { name = "Bọt, giọt (dòng chảy)", renderQueue = 3100, hideFlags = HideFlags.DontSave };
            fumeMat = new Material(fumeMat) { name = "Hơi, khói (dòng chảy)", renderQueue = 3050, hideFlags = HideFlags.DontSave };
            fumeMat.SetFloat("_Smoothness", 0f);
            fumeMat.SetFloat("_Metallic", 0f);
            var sphere = Resources.GetBuiltinResource<Mesh>("New-Sphere.fbx");
            var cyl = Resources.GetBuiltinResource<Mesh>("Cylinder.fbx");
            for (int i = 0; i < poolSize; i++)
            {
                var go = new GameObject("Hạt dòng chảy", typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(transform, false);
                go.GetComponent<MeshFilter>().sharedMesh = sphere;
                var r = go.GetComponent<MeshRenderer>();
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                go.SetActive(false);
                pool.Add(new Particle { t = go.transform, r = r });
            }
            for (int i = 0; i < streamCount; i++)
            {
                var go = new GameObject("Dòng chảy liền", typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(transform, false);
                go.GetComponent<MeshFilter>().sharedMesh = cyl;
                var r = go.GetComponent<MeshRenderer>();
                r.sharedMaterial = bubbleMat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                go.SetActive(false);
                streams.Add(go.transform);
            }
        }

        /// <summary>
        /// Bật / giữ một kênh hiệu ứng trong 0,5 s. key: (id đối tượng nguồn, khe). rate: giọt hoặc bọt mỗi giây
        /// (giọt: ml/s). from → to: điểm đầu / cuối (giọt: tới mặt hứng; bọt: tới mặt chất lỏng; giọt ngưng: dọc ống).
        /// </summary>
        public void Request(Object owner, int slot, Kind kind, Vector3 from, Vector3 to, float rate, Color color)
        {
            if (owner == null || rate <= 0) return;
            var key = (owner, slot);
            if (!channels.TryGetValue(key, out var ch)) channels[key] = ch = new Channel();
            ch.kind = kind;
            ch.from = from;
            ch.to = to;
            ch.rate = rate;
            ch.color = color;
            ch.expire = Time.time + 0.5f;
        }

        /// <summary>Màu chất lỏng để vẽ giọt / dòng (nước gần như trong: xanh nhạt cho dễ thấy).</summary>
        public static Color LiquidColor(LabContainer c)
        {
            if (c == null) return new Color(0.78f, 0.9f, 1f, 0.75f);
            var l = c.Look.Liquid;
            var col = new Color(l.r, l.g, l.b, 0.8f);
            if (l.r > 0.88f && l.g > 0.88f && l.b > 0.88f) col = new Color(0.78f, 0.9f, 1f, 0.75f);
            return col;
        }

        /// <summary>Thời gian CPU trung bình mỗi khung hình (ms) của hiệu ứng, để đo hiệu năng.</summary>
        public static float CostMs { get; private set; }
        readonly System.Diagnostics.Stopwatch watch = new System.Diagnostics.Stopwatch();

        void Update()
        {
            watch.Restart();
            Animate();
            CostMs = Mathf.Lerp(CostMs, (float)watch.Elapsed.TotalMilliseconds, 0.05f);
        }

        void Animate()
        {
            float dt = Time.deltaTime;
            dead.Clear();
            int streamUsed = 0;
            foreach (var kv in channels)
            {
                var ch = kv.Value;
                if (Time.time > ch.expire) { dead.Add(kv.Key); continue; }
                if (ch.kind == Kind.Drop && ch.rate > 1f && streamUsed < streams.Count)
                {
                    // chảy nhanh: dòng liền từ đầu ra tới mặt hứng
                    var s = streams[streamUsed++];
                    if (!s.gameObject.activeSelf) s.gameObject.SetActive(true);
                    var d = ch.to - ch.from;
                    s.position = (ch.from + ch.to) / 2;
                    s.up = d.sqrMagnitude > 1e-8f ? d.normalized : Vector3.up;
                    float w = Mathf.Lerp(0.0025f, 0.005f, Mathf.InverseLerp(1f, 5f, ch.rate));
                    s.localScale = new Vector3(w, d.magnitude / 2, w);
                    Tint(s.GetComponent<Renderer>(), ch.color);
                    continue;
                }
                float perS = ch.kind == Kind.Drop ? Mathf.Min(10f, ch.rate / 0.05f) : Mathf.Min(30f, ch.rate);
                ch.acc += perS * dt;
                int guard = 0;
                while (ch.acc >= 1f && guard++ < 8)
                {
                    ch.acc -= 1f;
                    Spawn(ch);
                }
            }
            foreach (var k in dead) channels.Remove(k);
            for (int i = streamUsed; i < streams.Count; i++) if (streams[i].gameObject.activeSelf) streams[i].gameObject.SetActive(false);

            for (int i = live.Count - 1; i >= 0; i--)
            {
                var p = live[i];
                p.age += dt;
                bool over = p.age >= p.life;
                if (!over)
                {
                    switch (p.kind)
                    {
                        case Kind.Drop:
                            if (p.age < p.hang)  // giọt đọng lớn dần ở đầu ống rồi mới rơi
                            {
                                p.t.localScale = Vector3.one * p.size * (0.45f + 0.55f * p.age / p.hang);
                                break;
                            }
                            p.vel += Physics.gravity * dt;
                            p.t.position += p.vel * dt;
                            over = p.t.position.y <= p.to.y;
                            break;
                        case Kind.Bubble:
                            p.t.position += (Vector3.up * (0.06f + p.size * 12f) + p.vel) * dt;
                            over = p.t.position.y >= p.to.y;
                            break;
                        case Kind.Condensate:
                            p.t.position = Vector3.Lerp(p.from, p.to, Mathf.SmoothStep(0, 1, p.age / p.life));
                            break;
                        default:  // hơi, khói: bốc lên, nở ra, nhạt dần
                            p.t.position += (Vector3.up * 0.07f + p.vel) * dt;
                            p.t.localScale = Vector3.one * p.size * (1f + (p.kind == Kind.Steam ? 1.6f : 2.5f) * p.age / p.life);
                            var c = p.color;
                            c.a *= 1f - p.age / p.life;
                            Tint(p.r, c);
                            break;
                    }
                }
                if (over)
                {
                    p.t.gameObject.SetActive(false);
                    live.RemoveAt(i);
                    pool.Add(p);
                }
            }
        }

        void Spawn(Channel ch)
        {
            if (pool.Count == 0) return;
            var p = pool[pool.Count - 1];
            pool.RemoveAt(pool.Count - 1);
            p.kind = ch.kind;
            p.age = 0;
            p.from = ch.from;
            p.to = ch.to;
            p.color = ch.color;
            p.vel = Vector3.zero;
            Vector2 j = Random.insideUnitCircle;
            switch (ch.kind)
            {
                case Kind.Drop:
                    p.size = 0.0045f;
                    p.hang = Mathf.Clamp(0.8f / Mathf.Max(0.5f, Mathf.Min(10f, ch.rate / 0.05f)), 0.06f, 0.6f);
                    p.life = p.hang + 2f;
                    p.t.position = ch.from;
                    p.r.sharedMaterial = bubbleMat;
                    break;
                case Kind.Bubble:
                    p.size = 0.0025f + Random.value * 0.003f;
                    p.life = 5f;
                    p.t.position = ch.from + new Vector3(j.x, 0, j.y) * 0.003f;
                    p.vel = new Vector3(j.x, 0, j.y) * 0.004f;
                    p.r.sharedMaterial = bubbleMat;
                    p.color = new Color(1f, 1f, 1f, 0.6f);
                    break;
                case Kind.Condensate:
                    p.size = 0.004f;
                    p.life = 1.6f + Random.value * 0.6f;
                    p.t.position = ch.from;
                    p.r.sharedMaterial = bubbleMat;
                    break;
                default:
                    p.size = ch.kind == Kind.Steam ? 0.006f + Random.value * 0.006f : 0.012f + Random.value * 0.01f;
                    p.life = (ch.kind == Kind.Steam ? 1.3f : 1.8f) + Random.value;
                    p.t.position = ch.from + new Vector3(j.x, 0, j.y) * 0.004f;
                    p.vel = new Vector3(j.x, 0, j.y) * 0.025f;
                    p.r.sharedMaterial = fumeMat;
                    break;
            }
            p.t.localScale = Vector3.one * p.size;
            Tint(p.r, p.color);
            p.t.gameObject.SetActive(true);
            live.Add(p);
        }

        void Tint(Renderer r, Color c)
        {
            r.GetPropertyBlock(mpb);
            mpb.SetColor(BaseColorId, c);
            r.SetPropertyBlock(mpb);
        }

        // =====================================================================
        // Ống úp trên kệ chậu, cột nhiệt kế
        // =====================================================================

        void LateUpdate()
        {
            var net = FluidNetwork.Instance;
            if (net == null) return;
            if (Time.time >= nextScan)
            {
                nextScan = Time.time + 1f;
                jars.Clear();
                thermometers.Clear();
                foreach (var part in Khkt.Rendering.ZoneRoot.FindAll<ApparatusPart>())
                {
                    if (part == null || !part.isActiveAndEnabled) continue;
                    if (part.libraryId == "nhiet_ke") thermometers.Add(part);
                    if (part.libraryId != "chau") continue;
                    foreach (var port in part.ports)
                        if (port != null && port.type == PortType.GiaUp && port.Connected != null && port.Connected.Part != null && port.Connected.Part.Container != null)
                            jars.Add((part, port.Connected.Part.Container));
                }
                foreach (var jar in new List<LabContainer>(jarWater.Keys))
                    if (jar == null || !jars.Exists(x => x.jar == jar)) { if (jarWater[jar] != null) jarWater[jar].gameObject.SetActive(false); jarWater.Remove(jar); }
            }
            foreach (var (trough, jar) in jars)
            {
                if (trough == null || jar == null) continue;
                bool water = trough.Container != null && trough.Container.VolumeMl >= 150;
                if (!jarWater.TryGetValue(jar, out var w) || w == null)
                {
                    var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    go.name = "Nước trong ống úp";
                    Destroy(go.GetComponent<Collider>());
                    var r = go.GetComponent<Renderer>();
                    r.sharedMaterial = bubbleMat;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    Tint(r, new Color(0.7f, 0.86f, 1f, 0.45f));
                    w = go.transform;
                    w.SetParent(jar.transform, false);
                    jarWater[jar] = w;
                }
                w.gameObject.SetActive(water);
                if (!water) continue;
                // ống úp ngược: đáy kín (y cục bộ 0) ở trên, miệng (yMouth) ngập trong chậu; khí chiếm phần trên
                float yMouth = jar.mouth != null ? jar.transform.InverseTransformPoint(jar.mouth.position).y : 0.15f;
                float full = jar.capacityMl * 0.97f;
                float frac = full > 0 ? Mathf.Clamp01((float)(net.CollectedMl(jar) / full)) : 0;
                float y0 = Mathf.Lerp(0.004f, yMouth, frac), y1 = yMouth - 0.002f;
                float rad = 0.0075f;
                if (jar.fill != null && jar.fill.innerProfile != null && jar.fill.innerProfile.Length > 1) rad = jar.fill.innerProfile[jar.fill.innerProfile.Length - 1].x * 0.92f;
                if (y1 - y0 < 0.001f) { w.gameObject.SetActive(false); continue; }
                w.localPosition = new Vector3(0, (y0 + y1) / 2, 0);
                w.localRotation = Quaternion.identity;
                w.localScale = new Vector3(rad * 2, (y1 - y0) / 2, rad * 2);
            }

            foreach (var th in thermometers)
            {
                if (th == null) continue;
                if (!columns.TryGetValue(th, out var col))
                {
                    var t = th.transform.Find("Cột thủy ngân");
                    if (t == null) continue;
                    columns[th] = col = (t, t.localPosition, t.localScale);
                }
                if (col.col == null) continue;
                float tc = net.Thermometers.TryGetValue(th, out var v) ? v : 25f;
                // thang 0–250 °C dọc thân nhiệt kế: từ bầu (y = -0.035) lên tới khoảng 0,225 m
                const float yBulb = -0.035f, span = 0.26f;
                float len0 = col.scale.y * 2f;  // cột dựng sẵn dài 0,065 m
                float len = Mathf.Clamp((tc + 10f) / 260f * span, 0.004f, span);
                col.col.localPosition = new Vector3(col.pos.x, yBulb + len / 2, col.pos.z);
                col.col.localScale = new Vector3(col.scale.x, col.scale.y * len / Mathf.Max(1e-4f, len0), col.scale.z);
            }
        }

        /// <summary>Đầu cột nhiệt kế (tọa độ cục bộ của nhiệt kế, m), để kiểm thử. Hình trụ cao 2 đơn vị nên nửa chiều dài = scale.y.</summary>
        public float ColumnTopLocal(ApparatusPart th) => columns.TryGetValue(th, out var c) && c.col != null ? c.col.localPosition.y + c.col.localScale.y : float.NaN;

        /// <summary>Phần ống úp còn nước (0..1), để kiểm thử.</summary>
        public float JarWaterFraction(LabContainer jar)
        {
            if (!jarWater.TryGetValue(jar, out var w) || w == null || !w.gameObject.activeSelf) return 0;
            float yMouth = jar.mouth != null ? jar.transform.InverseTransformPoint(jar.mouth.position).y : 0.15f;
            return Mathf.Clamp01(w.localScale.y * 2 / Mathf.Max(1e-4f, yMouth));
        }
    }
}
