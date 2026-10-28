// Ngọn lửa đèn cồn / đèn Bunsen chân thực: hai lớp giọt lửa (lõi xanh + vỏ) dùng shader PhongLab/NgonLua, tim đèn, đèn điểm nhấp nháy.
// Gắn vào hệ hạt ngọn lửa CŨ do Hat() tạo (CanhAnToan.DenCon, CanhNhanBiet): hạt cũ (tấm vuông phẳng) bị tắt hiển thị nhưng vẫn là nơi lưu MÀU –
// màu vỏ ngọn lửa tự đồng bộ từ main.startColor nên thử ngọn lửa theo ion (Na vàng, Cu xanh lục, Li đỏ…) vẫn chạy như trước.
using UnityEngine;

namespace PhongThiNghiem
{
    public static class NgonLua
    {
        static Mesh _giot;

        /// <summary>Lưới giọt lửa cao 1, bán kính tối đa 0,5 (UV.y chạy 0 ở chân → 1 ở chóp, UV.x quanh trục).</summary>
        static Mesh Giot()
        {
            if (_giot != null) return _giot;
            var ctrl = new System.Collections.Generic.List<Vector3>
            {
                LabHinh.T(0f, 0f), LabHinh.T(0.30f, 0.025f), LabHinh.T(0.46f, 0.12f), LabHinh.T(0.50f, 0.25f), LabHinh.T(0.43f, 0.45f),
                LabHinh.T(0.28f, 0.68f), LabHinh.T(0.12f, 0.87f), LabHinh.T(0f, 1f),
            };
            _giot = LabHinh.LatheMem(ctrl, 24, 6);
            _giot.name = "GiotLua";
            return _giot;
        }

        static Material VatLieu(Color loi, Color ngoai, float cuong, float doCao)
        {
            var goc = Resources.Load<Material>("PTN/NgonLua");
            Material m;
            if (goc != null) m = new Material(goc);
            else
            {
                var sh = Shader.Find("PhongLab/NgonLua");
                m = new Material(sh != null ? sh : Shader.Find("Universal Render Pipeline/Unlit"));
            }
            m.SetColor("_MauTrong", loi);
            m.SetColor("_MauNgoai", ngoai);
            m.SetFloat("_CuongDo", cuong);
            m.SetFloat("_DoCao", doCao);
            return m;
        }

        /// <summary>Thay ngọn lửa hạt cũ `ps` bằng ngọn lửa thật; trả về bộ điều khiển (có thể bỏ qua).</summary>
        public static NgonLuaDieuKhien GanVao(ParticleSystem ps, float tiLe = 1f)
        {
            if (ps == null) return null;
            var cha = ps.transform.parent;
            var goc = new GameObject("NgonLuaThat").transform;
            goc.SetParent(cha, false);
            goc.localPosition = ps.transform.localPosition;
            ps.GetComponent<ParticleSystemRenderer>().enabled = false;   // bỏ tấm vuông phẳng, giữ hệ hạt làm nơi lưu màu

            float cao = 0.062f * tiLe;
            var cl = ps.main.startColor.color;
            var ngoai = Mau(cl);
            var vlNgoai = VatLieu(new Color(0.35f, 0.5f, 1f), ngoai, 0.7f, cao);
            var vlLoi = VatLieu(new Color(0.2f, 0.35f, 1f), new Color(0.45f, 0.7f, 1f), 0.9f, cao * 0.5f);

            // tim đèn (sợi bấc sạm) ở chân ngọn lửa
            var bac = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            bac.name = "TimDen";
            Object.DestroyImmediate(bac.GetComponent<Collider>());
            bac.transform.SetParent(goc, false);
            bac.transform.localPosition = new Vector3(0f, -0.004f, 0f);
            bac.transform.localScale = new Vector3(0.0075f, 0.008f, 0.0075f);
            bac.GetComponent<Renderer>().sharedMaterial = LabVatLieu.Dac(new Color(0.78f, 0.7f, 0.55f), 0.1f);
            bac.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var o = Tao(goc, "VoLua", vlNgoai, new Vector3(0.036f * tiLe, cao, 0.036f * tiLe));
            var l = Tao(goc, "LoiLua", vlLoi, new Vector3(0.017f * tiLe, cao * 0.48f, 0.017f * tiLe));

            var den = new GameObject("DenLua").AddComponent<Light>();
            den.transform.SetParent(goc, false);
            den.transform.localPosition = new Vector3(0f, cao * 2.4f, 0f);
            den.type = LightType.Point;
            den.color = Color.Lerp(new Color(1f, 0.62f, 0.28f), ngoai, 0.4f);
            den.range = 0.9f;
            den.intensity = 0.12f;
            den.shadows = LightShadows.None;

            var dk = goc.gameObject.AddComponent<NgonLuaDieuKhien>();
            dk.Khoi(ps, vlNgoai, o.transform, l.transform, den, new Vector3(0.036f * tiLe, cao, 0.036f * tiLe), new Vector3(0.017f * tiLe, cao * 0.48f, 0.017f * tiLe));
            return dk;
        }

