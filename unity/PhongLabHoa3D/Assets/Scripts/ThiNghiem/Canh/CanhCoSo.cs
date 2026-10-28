// Lớp cơ sở cho 5 cảnh thí nghiệm: tiện ích dựng đối tượng, nhãn chữ, hạt (bọt khí / kết tủa / ngọn lửa).
using System.Collections.Generic;
using UnityEngine;

namespace PhongThiNghiem
{
    /// <summary>Gắn vào đối tượng bấm được: gửi thao tác (lenh + thamSo JSON) hoặc chọn (chonLoai + chonSo) về trang web.</summary>
    public class LabNhap : MonoBehaviour
    {
        public string lenh;
        public string thamSo = "{}";
        public string chonLoai;
        public int chonSo;
    }

    /// <summary>Nhãn chữ luôn quay về phía camera.</summary>
    public class LabNhinCamera : MonoBehaviour
    {
        void LateUpdate()
        {
            var cam = Camera.main;
            if (cam != null) transform.rotation = cam.transform.rotation;
        }
    }

    public abstract class CanhCoSo
    {
        protected Transform Goc;
        protected ICauNoiLoi Lab;
        public Vector3 GocViTri = new Vector3(0f, 0.6f, 1.2f), GocDich = new Vector3(0f, 0.2f, 0f);
        public float GocGan = 0.25f, GocXa = 4f;
        static Font _font;

        public void Dung(Transform goc, ICauNoiLoi lab, string tt)
        {
            Goc = goc;
            Lab = lab;
            DungCanh(tt);
        }

        protected abstract void DungCanh(string tt);
        public abstract void CapNhat(string json);
        public virtual void HieuUng(HieuUngDL e) { }
        public virtual void Tick(float dt) { }
        public virtual void DoiMau() { }
        public virtual void Huy() { }

        // ---------------------------------------------------------------- dựng đối tượng
        protected GameObject Moi(string ten, Transform cha = null)
        {
            var g = new GameObject(ten);
            g.transform.SetParent(cha != null ? cha : Goc, false);
            return g;
        }

        protected GameObject Khoi(PrimitiveType loai, Vector3 vt, Vector3 co, Material vl, Transform cha = null, bool vaCham = false)
        {
            var g = GameObject.CreatePrimitive(loai);
            g.transform.SetParent(cha != null ? cha : Goc, false);
            g.transform.localPosition = vt;
            g.transform.localScale = co;
            g.GetComponent<Renderer>().material = vl;
            if (!vaCham)
            {
                var c = g.GetComponent<Collider>();
                if (c != null) Object.Destroy(c);
            }
            return g;
        }

        /// <summary>Hình trụ đứng từ đáy y0, cao h, bán kính r (primitive Cylinder cao 2, bán kính 0,5).</summary>
        protected GameObject Tru(Vector3 day, float r, float h, Material vl, Transform cha = null, bool vaCham = false) =>
            Khoi(PrimitiveType.Cylinder, day + new Vector3(0f, h / 2f, 0f), new Vector3(2f * r, h / 2f, 2f * r), vl, cha, vaCham);

        protected GameObject LuoiGO(string ten, Mesh luoi, Material vl, Vector3 vt, Transform cha = null, bool vaCham = false)
        {
            var g = Moi(ten, cha);
            g.transform.localPosition = vt;
            g.AddComponent<MeshFilter>().sharedMesh = luoi;
            var mr = g.AddComponent<MeshRenderer>();
            if (luoi.subMeshCount == 2) mr.sharedMaterials = LabVatLieu.HaiLuot(vl);   // thuỷ tinh / chất lỏng: trong suốt vẽ hai lượt
            else mr.material = vl;
            if (vaCham) g.AddComponent<MeshCollider>().sharedMesh = luoi;
            return g;
        }

        protected TextMesh Chu(string chu, Vector3 vt, float co = 0.0025f, Transform cha = null)
        {
            if (_font == null) _font = LabFont.Get();
            var g = Moi("Chu", cha);
            g.transform.localPosition = vt;
            g.transform.localScale = new Vector3(co, co, co);   // dấu x dương: app này KHÔNG lật phép chiếu (dự án Unity cũ có lật nên cần -co) → -co làm chữ bị ngược
            var tm = g.AddComponent<TextMesh>();
            tm.font = _font;
            tm.fontSize = 48;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = Lab.Toi ? new Color(0.9f, 0.95f, 1f) : new Color(0.06f, 0.17f, 0.28f);
            tm.text = KyTu(chu);
            g.GetComponent<MeshRenderer>().material = _font.material;
            g.AddComponent<LabNhinCamera>();
            return tm;
        }

