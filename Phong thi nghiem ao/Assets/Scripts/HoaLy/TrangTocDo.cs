using System;
using System.Collections.Generic;
using Khkt.Demo;
using UnityEngine;
using UnityEngine.UI;

namespace Khkt.HoaLy
{
    /// <summary>
    /// Tốc độ phản ứng: đồ thị nồng độ – thời gian (chạy theo con trỏ thời gian), so với mốc 298 K không xúc tác;
    /// phân bố năng lượng Maxwell–Boltzmann với phần E ≥ Ea tô màu; Arrhenius, Van 't Hoff, xúc tác, diện tích bề mặt.
    /// </summary>
    public sealed class TrangTocDo : TrangHoaLy
    {
        public override string Ma => "toc_do";
        public override string Ten => "Tốc độ phản ứng";
        public override string MoTa => "Phương trình Arrhenius k = A·e^(−Ea/RT), bậc phản ứng, chất xúc tác và thuyết va chạm. Đổi T, Ea, nồng độ để xem đường C–t.";
        public override string TepCauHoi => "cau_hoi_toc_do";

        public readonly List<PhanUngTocDo> DanhSach = new List<PhanUngTocDo>();
        public PhanUngTocDo PhanUng { get; private set; }
        public double Ea { get; private set; } = 50;
        public double T { get; private set; } = 298.15;
        public double C0 { get; private set; } = 1.0;
        public bool XucTac { get; private set; }
        public double HeSoBeMat { get; private set; } = 1;
        public static readonly double[] BeMat = { 1, 6, 40 };

        public double EaHieuDung => XucTac ? PhanUng.EaXucTac(Ea) : Ea;
        public double K => TocDoMoHinh.KTheoArrhenius(PhanUng.A * HeSoBeMat, EaHieuDung, T);
        public double KMoc => TocDoMoHinh.KTheoArrhenius(PhanUng.A, Ea, 298.15);
        public double ThoiGianBanHuy => TocDoMoHinh.ThoiGianBanHuy(PhanUng.Bac, C0, K);
        /// <summary>Thời điểm con trỏ trên đồ thị (s thật của phản ứng).</summary>
        public double ThoiDiem { get; private set; }
        public double TCuoiTruc { get; private set; } = 1;

        public Slider ThanhEa, ThanhT, ThanhC0;
        Toggle nutXucTac;
        Button[] chipBeMat;
        Text nhanBeMat, ptChu, oK, oTBanHuy, oV0, oTiLe, oGamma, oPhanSo, oNguon, oDonVi;
        DoThi dtC, dtE;
        Text chuThichC, chuThichE;
        readonly Vector2[] duong = new Vector2[161];
        float tChay;   // 0..1 tiến độ con trỏ
        const float ThoiGianChay = 7f, Nghi = 1.5f;