        static Color Mau(Color c)
        {
            float h, s, v;
            Color.RGBToHSV(c, out h, out s, out v);
            return Color.HSVToRGB(h, Mathf.Clamp01(s * 1.05f), 1f);
        }

        static GameObject Tao(Transform cha, string ten, Material m, Vector3 tiLe)
        {
            var g = new GameObject(ten);
            g.transform.SetParent(cha, false);
            g.transform.localScale = tiLe;
            g.AddComponent<MeshFilter>().sharedMesh = Giot();
            var r = g.AddComponent<MeshRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return g;
        }
    }

    /// <summary>Đồng bộ màu vỏ ngọn lửa từ hệ hạt cũ, làm ngọn lửa phập phồng và đèn nhấp nháy theo nhiễu.</summary>
    public class NgonLuaDieuKhien : MonoBehaviour
    {
        ParticleSystem _ps;
        Material _vlNgoai;
        Transform _ngoai, _loi;
        Light _den;
        Vector3 _tlNgoai, _tlLoi;
        float _pha;
        Color _mauDen;

        public void Khoi(ParticleSystem ps, Material vlNgoai, Transform ngoai, Transform loi, Light den, Vector3 tlNgoai, Vector3 tlLoi)
        {
            _ps = ps; _vlNgoai = vlNgoai; _ngoai = ngoai; _loi = loi; _den = den; _tlNgoai = tlNgoai; _tlLoi = tlLoi;
            _pha = Random.value * 50f;
        }

        void Update()
        {
            if (_ps == null) return;
            // màu vỏ = màu hạt cũ (thử ngọn lửa theo ion đổi startColor) – giữ độ sáng tối đa
            Color c = _ps.main.startColor.color;
            Color.RGBToHSV(c, out float h, out float s, out _);
            Color vo = Color.HSVToRGB(h, Mathf.Clamp01(s * 1.05f), 1f);
            _vlNgoai.SetColor("_MauNgoai", vo);
            float t = Time.time + _pha;
            float phap = 0.94f + 0.13f * Mathf.PerlinNoise(t * 6.5f, 0.3f);
            float rong = 0.96f + 0.08f * Mathf.PerlinNoise(0.7f, t * 5.2f);
            _ngoai.localScale = new Vector3(_tlNgoai.x * rong, _tlNgoai.y * phap, _tlNgoai.z * rong);
            _loi.localScale = new Vector3(_tlLoi.x * rong, _tlLoi.y * phap, _tlLoi.z * rong);
            if (_den != null)
            {
                _den.intensity = 0.09f + 0.06f * Mathf.PerlinNoise(t * 9f, 1.7f);
                _den.color = Color.Lerp(new Color(1f, 0.62f, 0.28f), vo, 0.4f);
            }
        }
    }
}
