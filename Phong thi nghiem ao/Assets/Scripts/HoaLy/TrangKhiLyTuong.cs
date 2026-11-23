using System;
using System.Collections.Generic;
using Khkt.Demo;
using Khkt.Rendering;
using UnityEngine;
using UnityEngine.UI;

namespace Khkt.HoaLy
{
    /// <summary>
    /// Khí lý tưởng: bình có pít-tông, hạt chuyển động theo phân bố Maxwell (số hạt theo HardwareProfile.SoHatToiDa),
    /// đồ thị phân bố tốc độ (cột đếm hạt + đường Maxwell lý thuyết), pV = nRT, Van der Waals.
    /// </summary>
    public sealed class TrangKhiLyTuong : TrangHoaLy
    {
        public override string Ma => "khi_ly_tuong";
        public override string Ten => "Khí lý tưởng";
        public override string MoTa => "Phương trình trạng thái pV = nRT, thuyết động học phân tử và khí thực Van der Waals. Kéo V, T, n để xem hạt và áp suất đổi.";
        public override string TepCauHoi => "cau_hoi_khi_ly_tuong";

        public const int SucChua = 3000;
        public const double VMax = 50, NMax = 5;

        // tham số (đọc được từ kiểm thử)
        public double V { get; private set; } = 24.79;
        public double T { get; private set; } = 298.15;
        public double N { get; private set; } = 1.0;
        public bool KhiThuc { get; private set; }
        public LoaiKhi Khi { get; private set; }
        public readonly List<LoaiKhi> DanhSachKhi = new List<LoaiKhi>();
        public HopKhi2D Hop { get; private set; }

        // kết quả
        public double P => KhiLyTuongMoHinh.ApSuat(N, V, T);
        public KetQuaVanDerWaals Vdw => KhiLyTuongMoHinh.VanDerWaals(N, V, T, Khi.A, Khi.B);
        public double VRms => KhiLyTuongMoHinh.VRms(Khi.M, T);
        public int SoHatMucTieu => Math.Max(20, Math.Min(HardwareProfile.SoHatToiDa, (int)Math.Round(N / NMax * HardwareProfile.SoHatToiDa)));

        public Slider ThanhV, ThanhT, ThanhN;
        Toggle nutKhiThuc;
        LuoiVe veBinh;
        DoThi doThi;
        RectTransform theBinh;
        Text nhanV, nhanKhi, congThuc;
        Text oP, oPv, oVrms, oVtb, oDongNang, oNoiNang, oSoHat, oVaCham;
        readonly int[] ngan = new int[40];
        readonly Vector2[] duong = new Vector2[121];
        float rongHien = -1, vMaxHien;
        double vaChamGiay;

