// Cảnh 1 – Chuẩn độ acid – base: giá đỡ, burette có vạch, khoá (bấm được), bình tam giác (bấm để lắc), giọt / dòng NaOH, vệt màu.
using UnityEngine;

namespace PhongThiNghiem
{
    public class CanhChuanDo : CanhCoSo
    {
        const float BX = 0.02f, BZ = -0.05f, Y_DUOI = 0.34f, Y_VACH0 = 0.86f, Y_VACH50 = 0.38f, Y_TREN = 0.93f, RB = 0.0085f;
        readonly Vector2[] _pBinh = LabHinh.BinhTamGiac(0.05f, 0.15f, 0.016f, 0.1f);
        Transform _cotNaOH, _tay, _binh, _giot, _dong;
        MeshFilter _ddLuoi;
        Material _vlDd, _vlVet;
        Transform _vet;
        TextMesh _nhanKhoa, _nhanBinh;
        float _hDd = -1f, _giotT = -1f, _lacT = -1f, _vetT = -1f;
        TTChuanDo _s;

        protected override void DungCanh(string tt)
        {
            GocViTri = new Vector3(0.5f, 0.62f, 0.95f);
            GocDich = new Vector3(BX, 0.45f, BZ);
            GocGan = 0.25f; GocXa = 2.4f;
            var kl = LabVatLieu.KimLoai(new Color(0.6f, 0.62f, 0.66f));
            Khoi(PrimitiveType.Cube, new Vector3(0f, 0.01f, 0f), new Vector3(0.3f, 0.02f, 0.2f), LabVatLieu.Dac(new Color(0.17f, 0.2f, 0.25f)));
            Tru(new Vector3(-0.1f, 0.02f, -0.05f), 0.007f, 0.95f, kl);
            Khoi(PrimitiveType.Cube, new Vector3(-0.05f, 0.74f, -0.05f), new Vector3(0.12f, 0.018f, 0.022f), kl);
            // burette
            Tru(new Vector3(BX, Y_DUOI, BZ), RB, Y_TREN - Y_DUOI, LabVatLieu.ThuyTinh());
            _cotNaOH = Tru(new Vector3(BX, Y_DUOI, BZ), RB - 0.0018f, 0.5f, LabVatLieu.Long(new Color(0.84f, 0.92f, 0.97f, 0.5f))).transform;
            for (int v = 0; v <= 50; v += 5)
            {
                float y = Y_VACH0 - v / 50f * (Y_VACH0 - Y_VACH50);
                Khoi(PrimitiveType.Cube, new Vector3(BX + RB, y, BZ), new Vector3(0.006f, 0.0012f, 0.0012f), LabVatLieu.Dac(new Color(0.1f, 0.15f, 0.22f)));
                Chu(v.ToString(), new Vector3(BX + RB + 0.012f, y, BZ), 0.0012f);
            }
            var khoa = Tru(new Vector3(BX - 0.015f, Y_DUOI - 0.01f, BZ), 0.0055f, 0.03f, LabVatLieu.Dac(new Color(0.12f, 0.16f, 0.23f)), null, true);
            khoa.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            khoa.AddComponent<LabNhap>().lenh = "bat_tat_khoa";
            _tay = Khoi(PrimitiveType.Cube, new Vector3(BX + 0.02f, Y_DUOI - 0.01f, BZ), new Vector3(0.008f, 0.032f, 0.005f), LabVatLieu.Dac(MauNhan), null, true).transform;
            _tay.gameObject.AddComponent<LabNhap>().lenh = "bat_tat_khoa";
            Tru(new Vector3(BX, Y_DUOI - 0.07f, BZ), 0.0025f, 0.05f, LabVatLieu.ThuyTinh());
            // bình tam giác trên giấy trắng
            Khoi(PrimitiveType.Cube, new Vector3(BX, 0.0205f, BZ), new Vector3(0.16f, 0.0005f, 0.12f), LabVatLieu.Dac(Color.white, 0.05f));
            _binh = Moi("BinhTamGiac").transform;
            _binh.localPosition = new Vector3(BX, 0.021f, BZ);
            var binh = LuoiGO("Thuy tinh", LabHinh.Lathe(_pBinh, 48), LabVatLieu.ThuyTinh(), Vector3.zero, _binh, true);
            binh.AddComponent<LabNhap>().lenh = "lac";
            _vlDd = LabVatLieu.Long(new Color(0.84f, 0.92f, 0.97f, 0.42f));
            _ddLuoi = LuoiGO("Dung dich", LabHinh.ChatLong(_pBinh, 0.03f), _vlDd, Vector3.zero, _binh).GetComponent<MeshFilter>();
            _giot = Khoi(PrimitiveType.Sphere, Vector3.zero, Vector3.one * 0.006f, LabVatLieu.Long(new Color(0.8f, 0.9f, 0.97f, 0.85f))).transform;
            _giot.gameObject.SetActive(false);
            _dong = Tru(Vector3.zero, 0.0012f, 1f, LabVatLieu.Long(new Color(0.8f, 0.9f, 0.97f, 0.7f))).transform;
            _dong.gameObject.SetActive(false);
            _vlVet = LabVatLieu.PhatSang(new Color(0.91f, 0.24f, 0.55f, 0f));
            _vet = Khoi(PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.024f, 0.0004f, 0.024f), _vlVet).transform;
            Chu("Burette – NaOH 0,100 M", new Vector3(BX + 0.07f, 0.8f, BZ));
            _nhanKhoa = Chu("Khoá đóng", new Vector3(BX + 0.07f, Y_DUOI - 0.012f, BZ));
            _nhanBinh = Chu("Bình tam giác", new Vector3(BX + 0.1f, 0.09f, BZ + 0.02f));
        }

