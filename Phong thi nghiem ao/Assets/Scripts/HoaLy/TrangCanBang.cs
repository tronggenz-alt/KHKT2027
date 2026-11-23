using System;
using System.Collections.Generic;
using Khkt.Demo;
using UnityEngine;
using UnityEngine.UI;

namespace Khkt.HoaLy
{
    /// <summary>
    /// Cân bằng hóa học – nguyên lý Le Chatelier: đồ thị nồng độ – thời gian chạy liên tục (cửa sổ 40 s) có mốc tác động,
    /// ống nghiệm đổi màu theo nồng độ chất có màu, thang Q so với K (log) và lời giải thích chiều chuyển dịch.
    /// </summary>
    public sealed class TrangCanBang : TrangHoaLy
    {
        public override string Ma => "can_bang";
        public override string Ten => "Cân bằng – Le Chatelier";
        public override string MoTa => "Hằng số cân bằng K(T), thương số Q và chiều chuyển dịch. Đun nóng, nén, thêm chất hay xúc tác rồi xem hệ tự điều chỉnh.";
        public override string TepCauHoi => "cau_hoi_can_bang";

        public readonly List<HeCanBang> DanhSach = new List<HeCanBang>();
        public HeCanBang He { get; private set; }
        public double V { get; private set; } = 1;
        public double ThoiGian { get; private set; }
        /// <summary>Hệ số tua nhanh thời gian mô phỏng (kiểm thử đặt &gt; 1).</summary>
        public float TuaNhanh = 1f;

        // kết quả tác động gần nhất (cho kiểm thử)
        public string TacDongCuoi { get; private set; } = "";
        public ChieuDich ChieuDuDoan { get; private set; } = ChieuDich.CanBang;
        /// <summary>Tổng độ tiến ξ (mol/L) hệ đã đi kể từ tác động gần nhất: dương = thuận, âm = nghịch.</summary>
        public double XiSauTacDong { get; private set; }
        public double[] CTruocTacDong { get; private set; } = new double[0];

        public Slider ThanhT, ThanhV;
        Toggle nutXucTac;
        readonly List<Button> nutThem = new List<Button>();
        DoThi dtC;
        LuoiVe veOng;
        Text chuThich, giaiThich, nhanQ, nhanK, nhanChieu, oK, oQ, oTiLe, oVtVn, oDn, oDh, nhanOng;
        // lịch sử nồng độ (vòng tròn) — cấp sẵn
        const int SoMau = 400;
        const double CuaSo = 40;
        readonly double[,] lichSu = new double[4, SoMau];
        readonly double[] thoiDiem = new double[SoMau];
        int dauLs, soLs;
        double henMau;
        readonly double[] mocT = new double[8];
        readonly string[] mocTen = new string[8];
        readonly Text[] mocChu = new Text[8];
        int soMoc;
        readonly Vector2[] duong = new Vector2[SoMau];
        readonly float[] mau = new float[4];
        float yMaxHien;
        static readonly double[] TMin = { 273.15, 600, 600, 278.15 }, TMax = { 373.15, 800, 900, 348.15 }, TDau = { 298.15, 700, 723.15, 298.15 };
        static readonly double[] DoDamBaoHoa = { 0.03, 0.06, 1, 0.006 };

