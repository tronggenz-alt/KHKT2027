// Cảnh 4 – Pin Galvani: hai cốc điện cực, cầu muối KNO₃ (bấm để rút / lắp), vôn kế số, dây dẫn, dòng electron và ion chạy.
using UnityEngine;

namespace PhongThiNghiem
{
    public class CanhPin : CanhCoSo
    {
        const float XT = -0.17f, XP = 0.17f;
        Material _ddT, _ddP, _klT, _klP;
        Transform _thanhT, _thanhP, _phuT, _phuP, _cau;
        TextMesh _von, _nhanT, _nhanP, _nhanE;
        DongHat _e, _ionK, _ionNO3;
        float _cauMucTieu, _cauY;

        protected override void DungCanh(string tt)
        {
            GocViTri = new Vector3(0.05f, 0.5f, 1.05f);
            GocDich = new Vector3(0f, 0.25f, 0f);
            GocGan = 0.35f; GocXa = 2f;
            var p = LabHinh.Coc(0.07f, 0.14f);
            LuoiGO("Coc trai", LabHinh.Lathe(p, 48), LabVatLieu.ThuyTinh(), new Vector3(XT, 0f, 0f));
            LuoiGO("Coc phai", LabHinh.Lathe(p, 48), LabVatLieu.ThuyTinh(), new Vector3(XP, 0f, 0f));
            _ddT = LabVatLieu.Long(new Color(0.84f, 0.92f, 0.97f, 0.42f));
            _ddP = LabVatLieu.Long(new Color(0.84f, 0.92f, 0.97f, 0.42f));
            LuoiGO("Dd trai", LabHinh.ChatLong(p, 0.1f), _ddT, new Vector3(XT, 0f, 0f));
            LuoiGO("Dd phai", LabHinh.ChatLong(p, 0.1f), _ddP, new Vector3(XP, 0f, 0f));
            _klT = LabVatLieu.KimLoai(Color.gray);
            _klP = LabVatLieu.KimLoai(Color.gray);
            _thanhT = Khoi(PrimitiveType.Cube, new Vector3(XT - 0.02f, 0.12f, 0f), new Vector3(0.035f, 0.2f, 0.006f), _klT).transform;
            _thanhP = Khoi(PrimitiveType.Cube, new Vector3(XP - 0.02f, 0.12f, 0f), new Vector3(0.035f, 0.2f, 0.006f), _klP).transform;
            _phuT = Khoi(PrimitiveType.Cube, new Vector3(XT - 0.02f, 0.065f, 0f), new Vector3(0.037f, 0.09f, 0.0001f), _klT).transform;
            _phuP = Khoi(PrimitiveType.Cube, new Vector3(XP - 0.02f, 0.065f, 0f), new Vector3(0.037f, 0.09f, 0.0001f), _klP).transform;
            // cầu muối chữ U
            var u = new[] { new Vector3(XT + 0.03f, 0.06f, 0f), new Vector3(XT + 0.03f, 0.2f, 0f), new Vector3(-0.06f, 0.25f, 0f), new Vector3(0.06f, 0.25f, 0f), new Vector3(XP - 0.03f, 0.2f, 0f), new Vector3(XP - 0.03f, 0.06f, 0f) };
            _cau = Moi("Cau muoi").transform;
            var ong = LuoiGO("Ong U", LabHinh.Ong(u, 0.014f), LabVatLieu.ThuyTinh(), Vector3.zero, _cau, true);
            ong.AddComponent<LabNhap>().lenh = "doi_cau_muoi";
            LuoiGO("Ruot U", LabHinh.Ong(u, 0.0105f), LabVatLieu.Long(new Color(0.94f, 0.93f, 0.89f, 0.55f)), Vector3.zero, _cau);
            _ionK = new DongHat(_cau, u, 6, new Color(0.65f, 0.55f, 0.98f), 0.0035f) { TocDo = 0.12f };
            _ionNO3 = new DongHat(_cau, u, 6, new Color(0.98f, 0.57f, 0.24f), 0.0035f) { TocDo = 0.12f };
            Chu("Cầu muối KNO₃", new Vector3(0f, 0.29f, 0f));
            // vôn kế + dây
            Khoi(PrimitiveType.Cube, new Vector3(0f, 0.5f, -0.02f), new Vector3(0.16f, 0.1f, 0.05f), LabVatLieu.Dac(new Color(0.12f, 0.16f, 0.23f), 0.5f));
            _von = Chu("1,103 V", new Vector3(0f, 0.505f, 0.01f), 0.0045f);
            _von.color = new Color(0.49f, 0.94f, 0.76f);
            Object.Destroy(_von.GetComponent<LabNhinCamera>());
            _von.transform.localScale = Vector3.one * 0.0045f;
            Chu("Vôn kế", new Vector3(0f, 0.575f, 0f));
            var dayT = new[] { new Vector3(XT - 0.02f, 0.22f, 0f), new Vector3(XT - 0.02f, 0.36f, 0f), new Vector3(-0.12f, 0.5f, 0f), new Vector3(-0.08f, 0.5f, -0.02f) };
            var dayP = new[] { new Vector3(XP - 0.02f, 0.22f, 0f), new Vector3(XP - 0.02f, 0.36f, 0f), new Vector3(0.12f, 0.5f, 0f), new Vector3(0.08f, 0.5f, -0.02f) };
            LuoiGO("Day den", LabHinh.Ong(dayT, 0.0035f), LabVatLieu.Dac(new Color(0.12f, 0.16f, 0.22f)), Vector3.zero);
            LuoiGO("Day do", LabHinh.Ong(dayP, 0.0035f), LabVatLieu.Dac(new Color(0.73f, 0.11f, 0.11f)), Vector3.zero);
            var duongE = new[] { new Vector3(XT - 0.02f, 0.22f, 0.006f), new Vector3(XT - 0.02f, 0.36f, 0.006f), new Vector3(-0.12f, 0.5f, 0.008f), new Vector3(0f, 0.56f, 0.03f),
                new Vector3(0.12f, 0.5f, 0.008f), new Vector3(XP - 0.02f, 0.36f, 0.006f), new Vector3(XP - 0.02f, 0.22f, 0.006f) };
            _e = new DongHat(Goc, duongE, 14, Color.Lerp(MauNhan, new Color(0.49f, 0.83f, 0.99f), 0.4f), 0.004f);
            _nhanT = Chu("", new Vector3(XT, 0.3f, 0.06f));
            _nhanP = Chu("", new Vector3(XP, 0.3f, 0.06f));
            _nhanE = Chu("", new Vector3(0f, 0.62f, 0f));
        }