        protected override void Dung()
        {
            var ta = Resources.Load<TextAsset>("HoaLy/phan_ung");
            if (ta != null) DanhSach.AddRange(TocDoMoHinh.DocDanhSach(ta.text));
            if (DanhSach.Count == 0) DanhSach.Add(new PhanUngTocDo { Id = "A_B_C_D", Ten = "A + B → C + D", PhuongTrinh = "A + B → C + D", Bac = 2 });
            PhanUng = DanhSach[0];
            Ea = PhanUng.EaKJ;

            var the1 = HoaLyUi.The(VungTrai, "Đồ thị C–t", new Vector2(0, 0.45f), Vector2.one, new Vector2(0, 8), Vector2.zero);
            dtC = new DoThi(the1, "Đồ thị", "Nồng độ theo thời gian", "t", "C (mol/L)");
            chuThichC = HoaLyUi.Chu(the1, "Chú thích", "", 13, GlassKit.Dim, false, TextAnchor.UpperRight, new Vector2(-640, -14), new Vector2(620, 22), new Vector2(1, 1));
            var the2 = HoaLyUi.The(VungTrai, "Phân bố năng lượng", Vector2.zero, new Vector2(1, 0.45f), Vector2.zero, new Vector2(0, -8));
            dtE = new DoThi(the2, "Đồ thị", "Phân bố năng lượng phân tử – phần E ≥ Ea phản ứng được", "E (kJ/mol)", "f(E)");
            chuThichE = HoaLyUi.Chu(the2, "Chú thích", "", 13, GlassKit.Dim, false, TextAnchor.UpperRight, new Vector2(-640, -14), new Vector2(620, 22), new Vector2(1, 1));

            TieuMuc("Phản ứng", -14);
            string[] ten = new string[DanhSach.Count];
            for (int i = 0; i < ten.Length; i++) ten[i] = TenNgan(DanhSach[i]);
            NhomChip(ten, -36, 548, ChonPhanUng, 0, 2, 38, 14);
            ptChu = HoaLyUi.Chu(VungDieuKhien, "Phương trình", "", 14, GlassKit.Ink, true, TextAnchor.UpperLeft, new Vector2(20, -124), new Vector2(548, 22));
            TieuMuc("Điều kiện", -156);
            ThanhT = HoaLyUi.ThanhTruot(VungDieuKhien, "Nhiệt độ", "Nhiệt độ T", -178, 548, 273.15f, 700f, (float)T, v => { T = v; DoiThamSo(); }, v => HoaLyUi.So(v, 1) + " K (" + HoaLyUi.So(v - 273.15, 0) + " °C)");
            ThanhEa = HoaLyUi.ThanhTruot(VungDieuKhien, "Năng lượng hoạt hóa", "Năng lượng hoạt hóa Ea", -246, 548, 20f, 200f, (float)Ea, v => { Ea = v; DoiThamSo(); }, v => HoaLyUi.So(v, 0) + " kJ/mol");
            ThanhC0 = HoaLyUi.ThanhTruot(VungDieuKhien, "Nồng độ đầu", "Nồng độ đầu C₀", -314, 548, 0.1f, 2f, (float)C0, v => { C0 = v; DoiThamSo(); }, v => HoaLyUi.So(v, 2) + " M");
            nutXucTac = GlassKit.MakeToggle(VungDieuKhien, "Xúc tác", "Thêm chất xúc tác (giảm Ea)", new Vector2(20, -386), new Vector2(548, 36), false, b => { XucTac = b; DoiThamSo(); });
            nhanBeMat = HoaLyUi.Chu(VungDieuKhien, "Nhãn bề mặt", "Kẽm:", 14, GlassKit.Ink, true, TextAnchor.MiddleLeft, new Vector2(20, -430), new Vector2(90, 38));
            chipBeMat = new Button[3];
            string[] tenBm = { "Viên ×1", "Lá mỏng ×6", "Bột ×40" };
            for (int i = 0; i < 3; i++)
            {
                int ii = i;
                chipBeMat[i] = GlassKit.MakeChip(VungDieuKhien, "Bề mặt " + i, tenBm[i], new Vector2(110 + i * 154, -430), new Vector2(146, 38), () => ChonBeMat(ii), i == 0, 14);
            }

            TieuMuc("Số liệu", -484);
            oK = OSo("Hằng số tốc độ k", 0, -506);
            oDonVi = OSo("Bậc · đơn vị k", 1, -506);
            oTBanHuy = OSo("Chu kỳ bán hủy t½", 2, -506);
            oV0 = OSo("Tốc độ đầu v₀ = k·C₀ⁿ", 3, -506);
            oTiLe = OSo("k / k(298 K, không xúc tác)", 4, -506);
            oGamma = OSo("Hệ số Van 't Hoff γ", 5, -506);
            oPhanSo = HoaLyUi.Chu(VungDieuKhien, "Phần E ≥ Ea", "", 14, GlassKit.Dim, false, TextAnchor.UpperLeft, new Vector2(20, -738), new Vector2(548, 44));
            oNguon = HoaLyUi.Chu(VungDieuKhien, "Nguồn", "", 12, GlassKit.Muted, false, TextAnchor.UpperLeft, new Vector2(20, -786), new Vector2(548, 40));
            CapNhatBeMat();
        }