        protected override void Dung()
        {
            var ta = Resources.Load<TextAsset>("HoaLy/he_can_bang");
            if (ta != null) DanhSach.AddRange(CanBangMoHinh.DocDanhSach(ta.text));

            var the1 = HoaLyUi.The(VungTrai, "Đồ thị nồng độ", new Vector2(0, 0.42f), Vector2.one, new Vector2(0, 8), Vector2.zero);
            dtC = new DoThi(the1, "Đồ thị", "Nồng độ theo thời gian", "t (s)", "C (mol/L)");
            chuThich = dtC.ChuThich;
            for (int i = 0; i < mocChu.Length; i++)
            {
                mocChu[i] = HoaLyUi.Chu(the1, "Mốc", "", 12, GlassKit.Accent2, true, TextAnchor.UpperLeft, Vector2.zero, new Vector2(140, 18), new Vector2(0.5f, 0.5f));
                mocChu[i].gameObject.SetActive(false);
            }

            var the2 = HoaLyUi.The(VungTrai, "Ống nghiệm và Q–K", Vector2.zero, new Vector2(1, 0.42f), Vector2.zero, new Vector2(0, -8));
            HoaLyUi.Chu(the2, "Tiêu đề", "Màu hệ · Q so với K", 16, GlassKit.Ink, true, TextAnchor.UpperLeft, new Vector2(18, -12), new Vector2(400, 24));
            veOng = LuoiVe.Tao(the2, "Lưới ống");
            nhanOng = HoaLyUi.Chu(the2, "Nhãn ống", "", 12, GlassKit.Dim, false, TextAnchor.UpperCenter, new Vector2(30, 34), new Vector2(170, 34), new Vector2(0, 0));
            nhanQ = HoaLyUi.Chu(the2, "Nhãn Q", "Q", 14, GlassKit.Accent2, true, TextAnchor.LowerCenter, Vector2.zero, new Vector2(160, 20), new Vector2(0.5f, 0.5f));
            nhanQ.rectTransform.pivot = new Vector2(0.5f, 0);
            nhanK = HoaLyUi.Chu(the2, "Nhãn K", "K", 14, GlassKit.Accent, true, TextAnchor.UpperCenter, Vector2.zero, new Vector2(160, 20), new Vector2(0.5f, 0.5f));
            nhanK.rectTransform.pivot = new Vector2(0.5f, 1);
            nhanChieu = HoaLyUi.Chu(the2, "Chiều", "", 20, GlassKit.Ink, true, TextAnchor.UpperLeft, new Vector2(240, -50), new Vector2(900, 30));
            giaiThich = HoaLyUi.Chu(the2, "Giải thích", "", 15, GlassKit.Dim, false, TextAnchor.UpperLeft, new Vector2(240, -188), new Vector2(940, 130));

            TieuMuc("Hệ cân bằng", -14);
            string[] ten = new string[DanhSach.Count];
            for (int i = 0; i < ten.Length; i++) ten[i] = TenNgan(DanhSach[i].Id);
            NhomChip(ten, -36, 548, i => ChonHe(i), 0, 2, 38, 14);
            TieuMuc("Tác động lên hệ", -134);
            ThanhT = HoaLyUi.ThanhTruot(VungDieuKhien, "Nhiệt độ", "Nhiệt độ T", -156, 548, 273.15f, 373.15f, 298.15f, v => DoiT(v), v => HoaLyUi.So(v, 1) + " K");
            ThanhV = HoaLyUi.ThanhTruot(VungDieuKhien, "Thể tích", "Thể tích bình V (nén ← → giãn)", -224, 548, 0.2f, 2f, 1f, v => DoiV(v), v => HoaLyUi.So(v, 2) + " L");
            for (int i = 0; i < 3; i++)
            {
                int ii = i;
                nutThem.Add(GlassKit.MakeButton(VungDieuKhien, "Thêm " + i, "+", new Vector2(20 + i * 186, -296), new Vector2(176, 42), () => ThemChat(ii), false, 14));
            }
            nutXucTac = GlassKit.MakeToggle(VungDieuKhien, "Xúc tác", "Chất xúc tác (K không đổi, nhanh hơn)", new Vector2(20, -350), new Vector2(380, 36), false, b => { if (He != null) { He.CoXucTac = b; DanhDau("xúc tác"); } });
            GlassKit.MakeButton(VungDieuKhien, "Làm lại", "Làm lại", new Vector2(420, -348), new Vector2(148, 40), () => ChonHe(DanhSach.IndexOf(He)), false, 14);

            TieuMuc("Số liệu", -404);
            oK = OSo("Hằng số cân bằng Kc(T)", 0, -426);
            oQ = OSo("Thương số Q", 1, -426);
            oTiLe = OSo("Q / K", 2, -426);
            oVtVn = OSo("v_thuận / v_nghịch = K/Q", 3, -426);
            oDn = OSo("Δn khí", 4, -426);
            oDh = OSo("ΔH phản ứng thuận", 5, -426);
            if (DanhSach.Count > 0) ChonHe(0);
        }

        static string TenNgan(string id)
        {
            switch (id)
            {
                case "N2O4_2NO2": return "2NO₂ ⇌ N₂O₄";
                case "H2_I2_2HI": return "H₂ + I₂ ⇌ 2HI";
                case "N2_3H2_2NH3": return "Haber NH₃";
                case "Fe3_SCN_FeSCN": return "Fe³⁺ + SCN⁻";
            }
            return id;
        }

