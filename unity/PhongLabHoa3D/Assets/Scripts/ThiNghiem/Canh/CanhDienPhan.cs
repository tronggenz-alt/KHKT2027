// Cảnh 5 – Điện phân dung dịch: bình điện phân (màng ngăn với NaCl), điện cực, lớp Cu bám cathode / anode Cu tan, bọt khí,
// màu xanh Cu²⁺ nhạt dần, phenolphthalein hồng ở cathode, quỳ ẩm mất màu ở anode, nguồn một chiều có màn hình I và t.
using UnityEngine;

namespace PhongThiNghiem
{
    public class CanhDienPhan : CanhCoSo
    {
        Material _dd, _hong, _quy, _vlCa, _vlAn, _vlPhu;
        Transform _mang, _hongT, _quyT, _thanhAn, _phu;
        ParticleSystem _botC, _botA;
        TextMesh _I, _t, _nhanCa, _nhanAn, _kq;
        DongHat _e1, _e2;

        protected override void DungCanh(string tt)
        {
            GocViTri = new Vector3(0.18f, 0.45f, 0.72f);
            GocDich = new Vector3(0f, 0.12f, -0.08f);
            GocGan = 0.3f; GocXa = 1.8f;
            var p = LabHinh.Coc(0.11f, 0.17f);
            LuoiGO("Binh dien phan", LabHinh.Lathe(p, 64), LabVatLieu.ThuyTinh(), Vector3.zero);
            _dd = LabVatLieu.Long(new Color(0.27f, 0.59f, 0.9f, 0.6f));
            LuoiGO("Dung dich", LabHinh.ChatLong(p, 0.12f), _dd, Vector3.zero);
            _mang = Khoi(PrimitiveType.Cube, new Vector3(0f, 0.07f, 0f), new Vector3(0.002f, 0.12f, 0.2f), LabVatLieu.Long(new Color(0.95f, 0.96f, 0.98f, 0.45f))).transform;
            _hong = LabVatLieu.Long(new Color(0.91f, 0.24f, 0.55f, 0f));
            _hongT = Khoi(PrimitiveType.Cube, new Vector3(-0.05f, 0.062f, 0f), new Vector3(0.1f, 0.1f, 0.19f), _hong).transform;
            _vlCa = LabVatLieu.KimLoai(new Color(0.27f, 0.29f, 0.31f));
            _vlAn = LabVatLieu.KimLoai(new Color(0.27f, 0.29f, 0.31f));
            Khoi(PrimitiveType.Cube, new Vector3(-0.055f, 0.13f, 0f), new Vector3(0.03f, 0.22f, 0.012f), _vlCa);
            _thanhAn = Khoi(PrimitiveType.Cube, new Vector3(0.055f, 0.13f, 0f), new Vector3(0.03f, 0.22f, 0.012f), _vlAn).transform;
            _vlPhu = LabVatLieu.KimLoai(new Color(0.77f, 0.44f, 0.25f));
            _phu = Khoi(PrimitiveType.Cube, new Vector3(-0.055f, 0.07f, 0f), new Vector3(0.032f, 0.1f, 0.013f), _vlPhu).transform;
            _botC = Hat(Goc, new Vector3(-0.055f, 0.03f, 0f), new Color(1f, 1f, 1f, 0.55f), 0.004f, 0.07f, 1.3f, 0.012f, 60);
            _botA = Hat(Goc, new Vector3(0.055f, 0.03f, 0f), new Color(1f, 1f, 1f, 0.55f), 0.004f, 0.07f, 1.3f, 0.012f, 60);
            _quy = LabVatLieu.Dac(new Color(0.55f, 0.36f, 0.96f));
            _quyT = Khoi(PrimitiveType.Cube, new Vector3(0.075f, 0.2f, 0.02f), new Vector3(0.012f, 0.06f, 0.0005f), _quy).transform;
            Khoi(PrimitiveType.Cube, new Vector3(0f, 0.06f, -0.32f), new Vector3(0.22f, 0.12f, 0.12f), LabVatLieu.Dac(new Color(0.12f, 0.16f, 0.23f), 0.5f));
            _I = Chu("2,0 A", new Vector3(-0.05f, 0.08f, -0.258f), 0.003f);
            _t = Chu("0 s", new Vector3(0.05f, 0.08f, -0.258f), 0.003f);
            _I.color = new Color(0.49f, 0.94f, 0.76f);
            _t.color = new Color(0.98f, 0.75f, 0.14f);
            Object.Destroy(_I.GetComponent<LabNhinCamera>());
            Object.Destroy(_t.GetComponent<LabNhinCamera>());
            _I.transform.localScale = Vector3.one * 0.003f;
            _t.transform.localScale = Vector3.one * 0.003f;
            Chu("Nguồn một chiều", new Vector3(0f, 0.03f, -0.24f));
            var dayC = new[] { new Vector3(-0.06f, 0.06f, -0.26f), new Vector3(-0.12f, 0.22f, -0.2f), new Vector3(-0.055f, 0.3f, -0.02f), new Vector3(-0.055f, 0.24f, 0f) };
            var dayA = new[] { new Vector3(0.06f, 0.06f, -0.26f), new Vector3(0.12f, 0.22f, -0.2f), new Vector3(0.055f, 0.3f, -0.02f), new Vector3(0.055f, 0.24f, 0f) };
            LuoiGO("Day cathode", LabHinh.Ong(dayC, 0.0035f), LabVatLieu.Dac(new Color(0.12f, 0.16f, 0.22f)), Vector3.zero);
            LuoiGO("Day anode", LabHinh.Ong(dayA, 0.0035f), LabVatLieu.Dac(new Color(0.73f, 0.11f, 0.11f)), Vector3.zero);
            var mauE = new Color(0.22f, 0.74f, 0.97f);
            _e1 = new DongHat(Goc, new[] { new Vector3(0.055f, 0.24f, 0.004f), new Vector3(0.055f, 0.3f, -0.016f), new Vector3(0.12f, 0.22f, -0.196f), new Vector3(0.06f, 0.06f, -0.256f) }, 8, mauE, 0.0035f) { TocDo = 0.25f };
            _e2 = new DongHat(Goc, new[] { new Vector3(-0.06f, 0.06f, -0.256f), new Vector3(-0.12f, 0.22f, -0.196f), new Vector3(-0.055f, 0.3f, -0.016f), new Vector3(-0.055f, 0.24f, 0.004f) }, 8, mauE, 0.0035f) { TocDo = 0.25f };
            _nhanCa = Chu("Cathode (−)", new Vector3(-0.115f, 0.3f, 0.02f));
            _nhanAn = Chu("Anode (+)", new Vector3(0.115f, 0.3f, 0.02f));
            _kq = Chu("", new Vector3(0f, 0.36f, 0f));
        }