        protected override void Dung()
        {
            var ta = Resources.Load<TextAsset>("HoaLy/khi");
            if (ta != null)
                foreach (var o in JsonHoaLy.L(JsonHoaLy.Lay(JsonHoaLy.D(JsonHoaLy.Doc(ta.text)), "danh_sach")))
                {
                    var d = JsonHoaLy.D(o);
                    var mau = MauHex.Doc(JsonHoaLy.S(d, "mau")) ?? new[] { 0.6f, 0.8f, 1f };
                    DanhSachKhi.Add(new LoaiKhi
                    {
                        Id = JsonHoaLy.S(d, "id"), Ten = JsonHoaLy.S(d, "ten"), CongThuc = JsonHoaLy.S(d, "ct"),
                        M = JsonHoaLy.F(d, "M", 28), Gamma = JsonHoaLy.F(d, "gamma", 1.4), A = JsonHoaLy.F(d, "a"), B = JsonHoaLy.F(d, "b"),
                        R = mau[0], G = mau[1], Bl = mau[2],
                    });
                }
            if (DanhSachKhi.Count == 0) DanhSachKhi.Add(new LoaiKhi { Id = "N2", Ten = "Nitrogen", CongThuc = "N₂", M = 28.013, Gamma = 1.4, A = 1.37, B = 0.0387 });
            Khi = DanhSachKhi[0];
            Hop = new HopKhi2D(SucChua, 20261118);
            Hop.KhoiTao(SoHatMucTieu, Khi.M, T);

            // trái: bình khí (trên) + đồ thị Maxwell (dưới)
            theBinh = HoaLyUi.The(VungTrai, "Bình khí", new Vector2(0, 0.42f), Vector2.one, new Vector2(0, 8), Vector2.zero);
            HoaLyUi.Chu(theBinh, "Tiêu đề", "Bình khí có pít-tông", 16, GlassKit.Ink, true, TextAnchor.UpperLeft, new Vector2(18, -12), new Vector2(500, 24));
            nhanKhi = HoaLyUi.Chu(theBinh, "Khí", "", 14, GlassKit.Dim, false, TextAnchor.UpperRight, new Vector2(-520, -14), new Vector2(500, 22), new Vector2(1, 1));
            veBinh = LuoiVe.Tao(theBinh, "Lưới bình");
            nhanV = HoaLyUi.Chu(theBinh, "Nhãn V", "", 14, GlassKit.Accent2, true, TextAnchor.UpperCenter, Vector2.zero, new Vector2(160, 22), new Vector2(0.5f, 0.5f));
            nhanV.rectTransform.pivot = new Vector2(0.5f, 1f);

            var theDoThi = HoaLyUi.The(VungTrai, "Đồ thị Maxwell", Vector2.zero, new Vector2(1, 0.42f), Vector2.zero, new Vector2(0, -8));
            doThi = new DoThi(theDoThi, "Đồ thị", "Phân bố tốc độ phân tử (Maxwell–Boltzmann)", "v (m/s)", "số hạt / ngăn");

            // phải: điều khiển
            TieuMuc("Chất khí", -14);
            var ten = new string[Math.Min(8, DanhSachKhi.Count)];
            for (int i = 0; i < ten.Length; i++) ten[i] = DanhSachKhi[i].Id == "Air" ? "Không khí" : DanhSachKhi[i].CongThuc;
            NhomChip(ten, -36, 548, ChonKhi, 0, 4, 38, 14);
            TieuMuc("Tham số", -132);
            ThanhV = HoaLyUi.ThanhTruot(VungDieuKhien, "Thể tích", "Thể tích V", -154, 548, 0.5f, (float)VMax, (float)V, v => { V = v; LamMoiChu(); }, v => HoaLyUi.So(v, 2) + " L");
            ThanhT = HoaLyUi.ThanhTruot(VungDieuKhien, "Nhiệt độ", "Nhiệt độ T", -222, 548, 50f, 1500f, (float)T, v => { T = v; Hop.DatNhietDo(T); LamMoiChu(); }, v => HoaLyUi.So(v, 1) + " K");
            ThanhN = HoaLyUi.ThanhTruot(VungDieuKhien, "Số mol", "Lượng khí n", -290, 548, 0.05f, (float)NMax, (float)N, v => { N = v; LamMoiChu(); }, v => HoaLyUi.So(v, 2) + " mol");
            nutKhiThuc = GlassKit.MakeToggle(VungDieuKhien, "Khí thực", "So sánh khí thực (Van der Waals)", new Vector2(20, -362), new Vector2(548, 36), false, b => { KhiThuc = b; LamMoiChu(); });

            TieuMuc("Số liệu", -410);
            oP = OSo("Áp suất p = nRT/V", 0, -432);
            oPv = OSo("Van der Waals: p · Z", 1, -432);
            oVrms = OSo("v_rms = √(3RT/M)", 2, -432);
            oVtb = OSo("v_tb · v_p", 3, -432);
            oDongNang = OSo("Động năng TB 3/2·k_B·T", 4, -432);
            oNoiNang = OSo("Nội năng U = n·Cv·T", 5, -432);
            oSoHat = OSo("Hạt đang vẽ", 6, -432);
            oVaCham = OSo("Va chạm pít-tông / s", 7, -432);
            congThuc = HoaLyUi.Chu(VungDieuKhien, "Công thức", "", 14, GlassKit.Dim, false, TextAnchor.UpperLeft, new Vector2(20, -734), new Vector2(548, 90));
            vMaxHien = (float)VTruc(Khi.M);
        }

        static double VTruc(double M) => 2.2 * KhiLyTuongMoHinh.VRms(M, 1000);

        void ChonKhi(int i)
        {
            Khi = DanhSachKhi[i];
            Hop.DatKhoiLuongMol(Khi.M);
            LamMoiChu();
        }

        /// <summary>Đặt tham số trực tiếp (qua thanh trượt, như người dùng kéo).</summary>
        public void Dat(double v, double t, double n)
        {
            ThanhV.value = (float)v; ThanhT.value = (float)t; ThanhN.value = (float)n;
        }