        static string TenNgan(PhanUngTocDo p)
        {
            switch (p.Id)
            {
                case "A_B_C_D": return "A + B → C + D";
                case "H2O2_H2O_O2": return "Phân hủy H₂O₂";
                case "2HI_H2_I2": return "Phân hủy HI";
                case "Zn_HCl": return "Zn + HCl";
            }
            return p.Id;
        }

        public void ChonPhanUng(int i)
        {
            PhanUng = DanhSach[Mathf.Clamp(i, 0, DanhSach.Count - 1)];
            var nut = VungDieuKhien.Find("Chọn " + TenNgan(PhanUng))?.GetComponent<Button>();
            if (nut != null) foreach (Transform c in VungDieuKhien) if (c.name.StartsWith("Chọn ")) GlassKit.SetChip(c.GetComponent<Button>(), c == nut.transform);
            HeSoBeMat = 1;
            for (int k = 0; k < 3; k++) GlassKit.SetChip(chipBeMat[k], k == 0);
            ThanhEa.SetValueWithoutNotify((float)PhanUng.EaKJ);
            Ea = PhanUng.EaKJ;
            // phân hủy HI cần nhiệt độ cao mới thấy được trong thời gian xem
            ThanhT.value = PhanUng.Id == "2HI_H2_I2" ? 650f : 298.15f;
            ThanhEa.onValueChanged.Invoke(ThanhEa.value);
            CapNhatBeMat();
            DoiThamSo();
        }

        void ChonBeMat(int i)
        {
            HeSoBeMat = BeMat[i];
            for (int k = 0; k < 3; k++) GlassKit.SetChip(chipBeMat[k], k == i);
            DoiThamSo();
        }

        void CapNhatBeMat()
        {
            bool co = PhanUng.CoChatRan;
            nhanBeMat.gameObject.SetActive(co);
            foreach (var b in chipBeMat) b.gameObject.SetActive(co);
        }

        public void Dat(double t, double ea, double c0) { ThanhT.value = (float)t; ThanhEa.value = (float)ea; ThanhC0.value = (float)c0; }
        public void DatXucTac(bool b) => nutXucTac.isOn = b;

        void DoiThamSo()
        {
            tChay = 0;
            LamMoiChu();
        }

        public override void KhiHien() { base.KhiHien(); tChay = 0; }

