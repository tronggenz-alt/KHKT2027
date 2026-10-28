// Cảnh 3 – Nhận biết ion: giá ống nghiệm (bấm chọn ống), dung dịch đổi màu, kết tủa lắng, bọt khí, giấy quỳ, đèn cồn thử màu ngọn lửa.
using System.Collections.Generic;
using UnityEngine;

namespace PhongThiNghiem
{
    public class CanhNhanBiet : CanhCoSo
    {
        class Ong
        {
            public int so;
            public Transform g;
            public Material dd, ketTua, quy;
            public Transform dong, quyT;
            public ParticleSystem bot, roi;
            public TextMesh nhan;
            public float luong;
        }

        readonly List<Ong> _ong = new List<Ong>();
        readonly Vector2[] _pOng = LabHinh.OngNghiem(0.0115f, 0.15f);
        ParticleSystem _lua;
        TextMesh _nhanLua;
        int _soOng;

        protected override void DungCanh(string tt)
        {
            GocViTri = new Vector3(0.03f, 0.3f, 0.62f);
            GocDich = new Vector3(0.03f, 0.09f, -0.03f);
            GocGan = 0.2f; GocXa = 1.6f;
            var s = JsonUtility.FromJson<TTNhanBiet>(tt);
            int n = s?.ong?.Length ?? 0;
            _soOng = n;
            float cach = 0.075f, x0 = -(n - 1) * cach / 2f;
            var go = LabVatLieu.Dac(new Color(0.84f, 0.76f, 0.65f), 0.2f);
            Khoi(PrimitiveType.Cube, new Vector3(0f, 0.075f, 0f), new Vector3(n * cach + 0.08f, 0.014f, 0.08f), go);
            Khoi(PrimitiveType.Cube, new Vector3(x0 - 0.05f, 0.037f, 0f), new Vector3(0.012f, 0.075f, 0.07f), go);
            Khoi(PrimitiveType.Cube, new Vector3(-x0 + 0.05f, 0.037f, 0f), new Vector3(0.012f, 0.075f, 0.07f), go);
            for (int i = 0; i < n; i++)
            {
                var o = new Ong { so = s.ong[i].so };
                o.g = Moi("Ong " + o.so).transform;
                o.g.localPosition = new Vector3(x0 + i * cach, 0.012f, 0f);
                var tt2 = LuoiGO("Thuy tinh", LabHinh.Lathe(_pOng, 40), LabVatLieu.ThuyTinh(), Vector3.zero, o.g, true);
                var chon = tt2.AddComponent<LabNhap>();
                chon.chonLoai = "ong";
                chon.chonSo = o.so;
                o.dd = LabVatLieu.Long(new Color(0.84f, 0.92f, 0.97f, 0.42f));
                LuoiGO("Dung dich", LabHinh.ChatLong(_pOng, 0.075f), o.dd, Vector3.zero, o.g);
                o.ketTua = LabVatLieu.Dac(Color.white, 0.1f);
                o.dong = Khoi(PrimitiveType.Sphere, new Vector3(0f, 0.006f, 0f), new Vector3(0.018f, 0.0001f, 0.018f), o.ketTua, o.g).transform;
                o.bot = Hat(o.g, new Vector3(0f, 0.012f, 0f), new Color(1f, 1f, 1f, 0.55f), 0.0035f, 0.06f, 1.1f, 0.006f, 40);
                o.roi = Hat(o.g, new Vector3(0f, 0.07f, 0f), Color.white, 0.003f, -0.02f, 1.6f, 0.006f, 60, 0.05f);
                o.quy = LabVatLieu.Dac(new Color(0.55f, 0.36f, 0.96f));
                o.quyT = Khoi(PrimitiveType.Cube, new Vector3(0.004f, 0.178f, 0.012f), new Vector3(0.008f, 0.05f, 0.0005f), o.quy, o.g).transform;
                o.quyT.gameObject.SetActive(false);
                o.nhan = Chu(o.so.ToString(), new Vector3(0f, 0.2f, 0f), 0.0025f, o.g);
                _ong.Add(o);
            }
            if (s?.thuoc_thu != null)
                for (int i = 0; i < s.thuoc_thu.Length; i++)
                {
                    float x = -0.24f + i * 0.08f;
                    LuoiGO("Lo thuoc thu", LabHinh.Lathe(LabHinh.LoThuoc(0.022f, 0.085f, 0.0085f), 48),
                        LabVatLieu.ThuyTinh(), new Vector3(x, 0f, -0.16f));
                    Khoi(PrimitiveType.Sphere, new Vector3(x, 0.1f, -0.16f), Vector3.one * 0.022f, LabVatLieu.Dac(new Color(0.12f, 0.16f, 0.23f)));
                    Chu(s.thuoc_thu[i].ten, new Vector3(x, 0.135f, -0.16f));
                }
            LuoiGO("Den con", LabHinh.Lathe(LabHinh.DenCon(), 64),
                LabVatLieu.ThuyTinh(), new Vector3(0.3f, 0f, 0.1f));
            _lua = Hat(Goc, new Vector3(0.3f, 0.095f, 0.1f), new Color(1f, 0.72f, 0.25f, 0.9f), 0.03f, 0.08f, 0.35f, 0.004f, 40);
            DatPhat(_lua, 60f);
            NgonLua.GanVao(_lua);   // màu vỏ tự đồng bộ từ _lua.main.startColor (thử ngọn lửa theo ion)
            _nhanLua = Chu("", new Vector3(0.3f, 0.215f, 0.1f));
        }