        public override void CapNhat(string json)
        {
            var s = JsonUtility.FromJson<TTPin>(json);
            if (s == null || s.id != "pin-galvani") return;
            if (s.mau_kim_loai != null) { LabVatLieu.DatMau(_klT, Mau(s.mau_kim_loai.trai)); LabVatLieu.DatMau(_klP, Mau(s.mau_kim_loai.phai)); }
            LabVatLieu.DatMau(_ddT, s.mau_dd != null && CoMau(s.mau_dd.trai) ? Mau(s.mau_dd.trai, 0.6f) : new Color(0.84f, 0.92f, 0.97f, 0.42f));
            LabVatLieu.DatMau(_ddP, s.mau_dd != null && CoMau(s.mau_dd.phai) ? Mau(s.mau_dd.phai, 0.6f) : new Color(0.84f, 0.92f, 0.97f, 0.42f));
            _von.text = KyTu(s.E_pin.ToString("0.000").Replace(".", ",") + " V");
            bool traiLaAnode = s.ben_cathode == "phai";
            _nhanT.text = KyTu((traiLaAnode ? "Anode (−) " : "Cathode (+) ") + s.trai);
            _nhanP.text = KyTu((traiLaAnode ? "Cathode (+) " : "Anode (−) ") + s.phai);
            Transform anode = traiLaAnode ? _thanhT : _thanhP, phuCathode = traiLaAnode ? _phuP : _phuT, phuAnode = traiLaAnode ? _phuT : _phuP;
            (traiLaAnode ? _thanhP : _thanhT).localScale = new Vector3(0.035f, 0.2f, 0.006f);
            anode.localScale = new Vector3(0.035f * (1f - s.mon_anode * 0.5f), 0.2f, 0.006f * (1f - s.mon_anode * 0.4f));
            phuCathode.gameObject.SetActive(s.day_cathode > 0.01f);
            phuCathode.localScale = new Vector3(0.037f, 0.09f, 0.0001f + s.day_cathode * 0.013f);
            phuAnode.gameObject.SetActive(false);
            bool chay = !string.IsNullOrEmpty(s.chieu_electron);
            _e.Bat = _ionK.Bat = _ionNO3.Bat = chay;
            _e.Nguoc = s.chieu_electron == "phai_sang_trai";
            _ionK.Nguoc = !traiLaAnode;
            _ionNO3.Nguoc = traiLaAnode;
            _e.TocDo = 0.06f + Mathf.Min(0.3f, s.E_pin * 0.1f);
            _nhanE.text = KyTu(chay ? (_e.Nguoc ? "← e⁻ (dòng electron)" : "e⁻ → (dòng electron)") : "Không có dòng điện");
            _cauMucTieu = s.cau_muoi ? 0f : 1f;
        }

        public override void Tick(float dt)
        {
            _e?.Tick(dt); _ionK?.Tick(dt); _ionNO3?.Tick(dt);
            _cauY = Mathf.MoveTowards(_cauY, _cauMucTieu, dt * 1.5f);
            if (_cau != null) _cau.localPosition = new Vector3(0f, 0.25f * _cauY, -0.25f * _cauY);
        }
    }
}