        public override void CapNhat(float dt)
        {
            SoKhung++;
            int bac = PhanUng.Bac;
            double k = K, tHalf = ThoiGianBanHuy;
            // trục thời gian: đủ 4 chu kỳ bán hủy (bậc 0: tới khi hết)
            double tCuoi = bac == 0 ? C0 / k * 1.1 : 4 * tHalf;
            if (double.IsNaN(tCuoi) || double.IsInfinity(tCuoi) || tCuoi <= 0) tCuoi = 1;
            TCuoiTruc = tCuoi;
            tChay += dt / ThoiGianChay;
            if (tChay > 1 + Nghi / ThoiGianChay) tChay = 0;
            float tien = Mathf.Clamp01(tChay);
            ThoiDiem = tien * tCuoi;

            dtC.XMin = 0; dtC.XMax = tCuoi; dtC.YMin = 0; dtC.YMax = Math.Max(0.1, C0 * 1.08);
            var ve = dtC.Ve;
            ve.BatDau();
            dtC.VeKhung();
            var vung = dtC.Vung;
            // mốc 298 K không xúc tác (nét đứt mờ)
            double kMoc = KMoc;
            var mocMau = HoaLyUi.C32(GlassKit.Muted, 0.75f);
            for (int i = 0; i < duong.Length; i++)
            {
                double t = tCuoi * i / (duong.Length - 1);
                duong[i] = dtC.Diem(t, TocDoMoHinh.NongDo(bac, C0, kMoc, t));
            }
            for (int i = 1; i < duong.Length; i += 2) ve.VeDoan(duong[i - 1], duong[i], 2f, mocMau);
            // đường hiện tại: chất đầu giảm, sản phẩm tăng – vẽ tới con trỏ, có tô nhạt phía dưới
            var mA = HoaLyUi.C32(PhanUng.MauChatDau);
            var mC = HoaLyUi.C32(PhanUng.MauSanPham);
            int den = Mathf.Max(1, Mathf.RoundToInt(tien * (duong.Length - 1)));
            for (int i = 0; i <= den; i++)
            {
                double t = tCuoi * i / (duong.Length - 1);
                duong[i] = dtC.Diem(t, TocDoMoHinh.NongDo(bac, C0, k, t));
            }
            for (int i = 1; i <= den; i++) ve.VeDai(duong[i - 1].x, duong[i - 1].y, duong[i].x, duong[i].y, vung.yMin, new Color32(mA.r, mA.g, mA.b, 40));
            ve.VeDuong(duong, den + 1, 3.2f, mA);
            for (int i = 0; i <= den; i++)
            {
                double t = tCuoi * i / (duong.Length - 1);
                duong[i] = dtC.Diem(t, C0 - TocDoMoHinh.NongDo(bac, C0, k, t));
            }
            ve.VeDuong(duong, den + 1, 3.2f, mC);
            // con trỏ thời gian + chấm đầu đường
            float xc = dtC.X(ThoiDiem);
            ve.VeHop(xc - 1, vung.yMin, xc + 1, vung.yMax, HoaLyUi.C32(GlassKit.Ink, 0.35f));
            double cNow = TocDoMoHinh.NongDo(bac, C0, k, ThoiDiem);
            ve.VeHat(xc, dtC.Y(cNow), 7, mA);
            ve.VeHat(xc, dtC.Y(C0 - cNow), 7, mC);
            // mốc t½
            if (bac >= 0 && tHalf < tCuoi)
                ve.VeNetDut(new Vector2(dtC.X(tHalf), vung.yMin), new Vector2(dtC.X(tHalf), dtC.Y(C0 / 2)), 1.5f, 5, HoaLyUi.C32(GlassKit.Accent2, 0.9f));
            ve.KetThuc();

            // phân bố năng lượng ở T (và ở 298 K để so)
            double eaHd = EaHieuDung;
            double eMax = Math.Max(eaHd * 1.5, Math.Max(Ea * 1.25, 12 * TocDoMoHinh.R_SI * T / 1000));
            double fMax = TocDoMoHinh.MatDoNangLuong(TocDoMoHinh.R_SI * 298.15 / 2000, 298.15);
            dtE.XMin = 0; dtE.XMax = eMax; dtE.YMin = 0; dtE.YMax = fMax * 1.1;
            var ve2 = dtE.Ve;
            ve2.BatDau();
            dtE.VeKhung();
            var vung2 = dtE.Vung;
            var mauT = HoaLyUi.C32(GlassKit.Accent);
            var toT = HoaLyUi.C32(GlassKit.Accent2, 0.55f);
            for (int i = 0; i < duong.Length; i++)
            {
                double e = eMax * i / (duong.Length - 1);
                duong[i] = dtE.Diem(e, TocDoMoHinh.MatDoNangLuong(e, 298.15));
            }
            for (int i = 1; i < duong.Length; i += 2) ve2.VeDoan(duong[i - 1], duong[i], 2f, mocMau);
            for (int i = 0; i < duong.Length; i++)
            {
                double e = eMax * i / (duong.Length - 1);
                duong[i] = dtE.Diem(e, TocDoMoHinh.MatDoNangLuong(e, T));
                if (i > 0 && e >= eaHd) ve2.VeDai(duong[i - 1].x, duong[i - 1].y, duong[i].x, duong[i].y, vung2.yMin, toT);
            }
            ve2.VeDuong(duong, duong.Length, 3f, mauT);
            ve2.VeNetDut(new Vector2(dtE.X(Ea), vung2.yMin), new Vector2(dtE.X(Ea), vung2.yMax), 2f, 7, HoaLyUi.C32(GlassKit.Bad, 0.9f));
            if (XucTac) ve2.VeNetDut(new Vector2(dtE.X(eaHd), vung2.yMin), new Vector2(dtE.X(eaHd), vung2.yMax), 2f, 7, HoaLyUi.C32(GlassKit.Good, 0.95f));
            ve2.KetThuc();

            if (DenLucCapNhatChu(dt)) CapNhatChu();
        }

