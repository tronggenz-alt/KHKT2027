using System;
using System.Collections.Generic;
using Khkt.Demo;
using UnityEngine;
using UnityEngine.UI;

namespace Khkt.HoaLy
{
    /// <summary>
    /// Động cơ nhiệt 4 kỳ: xi-lanh – pít-tông – trục khuỷu chạy theo góc 0–720°, chu trình p–v (Otto / Diesel) có chấm trạng thái
    /// và diện tích công tô màu, thanh năng lượng q_in = w + q_out và so sánh η với η Carnot.
    /// </summary>
    public sealed class TrangDongCo : TrangHoaLy
    {
        public override string Ma => "dong_co_nhiet";
        public override string Ten => "Động cơ nhiệt";
        public override string MoTa => "Chu trình Otto và Diesel lý tưởng của động cơ 4 kỳ, định luật I nhiệt động lực học và giới hạn hiệu suất Carnot.";
        public override string TepCauHoi => "cau_hoi_dong_co_nhiet";

        public ThamSoChuTrinh ThamSo = ThamSoChuTrinh.MacDinh;
        public KetQuaChuTrinh KetQua { get; private set; }
        public readonly List<NhienLieu> DanhSachNhienLieu = new List<NhienLieu>();
        public NhienLieu NhienLieuChon { get; private set; }
        public double Goc { get; private set; }
        public float VongMoiGiay { get; private set; } = 0.35f;

        public Slider ThanhR, ThanhRc, ThanhGamma, ThanhQ, ThanhT1, ThanhTocDo;
        Button[] chipChuTrinh, chipNl;
        RectTransform hangRc, hangQ;
        DoThi dtPV;
        LuoiVe veMay, veNangLuong;
        Text nhanKy, nhanMay, chuNl, oEta, oCarnot, oW, oQ, oT3, oP3, oMep, oP, chuNangLuong, nhanQin, nhanW, nhanQout, nhanEta, nhanEtaC;
        readonly float[] pvV = new float[200], pvP = new float[200];
        readonly Vector2[] duong = new Vector2[200];
        readonly Vector2[] vet = new Vector2[90];
        float tiaLua;