        float YMatDd => _binh.localPosition.y + Mathf.Max(0f, _hDd);

        public override void CapNhat(string json)
        {
            var s = _s = JsonUtility.FromJson<TTChuanDo>(json);
            if (s == null || s.id != "chuan-do") return;
            float vBuret = s.V_buret > 0f ? s.V_buret : 50f;
            float muc = Y_VACH0 - s.V / vBuret * (Y_VACH0 - Y_VACH50);
            float h = Mathf.Max(0.001f, muc - (Y_DUOI - 0.005f));
            _cotNaOH.localScale = new Vector3(_cotNaOH.localScale.x, h / 2f, _cotNaOH.localScale.z);
            _cotNaOH.localPosition = new Vector3(BX, Y_DUOI - 0.005f + h / 2f, BZ);
            float hMoi = LabHinh.HDeCoTheTich(_pBinh, Mathf.Max(1f, s.the_tich_dd), 0.0025f, 0.12f);
            if (Mathf.Abs(hMoi - _hDd) > 0.0004f) { _hDd = hMoi; _ddLuoi.mesh = LabHinh.ChatLong(_pBinh, hMoi); }
            var a = s.mau != null ? s.mau.rgba : null;
            bool coMau = a != null && a.Length >= 4 && a[3] > 0.02f;
            Color c = coMau ? Color.Lerp(new Color(0.84f, 0.92f, 0.97f), new Color(a[0] / 255f, a[1] / 255f, a[2] / 255f), Mathf.Min(1f, a[3] + 0.15f)) : new Color(0.84f, 0.92f, 0.97f);
            c.a = coMau ? 0.45f + a[3] * 0.4f : 0.42f;
            LabVatLieu.DatMau(_vlDd, c);
            _tay.localRotation = s.khoa == "dong" ? Quaternion.identity : Quaternion.Euler(0f, 0f, 90f);
            _nhanKhoa.text = KyTu(s.khoa == "dong" ? "Khoá đóng" : s.khoa == "nhanh" ? "Chảy nhanh" : "Nhỏ từng giọt");
            _nhanBinh.text = KyTu((s.acid != null ? s.acid.cong_thuc : "") + " + " + (string.IsNullOrEmpty(s.ten_chi_thi) ? "chưa có chỉ thị" : s.ten_chi_thi));
            float yTip = Y_DUOI - 0.07f;
            bool nhanh = s.khoa == "nhanh";
            _dong.gameObject.SetActive(nhanh);
            if (nhanh)
            {
                float d = yTip - YMatDd;
                _dong.localScale = new Vector3(0.0024f, Mathf.Max(0.001f, d) / 2f, 0.0024f);
                _dong.localPosition = new Vector3(BX, YMatDd + d / 2f, BZ);
            }
            if (!s.vet_mau && _vetT >= 0f) _vetT = 2.4f;
        }

        public override void HieuUng(HieuUngDL e)
        {
            if (e.loai == "giot") { _giotT = 0f; _giot.gameObject.SetActive(true); }
            else if (e.loai == "lac") _lacT = 0f;
            else if (e.loai == "vet_mau")
            {
                _vetT = 0f;
                LabVatLieu.DatMau(_vlVet, _s != null && _s.ten_chi_thi == "Methyl orange" ? new Color(0.95f, 0.77f, 0.06f, 0.85f) : new Color(0.91f, 0.24f, 0.55f, 0.85f));
            }
        }

        public override void Tick(float dt)
        {
            if (_giotT >= 0f)
            {
                _giotT += dt;
                float u = Mathf.Clamp01(_giotT / 0.32f);
                _giot.localPosition = new Vector3(BX, Mathf.Lerp(Y_DUOI - 0.075f, YMatDd + 0.003f, u * u), BZ);
                if (u >= 1f) { _giotT = -1f; _giot.gameObject.SetActive(false); }
            }
            if (_lacT >= 0f)
            {
                _lacT += dt;
                float goc = Mathf.Sin(_lacT * 26f) * 5f * Mathf.Max(0f, 1f - _lacT / 0.8f);
                _binh.localRotation = Quaternion.Euler(0f, 0f, goc);
                if (_lacT > 0.8f) { _lacT = -1f; _binh.localRotation = Quaternion.identity; }
            }
            if (_vetT >= 0f)
            {
                _vetT += dt;
                float con = Mathf.Clamp01(1f - _vetT / 2.4f);
                _vet.localPosition = new Vector3(BX + 0.004f, YMatDd + 0.0035f, BZ);
                _vet.localScale = new Vector3(0.012f + 0.024f * (1f - con), 0.0004f, 0.012f + 0.024f * (1f - con));
                var c = _vlVet.GetColor("_BaseColor");
                c.a = 0.85f * con;
                _vlVet.SetColor("_BaseColor", c);
                if (con <= 0f) _vetT = -1f;
            }
        }
    }
}