        public void ChonHe(int i)
        {
            if (DanhSach.Count == 0) return;
            i = Mathf.Clamp(i, 0, DanhSach.Count - 1);
            He = DanhSach[i];
            foreach (Transform c in VungDieuKhien)
                if (c.name.StartsWith("Chọn ")) GlassKit.SetChip(c.GetComponent<Button>(), c.name == "Chọn " + TenNgan(He.Id));
            He.CoXucTac = false;
            nutXucTac.SetIsOnWithoutNotify(false);
            He.T = TDau[i];
            V = 1;
            ThanhT.minValue = (float)TMin[i]; ThanhT.maxValue = (float)TMax[i];
            ThanhT.SetValueWithoutNotify((float)TDau[i]);
            ThanhT.onValueChanged.Invoke(ThanhT.value);   // chỉ cập nhật nhãn: He.T đã bằng giá trị này nên DoiT bỏ qua
            ThanhV.SetValueWithoutNotify(1); ThanhV.onValueChanged.Invoke(1);
            // nồng độ đầu: chỉ có chất đầu (riêng N₂O₄ ⇌ NO₂ bắt đầu từ N₂O₄ như bảng ICE của SGK)
            for (int k = 0; k < He.Chat.Length; k++)
            {
                var ch = He.Chat[k];
                if (He.Id == "N2O4_2NO2") He.C[k] = ch.SanPham ? 0.04 : 0;
                else if (He.Id == "N2_3H2_2NH3") He.C[k] = ch.SanPham ? 0 : 0.5 * ch.HeSo;
                else if (He.Id == "Fe3_SCN_FeSCN") He.C[k] = ch.SanPham ? 0 : 0.004;
                else He.C[k] = ch.SanPham ? 0 : 0.1;
            }
            for (int k = 0; k < nutThem.Count; k++)
            {
                bool co = k < He.Chat.Length;
                nutThem[k].gameObject.SetActive(co);
                if (co) nutThem[k].transform.Find("Chữ").GetComponent<Text>().text = "+ " + HoaLyUi.CongThuc(He.Chat[k].CongThuc);
            }
            ThoiGian = 0; soLs = 0; dauLs = 0; soMoc = 0; yMaxHien = 0;
            foreach (var m in mocChu) m.gameObject.SetActive(false);
            DanhDau("bắt đầu");
            LamMoiChu();
        }

        void DoiT(float v)
        {
            if (He == null || Math.Abs(He.T - v) < 1e-6) return;
            bool tang = v > He.T;
            He.T = v;
            DanhDau(tang ? "đun nóng" : "làm lạnh");
        }

        void DoiV(float v)
        {
            if (He == null || Math.Abs(V - v) < 1e-9) return;
            bool nen = v < V;
            He.DoiTheTich(V, v);
            V = v;
            DanhDau(He.CoKhi ? (nen ? "nén" : "giãn") : (nen ? "cô đặc" : "pha loãng"));
        }

        public void ThemChat(int k)
        {
            if (He == null || k >= He.Chat.Length) return;
            double tong = 0;
            foreach (var c in He.C) tong += c;
            He.C[k] += Math.Max(0.002, 0.25 * tong / He.C.Length);
            DanhDau("thêm " + HoaLyUi.CongThuc(He.Chat[k].CongThuc));
        }

        public void Dat(double T, double v) { ThanhT.value = (float)T; ThanhV.value = (float)v; }
        public void DatXucTac(bool b) => nutXucTac.isOn = b;

        /// <summary>Ghi mốc tác động: dự đoán chiều theo Q và K ngay sau tác động, đặt lại ξ tích lũy.</summary>
        void DanhDau(string ten)
        {
            TacDongCuoi = ten;
            ChieuDuDoan = CanBangMoHinh.Chieu(He.Q(), He.K);
            XiSauTacDong = 0;
            CTruocTacDong = (double[])He.C.Clone();
            if (ten != "bắt đầu" && ten != "xúc tác")
            {
                if (soMoc == mocT.Length) { Array.Copy(mocT, 1, mocT, 0, mocT.Length - 1); Array.Copy(mocTen, 1, mocTen, 0, mocTen.Length - 1); soMoc--; }
                mocT[soMoc] = ThoiGian; mocTen[soMoc] = ten; soMoc++;
                for (int i = 0; i < mocChu.Length; i++) if (i < soMoc) mocChu[i].text = mocTen[i];
            }
            GhiMau();
            LamMoiChu();
        }

        void GhiMau()
        {
            int vt;
            if (soLs < SoMau) { vt = (dauLs + soLs) % SoMau; soLs++; }
            else { vt = dauLs; dauLs = (dauLs + 1) % SoMau; }
            thoiDiem[vt] = ThoiGian;
            for (int k = 0; k < He.C.Length && k < 4; k++) lichSu[k, vt] = He.C[k];
        }