        protected override void Dung()
        {
            var ta = Resources.Load<TextAsset>("HoaLy/nhien_lieu");
            if (ta != null) DanhSachNhienLieu.AddRange(DongCoNhietMoHinh.DocNhienLieu(ta.text));
            TinhLai();

            var theMay = HoaLyUi.The(VungTrai, "Xi-lanh", new Vector2(0, 0.40f), new Vector2(0.32f, 1), new Vector2(0, 8), new Vector2(-8, 0));
            HoaLyUi.Chu(theMay, "Tiêu đề", "Xi-lanh 4 kỳ", 16, GlassKit.Ink, true, TextAnchor.UpperLeft, new Vector2(18, -12), new Vector2(300, 24));
            veMay = LuoiVe.Tao(theMay, "Lưới máy");
            nhanKy = HoaLyUi.Chu(theMay, "Kỳ", "", 17, GlassKit.Accent2, true, TextAnchor.UpperCenter, new Vector2(-150, 64), new Vector2(300, 26), new Vector2(0.5f, 0));
            nhanMay = HoaLyUi.Chu(theMay, "Trạng thái", "", 13, GlassKit.Dim, false, TextAnchor.UpperCenter, new Vector2(-150, 36), new Vector2(300, 22), new Vector2(0.5f, 0));
            var thePV = HoaLyUi.The(VungTrai, "Đồ thị p–v", new Vector2(0.32f, 0.40f), Vector2.one, new Vector2(8, 8), Vector2.zero);
            dtPV = new DoThi(thePV, "Đồ thị", "Chu trình trên đồ thị p–v", "v (m³/kg)", "p (bar)") { ChiaY = 100 };
            var theNl = HoaLyUi.The(VungTrai, "Năng lượng", Vector2.zero, new Vector2(1, 0.40f), Vector2.zero, new Vector2(0, -8));
            HoaLyUi.Chu(theNl, "Tiêu đề", "Cân bằng năng lượng mỗi kg khí (định luật I) và giới hạn Carnot", 16, GlassKit.Ink, true, TextAnchor.UpperLeft, new Vector2(18, -12), new Vector2(900, 24));
            veNangLuong = LuoiVe.Tao(theNl, "Lưới năng lượng");
            nhanQin = NhanNl(theNl, "q_in"); nhanW = NhanNl(theNl, "w"); nhanQout = NhanNl(theNl, "q_out"); nhanEta = NhanNl(theNl, "η"); nhanEtaC = NhanNl(theNl, "η Carnot");
            chuNangLuong = HoaLyUi.Chu(theNl, "Giải thích", "", 14, GlassKit.Dim, false, TextAnchor.UpperLeft, new Vector2(18, -300), new Vector2(1200, 60));

            TieuMuc("Chu trình", -14);
            chipChuTrinh = NhomChip(new[] { "Otto (xăng)", "Diesel (dầu)" }, -36, 548, i => DatChuTrinh(i == 1 ? KieuChuTrinh.Diesel : KieuChuTrinh.Otto), 0, 2, 38, 14);
            TieuMuc("Nhiên liệu (đặt q_in cho Otto)", -88);
            var tenNl = new string[DanhSachNhienLieu.Count];
            for (int i = 0; i < tenNl.Length; i++) tenNl[i] = DanhSachNhienLieu[i].CongThuc.Replace("C8H18", "C₈H₁₈").Replace("C2H5OH", "C₂H₅OH").Replace("CH4", "CH₄").Replace("H2", "H₂");
            chipNl = NhomChip(tenNl, -110, 548, ChonNhienLieu, -1, 4, 38, 14);
            chuNl = HoaLyUi.Chu(VungDieuKhien, "Phản ứng cháy", "", 13, GlassKit.Dim, false, TextAnchor.UpperLeft, new Vector2(20, -154), new Vector2(548, 36));
            TieuMuc("Tham số", -196);
            ThanhR = HoaLyUi.ThanhTruot(VungDieuKhien, "Tỉ số nén", "Tỉ số nén r = V₁/V₂", -218, 548, 4f, 22f, (float)ThamSo.R, v => { ThamSo.R = v; TinhLai(); }, v => HoaLyUi.So(v, 1));
            hangRc = HoaLyUi.Day(VungDieuKhien, "Hàng rc");
            ThanhRc = HoaLyUi.ThanhTruot(hangRc, "Tỉ số ngắt", "Tỉ số ngắt rc = V₃/V₂ (Diesel)", -286, 548, 1.1f, 3.5f, (float)ThamSo.Rc, v => { ThamSo.Rc = v; TinhLai(); }, v => HoaLyUi.So(v, 2));
            hangQ = HoaLyUi.Day(VungDieuKhien, "Hàng q");
            ThanhQ = HoaLyUi.ThanhTruot(hangQ, "Nhiệt cấp", "Nhiệt cấp q_in (Otto)", -286, 548, 200f, 2500f, (float)ThamSo.QIn, v => { ThamSo.QIn = v; TinhLai(); }, v => HoaLyUi.So(v, 0) + " kJ/kg");
            ThanhGamma = HoaLyUi.ThanhTruot(VungDieuKhien, "Gamma", "Chỉ số đoạn nhiệt γ = Cp/Cv", -354, 548, 1.25f, 1.67f, (float)ThamSo.Gamma, v => { ThamSo.Gamma = v; TinhLai(); }, v => HoaLyUi.So(v, 2));
            ThanhT1 = HoaLyUi.ThanhTruot(VungDieuKhien, "Nhiệt độ nạp", "Nhiệt độ khí nạp T₁", -422, 548, 250f, 350f, (float)ThamSo.T1, v => { ThamSo.T1 = v; TinhLai(); }, v => HoaLyUi.So(v, 0) + " K");
            ThanhTocDo = HoaLyUi.ThanhTruot(VungDieuKhien, "Tốc độ quay", "Tốc độ hoạt hình", -490, 548, 0.05f, 2f, VongMoiGiay, v => VongMoiGiay = v, v => HoaLyUi.So(v, 2) + " vòng/s");
            TieuMuc("Số liệu", -562);
            oEta = OSo("Hiệu suất η", 0, -584);
            oCarnot = OSo("η Carnot = 1 − T₁/T₃", 1, -584);
            oW = OSo("Công w = q_in − q_out", 2, -584);
            oQ = OSo("q_in · q_out", 3, -584);
            oT3 = OSo("T₂ · T₃ (cao nhất)", 4, -584);
            oP3 = OSo("p₂ · p₃ (cao nhất)", 5, -584);
            oMep = HoaLyUi.Chu(VungDieuKhien, "MEP", "", 13, GlassKit.Dim, false, TextAnchor.UpperLeft, new Vector2(20, -808), new Vector2(548, 40));
            DatChuTrinh(KieuChuTrinh.Otto);
        }