        public override void CapNhat(string json)
        {
            var s = JsonUtility.FromJson<TTNhanBiet>(json);
            if (s?.ong == null) return;
            if (s.ong.Length != _soOng) return;   // số ống đổi → trang web mở lại cảnh (mo_thi_nghiem)
            for (int i = 0; i < s.ong.Length && i < _ong.Count; i++)
            {
                var o = _ong[i];
                var t = s.ong[i];
                Color c = t.mau_dd != null && CoMau(t.mau_dd.rgb) ? Mau(t.mau_dd.rgb, 0.72f) : new Color(0.84f, 0.92f, 0.97f, 0.42f);
                LabVatLieu.DatMau(o.dd, c);
                float luong = t.co_ket_tua && t.ket_tua != null ? t.ket_tua.luong : 0f;
                if (luong > o.luong + 0.01f && o.roi != null) { var m = o.roi.main; m.startColor = Mau(t.ket_tua.rgb); o.roi.Emit(Mathf.RoundToInt(30 * (luong - o.luong))); }
                o.luong = luong;
                o.dong.gameObject.SetActive(luong > 0.01f);
                if (luong > 0.01f)
                {
                    LabVatLieu.DatMau(o.ketTua, Mau(t.ket_tua.rgb));
                    o.dong.localScale = new Vector3(0.018f, 0.004f + 0.012f * luong, 0.018f);
                }
                DatPhat(o.bot, t.co_khi ? 30f : 0f);
                o.quyT.gameObject.SetActive(!string.IsNullOrEmpty(t.quy));
                if (!string.IsNullOrEmpty(t.quy))
                    LabVatLieu.DatMau(o.quy, t.quy == "xanh" ? new Color(0.15f, 0.39f, 0.92f) : t.quy == "đỏ" ? new Color(0.86f, 0.15f, 0.15f) : new Color(0.55f, 0.36f, 0.96f));
                o.nhan.text = KyTu(string.IsNullOrEmpty(t.ten_ion) ? t.so.ToString() : t.so + "·" + t.ten_ion);
            }
            bool coLua = s.ngon_lua != null && !string.IsNullOrEmpty(s.ngon_lua.kim_loai);
            var main = _lua.main;
            main.startColor = coLua ? Mau(s.ngon_lua.rgb, 0.95f) : new Color(1f, 0.72f, 0.25f, 0.9f);
            _nhanLua.text = KyTu(coLua ? "Ngọn lửa " + s.ngon_lua.mau : "");
        }

        public override void HieuUng(HieuUngDL e)
        {
            if (e.loai != "nho") return;
            foreach (var o in _ong) if (o.so == e.so) o.g.localPosition += new Vector3(0f, 0.0001f, 0f);
        }
    }
}