        public void ChonKhiTheoId(string id)
        {
            int i = DanhSachKhi.FindIndex(k => k.Id == id);
            if (i < 0) return;
            var nut = VungDieuKhien.Find("Chọn " + (id == "Air" ? "Không khí" : DanhSachKhi[i].CongThuc))?.GetComponent<Button>();
            if (nut != null) nut.onClick.Invoke(); else ChonKhi(i);
        }

        public void DatKhiThuc(bool b) => nutKhiThuc.isOn = b;

        public override void CapNhat(float dt)
        {
            SoKhung++;
            int mucTieu = SoHatMucTieu;
            if (Hop.SoHat != mucTieu) Hop.DatSoHat(mucTieu);

            var r = veBinh.rectTransform.rect;
            // vùng bình: lề, tỉ lệ ô vuông (Cao = 1 đơn vị)
            float le = 26, top = 52;
            var binh = Rect.MinMaxRect(r.xMin + le, r.yMin + le + 8, r.xMax - le - 30, r.yMax - top);
            float aspect = binh.width / Mathf.Max(1, binh.height);
            float rongDich = Mathf.Max(0.04f, (float)(V / VMax)) * aspect;
            rongHien = rongHien < 0 ? rongDich : HoaLyUi.Muot(rongHien, rongDich, 0.12f, dt);
            Hop.DatRong(rongHien);
            // 515 m/s (N₂, 25 °C) đi hết chiều cao bình trong ~1,6 s
            Hop.Buoc(dt, 1f / (515f * 1.6f));
            if (Hop.ThoiGianDem >= 1.0) { vaChamGiay = Hop.VaChamPitTong / Hop.ThoiGianDem; Hop.DatLaiDem(); }

            float s = binh.height;   // px trên 1 đơn vị
            veBinh.BatDau();
            var nen = HoaLyUi.C32(GlassKit.Well);
            var vien = HoaLyUi.C32(GlassKit.Dim, 0.85f);
            float xPit = binh.xMin + rongHien * s;
            veBinh.VeHop(binh.xMin, binh.yMin, xPit, binh.yMax, nen);
            // thành bình (ba phía), pít-tông và cán
            veBinh.VeHop(binh.xMin - 4, binh.yMin - 4, binh.xMax + 30, binh.yMin, vien);
            veBinh.VeHop(binh.xMin - 4, binh.yMax, binh.xMax + 30, binh.yMax + 4, vien);
            veBinh.VeHop(binh.xMin - 4, binh.yMin - 4, binh.xMin, binh.yMax + 4, vien);
            var mauPit = HoaLyUi.C32(GlassKit.Accent);
            veBinh.VeHop(xPit, binh.yMin, xPit + 12, binh.yMax, mauPit);
            veBinh.VeHop(xPit + 12, (binh.yMin + binh.yMax) / 2 - 5, binh.xMax + 30, (binh.yMin + binh.yMax) / 2 + 5, HoaLyUi.C32(GlassKit.Accent, 0.6f));
            // hạt: màu theo tốc độ (so với v_rms của chính khí ở 300 K)
            float vRef = (float)KhiLyTuongMoHinh.VRms(Khi.M, 300) * 2.2f;
            float rHat = Mathf.Clamp(5.5f - Hop.SoHat / 900f, 2.6f, 5.5f);
            for (int i = 0; i < Hop.SoHat; i++)
            {
                float v = Mathf.Sqrt(Hop.VX[i] * Hop.VX[i] + Hop.VY[i] * Hop.VY[i] + Hop.VZ[i] * Hop.VZ[i]);
                veBinh.VeHat(binh.xMin + Hop.X[i] * s, binh.yMin + Hop.Y[i] * s, rHat, HoaLyUi.MauNhiet(v / vRef));
            }
            veBinh.KetThuc();
            nhanV.rectTransform.anchoredPosition = new Vector2(xPit + 6, binh.yMin - 6);

            // đồ thị phân bố tốc độ
            vMaxHien = HoaLyUi.Muot(vMaxHien, (float)VTruc(Khi.M), 0.25f, dt);
            Hop.BieuDoTocDo(ngan, vMaxHien);
            double dv = vMaxHien / ngan.Length;
            double M_kg = Khi.M * 1e-3;
            double fMax = KhiLyTuongMoHinh.PhanBoMaxwell(KhiLyTuongMoHinh.VP(Khi.M, T), M_kg, T) * Hop.SoHat * dv;
            int demMax = 0;
            for (int i = 0; i < ngan.Length; i++) demMax = Math.Max(demMax, ngan[i]);
            doThi.XMin = 0; doThi.XMax = vMaxHien;
            doThi.YMin = 0;
            double yDich = Math.Max(5, Math.Max(fMax, demMax) * 1.15);
            doThi.YMax = doThi.YMax <= 1 ? yDich : HoaLyUi.Muot(doThi.YMax, yDich, 0.3, dt);
            if (Math.Abs(doThi.YMax / yDich - 1) < 0.01) doThi.YMax = yDich;
            var ve = doThi.Ve;
            ve.BatDau();
            doThi.VeKhung();
            var vung = doThi.Vung;
            var mauCot = HoaLyUi.C32(GlassKit.Accent, 0.38f);
            for (int i = 0; i < ngan.Length; i++)
            {
                if (ngan[i] == 0) continue;
                ve.VeHop(doThi.X(i * dv) + 1, vung.yMin, doThi.X((i + 1) * dv) - 1, doThi.Y(ngan[i]), mauCot);
            }
            for (int i = 0; i < duong.Length; i++)
            {
                double v = vMaxHien * i / (duong.Length - 1);
                duong[i] = doThi.Diem(v, KhiLyTuongMoHinh.PhanBoMaxwell(v, M_kg, T) * Hop.SoHat * dv);
            }
            ve.VeDuong(duong, duong.Length, 3f, HoaLyUi.C32(GlassKit.Accent2));
            // vạch v_p, v_tb, v_rms
            VachDung(ve, KhiLyTuongMoHinh.VP(Khi.M, T), HoaLyUi.C32(GlassKit.Good, 0.9f));
            VachDung(ve, KhiLyTuongMoHinh.VTb(Khi.M, T), HoaLyUi.C32(GlassKit.Ink, 0.6f));
            VachDung(ve, VRms, HoaLyUi.C32(GlassKit.Bad, 0.9f));
            ve.KetThuc();

            if (DenLucCapNhatChu(dt)) CapNhatChu();
        }