        Text NhanNl(Transform cha, string ten)
        {
            var t = HoaLyUi.Chu(cha, "Nhãn " + ten, "", 14, GlassKit.Ink, true, TextAnchor.MiddleLeft, Vector2.zero, new Vector2(420, 22), new Vector2(0.5f, 0.5f));
            t.rectTransform.pivot = new Vector2(0, 0.5f);
            return t;
        }

        public void DatChuTrinh(KieuChuTrinh k)
        {
            ThamSo.Kieu = k;
            if (chipChuTrinh != null) { GlassKit.SetChip(chipChuTrinh[0], k == KieuChuTrinh.Otto); GlassKit.SetChip(chipChuTrinh[1], k == KieuChuTrinh.Diesel); }
            hangRc.gameObject.SetActive(k == KieuChuTrinh.Diesel);
            hangQ.gameObject.SetActive(k == KieuChuTrinh.Otto);
            // Diesel nén cao hơn (không tự cháy sớm); Otto r ~ 8–12
            ThanhR.value = k == KieuChuTrinh.Diesel ? Mathf.Max(ThanhR.value, 16f) : Mathf.Min(ThanhR.value, 12f);
            TinhLai();
        }

        public void ChonNhienLieu(int i)
        {
            if (i < 0 || i >= DanhSachNhienLieu.Count) return;
            NhienLieuChon = DanhSachNhienLieu[i];
            for (int k = 0; k < chipNl.Length; k++) GlassKit.SetChip(chipNl[k], k == i);
            var kq = DongCoNhietMoHinh.TinhNhienLieu(NhienLieuChon);
            ThanhQ.value = Mathf.Clamp((float)kq.QIn, 200f, 2500f);
            chuNl.text = kq.PhuongTrinh + $"\nLHV = {HoaLyUi.So(kq.LHV_MJ_kg, 2)} MJ/kg · AFR = {HoaLyUi.So(kq.AFR, 2)} → q_in = {HoaLyUi.So(kq.QIn, 0)} kJ/kg (k = 0,55)";
            TinhLai();
        }

        public void Dat(double r, double gamma, double t1, double qIn, double rc)
        {
            ThanhR.value = (float)r; ThanhGamma.value = (float)gamma; ThanhT1.value = (float)t1; ThanhQ.value = (float)qIn; ThanhRc.value = (float)rc;
        }

        void TinhLai()
        {
            KetQua = DongCoNhietMoHinh.Tinh(ThamSo);
            LamMoiChu();
        }