        static readonly string[,] _thay =
        {
            { "₀", "0" }, { "₁", "1" }, { "₂", "2" }, { "₃", "3" }, { "₄", "4" }, { "₅", "5" }, { "₆", "6" }, { "₇", "7" }, { "₈", "8" }, { "₉", "9" },
            { "⁺", "+" }, { "⁻", "-" }, { "²", "2" }, { "³", "3" }, { "−", "-" }, { "–", "-" }, { "✓", "√" }, { "→", "->" }, { "←", "<-" },
        };

        /// <summary>Font LegacyRuntime có thể thiếu chỉ số dưới / trên → đổi sang ký tự thường (CuSO₄ → CuSO4).</summary>
        public static string KyTu(string s)
        {
            if (string.IsNullOrEmpty(s)) return s ?? "";
            for (int i = 0; i < _thay.GetLength(0); i++) s = s.Replace(_thay[i, 0], _thay[i, 1]);
            return s;
        }

        protected static Color Mau(float[] rgb, float a = 1f) =>
            rgb == null || rgb.Length < 3 ? new Color(0.84f, 0.92f, 0.97f, a) : new Color(rgb[0] / 255f, rgb[1] / 255f, rgb[2] / 255f, a);

        protected static bool CoMau(float[] rgb) => rgb != null && rgb.Length >= 3;

        /// <summary>Hệ hạt: bọt khí (bay lên), kết tủa (rơi xuống), ngọn lửa. tocDo âm = rơi.</summary>
        protected ParticleSystem Hat(Transform cha, Vector3 vt, Color mau, float co, float tocDo, float tuoi, float banKinh, int toiDa, float trongLuc = 0f)
        {
            var g = Moi("Hat", cha);
            g.transform.localPosition = vt;
            g.transform.localRotation = Quaternion.Euler(tocDo >= 0f ? -90f : 90f, 0f, 0f);
            var ps = g.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 1f;
            main.loop = true;
            main.startLifetime = tuoi;
            main.startSpeed = Mathf.Abs(tocDo);
            main.startSize = co;
            main.startColor = mau;
            main.maxParticles = toiDa;
            main.gravityModifier = trongLuc;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            var em = ps.emission;
            em.rateOverTime = 0f;
            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Cone;
            sh.angle = 4f;
            sh.radius = banKinh;
            var rd = g.GetComponent<ParticleSystemRenderer>();
            rd.material = LabVatLieu.PhatSang(mau);
            ps.Play();
            return ps;
        }

        protected static void DatPhat(ParticleSystem ps, float moiGiay)
        {
            if (ps == null) return;
            var em = ps.emission;
            em.rateOverTime = moiGiay;
        }

        protected Color MauNhan => Lab.MauNhan;
    }

    /// <summary>Chấm sáng chạy dọc một đường (dòng electron / ion).</summary>
    public class DongHat
    {
        readonly List<Transform> _cham = new List<Transform>();
        readonly Vector3[] _duong;
        float _pha;
        public float TocDo = 0.16f;
        public bool Bat = true, Nguoc;

        public DongHat(Transform cha, Vector3[] duong, int so, Color mau, float r)
        {
            _duong = duong;
            var vl = LabVatLieu.PhatSang(mau);
            for (int i = 0; i < so; i++)
            {
                var g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Object.Destroy(g.GetComponent<Collider>());
                g.transform.SetParent(cha, false);
                g.transform.localScale = Vector3.one * r * 2f;
                g.GetComponent<Renderer>().material = vl;
                _cham.Add(g.transform);
            }
        }

        public void Tick(float dt)
        {
            for (int i = 0; i < _cham.Count; i++) _cham[i].gameObject.SetActive(Bat);
            if (!Bat) return;
            _pha = (_pha + dt * TocDo) % 1f;
            for (int i = 0; i < _cham.Count; i++)
            {
                float u = ((float)i / _cham.Count + _pha) % 1f;
                if (Nguoc) u = 1f - u;
                _cham[i].localPosition = LabHinh.CatmullRom(_duong, u);
            }
        }
    }
}