        public override void CapNhat(float dt)
        {
            if (He == null) return;
            SoKhung++;
            dt *= TuaNhanh;
            ThoiGian += dt;
            XiSauTacDong += He.Buoc(dt);
            henMau -= dt;
            if (henMau <= 0) { henMau = CuaSo / SoMau; GhiMau(); }

            // đồ thị nồng độ – thời gian
            double t0 = Math.Max(0, ThoiGian - CuaSo);
            double cMax = 0;
            for (int j = 0; j < soLs; j++)
                for (int k = 0; k < He.Chat.Length && k < 4; k++) cMax = Math.Max(cMax, lichSu[k, (dauLs + j) % SoMau]);
            yMaxHien = yMaxHien <= 0 ? (float)(cMax * 1.15) : HoaLyUi.Muot(yMaxHien, (float)(cMax * 1.15), 0.4f, dt);
            dtC.XMin = t0; dtC.XMax = t0 + CuaSo; dtC.YMin = 0; dtC.YMax = Math.Max(1e-4, yMaxHien);
            var ve = dtC.Ve;
            ve.BatDau();
            dtC.VeKhung();
            var vung = dtC.Vung;
            for (int m = 0; m < mocChu.Length; m++)
            {
                bool hien = m < soMoc && mocT[m] >= t0;
                if (hien)
                {
                    float x = dtC.X(mocT[m]);
                    ve.VeNetDut(new Vector2(x, vung.yMin), new Vector2(x, vung.yMax), 1.5f, 5, HoaLyUi.C32(GlassKit.Accent2, 0.8f));
                    mocChu[m].rectTransform.anchoredPosition = new Vector2(x + 4, vung.yMax - 2);
                }
                if (mocChu[m].gameObject.activeSelf != hien) mocChu[m].gameObject.SetActive(hien);
            }
            for (int k = 0; k < He.Chat.Length && k < 4; k++)
            {
                int so = 0;
                for (int j = 0; j < soLs; j++)
                {
                    int vt = (dauLs + j) % SoMau;
                    if (thoiDiem[vt] < t0) continue;
                    duong[so++] = dtC.Diem(thoiDiem[vt], lichSu[k, vt]);
                }
                if (so < SoMau) duong[so++] = dtC.Diem(ThoiGian, He.C[k]);
                var mauK = HoaLyUi.C32(HoaLyUi.MauChuoi(k));
                ve.VeDuong(duong, so, 3f + 2.5f * (He.Chat.Length - 1 - k), mauK);
                ve.VeHat(duong[so - 1].x, duong[so - 1].y, 6, mauK);
            }
            ve.KetThuc();

            VeOngVaThang();
            if (DenLucCapNhatChu(dt)) CapNhatChu();
        }

        void VeOngVaThang()
        {
            var r = veOng.rectTransform.rect;
            veOng.BatDau();
            // ống nghiệm
            float ox = r.xMin + 60, oy = r.yMin + 74, ow = 110, oh = r.height - 140;
            var vien = HoaLyUi.C32(GlassKit.Dim, 0.9f);
            int idx = DanhSach.IndexOf(He);
            He.MauHonHop(mau, DoDamBaoHoa[Mathf.Clamp(idx, 0, 3)]);
            var nen = HoaLyUi.C32(GlassKit.Well);
            veOng.VeHop(ox, oy + 30, ox + ow, oy + oh, nen);
            veOng.VeHat(ox + ow / 2, oy + 30, ow / 2, nen);
            var mauDd = new Color32((byte)(mau[0] * 255), (byte)(mau[1] * 255), (byte)(mau[2] * 255), (byte)(mau[3] * 235));
            float mucNuoc = oy + oh * 0.82f;
            veOng.VeHop(ox, oy + 30, ox + ow, mucNuoc, mauDd);
            veOng.VeHat(ox + ow / 2, oy + 30, ow / 2, mauDd);
            veOng.VeHop(ox - 3, oy + 30, ox, oy + oh, vien);
            veOng.VeHop(ox + ow, oy + 30, ox + ow + 3, oy + oh, vien);
            veOng.VeHop(ox - 10, oy + oh, ox + ow + 10, oy + oh + 4, vien);

            // thang log Q – K (ngang)
            float tx0 = r.xMin + 240, tx1 = r.xMax - 40, ty = r.yMax - 135;
            double lnK = Math.Log10(He.K), lnQ = Math.Log10(Math.Max(1e-300, He.Q()));
            if (double.IsInfinity(lnQ) || double.IsNaN(lnQ)) lnQ = lnK - 3;
            double nua = Math.Max(1.5, Math.Min(6, Math.Abs(lnQ - lnK) * 1.3));
            float X(double l) => Mathf.Lerp(tx0, tx1, (float)((l - (lnK - nua)) / (2 * nua)));
            veOng.VeHop(tx0, ty - 2, tx1, ty + 2, HoaLyUi.C32(GlassKit.Line, 1.5f));
            float xk = X(lnK), xq = Mathf.Clamp(X(lnQ), tx0, tx1);
            veOng.VeHop(xk - 2, ty - 18, xk + 2, ty + 18, HoaLyUi.C32(GlassKit.Accent));
            veOng.VeHat(xq, ty, 9, HoaLyUi.C32(GlassKit.Accent2));
            if (Math.Abs(xq - xk) > 14)
            {
                float d = Mathf.Sign(xk - xq);
                veOng.VeDoan(xq + d * 12, ty + 26, xk - d * 10, ty + 26, 3, HoaLyUi.C32(GlassKit.Accent2, 0.8f));
                veOng.VeTuGiac(new Vector2(xk - d * 2, ty + 26), new Vector2(xk - d * 14, ty + 33), new Vector2(xk - d * 14, ty + 19), new Vector2(xk - d * 14, ty + 19), HoaLyUi.C32(GlassKit.Accent2, 0.8f));
            }
            veOng.KetThuc();
            nhanK.rectTransform.anchoredPosition = new Vector2(xk, ty - 20);
            nhanQ.rectTransform.anchoredPosition = new Vector2(xq, ty + 34);
        }