        public override void CapNhat(float dt)
        {
            SoKhung++;
            var kq = KetQua;
            double gocTruoc = Goc;
            Goc = (Goc + dt * VongMoiGiay * 360) % 720;
            if (gocTruoc < 360 && Goc >= 360) tiaLua = 1f;
            tiaLua = Mathf.Max(0, tiaLua - dt * 3f);
            int ky = DongCoNhietMoHinh.TrangThaiTheoGoc(kq, Goc, out double v, out double p, out double T);

            // đồ thị p–v
            int n = DongCoNhietMoHinh.DuongPV(kq, pvV, pvP, 90);
            dtPV.XMin = 0; dtPV.XMax = kq.V1 * 1.08; dtPV.YMin = 0; dtPV.YMax = kq.P3 * 1.1;
            var ve = dtPV.Ve;
            ve.BatDau();
            dtPV.VeKhung();
            // diện tích công: giữa đường giãn (3→4) và đường nén (1→2)
            var toCong = HoaLyUi.C32(GlassKit.Accent, 0.16f);
            const int soDai = 80;
            for (int i = 0; i < soDai; i++)
            {
                double va = kq.V2 + (kq.V1 - kq.V2) * i / soDai, vb = kq.V2 + (kq.V1 - kq.V2) * (i + 1) / soDai;
                Vector2 a1 = dtPV.Diem(va, PNen(kq, va)), b1 = dtPV.Diem(vb, PNen(kq, vb));
                Vector2 a2 = dtPV.Diem(va, PGian(kq, va)), b2 = dtPV.Diem(vb, PGian(kq, vb));
                ve.VeTuGiac(a1, a2, b2, b1, toCong);
            }
            for (int i = 0; i < n; i++) duong[i] = dtPV.Diem(pvV[i], pvP[i]);
            ve.VeDuong(duong, n, 3f, HoaLyUi.C32(GlassKit.Accent));
            // đường nạp/xả p1
            ve.VeNetDut(dtPV.Diem(kq.V2, kq.P1), dtPV.Diem(kq.V1, kq.P1), 2f, 6f, HoaLyUi.C32(GlassKit.Muted));
            // vệt sau chấm trạng thái
            for (int i = 0; i < vet.Length; i++)
            {
                DongCoNhietMoHinh.TrangThaiTheoGoc(kq, Goc - (vet.Length - 1 - i) * 2.0, out double vv, out double pp, out _);
                vet[i] = dtPV.Diem(vv, pp);
            }
            for (int i = 1; i < vet.Length; i++)
            {
                if ((vet[i] - vet[i - 1]).sqrMagnitude > 3600) continue;   // bước nhảy đẳng tích
                ve.VeDoan(vet[i - 1], vet[i], 2f + 3f * i / vet.Length, HoaLyUi.C32(GlassKit.Accent2, (float)i / vet.Length));
            }
            var cham = dtPV.Diem(v, p);
            ve.VeHat(cham.x, cham.y, 10, HoaLyUi.C32(GlassKit.Accent2));
            // nhãn điểm 1..4 (chấm nhỏ)
            ve.VeHat(dtPV.X(kq.V1), dtPV.Y(kq.P1), 5, HoaLyUi.C32(GlassKit.Ink));
            ve.VeHat(dtPV.X(kq.V2), dtPV.Y(kq.P2), 5, HoaLyUi.C32(GlassKit.Ink));
            ve.VeHat(dtPV.X(kq.V3), dtPV.Y(kq.P3), 5, HoaLyUi.C32(GlassKit.Ink));
            ve.VeHat(dtPV.X(kq.V1), dtPV.Y(kq.P4), 5, HoaLyUi.C32(GlassKit.Ink));
            ve.KetThuc();

            VeMay(kq, ky, v, T);
            VeNangLuong(kq);
            if (DenLucCapNhatChu(dt))
            {
                DatChu(nhanKy, DongCoNhietMoHinh.TenKy[ky]);
                DatChu(nhanMay, $"φ = {Goc:0}° · p = {HoaLyUi.So(p / 100, 1)} bar · T = {HoaLyUi.So(T, 0)} K");
                CapNhatChu();
            }
        }

        static double PNen(KetQuaChuTrinh kq, double v) => kq.P1 * Math.Pow(kq.V1 / v, kq.Gamma);
        static double PGian(KetQuaChuTrinh kq, double v) =>
            kq.Kieu == KieuChuTrinh.Diesel && v <= kq.V3 ? kq.P3 : kq.P3 * Math.Pow(kq.V3 / v, kq.Gamma);