        void VachDung(LuoiVe ve, double v, Color32 mau)
        {
            if (v > doThi.XMax) return;
            var vung = doThi.Vung;
            ve.VeNetDut(new Vector2(doThi.X(v), vung.yMin), new Vector2(doThi.X(v), vung.yMax), 2f, 6f, mau);
        }

        void CapNhatChu()
        {
            DatChu(nhanV, "V = " + HoaLyUi.So(V, 2) + " L");
            DatChu(nhanKhi, $"{Khi.Ten} ({Khi.CongThuc}) · M = {HoaLyUi.So(Khi.M, 3)} g/mol · γ = {HoaLyUi.So(Khi.Gamma, 2)}");
            var vdw = Vdw;
            DatChu(oP, HoaLyUi.So(P, 3) + " bar");
            DatChu(oPv, KhiThuc ? (double.IsInfinity(vdw.PVdw) ? "V < nb!" : HoaLyUi.So(vdw.PVdw, 3) + " bar · Z " + HoaLyUi.So(vdw.Z, 3)) : "(bật khí thực)");
            DatChu(oVrms, HoaLyUi.So(VRms, 1) + " m/s");
            DatChu(oVtb, HoaLyUi.So(KhiLyTuongMoHinh.VTb(Khi.M, T), 0) + " · " + HoaLyUi.So(KhiLyTuongMoHinh.VP(Khi.M, T), 0) + " m/s");
            DatChu(oDongNang, HoaLyUi.So(KhiLyTuongMoHinh.DongNangTb(T) * 1e21, 3) + "·10⁻²¹ J");
            DatChu(oNoiNang, HoaLyUi.So(KhiLyTuongMoHinh.NoiNang(N, T, Khi.Gamma), 0) + " J");
            DatChu(oSoHat, Hop.SoHat + " / tối đa " + HardwareProfile.SoHatToiDa);
            DatChu(oVaCham, HoaLyUi.So(vaChamGiay, 0));
            DatChu(congThuc, $"p = nRT/V = {HoaLyUi.So(N, 2)} × 0,08314 × {HoaLyUi.So(T, 1)} / {HoaLyUi.So(V, 2)} = <b>{HoaLyUi.So(P, 3)} bar</b>\n" +
                             "<color=#" + ColorUtility.ToHtmlStringRGB(GlassKit.Good) + ">┆ v_p</color>   <color=#" + ColorUtility.ToHtmlStringRGB(GlassKit.Ink) + ">┆ v_tb</color>   <color=#" +
                             ColorUtility.ToHtmlStringRGB(GlassKit.Bad) + ">┆ v_rms</color>   ▮ hạt đếm được   — Maxwell lý thuyết");
        }
    }
}