        void CapNhatChu()
        {
            double K = He.K, Q = He.Q();
            var ch = CanBangMoHinh.Chieu(Q, K);
            var sb = new System.Text.StringBuilder();
            for (int k = 0; k < He.Chat.Length && k < 4; k++)
                sb.Append($"<color=#{ColorUtility.ToHtmlStringRGB(HoaLyUi.MauChuoi(k))}>━ [{HoaLyUi.CongThuc(He.Chat[k].CongThuc)}] {HoaLyUi.So(He.C[k], 4)} M</color>   ");
            DatChu(chuThich, sb.ToString());
            dtC.TieuDe = He.PhuongTrinh + " · " + (He.LaKp ? "K gốc là Kp, đổi sang Kc" : "Kc");
            DatChu(oK, HoaLyUi.So(K, 4));
            DatChu(oQ, HoaLyUi.So(Q, 4));
            DatChu(oTiLe, HoaLyUi.So(Q / K, 3));
            DatChu(oVtVn, HoaLyUi.So(K / Q, 3));
            DatChu(oDn, He.CoKhi ? (He.DeltaN > 0 ? "+" : "") + He.DeltaN : "dung dịch");
            DatChu(oDh, HoaLyUi.So(He.DHKJ, 1) + " kJ/mol " + (He.DHKJ < 0 ? "(tỏa nhiệt)" : "(thu nhiệt)"));
            DatChu(nhanQ, "Q = " + HoaLyUi.So(Q, 3));
            DatChu(nhanK, "K = " + HoaLyUi.So(K, 3));
            DatChu(nhanOng, He.CoKhi ? "Bình khí · " + HoaLyUi.So(He.T, 0) + " K" : "Dung dịch · " + HoaLyUi.So(He.T, 0) + " K");
            string mui = ch == ChieuDich.Thuan ? "→ Dịch chuyển theo chiều THUẬN" : ch == ChieuDich.Nghich ? "← Dịch chuyển theo chiều NGHỊCH" : "⇌ Cân bằng động (Q ≈ K)";
            nhanChieu.color = ch == ChieuDich.CanBang ? GlassKit.Good : GlassKit.Accent2;
            DatChu(nhanChieu, mui);
            string tacDong = TacDongCuoi == "bắt đầu" ? "trộn các chất ban đầu" : TacDongCuoi;
            var gt = CanBangMoHinh.GiaiThich(Q, K, tacDong, He.LaN2O4);
            string them = He.CoXucTac ? " Chất xúc tác làm hệ đạt cân bằng nhanh hơn nhưng không làm thay đổi K." : "";
            if (ch == ChieuDich.CanBang && TacDongCuoi != "bắt đầu" && TacDongCuoi != "xúc tác")
                them += $" Tác động gần nhất: {TacDongCuoi} → hệ đã dịch {(XiSauTacDong > 0 ? "thuận" : XiSauTacDong < 0 ? "nghịch" : "không đáng kể")}.";
            DatChu(giaiThich, gt.VanBan + them);
        }
    }
}