        void CapNhatChu()
        {
            int bac = PhanUng.Bac;
            double k = K;
            dtC.NhanX = "t (s) · cả trục " + HoaLyUi.ThoiGian(TCuoiTruc);
            DatChu(ptChu, PhanUng.PhuongTrinh + (XucTac && !string.IsNullOrEmpty(PhanUng.XucTac) ? "   · xúc tác " + PhanUng.XucTac : ""));
            DatChu(chuThichC, $"<color=#{ColorUtility.ToHtmlStringRGB(new Color(PhanUng.MauChatDau[0], PhanUng.MauChatDau[1], PhanUng.MauChatDau[2]))}>━ {PhanUng.ChatDau}</color>   " +
                              $"<color=#{ColorUtility.ToHtmlStringRGB(new Color(PhanUng.MauSanPham[0], PhanUng.MauSanPham[1], PhanUng.MauSanPham[2]))}>━ {PhanUng.SanPham}</color>   ┅ mốc 298 K   t = {HoaLyUi.ThoiGian(ThoiDiem)}");
            DatChu(chuThichE, $"━ {HoaLyUi.So(T, 0)} K   ┅ 298 K   <color=#{ColorUtility.ToHtmlStringRGB(GlassKit.Bad)}>┆ Ea</color>" + (XucTac ? $"   <color=#{ColorUtility.ToHtmlStringRGB(GlassKit.Good)}>┆ Ea xúc tác</color>" : ""));
            DatChu(oK, HoaLyUi.So(k, 4));
            DatChu(oDonVi, "bậc " + bac + " · " + (bac == 0 ? "M/s" : bac == 1 ? "s⁻¹" : "M⁻¹·s⁻¹"));
            DatChu(oTBanHuy, HoaLyUi.ThoiGian(ThoiGianBanHuy));
            DatChu(oV0, HoaLyUi.So(TocDoMoHinh.TocDoTucThoi(bac, C0, k), 4) + " M/s");
            DatChu(oTiLe, "× " + HoaLyUi.So(k / KMoc, 3));
            DatChu(oGamma, HoaLyUi.So(TocDoMoHinh.HeSoVantHoff(EaHieuDung, T), 3) + " (tăng 10 K)");
            var pb = TocDoMoHinh.PhanBoNangLuong3D(EaHieuDung, T);
            DatChu(oPhanSo, $"Phần phân tử có E ≥ Ea ({HoaLyUi.So(EaHieuDung, 0)} kJ/mol): <b>{HoaLyUi.So(pb.TiLe3D * 100, 4)} %</b> · e^(−Ea/RT) = {HoaLyUi.So(pb.ExpAmX, 3)}" +
                             (XucTac ? $" · xúc tác giảm Ea {HoaLyUi.So(Ea - EaHieuDung, 0)} kJ/mol → nhanh ×{HoaLyUi.So(TocDoMoHinh.HeSoTangXucTac(Ea - EaHieuDung, T), 1)}" : ""));
            DatChu(oNguon, "A = " + HoaLyUi.So(PhanUng.A * HeSoBeMat, 2) + " · ΔH = " + HoaLyUi.So(PhanUng.DHKJ, 1) + " kJ/mol · " + PhanUng.Nguon);
        }
    }
}