        void VeMay(KetQuaChuTrinh kq, int ky, double v, double T)
        {
            var r = veMay.rectTransform.rect;
            veMay.BatDau();
            float cx = r.center.x, w = Mathf.Min(170, r.width * 0.42f);
            float dinh = r.yMax - 60, hanhTrinh = Mathf.Min(165, r.height * 0.30f), R = hanhTrinh / 2, L = 110;
            float khoangChet = Mathf.Clamp(hanhTrinh / (float)(kq.R - 1), 10, 80);   // V₂/(V₁−V₂) = 1/(r−1)
            float frac = (float)((v - kq.V2) / (kq.V1 - kq.V2));
            float yPit = dinh - khoangChet - hanhTrinh * frac;
            float dayThanh = dinh - khoangChet - hanhTrinh - 44;
            var vien = HoaLyUi.C32(GlassKit.Dim, 0.95f);
            // khí trong xi-lanh: màu theo nhiệt độ
            float tMau = Mathf.InverseLerp(280, 2600, (float)T);
            var mauKhi = HoaLyUi.MauNhiet(tMau);
            mauKhi.a = 170;
            veMay.VeHop(cx - w / 2, yPit, cx + w / 2, dinh, mauKhi);
            if (tiaLua > 0 && kq.Kieu == KieuChuTrinh.Otto) veMay.VeHat(cx, dinh - 14, 14 + 26 * tiaLua, new Color32(255, 230, 120, (byte)(220 * tiaLua)));
            // thành xi-lanh + nắp + van
            veMay.VeHop(cx - w / 2 - 8, dayThanh, cx - w / 2, dinh + 8, vien);
            veMay.VeHop(cx + w / 2, dayThanh, cx + w / 2 + 8, dinh + 8, vien);
            veMay.VeHop(cx - w / 2 - 8, dinh, cx + w / 2 + 8, dinh + 10, vien);
            bool vanNap = ky == 0, vanXa = ky == 3;
            float moNap = vanNap ? 8 : 0, moXa = vanXa ? 8 : 0;
            veMay.VeHop(cx - w / 2 + 14, dinh - moNap, cx - w / 2 + 44, dinh + 4 - moNap, HoaLyUi.C32(vanNap ? GlassKit.Good : GlassKit.Muted));
            veMay.VeHop(cx + w / 2 - 44, dinh - moXa, cx + w / 2 - 14, dinh + 4 - moXa, HoaLyUi.C32(vanXa ? GlassKit.Bad : GlassKit.Muted));
            // pít-tông
            veMay.VeHop(cx - w / 2 + 2, yPit - 34, cx + w / 2 - 2, yPit, HoaLyUi.C32(GlassKit.Accent));
            veMay.VeHop(cx - w / 2 + 2, yPit - 10, cx + w / 2 - 2, yPit - 7, HoaLyUi.C32(GlassKit.Ink, 0.4f));
            // trục khuỷu: tâm cố định dưới hành trình, thanh truyền nối chốt pít-tông
            // chốt pít-tông ở yPit − 20 = yTruc + R·cos φ + L ⇒ tâm trục cố định
            float banKinh = R;
            float yTruc = dinh - khoangChet - R - 20 - L;
            float a = (float)(Goc * Mathf.Deg2Rad);
            var chot = new Vector2(cx + banKinh * Mathf.Sin(a), yTruc + banKinh * Mathf.Cos(a));
            var mauTruc = HoaLyUi.C32(GlassKit.Ink, 0.55f);
            veMay.VeHat(cx, yTruc, banKinh + 10, HoaLyUi.C32(GlassKit.Line, 2f));
            veMay.VeDoan(new Vector2(cx, yTruc), chot, 10, mauTruc);
            veMay.VeDoan(chot, new Vector2(cx, yPit - 20), 9, HoaLyUi.C32(GlassKit.Dim));
            veMay.VeHat(chot.x, chot.y, 8, HoaLyUi.C32(GlassKit.Accent2));
            veMay.VeHat(cx, yTruc, 6, mauTruc);
            veMay.KetThuc();
        }