        public override void CapNhat(string json)
        {
            var s = JsonUtility.FromJson<TTDienPhan>(json);
            if (s == null || s.id != "dien-phan") return;
            bool nacl = s.bai == "nacl_mang_ngan", anodeCu = s.bai == "cuso4_anode_cu";
            var kq = s.ket_qua ?? new KetQuaDP();
            _mang.gameObject.SetActive(nacl);
            if (s.mau_dd != null && CoMau(s.mau_dd.rgb))
                LabVatLieu.DatMau(_dd, Color.Lerp(new Color(0.84f, 0.92f, 0.97f), Mau(s.mau_dd.rgb), s.mau_dd.dam) * new Color(1f, 1f, 1f, 0f) + new Color(0f, 0f, 0f, 0.42f + 0.3f * s.mau_dd.dam));
            else LabVatLieu.DatMau(_dd, new Color(0.84f, 0.92f, 0.97f, 0.42f));
            const float mMax = 1.5f;
            LabVatLieu.DatMau(_vlCa, anodeCu ? new Color(0.77f, 0.44f, 0.25f) : new Color(0.27f, 0.29f, 0.31f));
            LabVatLieu.DatMau(_vlAn, anodeCu ? new Color(0.69f, 0.42f, 0.27f) : new Color(0.27f, 0.29f, 0.31f));
            _phu.gameObject.SetActive(kq.m_cathode > 0.0005f);
            _phu.localScale = new Vector3(0.032f, 0.1f, 0.013f + Mathf.Min(1f, kq.m_cathode / mMax) * 0.02f);
            float mon = anodeCu ? Mathf.Min(0.45f, kq.m_anode_giam / mMax * 0.5f) : 0f;
            _thanhAn.localScale = new Vector3(0.03f * (1f - mon), 0.22f, 0.012f * (1f - mon));
            DatPhat(_botC, s.khi_dang_thoat != null && s.khi_dang_thoat.cathode ? 35f : 0f);
            DatPhat(_botA, s.khi_dang_thoat != null && s.khi_dang_thoat.anode ? 35f : 0f);
            _hongT.gameObject.SetActive(nacl);
            LabVatLieu.DatMau(_hong, new Color(0.91f, 0.24f, 0.55f, nacl && s.chi_thi_cathode != null ? s.chi_thi_cathode.ti_le * 0.55f : 0f));
            _quyT.gameObject.SetActive(nacl);
            LabVatLieu.DatMau(_quy, s.quy_anode == "mất màu" ? new Color(0.96f, 0.96f, 0.95f) : new Color(0.55f, 0.36f, 0.96f));
            _e1.Bat = _e2.Bat = s.dang_chay;
            if (s.dien_cuc != null)
            {
                _nhanCa.text = KyTu("Cathode (−) " + s.dien_cuc.cathode);
                _nhanAn.text = KyTu("Anode (+) " + s.dien_cuc.anode);
            }
            _I.text = KyTu(s.I.ToString("0.0").Replace(".", ",") + " A");
            _t.text = KyTu(Mathf.RoundToInt(s.t) + " s");
            _kq.text = KyTu(nacl ? "V(H₂) = " + kq.V_khi_cathode.ToString("0.000").Replace(".", ",") + " L"
                : anodeCu ? "Cathode +" + kq.m_cathode.ToString("0.000").Replace(".", ",") + " g"
                : "m(Cu) = " + kq.m_cathode.ToString("0.000").Replace(".", ",") + " g");
        }

        public override void Tick(float dt) { _e1?.Tick(dt); _e2?.Tick(dt); }
    }
}