        void VeNangLuong(KetQuaChuTrinh kq)
        {
            var r = veNangLuong.rectTransform.rect;
            veNangLuong.BatDau();
            float x0 = r.xMin + 120, x1 = r.xMax - 470, h = 30;
            float yA = r.yMax - 70, yB = yA - 46, yC = yB - 70, yD = yC - 46;
            float s = (x1 - x0) / (float)Math.Max(1, kq.QIn);
            var nen = HoaLyUi.C32(GlassKit.Well);
            veNangLuong.VeHop(x0, yA - h / 2, x1, yA + h / 2, nen);
            veNangLuong.VeHop(x0, yA - h / 2, x0 + (float)kq.QIn * s, yA + h / 2, HoaLyUi.C32(GlassKit.Bad, 0.85f));
            veNangLuong.VeHop(x0, yB - h / 2, x1, yB + h / 2, nen);
            veNangLuong.VeHop(x0, yB - h / 2, x0 + (float)kq.W * s, yB + h / 2, HoaLyUi.C32(GlassKit.Good, 0.9f));
            veNangLuong.VeHop(x0 + (float)kq.W * s, yB - h / 2, x0 + (float)kq.QIn * s, yB + h / 2, HoaLyUi.C32(GlassKit.Accent, 0.55f));
            // η và η Carnot (thang 0–100 %)
            veNangLuong.VeHop(x0, yC - h / 2, x1, yC + h / 2, nen);
            veNangLuong.VeHop(x0, yC - h / 2, x0 + (x1 - x0) * (float)kq.Eta, yC + h / 2, HoaLyUi.C32(GlassKit.Accent2, 0.95f));
            veNangLuong.VeHop(x0, yD - h / 2, x1, yD + h / 2, nen);
            veNangLuong.VeHop(x0, yD - h / 2, x0 + (x1 - x0) * (float)kq.EtaCarnot, yD + h / 2, HoaLyUi.C32(GlassKit.Ink, 0.35f));
            float xC = x0 + (x1 - x0) * (float)kq.EtaCarnot;
            veNangLuong.VeNetDut(new Vector2(xC, yD - h / 2), new Vector2(xC, yC + h / 2 + 6), 2, 4, HoaLyUi.C32(GlassKit.Bad));
            veNangLuong.KetThuc();
            void Dat(Text t, float y) => t.rectTransform.anchoredPosition = new Vector2(x1 + 14, y);
            Dat(nhanQin, yA); Dat(nhanW, yB); Dat(nhanEta, yC); Dat(nhanEtaC, yD);
            nhanQout.gameObject.SetActive(false);
        }

        void CapNhatChu()
        {
            var kq = KetQua;
            DatChu(oEta, HoaLyUi.So(kq.EtaPhanTram, 1) + " %");
            DatChu(oCarnot, HoaLyUi.So(kq.EtaCarnot * 100, 1) + " %");
            DatChu(oW, HoaLyUi.So(kq.W, 0) + " kJ/kg");
            DatChu(oQ, HoaLyUi.So(kq.QIn, 0) + " · " + HoaLyUi.So(kq.QOut, 0) + " kJ/kg");
            DatChu(oT3, HoaLyUi.So(kq.T2, 0) + " · " + HoaLyUi.So(kq.T3, 0) + " K");
            DatChu(oP3, HoaLyUi.So(kq.P2 / 100, 1) + " · " + HoaLyUi.So(kq.P3 / 100, 1) + " bar");
            DatChu(oMep, $"MEP = {HoaLyUi.So(kq.MEP / 100, 2)} bar · xi-lanh 80×80 mm, 1200 vòng/phút: công {HoaLyUi.So(kq.WJ, 0)} J/chu trình, P ≈ {HoaLyUi.So(kq.PKW, 2)} kW");
            DatChu(nhanQin, "q_in = " + HoaLyUi.So(kq.QIn, 0) + " kJ/kg (nhiệt nhận)");
            DatChu(nhanW, "<color=#" + ColorUtility.ToHtmlStringRGB(GlassKit.Good) + ">w = " + HoaLyUi.So(kq.W, 0) + "</color> + q_out = " + HoaLyUi.So(kq.QOut, 0) + " kJ/kg");
            DatChu(nhanEta, "η = " + HoaLyUi.So(kq.EtaPhanTram, 1) + " %");
            DatChu(nhanEtaC, "η Carnot (T₁ → T₃) = " + HoaLyUi.So(kq.EtaCarnot * 100, 1) + " %");
            string ct = kq.Kieu == KieuChuTrinh.Otto ? "η Otto = 1 − 1/r^(γ−1)" : "η Diesel = 1 − (rc^γ − 1) / (γ·(rc − 1)·r^(γ−1))";
            DatChu(chuNangLuong, $"{ct} = {HoaLyUi.So(kq.EtaPhanTram, 1)} % < η Carnot = {HoaLyUi.So(kq.EtaCarnot * 100, 1)} %: không động cơ nào chạy giữa T₁ = {HoaLyUi.So(kq.T1, 0)} K và T₃ = {HoaLyUi.So(kq.T3, 0)} K vượt được giới hạn Carnot. " +
                                 "Phần tô xanh trên đồ thị p–v là công w = ∮p dv.");
        }
    }
}
