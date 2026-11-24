using System;
using System.Collections.Generic;
using UnityEngine;

namespace Khkt.Simulation.Bio.GiaiPhau
{
    // =====================================================================================
    // Mô hình bàn mổ tự do: mọi thứ người học thấy (vết rạch, vạt da, máu chảy, tim đập, phổi phồng, ruột nhu động,
    // chân ếch co) đều là kết quả tính từ trạng thái ở đây. Không có kịch bản, không có thứ tự bước quy định sẵn:
    // dụng cụ nào tác động lên mô nào được hay không là do luật (loại mô, lớp che phía trên, mạch nằm dưới đường dao...).
    // Phần hiển thị (Demo/Bio/GiaiPhau/BanMo*) chỉ đọc trạng thái này.
    // =====================================================================================

    public enum LoaiMau { Ech = 0, Nguoi = 1 }

    public enum DungCuMo
    {
        DaoMo = 0,      // dao mổ: rạch da, cơ, màng; không cắt được xương
        Keo,            // kéo mổ: phải luồn mũi kéo vào chỗ đã rạch; cắt cơ, màng, xương mỏng (ếch), mạch, cuống cơ quan
        KepPanh,        // kẹp panh: lật vạt đã rạch rời 3 phía, vén cơ quan, gắp mảnh đã rời
        KimGhim,        // kim ghim: ghim chi ếch lên khay sáp, ghim vạt đã lật
        KepCamMau,      // kẹp cầm máu: kẹp mạch đang chảy hoặc kẹp trước khi cắt
        BongGac,        // bông gạc: thấm máu đọng, ép chỗ rỉ máu nhỏ cho mau đông
        OngTiem,        // ống tiêm: hút dịch trong cơ quan, mạch, khoang
        OngNhoGiot,     // ống nhỏ giọt: nhỏ thuốc (ếch) / tiêm tĩnh mạch qua đường truyền (người)
        DienCuc,        // điện cực kích thích: dây thần kinh, cơ, tim
        OngThoi,        // ống thổi: thổi phồng phổi ếch qua khe thanh quản
        CuaXuong,       // cưa xương ức (người)
        Banh,           // banh: giữ mở mép vết mổ / hai nửa xương ức
        KimHuyTuy       // kim mũi mác hủy não, tủy ếch
    }

    public enum ThuocMo { Ringer = 0, RingerLanh, Adrenalin, Acetylcholin, Atropin, HCl }

    public enum LoaiLop { Da = 0, Co, Xuong, Mang }

    public enum TrangThaiO : byte { Lien = 0, Lat = 1, Bo = 2 }

    public enum HinhCoQuan { Elip = 0, Ong, ThuyGan, Tim, Phoi, Day }

    public enum ChuyenDong { Khong = 0, Tim, Phoi, NhuDong, CoHoanh }

    /// <summary>Dịch cơ thể: tính chất đọc khi hút bằng ống tiêm hoặc khi tràn ra.</summary>
    public sealed class DichCoThe
    {
        public string id, ten, mauSac, pH, ghiChu;
        public Color mau;
        static readonly Dictionary<string, DichCoThe> db = new Dictionary<string, DichCoThe>();
        static DichCoThe() => KhoiTao();

        static void Them(string id, string ten, Color mau, string mauSac, string pH, string ghiChu) =>
            db[id] = new DichCoThe { id = id, ten = ten, mau = mau, mauSac = mauSac, pH = pH, ghiChu = ghiChu };

        static void KhoiTao()
        {
            Them("mau_dm", "Máu động mạch", new Color(0.78f, 0.06f, 0.08f), "đỏ tươi", "7,35 – 7,45", "Giàu O₂ (độ bão hòa O₂ khoảng 95 – 100 %), chảy thành tia theo nhịp tim.");
            Them("mau_tm", "Máu tĩnh mạch", new Color(0.42f, 0.04f, 0.08f), "đỏ sẫm", "7,32 – 7,42", "Nghèo O₂ hơn (độ bão hòa O₂ khoảng 70 – 75 %), chảy đều, không thành tia.");
            Them("mau_pha", "Máu pha (tâm thất ếch)", new Color(0.60f, 0.05f, 0.08f), "đỏ thẫm", "≈ 7,4", "Tim ếch có một tâm thất: máu giàu O₂ từ phổi và máu nghèo O₂ từ cơ thể trộn một phần trong tâm thất.");
            Them("bach_huyet", "Bạch huyết dưới da", new Color(0.93f, 0.92f, 0.78f, 0.55f), "trong, hơi vàng", "≈ 7,4", "Ếch có các túi bạch huyết lớn dưới da nên da ếch rời, rạch da chảy dịch trong.");
            Them("mat", "Dịch mật", new Color(0.30f, 0.52f, 0.12f), "vàng – xanh lục", "≈ 7 – 8", "Gan tiết, túi mật cô đặc và dự trữ; muối mật nhũ tương hóa lipid ở ruột non. Không có enzyme.");
            Them("dich_vi", "Dịch vị", new Color(0.86f, 0.86f, 0.74f, 0.6f), "trong, hơi đục", "1,5 – 3,5", "Có HCl và pepsin; pH rất thấp diệt khuẩn và hoạt hóa pepsinogen thành pepsin.");
            Them("nhu_tran", "Dịch ruột và dưỡng chấp", new Color(0.72f, 0.62f, 0.32f), "vàng nâu, sệt", "≈ 6 – 7,5", "Thức ăn đã trộn dịch tụy, mật, dịch ruột; được hấp thụ qua lông ruột.");
            Them("nuoc_tieu", "Nước tiểu", new Color(0.95f, 0.85f, 0.35f, 0.7f), "vàng nhạt, trong", "4,5 – 8 (thường ≈ 6)", "Thận lọc máu tạo nước tiểu, dẫn xuống bàng quang. Ở ếch, bàng quang còn tái hấp thụ nước.");
            Them("dich_tim", "Dịch màng ngoài tim", new Color(0.95f, 0.92f, 0.70f, 0.6f), "trong, vàng nhạt", "≈ 7,4", "Lượng nhỏ (người khoảng 15 – 50 mL), giảm ma sát khi tim co bóp.");
            Them("dich_phuc_mac", "Dịch màng bụng", new Color(0.95f, 0.93f, 0.75f, 0.5f), "trong", "≈ 7,4", "Lớp mỏng giữa hai lá phúc mạc cho các quai ruột trượt lên nhau khi nhu động.");
            Them("khi", "Không khí trong phổi", new Color(1f, 1f, 1f, 0.2f), "không màu", "—", "Phổi chứa khí, ống tiêm hút ra bọt khí lẫn ít máu.");
        }

        public static DichCoThe Lay(string id) => id != null && db.TryGetValue(id, out var d) ? d : null;
    }

    /// <summary>Một lớp mô thành cơ thể ở vùng mổ (da, cơ, xương, màng) chia thành lưới ô; vết rạch là các cạnh giữa hai ô bị cắt.</summary>
    public sealed class LopMo
    {
        public string id, ten;
        public LoaiLop loai;
        public float doSau;                  // độ sâu dưới mặt da (đơn vị mô hình)
        public float u0, u1, v0, v1;         // vùng lớp phủ trong cửa sổ mổ
        public int nu, nv;
        public bool dinhTrai = true, dinhPhai = true, dinhDuoi = true, dinhTren = true; // mép nào còn liền với phần cơ thể ngoài cửa sổ
        public Color mauNgoai, mauTrong;
        public float thoiGianRoi = 3.5f;     // vạt chưa ghim tự rơi về sau (giây)

        public TrangThaiO[] o;
        public bool[] catDoc;   // cạnh giữa ô (i,j) và (i+1,j): (nu-1)*nv
        public bool[] catNgang; // cạnh giữa ô (i,j) và (i,j+1): nu*(nv-1)
        public int[] vatCuaO;   // chỉ số vạt đang lật (-1 nếu không)
        public int phienBan;

        public LopMo Tao()
        {
            o = new TrangThaiO[nu * nv];
            catDoc = new bool[(nu - 1) * nv];
            catNgang = new bool[nu * (nv - 1)];
            vatCuaO = new int[nu * nv];
            for (int k = 0; k < vatCuaO.Length; k++) vatCuaO[k] = -1;
            return this;
        }

        public bool Phu(float u, float v) => u >= u0 && u <= u1 && v >= v0 && v <= v1;

        public bool O(float u, float v, out int i, out int j)
        {
            i = j = -1;
            if (!Phu(u, v)) return false;
            i = Mathf.Clamp((int)((u - u0) / (u1 - u0) * nu), 0, nu - 1);
            j = Mathf.Clamp((int)((v - v0) / (v1 - v0) * nv), 0, nv - 1);
            return true;
        }

        public int Idx(int i, int j) => i + j * nu;
        public Vector2 TamO(int i, int j) => new Vector2(u0 + (i + 0.5f) / nu * (u1 - u0), v0 + (j + 0.5f) / nv * (v1 - v0));
        public float CoO => (u1 - u0) / nu;

        public bool DaCatDoc(int i, int j) => i >= 0 && i < nu - 1 && catDoc[i + j * (nu - 1)];
        public bool DaCatNgang(int i, int j) => j >= 0 && j < nv - 1 && catNgang[i + j * nu];

        public int SoCanhCat()
        {
            int n = 0;
            foreach (var b in catDoc) if (b) n++;
            foreach (var b in catNgang) if (b) n++;
            return n;
        }

        public float TiLeMo()
        {
            int n = 0;
            foreach (var s in o) if (s != TrangThaiO.Lien) n++;
            return n / (float)o.Length;
        }
    }

    /// <summary>Vạt mô đang lật: tập ô, trục bản lề (mép còn dính), góc lật, đã ghim hay chưa.</summary>
    public sealed class VatMo
    {
        public LopMo lop;
        public List<int> oVat = new List<int>();
        public int phiaBanLe;          // 0 trái, 1 phải, 2 dưới, 3 trên, -1 rời hẳn
        public bool ghim, banh;
        public float tuoi;             // giây kể từ khi lật
        public float goc;              // góc lật hiện tại (độ), hiển thị đọc
    }

    public sealed class MachMau
    {
        public string id, ten;
        public bool dongMach;
        public float banKinhMm;
        public string lopId = "";          // nằm trong lớp nào ("" = trong khoang)
        public Vector3[] duong;            // (u, v, độ sâu) theo đường mạch
        public float luuLuongCat;          // mL/s khi đứt ở huyết áp bình thường
        public float thoiGianDong = -1f;   // mạch nhỏ tự cầm sau (giây), -1 = không tự cầm
        public bool dut, kep, dongCuc;
        public Vector3 diemDut;
        public float tuoiDut, epGac;
        public string coQuanNuoi = "";     // mạch cuống của cơ quan nào
    }

    public sealed class DayThanKinh
    {
        public string id, ten;
        public Vector3[] duong;            // điểm 0 = gần trung ương, cuối = cơ quan đích
        public string dich;                // "chi_sau_trai", "chi_sau_phai", "tim", "co_hoanh_trai", "co_hoanh_phai"
        public string coQuanChe = "";
        public readonly List<float> diemCat = new List<float>();
        public bool LienTu(float t) { foreach (var c in diemCat) if (c > t) return false; return true; } // từ t tới cơ quan đích còn liền
        public bool LienToanBo => diemCat.Count == 0;
    }

    public sealed class CoQuanMo
    {
        public string id, ten, he, moTa;
        public HinhCoQuan hinh;
        public ChuyenDong chuyenDong;
        public Vector3 tam;                // (u, v, độ sâu)
        public Vector3 kichThuoc;          // (rộng theo u, dài theo v, dày)
        public Color mau;
        public string dich;                // dịch chứa bên trong
        public float theTichDich;          // mL
        public float luuLuongRach;         // mL/s máu khi rạch nhu mô (gan, lách…)
        public bool coTheVen, coTheCatBo = true;
        public string[] che = new string[0];       // các cơ quan bị cơ quan này che
        public Vector3[] duongOng;          // ruột: đường đi (u, v, sâu)
        public bool ven, catBo, rach, tachRoi;
        public float coGiat;                 // 0..1 co do kích thích
        public string ghiChuRieng = "";
    }

    public sealed class KetQua
    {
        public bool ok;
        public string thongBao;
        public KetQua(bool ok, string tb) { this.ok = ok; thongBao = tb; }
    }

    // =====================================================================================
    // Sinh lý: tim (nút xoang → nhĩ → thất, thời kỳ trơ), huyết áp theo thể tích máu, phản xạ áp lực,
    // thuốc, nhiệt độ (ếch biến nhiệt), độ ẩm mô, hô hấp (thở máy / bơm miệng của ếch), nhu động.
    // =====================================================================================
    public sealed class SinhLy
    {
        public LoaiMau loai;
        public float theTichMau0, theTichMau;   // mL
        public float nhietDo = 22f;             // °C (ếch theo môi trường)
        public float doAm = 1f;                 // ếch: độ ẩm mô hở (khô dần nếu không nhỏ Ringer)
        public float ach, adr, atropin;         // nồng độ tương đối 0..2
        public float kichThichX;                // > 0 khi đang kích thích dây X (giây còn lại)
        public float thoiGianKichX;             // đã kích thích liên tục bao lâu (thoát ức chế)
        public bool coTheTho = true;            // ếch: còn não (hành tủy) thì còn bơm miệng; người: thở máy
        public bool thoMay;                     // người: máy thở
        public float phoiRo;                    // 0..1 rò khí do phổi bị rạch
        public bool ngungTim;
        public float thoiGianHaHuyetAp;
        public bool timTachRoi;

        // Dẫn truyền tim
        public float chuKyXoang;                // giây tới lần phát xung xoang kế tiếp
        public float nhiCo, thatCo;             // 0..1 mức co hiện tại (hiển thị)
        float tNhi = 9f, tThat = 9f;            // thời gian kể từ lần co nhĩ / thất gần nhất
        float hoanThat = -1f;                   // thời điểm hẹn kích thích thất (sau trễ nhĩ thất)
        public int soNhatBop, soNgoaiTamThu, soXungBiChan;
        public float nhipDo;                    // nhịp tim tính được (lần/phút)
        public float lucCo = 1f;                // 0..1.6
        public float huyetAp;                   // mmHg trung bình (người) / tương đối (ếch)
        public float phoi;                      // 0..1 độ phồng phổi
        public float phaTho;
        public float phaNhuDong;
        public float tanSoNhuDong;              // lần/phút
        public float bienDoNhuDong = 1f;
        public float coHoanhCo;                 // co cơ hoành do kích thích thần kinh hoành
        public float phoiThoiPhong;             // ếch: khí còn lại sau khi thổi

        public float TiLeMatMau => theTichMau0 > 0 ? Mathf.Clamp01(1f - theTichMau / theTichMau0) : 0f;

        public float NhipNen
        {
            get
            {
                if (loai == LoaiMau.Nguoi) return 72f;
                // Ếch biến nhiệt: nhịp tim tăng khoảng gấp đôi khi nhiệt độ tăng 10 °C (Q10 ≈ 2)
                return 42f * Mathf.Pow(2f, (nhietDo - 20f) / 10f);
            }
        }

        /// <summary>Thời kỳ trơ tuyệt đối của tâm thất xấp xỉ bằng thời gian tâm thu.</summary>
        public float ThoiGianTamThu => Mathf.Clamp(60f / Mathf.Max(20f, nhipDo) * 0.375f, 0.18f, 0.9f);

        public void KhoiTao(LoaiMau l)
        {
            loai = l;
            if (l == LoaiMau.Nguoi)
            {
                theTichMau0 = theTichMau = 5000f; // ≈ 70 mL/kg × 70 kg
                thoMay = true; coTheTho = true;
                huyetAp = 85f;
                tanSoNhuDong = 9f;
            }
            else
            {
                theTichMau0 = theTichMau = 2.4f;  // ếch ≈ 40 g
                thoMay = false; coTheTho = true;
                huyetAp = 30f;
                tanSoNhuDong = 3f;
            }
            nhipDo = NhipNen;
            chuKyXoang = 60f / nhipDo * 0.5f;
        }

        /// <summary>Nhịp xoang mục tiêu theo thể tích máu, thuốc, dây X, nhiệt độ, độ ẩm.</summary>
        public float NhipMucTieu()
        {
            if (ngungTim) return 0f;
            float hr = NhipNen;
            float matMau = TiLeMatMau;
            if (loai == LoaiMau.Nguoi && !timTachRoi)
            {
                // Phản xạ áp lực (gây mê vẫn còn, yếu hơn): mất 15 % ≈ 100, 30 % ≈ 120, 40 % ≈ 140 lần/phút (phân độ sốc mất máu)
                hr = NoiSuy(matMau, new[] { 0f, 0.15f, 0.30f, 0.40f, 0.50f }, new[] { 72f, 98f, 118f, 138f, 150f });
            }
            float achHieuLuc = ach * (1f - Mathf.Clamp01(atropin));
            hr *= 1f + 0.45f * Mathf.Min(adr, 2f);
            hr /= 1f + 1.6f * achHieuLuc;
            if (kichThichX > 0f && atropin < 0.3f)
            {
                // Kích thích dây X: tim chậm hẳn (ếch có thể ngừng), sau vài giây tim "thoát" khỏi ức chế
                float thoat = Mathf.Clamp01((thoiGianKichX - 6f) / 6f);
                hr *= Mathf.Lerp(loai == LoaiMau.Ech ? 0f : 0.35f, 0.6f, thoat);
            }
            if (loai == LoaiMau.Ech)
            {
                if (doAm < 0.35f) hr *= Mathf.Clamp01(doAm / 0.35f);
                if (achHieuLuc > 1.4f) hr = 0f; // nồng độ acetylcholin cao: tim ếch ngừng ở thì tâm trương (hồi phục khi rửa Ringer)
            }
            return Mathf.Max(0f, hr);
        }

        public static float NoiSuy(float x, float[] xs, float[] ys)
        {
            if (x <= xs[0]) return ys[0];
            for (int k = 1; k < xs.Length; k++)
                if (x <= xs[k]) return Mathf.Lerp(ys[k - 1], ys[k], (x - xs[k - 1]) / (xs[k] - xs[k - 1]));
            return ys[ys.Length - 1];
        }

        public void Buoc(float dt, Action<string> suKien)
        {
            // Thuốc thải dần (bán thải vài chục giây ở mức mô phỏng); Ringer rửa thì giảm nhanh (xử lý ở chỗ nhỏ thuốc)
            ach = Mathf.MoveTowards(ach, 0f, dt * 0.015f);
            adr = Mathf.MoveTowards(adr, 0f, dt * 0.012f);
            atropin = Mathf.MoveTowards(atropin, 0f, dt * 0.004f);
            if (kichThichX > 0f) { kichThichX -= dt; thoiGianKichX += dt; } else thoiGianKichX = 0f;
            if (loai == LoaiMau.Ech) doAm = Mathf.MoveTowards(doAm, 0f, dt / 240f); // mô hở khô dần trong khoảng 4 phút

            // Huyết áp theo thể tích máu và lực co
            float matMau = TiLeMatMau;
            float apNen = loai == LoaiMau.Nguoi
                ? NoiSuy(matMau, new[] { 0f, 0.15f, 0.30f, 0.40f, 0.50f, 0.65f }, new[] { 85f, 84f, 70f, 55f, 35f, 0f })
                : NoiSuy(matMau, new[] { 0f, 0.20f, 0.40f, 0.60f, 0.80f }, new[] { 30f, 26f, 18f, 9f, 0f });
            if (timTachRoi) apNen = 0f;
            float lucMucTieu = Mathf.Clamp(1f + 0.35f * Mathf.Min(adr, 2f) - 0.35f * ach * (1f - Mathf.Clamp01(atropin)), 0.2f, 1.6f);
            if (loai == LoaiMau.Ech && !timTachRoi) lucMucTieu *= Mathf.Lerp(0.35f, 1f, 1f - matMau); // tim rỗng co yếu
            lucCo = Mathf.MoveTowards(lucCo, lucMucTieu, dt * 0.5f);
            float apMucTieu = ngungTim || nhipDo < 1f ? 0f : apNen * Mathf.Lerp(0.6f, 1.15f, Mathf.InverseLerp(0.2f, 1.6f, lucCo));
            if (loai == LoaiMau.Nguoi) apMucTieu += 12f * Mathf.Min(adr, 2f);
            huyetAp = Mathf.MoveTowards(huyetAp, apMucTieu, dt * (loai == LoaiMau.Nguoi ? 20f : 8f));

            if (loai == LoaiMau.Nguoi && !ngungTim)
            {
                if (huyetAp < 25f) thoiGianHaHuyetAp += dt; else thoiGianHaHuyetAp = 0f;
                if (thoiGianHaHuyetAp > 10f)
                {
                    ngungTim = true;
                    suKien?.Invoke("Ngừng tuần hoàn: mất máu quá nhiều, huyết áp không duy trì được tưới máu cơ tim.");
                }
            }

            // Nhịp xoang: tiến tới nhịp mục tiêu một cách êm (thần kinh, thuốc tác động trong vài giây)
            float muc = NhipMucTieu();
            nhipDo = Mathf.MoveTowards(nhipDo, muc, dt * (muc < nhipDo ? 25f : 12f));

            // Dẫn truyền: nút xoang phát xung → nhĩ co → trễ nhĩ thất ≈ 0,1 s (ếch dài hơn) → thất co nếu thất đã hết trơ
            tNhi += dt; tThat += dt;
            if (nhipDo > 2f)
            {
                chuKyXoang -= dt;
                if (chuKyXoang <= 0f)
                {
                    chuKyXoang += 60f / nhipDo;
                    tNhi = 0f;
                    hoanThat = loai == LoaiMau.Ech ? 0.22f : 0.12f;
                }
            }
            else chuKyXoang = Mathf.Min(chuKyXoang, 0.6f);

            if (hoanThat >= 0f)
            {
                hoanThat -= dt;
                if (hoanThat < 0f)
                {
                    hoanThat = -1f;
                    if (tThat >= ThoiGianTamThu) { tThat = 0f; soNhatBop++; }
                    else soXungBiChan++; // xung đến lúc thất còn trơ do ngoại tâm thu → mất một nhát: nghỉ bù
                }
            }

            float tamThuNhi = Mathf.Min(0.1f, 60f / Mathf.Max(30f, nhipDo) * 0.125f);
            nhiCo = tNhi < tamThuNhi ? Mathf.Sin(tNhi / tamThuNhi * Mathf.PI) : 0f;
            float tt = ThoiGianTamThu;
            thatCo = tThat < tt ? Mathf.Sin(tThat / tt * Mathf.PI) * Mathf.Clamp(lucCo, 0.3f, 1.4f) : 0f;

            // Hô hấp
            if (loai == LoaiMau.Nguoi)
            {
                // Thở máy 14 lần/phút, hít : thở = 1 : 2; phổi vẫn phồng khi đã mở ngực vì máy đẩy khí áp lực dương
                phaTho = (phaTho + dt * 14f / 60f) % 1f;
                float muctieu = phaTho < 0.333f ? Mathf.SmoothStep(0f, 1f, phaTho / 0.333f) : 1f - Mathf.SmoothStep(0f, 1f, (phaTho - 0.333f) / 0.667f);
                phoi = muctieu * (1f - 0.55f * phoiRo);
            }
            else
            {
                if (coTheTho)
                {
                    // Ếch còn não: thở bằng nâng hạ thềm miệng, phổi phồng từng đợt
                    phaTho = (phaTho + dt * 50f / 60f) % 1f;
                    phoi = 0.55f + 0.35f * Mathf.Max(0f, Mathf.Sin(phaTho * Mathf.PI * 2f));
                }
                else
                {
                    phoiThoiPhong = Mathf.MoveTowards(phoiThoiPhong, 0f, dt / (18f * (1f + 2f * (1f - phoiRo))));
                    phoi = phoiThoiPhong * (1f - 0.8f * phoiRo);
                }
            }

            // Nhu động: cơ trơn có hệ thần kinh ruột riêng nên vẫn chạy khi đã hủy tủy; acetylcholin tăng, adrenalin giảm
            float achRuot = ach * (1f - Mathf.Clamp01(atropin));
            float f = loai == LoaiMau.Nguoi ? 9f : 3f * Mathf.Pow(2f, (nhietDo - 20f) / 10f);
            f *= (1f + 0.6f * achRuot) / (1f + 0.8f * adr);
            if (loai == LoaiMau.Ech && doAm < 0.2f) f *= doAm / 0.2f;
            tanSoNhuDong = f;
            bienDoNhuDong = Mathf.Clamp((1f + 0.7f * achRuot) / (1f + 1.2f * adr), 0.2f, 1.8f);
            phaNhuDong += dt * f / 60f;

            coHoanhCo = Mathf.MoveTowards(coHoanhCo, 0f, dt * 2.5f);
        }

        /// <summary>Kích thích điện trực tiếp lên tâm thất: chỉ gây co khi thất đã hết trơ (ngoại tâm thu), sau đó có nghỉ bù.</summary>
        public KetQua KichThichTim()
        {
            if (nhipDo < 1f && !timTachRoi && loai == LoaiMau.Nguoi && ngungTim)
                return new KetQua(false, "Tim đã ngừng do thiếu tưới máu; một xung đơn lẻ không làm tim đập lại.");
            if (tThat < ThoiGianTamThu)
                return new KetQua(false, $"Kích thích rơi vào lúc tâm thất đang co (thời kỳ trơ tuyệt đối ≈ {ThoiGianTamThu:0.00} s): tim không đáp ứng.");
            tThat = 0f;
            soNgoaiTamThu++;
            return new KetQua(true, "Kích thích rơi vào thì tâm trương: tâm thất co thêm một nhát (ngoại tâm thu). Xung xoang kế tiếp đến lúc thất còn trơ nên bị chặn, tạo khoảng nghỉ bù.");
        }
    }

    // =====================================================================================
    // Mẫu mổ: lớp mô, cơ quan, mạch, thần kinh và luật dụng cụ.
    // =====================================================================================
    public sealed class MauMo
    {
        public LoaiMau loai;
        public string ten;
        public readonly List<LopMo> lop = new List<LopMo>();
        public readonly List<CoQuanMo> coQuan = new List<CoQuanMo>();
        public readonly List<MachMau> mach = new List<MachMau>();
        public readonly List<DayThanKinh> thanKinh = new List<DayThanKinh>();
        public readonly List<VatMo> vat = new List<VatMo>();
        public readonly SinhLy sinhLy = new SinhLy();
        public readonly List<string> nhatKy = new List<string>();

        // Trạng thái riêng của ếch
        public bool huyNao, huyTuy;
        public int soChiGhim;
        public readonly bool[] chiGhim = new bool[4];       // 0 trước trái, 1 trước phải, 2 sau trái, 3 sau phải
        public readonly float[] chiCo = new float[4];        // 0..1 mức co cơ chi (hiển thị)

        public float mauDong;                               // mL máu đọng trong vết mổ
        public float dichTran;                              // mL dịch khác tràn ra
        public Color mauDichTran = Color.clear;
        public float thoiGian;
        public int phienBanCoQuan;
        public string dichVuaHut = "";

        public float MatMauMl => sinhLy.theTichMau0 - sinhLy.theTichMau;

        public MauMo(LoaiMau l)
        {
            loai = l;
            sinhLy.KhoiTao(l);
            if (l == LoaiMau.Ech) DungEch(); else DungNguoi();
        }

        public LopMo Lop(string id) => lop.Find(x => x.id == id);
        public CoQuanMo CQ(string id) => coQuan.Find(x => x.id == id);
        public MachMau Mach(string id) => mach.Find(x => x.id == id);
        public DayThanKinh TK(string id) => thanKinh.Find(x => x.id == id);

        void Ghi(string s)
        {
            nhatKy.Add(s);
            if (nhatKy.Count > 60) nhatKy.RemoveAt(0);
        }

        // ---------------------------------------------------------------- dữ liệu mẫu

        static Color C(float r, float g, float b, float a = 1f) => new Color(r, g, b, a);

        void DungEch()
        {
            ten = "Ếch đồng (Hoplobatrachus rugulosus), khoảng 40 g";
            sinhLy.nhietDo = 22f;
            lop.Add(new LopMo { id = "da", ten = "Da bụng", loai = LoaiLop.Da, doSau = 0f, u0 = 0f, u1 = 1f, v0 = 0f, v1 = 1f, nu = 20, nv = 28,
                mauNgoai = C(0.86f, 0.84f, 0.70f), mauTrong = C(0.82f, 0.62f, 0.58f), thoiGianRoi = 8f }.Tao());
            lop.Add(new LopMo { id = "co", ten = "Cơ thành bụng", loai = LoaiLop.Co, doSau = 0.035f, u0 = 0.04f, u1 = 0.96f, v0 = 0.02f, v1 = 0.70f, nu = 18, nv = 20,
                mauNgoai = C(0.80f, 0.42f, 0.40f), mauTrong = C(0.70f, 0.30f, 0.30f), thoiGianRoi = 8f }.Tao());
            lop.Add(new LopMo { id = "xuong", ten = "Đai vai và xương ức", loai = LoaiLop.Xuong, doSau = 0.04f, u0 = 0.14f, u1 = 0.86f, v0 = 0.70f, v1 = 0.98f, nu = 14, nv = 8,
                dinhDuoi = false, mauNgoai = C(0.90f, 0.88f, 0.80f), mauTrong = C(0.80f, 0.70f, 0.62f), thoiGianRoi = 4f }.Tao());
            lop.Add(new LopMo { id = "mang_tim", ten = "Màng ngoài tim", loai = LoaiLop.Mang, doSau = 0.07f, u0 = 0.36f, u1 = 0.64f, v0 = 0.70f, v1 = 0.92f, nu = 8, nv = 7,
                dinhDuoi = false, dinhTrai = false, dinhPhai = false, mauNgoai = C(0.92f, 0.88f, 0.86f, 0.6f), mauTrong = C(0.9f, 0.8f, 0.78f, 0.5f), thoiGianRoi = 99f }.Tao());

            // Cơ quan (u: 0 = mép phải con ếch, nhìn từ phía bụng; v: 0 = huyệt, 1 = cổ họng)
            coQuan.Add(new CoQuanMo { id = "tim", ten = "Tim (2 tâm nhĩ, 1 tâm thất)", he = "Tuần hoàn", hinh = HinhCoQuan.Tim, chuyenDong = ChuyenDong.Tim,
                tam = new Vector3(0.5f, 0.80f, 0.14f), kichThuoc = new Vector3(0.17f, 0.15f, 0.12f), mau = C(0.62f, 0.10f, 0.12f), dich = "mau_pha", theTichDich = 0.08f, coTheCatBo = true,
                moTa = "Tâm thất co đẩy máu vào hành động mạch rồi các cung động mạch. Tim ếch tự động: nút xoang (xoang tĩnh mạch) phát nhịp nên tim vẫn đập sau khi hủy não, tủy." });
            coQuan.Add(new CoQuanMo { id = "phoi_phai", ten = "Phổi phải", he = "Hô hấp", hinh = HinhCoQuan.Phoi, chuyenDong = ChuyenDong.Phoi,
                tam = new Vector3(0.30f, 0.74f, 0.24f), kichThuoc = new Vector3(0.12f, 0.17f, 0.09f), mau = C(0.86f, 0.55f, 0.55f), dich = "khi", theTichDich = 0.5f,
                moTa = "Phổi ếch là túi mỏng có vách ngăn thưa; ếch thở bằng nâng hạ thềm miệng (không có cơ hoành) và trao đổi khí qua da." });
            coQuan.Add(new CoQuanMo { id = "phoi_trai", ten = "Phổi trái", he = "Hô hấp", hinh = HinhCoQuan.Phoi, chuyenDong = ChuyenDong.Phoi,
                tam = new Vector3(0.70f, 0.74f, 0.24f), kichThuoc = new Vector3(0.12f, 0.17f, 0.09f), mau = C(0.86f, 0.55f, 0.55f), dich = "khi", theTichDich = 0.5f,
                moTa = "Phổi ếch là túi mỏng có vách ngăn thưa; ếch thở bằng nâng hạ thềm miệng (không có cơ hoành) và trao đổi khí qua da." });
            coQuan.Add(new CoQuanMo { id = "gan", ten = "Gan (3 thùy)", he = "Tiêu hóa", hinh = HinhCoQuan.ThuyGan,
                tam = new Vector3(0.5f, 0.60f, 0.12f), kichThuoc = new Vector3(0.62f, 0.20f, 0.08f), mau = C(0.42f, 0.14f, 0.10f), luuLuongRach = 0.006f, coTheVen = true,
                che = new[] { "tui_mat", "da_day", "phoi_phai", "phoi_trai" }, moTa = "Gan lớn nhất trong khoang bụng, màu nâu đỏ, tiết mật dự trữ ở túi mật." });
            coQuan.Add(new CoQuanMo { id = "tui_mat", ten = "Túi mật", he = "Tiêu hóa", hinh = HinhCoQuan.Elip,
                tam = new Vector3(0.52f, 0.58f, 0.20f), kichThuoc = new Vector3(0.06f, 0.06f, 0.05f), mau = C(0.25f, 0.50f, 0.20f), dich = "mat", theTichDich = 0.05f,
                moTa = "Túi nhỏ xanh lục giữa các thùy gan; dẫn mật qua ống mật vào tá tràng." });
            coQuan.Add(new CoQuanMo { id = "da_day", ten = "Dạ dày", he = "Tiêu hóa", hinh = HinhCoQuan.Ong, chuyenDong = ChuyenDong.NhuDong,
                tam = new Vector3(0.68f, 0.50f, 0.20f), kichThuoc = new Vector3(0.10f, 0.26f, 0.08f), mau = C(0.86f, 0.70f, 0.62f), dich = "dich_vi", theTichDich = 0.2f, coTheVen = true,
                duongOng = new[] { new Vector3(0.62f, 0.68f, 0.22f), new Vector3(0.70f, 0.56f, 0.2f), new Vector3(0.70f, 0.44f, 0.2f), new Vector3(0.60f, 0.36f, 0.2f) },
                moTa = "Dạ dày hình ống cong nằm bên trái cơ thể, tiết dịch vị có HCl và pepsin." });
            coQuan.Add(new CoQuanMo { id = "ruot_non", ten = "Ruột non", he = "Tiêu hóa", hinh = HinhCoQuan.Ong, chuyenDong = ChuyenDong.NhuDong,
                tam = new Vector3(0.45f, 0.34f, 0.18f), kichThuoc = new Vector3(0.36f, 0.24f, 0.07f), mau = C(0.90f, 0.76f, 0.68f), dich = "nhu_tran", theTichDich = 0.15f, coTheVen = true,
                che = new[] { "lach", "than_phai", "than_trai", "tinh_hoan_phai", "tinh_hoan_trai", "the_mo_phai", "the_mo_trai" },
                duongOng = new[] { new Vector3(0.60f, 0.36f, 0.17f), new Vector3(0.40f, 0.42f, 0.18f), new Vector3(0.30f, 0.34f, 0.17f), new Vector3(0.44f, 0.28f, 0.18f),
                                   new Vector3(0.62f, 0.30f, 0.18f), new Vector3(0.56f, 0.22f, 0.18f), new Vector3(0.36f, 0.24f, 0.18f), new Vector3(0.46f, 0.18f, 0.2f) },
                moTa = "Ruột non cuộn khúc, treo bởi màng treo ruột có mạch máu; tiêu hóa và hấp thụ." });
            coQuan.Add(new CoQuanMo { id = "ruot_gia", ten = "Ruột già và ruột thẳng", he = "Tiêu hóa", hinh = HinhCoQuan.Ong, chuyenDong = ChuyenDong.NhuDong,
                tam = new Vector3(0.5f, 0.13f, 0.20f), kichThuoc = new Vector3(0.09f, 0.12f, 0.08f), mau = C(0.78f, 0.62f, 0.58f), dich = "nhu_tran", theTichDich = 0.1f, coTheVen = true,
                duongOng = new[] { new Vector3(0.48f, 0.19f, 0.2f), new Vector3(0.5f, 0.12f, 0.2f), new Vector3(0.5f, 0.04f, 0.22f) },
                moTa = "Ruột già ngắn, rộng, đổ vào huyệt cùng ống dẫn niệu và ống sinh dục." });
            coQuan.Add(new CoQuanMo { id = "lach", ten = "Lách", he = "Tuần hoàn", hinh = HinhCoQuan.Elip,
                tam = new Vector3(0.56f, 0.30f, 0.27f), kichThuoc = new Vector3(0.04f, 0.04f, 0.04f), mau = C(0.55f, 0.08f, 0.12f), luuLuongRach = 0.004f,
                moTa = "Lách nhỏ, tròn, đỏ sẫm, nằm trong màng treo ruột; tạo và lọc tế bào máu." });
            coQuan.Add(new CoQuanMo { id = "the_mo_phai", ten = "Thể mỡ phải", he = "Dự trữ", hinh = HinhCoQuan.Elip,
                tam = new Vector3(0.36f, 0.42f, 0.27f), kichThuoc = new Vector3(0.08f, 0.12f, 0.04f), mau = C(0.96f, 0.78f, 0.22f), coTheVen = true,
                moTa = "Thể mỡ hình ngón tay màu vàng, dự trữ năng lượng cho mùa ngủ đông và mùa sinh sản." });
            coQuan.Add(new CoQuanMo { id = "the_mo_trai", ten = "Thể mỡ trái", he = "Dự trữ", hinh = HinhCoQuan.Elip,
                tam = new Vector3(0.64f, 0.42f, 0.27f), kichThuoc = new Vector3(0.08f, 0.12f, 0.04f), mau = C(0.96f, 0.78f, 0.22f), coTheVen = true,
                moTa = "Thể mỡ hình ngón tay màu vàng, dự trữ năng lượng cho mùa ngủ đông và mùa sinh sản." });
            coQuan.Add(new CoQuanMo { id = "tinh_hoan_phai", ten = "Tinh hoàn phải", he = "Sinh dục", hinh = HinhCoQuan.Elip,
                tam = new Vector3(0.43f, 0.34f, 0.32f), kichThuoc = new Vector3(0.04f, 0.06f, 0.03f), mau = C(0.95f, 0.90f, 0.62f),
                moTa = "Hai tinh hoàn bầu dục vàng nhạt bám mặt bụng thận." });
            coQuan.Add(new CoQuanMo { id = "tinh_hoan_trai", ten = "Tinh hoàn trái", he = "Sinh dục", hinh = HinhCoQuan.Elip,
                tam = new Vector3(0.57f, 0.34f, 0.32f), kichThuoc = new Vector3(0.04f, 0.06f, 0.03f), mau = C(0.95f, 0.90f, 0.62f),
                moTa = "Hai tinh hoàn bầu dục vàng nhạt bám mặt bụng thận." });
            coQuan.Add(new CoQuanMo { id = "than_phai", ten = "Thận phải", he = "Bài tiết", hinh = HinhCoQuan.Elip,
                tam = new Vector3(0.42f, 0.27f, 0.36f), kichThuoc = new Vector3(0.05f, 0.16f, 0.03f), mau = C(0.50f, 0.16f, 0.14f), luuLuongRach = 0.003f,
                moTa = "Thận giữa dạng dải dài sát lưng, lọc máu tạo nước tiểu đổ qua ống dẫn niệu vào huyệt." });
            coQuan.Add(new CoQuanMo { id = "than_trai", ten = "Thận trái", he = "Bài tiết", hinh = HinhCoQuan.Elip,
                tam = new Vector3(0.58f, 0.27f, 0.36f), kichThuoc = new Vector3(0.05f, 0.16f, 0.03f), mau = C(0.50f, 0.16f, 0.14f), luuLuongRach = 0.003f,
                moTa = "Thận giữa dạng dải dài sát lưng, lọc máu tạo nước tiểu đổ qua ống dẫn niệu vào huyệt." });
            coQuan.Add(new CoQuanMo { id = "bang_quang", ten = "Bàng quang (2 thùy)", he = "Bài tiết", hinh = HinhCoQuan.Elip,
                tam = new Vector3(0.5f, 0.07f, 0.16f), kichThuoc = new Vector3(0.12f, 0.07f, 0.06f), mau = C(0.92f, 0.86f, 0.72f, 0.75f), dich = "nuoc_tieu", theTichDich = 0.4f,
                moTa = "Bàng quang mỏng hai thùy, chứa nước tiểu và tái hấp thụ nước khi ếch lên cạn." });

            // Mạch máu
            mach.Add(new MachMau { id = "tm_bung", ten = "Tĩnh mạch bụng", dongMach = false, banKinhMm = 0.8f, lopId = "co",
                duong = new[] { new Vector3(0.5f, 0.06f, 0.04f), new Vector3(0.5f, 0.40f, 0.04f), new Vector3(0.5f, 0.68f, 0.045f) }, luuLuongCat = 0.010f });
            mach.Add(new MachMau { id = "dm_chu", ten = "Cung động mạch chủ", dongMach = true, banKinhMm = 0.7f, coQuanNuoi = "tim",
                duong = new[] { new Vector3(0.46f, 0.86f, 0.15f), new Vector3(0.38f, 0.90f, 0.20f), new Vector3(0.32f, 0.82f, 0.28f) }, luuLuongCat = 0.03f });
            mach.Add(new MachMau { id = "dm_chu_2", ten = "Cung động mạch chủ (trái)", dongMach = true, banKinhMm = 0.7f, coQuanNuoi = "tim",
                duong = new[] { new Vector3(0.54f, 0.86f, 0.15f), new Vector3(0.62f, 0.90f, 0.20f), new Vector3(0.68f, 0.82f, 0.28f) }, luuLuongCat = 0.03f });
            mach.Add(new MachMau { id = "tm_chu_sau", ten = "Tĩnh mạch chủ sau", dongMach = false, banKinhMm = 0.9f, coQuanNuoi = "tim",
                duong = new[] { new Vector3(0.5f, 0.74f, 0.22f), new Vector3(0.5f, 0.50f, 0.32f), new Vector3(0.5f, 0.30f, 0.36f) }, luuLuongCat = 0.025f });
            mach.Add(new MachMau { id = "dm_mang_treo", ten = "Mạch màng treo ruột", dongMach = true, banKinhMm = 0.4f, coQuanNuoi = "ruot_non",
                duong = new[] { new Vector3(0.5f, 0.40f, 0.30f), new Vector3(0.46f, 0.32f, 0.22f) }, luuLuongCat = 0.008f });

            // Thần kinh
            thanKinh.Add(new DayThanKinh { id = "x_trai", ten = "Dây thần kinh X (phế vị)", dich = "tim",
                duong = new[] { new Vector3(0.40f, 0.96f, 0.22f), new Vector3(0.43f, 0.88f, 0.18f), new Vector3(0.46f, 0.84f, 0.16f) } });
            thanKinh.Add(new DayThanKinh { id = "toa_phai", ten = "Đám rối thần kinh hông phải (dây tọa)", dich = "chi_sau_phai", coQuanChe = "ruot_non",
                duong = new[] { new Vector3(0.45f, 0.40f, 0.42f), new Vector3(0.40f, 0.22f, 0.42f), new Vector3(0.30f, 0.02f, 0.40f) } });
            thanKinh.Add(new DayThanKinh { id = "toa_trai", ten = "Đám rối thần kinh hông trái (dây tọa)", dich = "chi_sau_trai", coQuanChe = "ruot_non",
                duong = new[] { new Vector3(0.55f, 0.40f, 0.42f), new Vector3(0.60f, 0.22f, 0.42f), new Vector3(0.70f, 0.02f, 0.40f) } });
        }

        void DungNguoi()
        {
            ten = "Người: bệnh nhân ảo đã gây mê toàn thân, đặt nội khí quản, thở máy";
            sinhLy.nhietDo = 36.8f;
            huyNao = false; huyTuy = false;
            lop.Add(new LopMo { id = "da", ten = "Da và mô dưới da", loai = LoaiLop.Da, doSau = 0f, u0 = 0f, u1 = 1f, v0 = 0f, v1 = 1f, nu = 20, nv = 32,
                mauNgoai = C(0.86f, 0.68f, 0.58f), mauTrong = C(0.95f, 0.82f, 0.45f), thoiGianRoi = 2.5f }.Tao());
            lop.Add(new LopMo { id = "co", ten = "Cơ và cân thành bụng", loai = LoaiLop.Co, doSau = 0.04f, u0 = 0.03f, u1 = 0.97f, v0 = 0.02f, v1 = 0.58f, nu = 18, nv = 18,
                mauNgoai = C(0.72f, 0.24f, 0.24f), mauTrong = C(0.62f, 0.18f, 0.2f), thoiGianRoi = 2.5f }.Tao());
            lop.Add(new LopMo { id = "phuc_mac", ten = "Phúc mạc thành", loai = LoaiLop.Mang, doSau = 0.06f, u0 = 0.05f, u1 = 0.95f, v0 = 0.03f, v1 = 0.58f, nu = 16, nv = 16,
                mauNgoai = C(0.92f, 0.86f, 0.84f, 0.75f), mauTrong = C(0.9f, 0.8f, 0.78f, 0.6f), thoiGianRoi = 2.5f }.Tao());
            lop.Add(new LopMo { id = "xuong", ten = "Xương ức và sụn sườn", loai = LoaiLop.Xuong, doSau = 0.045f, u0 = 0.12f, u1 = 0.88f, v0 = 0.58f, v1 = 0.98f, nu = 16, nv = 12,
                dinhDuoi = false, dinhTren = false, mauNgoai = C(0.90f, 0.87f, 0.80f), mauTrong = C(0.78f, 0.55f, 0.50f), thoiGianRoi = 1.5f }.Tao());
            lop.Add(new LopMo { id = "mang_tim", ten = "Màng ngoài tim", loai = LoaiLop.Mang, doSau = 0.10f, u0 = 0.36f, u1 = 0.72f, v0 = 0.62f, v1 = 0.88f, nu = 10, nv = 8,
                dinhDuoi = false, dinhTrai = false, dinhPhai = false, mauNgoai = C(0.94f, 0.90f, 0.86f, 0.7f), mauTrong = C(0.9f, 0.8f, 0.78f, 0.55f), thoiGianRoi = 99f }.Tao());

            // u: 0 = bên phải bệnh nhân (người mổ đứng bên phải), v: 0 = xương mu, 1 = hõm ức
            coQuan.Add(new CoQuanMo { id = "tim", ten = "Tim (4 ngăn)", he = "Tuần hoàn", hinh = HinhCoQuan.Tim, chuyenDong = ChuyenDong.Tim,
                tam = new Vector3(0.56f, 0.74f, 0.20f), kichThuoc = new Vector3(0.20f, 0.18f, 0.14f), mau = C(0.62f, 0.12f, 0.12f), dich = "mau_dm", theTichDich = 120f, coTheCatBo = false,
                moTa = "Tâm thất trái thành dày nhất, đẩy máu vào động mạch chủ. Chu kỳ ở 75 lần/phút ≈ 0,8 s: nhĩ co 0,1 s, thất co 0,3 s, dãn chung 0,4 s." });
            coQuan.Add(new CoQuanMo { id = "phoi_phai", ten = "Phổi phải (3 thùy)", he = "Hô hấp", hinh = HinhCoQuan.Phoi, chuyenDong = ChuyenDong.Phoi,
                tam = new Vector3(0.28f, 0.78f, 0.24f), kichThuoc = new Vector3(0.22f, 0.30f, 0.14f), mau = C(0.88f, 0.58f, 0.60f), dich = "khi", theTichDich = 500f,
                moTa = "Đang thở máy khoảng 14 lần/phút, mỗi lần khoảng 6 – 8 mL/kg; mở ngực phổi vẫn phồng vì máy đẩy khí bằng áp lực dương." });
            coQuan.Add(new CoQuanMo { id = "phoi_trai", ten = "Phổi trái (2 thùy)", he = "Hô hấp", hinh = HinhCoQuan.Phoi, chuyenDong = ChuyenDong.Phoi,
                tam = new Vector3(0.76f, 0.78f, 0.26f), kichThuoc = new Vector3(0.18f, 0.28f, 0.14f), mau = C(0.88f, 0.58f, 0.60f), dich = "khi", theTichDich = 450f,
                moTa = "Phổi trái nhỏ hơn, có khuyết tim; trao đổi khí qua màng phế nang – mao mạch." });
            coQuan.Add(new CoQuanMo { id = "co_hoanh", ten = "Cơ hoành", he = "Hô hấp", hinh = HinhCoQuan.Day, chuyenDong = ChuyenDong.CoHoanh,
                tam = new Vector3(0.5f, 0.59f, 0.26f), kichThuoc = new Vector3(0.80f, 0.05f, 0.04f), mau = C(0.66f, 0.22f, 0.22f), coTheCatBo = false,
                moTa = "Cơ vân hình vòm ngăn ngực và bụng, do thần kinh hoành (C3 – C5) chi phối." });
            coQuan.Add(new CoQuanMo { id = "gan", ten = "Gan", he = "Tiêu hóa", hinh = HinhCoQuan.ThuyGan,
                tam = new Vector3(0.32f, 0.50f, 0.17f), kichThuoc = new Vector3(0.48f, 0.16f, 0.12f), mau = C(0.45f, 0.13f, 0.10f), luuLuongRach = 4f, coTheVen = true, coTheCatBo = false,
                che = new[] { "tui_mat" }, moTa = "Tuyến lớn nhất, tiết mật, chuyển hóa glucose, protein, khử độc. Nhu mô nhiều máu nên rách gan chảy máu nhiều." });
            coQuan.Add(new CoQuanMo { id = "tui_mat", ten = "Túi mật", he = "Tiêu hóa", hinh = HinhCoQuan.Elip,
                tam = new Vector3(0.38f, 0.44f, 0.22f), kichThuoc = new Vector3(0.05f, 0.08f, 0.05f), mau = C(0.30f, 0.52f, 0.22f), dich = "mat", theTichDich = 40f,
                moTa = "Đáy túi mật lộ ở bờ dưới gan, chứa khoảng 30 – 50 mL mật đã cô đặc." });
            coQuan.Add(new CoQuanMo { id = "da_day", ten = "Dạ dày", he = "Tiêu hóa", hinh = HinhCoQuan.Ong, chuyenDong = ChuyenDong.NhuDong,
                tam = new Vector3(0.64f, 0.48f, 0.18f), kichThuoc = new Vector3(0.20f, 0.16f, 0.12f), mau = C(0.86f, 0.66f, 0.60f), dich = "dich_vi", theTichDich = 50f, coTheVen = true,
                che = new[] { "tuy" }, duongOng = new[] { new Vector3(0.58f, 0.56f, 0.2f), new Vector3(0.70f, 0.52f, 0.18f), new Vector3(0.70f, 0.44f, 0.18f), new Vector3(0.56f, 0.42f, 0.2f) },
                moTa = "Co bóp nhào trộn khoảng 3 lần/phút, tiết dịch vị (HCl, pepsin)." });
            coQuan.Add(new CoQuanMo { id = "lach", ten = "Lách", he = "Tuần hoàn", hinh = HinhCoQuan.Elip,
                tam = new Vector3(0.84f, 0.50f, 0.30f), kichThuoc = new Vector3(0.07f, 0.10f, 0.05f), mau = C(0.48f, 0.10f, 0.20f), luuLuongRach = 3f,
                moTa = "Lọc hồng cầu già, chứa nhiều máu; vỡ lách gây chảy máu trong ổ bụng." });
            coQuan.Add(new CoQuanMo { id = "tuy", ten = "Tụy", he = "Tiêu hóa", hinh = HinhCoQuan.Elip,
                tam = new Vector3(0.58f, 0.42f, 0.34f), kichThuoc = new Vector3(0.24f, 0.05f, 0.04f), mau = C(0.92f, 0.78f, 0.62f), coTheCatBo = false,
                moTa = "Nằm sau phúc mạc sau dạ dày; ngoại tiết dịch tụy, nội tiết insulin, glucagon." });
            coQuan.Add(new CoQuanMo { id = "mac_noi", ten = "Mạc nối lớn", he = "Tiêu hóa", hinh = HinhCoQuan.Day,
                tam = new Vector3(0.5f, 0.27f, 0.12f), kichThuoc = new Vector3(0.70f, 0.26f, 0.02f), mau = C(0.98f, 0.86f, 0.42f, 0.92f), coTheVen = true,
                che = new[] { "ruot_non", "dai_trang" }, moTa = "Tấm mỡ như tạp dề treo từ bờ cong lớn dạ dày phủ trước các quai ruột; là thứ thấy đầu tiên khi mở phúc mạc." });
            coQuan.Add(new CoQuanMo { id = "ruot_non", ten = "Ruột non", he = "Tiêu hóa", hinh = HinhCoQuan.Ong, chuyenDong = ChuyenDong.NhuDong,
                tam = new Vector3(0.5f, 0.24f, 0.20f), kichThuoc = new Vector3(0.44f, 0.24f, 0.08f), mau = C(0.92f, 0.72f, 0.66f), dich = "nhu_tran", theTichDich = 200f, coTheVen = true,
                che = new[] { "than_phai", "than_trai", "tm_chu_duoi" },
                duongOng = new[] { new Vector3(0.62f, 0.38f, 0.2f), new Vector3(0.36f, 0.34f, 0.2f), new Vector3(0.30f, 0.26f, 0.2f), new Vector3(0.52f, 0.28f, 0.19f),
                                   new Vector3(0.68f, 0.24f, 0.2f), new Vector3(0.64f, 0.16f, 0.2f), new Vector3(0.40f, 0.18f, 0.2f), new Vector3(0.34f, 0.12f, 0.2f), new Vector3(0.24f, 0.16f, 0.22f) },
                moTa = "Dài khoảng 6 m, nhu động 8 – 12 lần/phút; hấp thụ qua lông ruột." });
            coQuan.Add(new CoQuanMo { id = "dai_trang", ten = "Đại tràng", he = "Tiêu hóa", hinh = HinhCoQuan.Ong, chuyenDong = ChuyenDong.NhuDong,
                tam = new Vector3(0.5f, 0.28f, 0.22f), kichThuoc = new Vector3(0.70f, 0.30f, 0.10f), mau = C(0.80f, 0.62f, 0.56f), dich = "nhu_tran", theTichDich = 150f, coTheVen = true,
                duongOng = new[] { new Vector3(0.18f, 0.10f, 0.24f), new Vector3(0.16f, 0.36f, 0.22f), new Vector3(0.30f, 0.42f, 0.2f), new Vector3(0.70f, 0.42f, 0.2f),
                                   new Vector3(0.84f, 0.36f, 0.22f), new Vector3(0.82f, 0.12f, 0.24f), new Vector3(0.62f, 0.06f, 0.26f) },
                moTa = "Khung đại tràng bao quanh ruột non; hấp thu nước, nhu động chậm, có các đợt co khối vài lần mỗi ngày." });
            coQuan.Add(new CoQuanMo { id = "than_phai", ten = "Thận phải", he = "Bài tiết", hinh = HinhCoQuan.Elip,
                tam = new Vector3(0.34f, 0.38f, 0.44f), kichThuoc = new Vector3(0.07f, 0.12f, 0.05f), mau = C(0.52f, 0.16f, 0.14f), luuLuongRach = 2f, coTheCatBo = false,
                moTa = "Sau phúc mạc, lọc khoảng 180 L dịch mỗi ngày qua khoảng 1 triệu đơn vị thận." });
            coQuan.Add(new CoQuanMo { id = "than_trai", ten = "Thận trái", he = "Bài tiết", hinh = HinhCoQuan.Elip,
                tam = new Vector3(0.66f, 0.40f, 0.44f), kichThuoc = new Vector3(0.07f, 0.12f, 0.05f), mau = C(0.52f, 0.16f, 0.14f), luuLuongRach = 2f, coTheCatBo = false,
                moTa = "Sau phúc mạc, lọc khoảng 180 L dịch mỗi ngày qua khoảng 1 triệu đơn vị thận." });
            coQuan.Add(new CoQuanMo { id = "bang_quang", ten = "Bàng quang", he = "Bài tiết", hinh = HinhCoQuan.Elip,
                tam = new Vector3(0.5f, 0.06f, 0.20f), kichThuoc = new Vector3(0.12f, 0.08f, 0.08f), mau = C(0.90f, 0.82f, 0.70f), dich = "nuoc_tieu", theTichDich = 250f, coTheCatBo = false,
                moTa = "Chứa nước tiểu, thành cơ trơn (cơ bức niệu)." });

            mach.Add(new MachMau { id = "mm_da", ten = "Mao mạch dưới da", dongMach = false, banKinhMm = 0.1f, lopId = "da", luuLuongCat = 0f, thoiGianDong = 150f });
            mach.Add(new MachMau { id = "dm_thuong_vi_p", ten = "Động mạch thượng vị dưới phải", dongMach = true, banKinhMm = 1.5f, lopId = "co",
                duong = new[] { new Vector3(0.38f, 0.04f, 0.045f), new Vector3(0.38f, 0.30f, 0.045f), new Vector3(0.36f, 0.56f, 0.045f) }, luuLuongCat = 1.2f });
            mach.Add(new MachMau { id = "dm_thuong_vi_t", ten = "Động mạch thượng vị dưới trái", dongMach = true, banKinhMm = 1.5f, lopId = "co",
                duong = new[] { new Vector3(0.62f, 0.04f, 0.045f), new Vector3(0.62f, 0.30f, 0.045f), new Vector3(0.64f, 0.56f, 0.045f) }, luuLuongCat = 1.2f });
            mach.Add(new MachMau { id = "dm_chu", ten = "Động mạch chủ lên và quai", dongMach = true, banKinhMm = 14f, coQuanNuoi = "tim",
                duong = new[] { new Vector3(0.54f, 0.84f, 0.22f), new Vector3(0.52f, 0.92f, 0.24f), new Vector3(0.60f, 0.95f, 0.30f) }, luuLuongCat = 70f });
            mach.Add(new MachMau { id = "tm_chu_duoi", ten = "Tĩnh mạch chủ dưới", dongMach = false, banKinhMm = 11f,
                duong = new[] { new Vector3(0.44f, 0.08f, 0.48f), new Vector3(0.44f, 0.30f, 0.48f), new Vector3(0.44f, 0.50f, 0.42f) }, luuLuongCat = 25f });
            mach.Add(new MachMau { id = "dm_mac_treo", ten = "Mạch mạc treo ruột", dongMach = true, banKinhMm = 2f, coQuanNuoi = "ruot_non",
                duong = new[] { new Vector3(0.52f, 0.40f, 0.30f), new Vector3(0.46f, 0.26f, 0.24f) }, luuLuongCat = 3f });

            thanKinh.Add(new DayThanKinh { id = "hoanh_phai", ten = "Thần kinh hoành phải", dich = "co_hoanh_phai",
                duong = new[] { new Vector3(0.36f, 0.94f, 0.18f), new Vector3(0.36f, 0.76f, 0.16f), new Vector3(0.36f, 0.60f, 0.24f) } });
            thanKinh.Add(new DayThanKinh { id = "hoanh_trai", ten = "Thần kinh hoành trái", dich = "co_hoanh_trai",
                duong = new[] { new Vector3(0.72f, 0.94f, 0.18f), new Vector3(0.72f, 0.76f, 0.16f), new Vector3(0.70f, 0.60f, 0.24f) } });
            thanKinh.Add(new DayThanKinh { id = "x_trai", ten = "Dây thần kinh X trái (bắt chéo trước quai động mạch chủ)", dich = "tim",
                duong = new[] { new Vector3(0.62f, 0.99f, 0.26f), new Vector3(0.60f, 0.93f, 0.25f), new Vector3(0.58f, 0.86f, 0.28f) } });
        }

        // ---------------------------------------------------------------- truy vấn hình học

        /// <summary>Lớp trên cùng còn liền (chưa mở) tại điểm (u, v); null nếu đã hở tới khoang.</summary>
        public LopMo LopTrenCung(float u, float v)
        {
            LopMo tot = null;
            foreach (var l in lop)
            {
                if (!l.O(u, v, out int i, out int j)) continue;
                if (l.o[l.Idx(i, j)] != TrangThaiO.Lien) continue;
                if (tot == null || l.doSau < tot.doSau) tot = l;
            }
            return tot;
        }

        /// <summary>Có lớp nào phía trên độ sâu d tại (u, v) còn che không.</summary>
        public LopMo LopChe(float u, float v, float d)
        {
            foreach (var l in lop)
            {
                if (l.doSau >= d - 0.0001f) continue;
                if (!l.O(u, v, out int i, out int j)) continue;
                if (l.o[l.Idx(i, j)] == TrangThaiO.Lien) return l;
            }
            return null;
        }

        public CoQuanMo CoQuanChe(CoQuanMo cq)
        {
            foreach (var k in coQuan)
            {
                if (k == cq || k.ven || k.catBo || k.tachRoi) continue;
                if (Array.IndexOf(k.che, cq.id) >= 0) return k;
            }
            return null;
        }

        public string LyDoChuaLo(CoQuanMo cq)
        {
            var l = LopChe(cq.tam.x, cq.tam.y, cq.tam.z);
            if (l != null) return $"{cq.ten} còn bị {l.ten.ToLowerInvariant()} che.";
            var k = CoQuanChe(cq);
            if (k != null) return $"{cq.ten} nằm sau {k.ten.ToLowerInvariant()}; dùng kẹp panh vén {k.ten.ToLowerInvariant()} sang bên.";
            return null;
        }

        public bool DiemLo(Vector3 p) => LopChe(p.x, p.y, p.z) == null;

        // ---------------------------------------------------------------- luật dụng cụ trên lớp mô

        static bool CatDuoc(DungCuMo dc, LoaiLop l, LoaiMau m)
        {
            switch (dc)
            {
                case DungCuMo.DaoMo: return l != LoaiLop.Xuong;
                case DungCuMo.Keo: return l != LoaiLop.Xuong || m == LoaiMau.Ech; // xương ếch mỏng, kéo cắt được; xương ức người cần cưa
                case DungCuMo.CuaXuong: return l == LoaiLop.Xuong;
                default: return false;
            }
        }

        /// <summary>Kiểm tra điều kiện trước khi đặt dụng cụ cắt lên mẫu (đạo đức, cố định mẫu).</summary>
        public string LyDoChuaChoMo()
        {
            if (loai == LoaiMau.Ech)
            {
                if (!huyNao) return "Ếch còn sống và còn cảm giác. Phải hủy não, tủy bằng kim mũi mác (qua lỗ chẩm) trước khi mổ.";
                if (soChiGhim < 4) return $"Ếch mới ghim {soChiGhim}/4 chi lên khay sáp, mẫu còn trượt theo lưỡi dao. Ghim đủ 4 chi trước.";
            }
            return null;
        }

        /// <summary>Đường rạch (u, v) bằng dao, kéo hoặc cưa. Chỉ cắt lớp trên cùng tại điểm đặt dụng cụ, đúng như lưỡi dao chạm mô.</summary>
        public KetQua Rach(DungCuMo dc, IList<Vector2> duong)
        {
            if (duong == null || duong.Count < 2) return new KetQua(false, "Kéo một đường trên mô để rạch.");
            string ly = LyDoChuaChoMo();
            if (ly != null) return new KetQua(false, ly);
            var dau = duong[0];
            var l = LopTrenCung(dau.x, dau.y);
            if (l == null) return new KetQua(false, "Chỗ này đã hở tới khoang cơ thể; muốn cắt cơ quan, mạch thì dùng dụng cụ trực tiếp trên chúng.");
            if (!CatDuoc(dc, l.loai, loai))
            {
                string ten = dc == DungCuMo.DaoMo ? "Dao mổ" : dc == DungCuMo.Keo ? "Kéo" : "Cưa";
                if (l.loai == LoaiLop.Xuong) return new KetQua(false, $"{ten} không cắt được {l.ten.ToLowerInvariant()}. Xương ức người cần cưa xương ức.");
                return new KetQua(false, $"Cưa chỉ dùng cho xương; {l.ten.ToLowerInvariant()} rạch bằng dao hoặc kéo.");
            }
            if (dc == DungCuMo.Keo && !CoLoVao(l, dau))
                return new KetQua(false, $"Mũi kéo cần luồn vào một chỗ đã rạch hoặc mép đã hở của {l.ten.ToLowerInvariant()}. Rạch mở đầu bằng dao, hoặc bắt đầu kéo từ mép vết rạch.");

            // Tay người kéo dao thường dừng sát mép chứ không đúng mép: đầu đường rạch nằm trong khoảng 1,5 ô
            // cạnh mép vùng mô thì nối thẳng ra quá mép, để vết rạch thật sự chạy hết tới mép
            var duongCat = new List<Vector2>(duong);
            NoiRaMep(l, duongCat, 0, 1);
            NoiRaMep(l, duongCat, duongCat.Count - 1, duongCat.Count - 2);
            int canhMoi = 0;
            for (int k = 1; k < duongCat.Count; k++)
                canhMoi += CatDoan(l, duongCat[k - 1], duongCat[k]);
            if (canhMoi == 0) return new KetQua(false, $"Đường kéo quá ngắn hoặc chạy ra ngoài vùng {l.ten.ToLowerInvariant()}, chưa cắt được gì.");
            l.phienBan++;

            var sb = new System.Text.StringBuilder();
            sb.Append($"Rạch {l.ten.ToLowerInvariant()} dài khoảng {canhMoi * l.CoO * DoDaiThatCm():0.0} cm.");
            // Mạch nằm trong lớp này dưới đường rạch thì đứt
            foreach (var m in mach)
            {
                if (m.lopId != l.id || m.dut) continue;
                if (m.duong == null) continue;
                if (CatQuaMach(duong, m, out Vector3 p))
                {
                    m.dut = true; m.diemDut = p; m.tuoiDut = 0f;
                    sb.Append($" Lưỡi cắt đứt {m.ten.ToLowerInvariant()}: máu {(m.dongMach ? "phụt thành tia theo nhịp tim" : "chảy đều, sẫm màu")}.");
                }
            }
            if (l.id == "da")
            {
                var mm = Mach("mm_da");
                if (mm != null) { mm.dut = true; mm.dongCuc = false; mm.tuoiDut = 0f; mm.luuLuongCat += 0.004f * canhMoi; mm.diemDut = new Vector3(dau.x, dau.y, 0.01f); }
                // Túi bạch huyết nằm dưới da, ngoài thành bụng: dịch chảy ra khay, không dồn vào khoang cơ thể
                if (loai == LoaiMau.Ech) sb.Append(" Dịch bạch huyết trong chảy ra khay từ túi bạch huyết dưới da (da ếch dính lỏng vào cơ).");
                else sb.Append(" Mép da rỉ máu từ mao mạch, tự cầm sau vài phút.");
            }
            if (l.id == "co" && loai == LoaiMau.Nguoi && Mathf.Abs(dau.x - 0.5f) < 0.04f && Mathf.Abs(duong[duong.Count - 1].x - 0.5f) < 0.04f)
                sb.Append(" Đường giữa (đường trắng) là cân, ít mạch nên gần như không chảy máu.");
            Ghi(sb.ToString());
            return new KetQua(true, sb.ToString());
        }

        static void NoiRaMep(LopMo l, List<Vector2> d, int dau, int ke)
        {
            var p = d[dau];
            float du = (l.u1 - l.u0) / l.nu, dv = (l.v1 - l.v0) / l.nv;
            var huong = (p - d[ke]);
            if (huong.sqrMagnitude < 1e-10f) return;
            huong.Normalize();
            Vector2 q = p;
            if (Mathf.Abs(huong.x) > 0.6f)
            {
                if (huong.x < 0 && p.x - l.u0 < 1.5f * du) q.x = l.u0 - du;
                if (huong.x > 0 && l.u1 - p.x < 1.5f * du) q.x = l.u1 + du;
            }
            if (Mathf.Abs(huong.y) > 0.6f)
            {
                if (huong.y < 0 && p.y - l.v0 < 1.5f * dv) q.y = l.v0 - dv;
                if (huong.y > 0 && l.v1 - p.y < 1.5f * dv) q.y = l.v1 + dv;
            }
            if (q != p) { if (dau == 0) d.Insert(0, q); else d.Add(q); }
        }

        float DoDaiThatCm() => loai == LoaiMau.Ech ? 4.2f : 34f; // bề rộng cửa sổ mổ ngoài đời (cm)

        bool CoLoVao(LopMo l, Vector2 p)
        {
            // Chỗ đặt mũi kéo phải gần một cạnh đã cắt, ô đã mở, hoặc mép tự do của lớp
            float r = l.CoO * 1.6f;
            for (int a = -1; a <= 1; a++)
                for (int b = -1; b <= 1; b++)
                {
                    var q = new Vector2(p.x + a * r * 0.7f, p.y + b * r * 0.7f);
                    if (!l.O(q.x, q.y, out int i, out int j)) { if (!MepDinh(l, q)) return true; continue; }
                    if (l.o[l.Idx(i, j)] != TrangThaiO.Lien) return true;
                    if (l.DaCatDoc(i, j) || l.DaCatDoc(i - 1, j) || l.DaCatNgang(i, j) || l.DaCatNgang(i, j - 1)) return true;
                }
            return false;
        }

        bool MepDinh(LopMo l, Vector2 q)
        {
            if (q.x < l.u0) return l.dinhTrai;
            if (q.x > l.u1) return l.dinhPhai;
            if (q.y < l.v0) return l.dinhDuoi;
            if (q.y > l.v1) return l.dinhTren;
            return true;
        }

        static int CatDoan(LopMo l, Vector2 a, Vector2 b)
        {
            // Đổi sang tọa độ ô; cạnh giữa hai ô bị cắt khi đoạn rạch cắt qua đoạn nối tâm hai ô (cạnh đối ngẫu)
            Vector2 A = new Vector2((a.x - l.u0) / (l.u1 - l.u0) * l.nu, (a.y - l.v0) / (l.v1 - l.v0) * l.nv);
            Vector2 B = new Vector2((b.x - l.u0) / (l.u1 - l.u0) * l.nu, (b.y - l.v0) / (l.v1 - l.v0) * l.nv);
            int n = 0;
            // đường ngang y = j + 0.5 → cạnh dọc (i, j) giữa ô i và i+1
            for (int j = 0; j < l.nv; j++)
            {
                float y = j + 0.5f;
                if ((A.y - y) * (B.y - y) > 0f || Mathf.Approximately(A.y, B.y)) continue;
                float t = (y - A.y) / (B.y - A.y);
                float x = Mathf.Lerp(A.x, B.x, t);
                int i = Mathf.FloorToInt(x - 0.5f);
                if (i < 0 || i >= l.nu - 1) continue;
                if (!OLienHoacMo(l, i, j) && !OLienHoacMo(l, i + 1, j)) continue;
                int k = i + j * (l.nu - 1);
                if (!l.catDoc[k]) { l.catDoc[k] = true; n++; }
            }
            // đường dọc x = i + 0.5 → cạnh ngang (i, j) giữa ô j và j+1
            for (int i = 0; i < l.nu; i++)
            {
                float x = i + 0.5f;
                if ((A.x - x) * (B.x - x) > 0f || Mathf.Approximately(A.x, B.x)) continue;
                float t = (x - A.x) / (B.x - A.x);
                float y = Mathf.Lerp(A.y, B.y, t);
                int j = Mathf.FloorToInt(y - 0.5f);
                if (j < 0 || j >= l.nv - 1) continue;
                int k = i + j * l.nu;
                if (!l.catNgang[k]) { l.catNgang[k] = true; n++; }
            }
            return n;
        }

        static bool OLienHoacMo(LopMo l, int i, int j) => i >= 0 && i < l.nu && j >= 0 && j < l.nv;

        static bool CatQuaMach(IList<Vector2> duong, MachMau m, out Vector3 diem)
        {
            diem = Vector3.zero;
            for (int k = 1; k < duong.Count; k++)
                for (int s = 1; s < m.duong.Length; s++)
                {
                    Vector2 a = duong[k - 1], b = duong[k];
                    Vector2 c = new Vector2(m.duong[s - 1].x, m.duong[s - 1].y), d = new Vector2(m.duong[s].x, m.duong[s].y);
                    if (GiaoDoan(a, b, c, d, out float t, out float w))
                    {
                        diem = Vector3.Lerp(m.duong[s - 1], m.duong[s], w);
                        return true;
                    }
                    // đường rạch chạy dọc sát mạch (trong 0,02) cũng làm đứt mạch
                    if (KhoangCachDoan(Vector2.Lerp(a, b, 0.5f), c, d) < 0.015f && (b - a).sqrMagnitude > 0.0001f)
                    {
                        diem = new Vector3((a.x + b.x) * 0.5f, (a.y + b.y) * 0.5f, m.duong[s].z);
                        return true;
                    }
                }
            return false;
        }

        public static bool GiaoDoan(Vector2 a, Vector2 b, Vector2 c, Vector2 d, out float t, out float w)
        {
            t = w = 0f;
            Vector2 r = b - a, s = d - c;
            float den = r.x * s.y - r.y * s.x;
            if (Mathf.Abs(den) < 1e-8f) return false;
            Vector2 ca = c - a;
            t = (ca.x * s.y - ca.y * s.x) / den;
            w = (ca.x * r.y - ca.y * r.x) / den;
            return t >= 0f && t <= 1f && w >= 0f && w <= 1f;
        }

        public static float KhoangCachDoan(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = ab.sqrMagnitude < 1e-9f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(p, a + ab * t);
        }

        /// <summary>Kẹp panh / banh lật vạt tại (u, v): vạt phải rời ít nhất 3 phía và chỉ còn dính theo một mép.</summary>
        public KetQua LatVat(float u, float v, bool banh)
        {
            var l = LopTrenCung(u, v);
            if (l == null) return new KetQua(false, "Không có lớp mô nào chưa mở ở chỗ này.");
            if (!l.O(u, v, out int i0, out int j0)) return new KetQua(false, "Ngoài vùng mô.");

            // Loang các ô liền nhau qua cạnh chưa cắt
            var thuoc = new bool[l.o.Length];
            var hang = new Queue<int>();
            var vung = new List<int>();
            int s0 = l.Idx(i0, j0);
            thuoc[s0] = true; hang.Enqueue(s0);
            int[] banLe = new int[4]; // số cạnh còn dính theo từng mép: trái, phải, dưới, trên
            while (hang.Count > 0)
            {
                int c = hang.Dequeue();
                vung.Add(c);
                int i = c % l.nu, j = c / l.nu;
                // trái
                if (i == 0) { if (l.dinhTrai) banLe[0]++; }
                else if (!l.DaCatDoc(i - 1, j)) ThuNhan(l, i - 1, j, thuoc, hang);
                // phải
                if (i == l.nu - 1) { if (l.dinhPhai) banLe[1]++; }
                else if (!l.DaCatDoc(i, j)) ThuNhan(l, i + 1, j, thuoc, hang);
                // dưới
                if (j == 0) { if (l.dinhDuoi) banLe[2]++; }
                else if (!l.DaCatNgang(i, j - 1)) ThuNhan(l, i, j - 1, thuoc, hang);
                // trên
                if (j == l.nv - 1) { if (l.dinhTren) banLe[3]++; }
                else if (!l.DaCatNgang(i, j)) ThuNhan(l, i, j + 1, thuoc, hang);
            }

            int soPhia = 0, phia = -1, tong = 0;
            for (int k = 0; k < 4; k++) if (banLe[k] > 0) { soPhia++; phia = k; tong += banLe[k]; }

            if (soPhia == 0)
            {
                foreach (var c in vung) { l.o[c] = TrangThaiO.Bo; l.vatCuaO[c] = -1; }
                l.phienBan++;
                string tb = $"Gắp bỏ mảnh {l.ten.ToLowerInvariant()} đã rời hẳn ({vung.Count * l.CoO * l.CoO * DoDaiThatCm() * DoDaiThatCm():0.0} cm²).";
                Ghi(tb);
                return new KetQua(true, tb);
            }
            if (vung.Count > l.o.Length * 0.8f && l.SoCanhCat() == 0)
                return new KetQua(false, $"{l.ten} còn nguyên, chưa có đường rạch nào; kẹp chỉ nhấc được nếp mô lên.");
            if (soPhia > 1)
            {
                string[] tenMep = { "bên phải mẫu", "bên trái mẫu", "phía dưới", "phía trên" };
                if (loai == LoaiMau.Nguoi) tenMep = new[] { "bên phải bệnh nhân", "bên trái bệnh nhân", "phía chân", "phía đầu" };
                var con = new List<string>();
                for (int k = 0; k < 4; k++) if (banLe[k] > 0) con.Add(tenMep[k]);
                return new KetQua(false, $"Vạt {l.ten.ToLowerInvariant()} còn dính {string.Join(", ", con)}. Vạt chỉ lật được khi đã rạch rời các phía và chỉ còn dính theo một mép (ví dụ một đường dọc giữa và hai đường ngang ở hai đầu).");
            }
            if (l.loai == LoaiLop.Xuong && loai == LoaiMau.Nguoi && !banh)
                return new KetQua(false, "Nửa xương ức đã cưa rời nhưng lồng ngực cứng, kẹp panh không giữ nổi: dùng banh để tách rộng hai nửa xương ức.");

            var vm = new VatMo { lop = l, phiaBanLe = phia, banh = banh };
            int id = vat.Count;
            vat.Add(vm);
            foreach (var c in vung) { vm.oVat.Add(c); l.o[c] = TrangThaiO.Lat; l.vatCuaO[c] = id; }
            l.phienBan++;
            string kq = banh
                ? $"Đặt banh giữ mở {l.ten.ToLowerInvariant()} ({vung.Count} ô, bản lề {TenMep(phia)})."
                : $"Lật vạt {l.ten.ToLowerInvariant()} sang {TenMep(phia)}. Vạt chưa ghim sẽ tự rơi về sau vài giây.";
            Ghi(kq);
            return new KetQua(true, kq);
        }

        string TenMep(int phia)
        {
            if (loai == LoaiMau.Nguoi) return phia == 0 ? "bên phải bệnh nhân" : phia == 1 ? "bên trái bệnh nhân" : phia == 2 ? "phía chân" : "phía đầu";
            return phia == 0 ? "bên phải con ếch" : phia == 1 ? "bên trái con ếch" : phia == 2 ? "phía huyệt" : "phía đầu";
        }

        static void ThuNhan(LopMo l, int i, int j, bool[] thuoc, Queue<int> hang)
        {
            int c = l.Idx(i, j);
            if (thuoc[c]) return;
            if (l.o[c] != TrangThaiO.Lien) return; // ô đã mở không dính kéo theo
            thuoc[c] = true;
            hang.Enqueue(c);
        }

        public KetQua GhimVat(float u, float v)
        {
            // Ghim vạt đang lật gần điểm (vạt nằm ngoài mép nên tìm theo ô mở gần nhất trong mọi lớp)
            foreach (var vm in vat)
            {
                if (vm.ghim || vm.oVat.Count == 0 || vm.lop.o[vm.oVat[0]] != TrangThaiO.Lat) continue;
                foreach (var c in vm.oVat)
                {
                    var t = vm.lop.TamO(c % vm.lop.nu, c / vm.lop.nu);
                    if (Mathf.Abs(t.x - u) < 0.12f && Mathf.Abs(t.y - v) < 0.12f || GanMep(vm, u, v))
                    {
                        vm.ghim = true;
                        string tb = $"Ghim vạt {vm.lop.ten.ToLowerInvariant()} xuống khay, vạt giữ mở.";
                        Ghi(tb);
                        return new KetQua(true, tb);
                    }
                }
            }
            return new KetQua(false, "Không có vạt đang lật ở gần để ghim. Lật vạt bằng kẹp panh rồi ghim ngay mép vạt.");
        }

        VatMo VatTheoO(LopMo l, int c)
        {
            if (l == null || c < 0 || c >= l.o.Length || l.o[c] != TrangThaiO.Lat) return null;
            int id = l.vatCuaO[c];
            return id >= 0 && id < vat.Count ? vat[id] : null;
        }

        public KetQua GhimVatTheoO(LopMo l, int c)
        {
            var vm = VatTheoO(l, c);
            if (vm == null) return new KetQua(false, "Chỗ này không phải vạt đang lật.");
            if (vm.ghim) return new KetQua(false, "Vạt này đã ghim.");
            if (l.loai == LoaiLop.Xuong) return new KetQua(false, "Xương không ghim được; dùng banh giữ mở.");
            vm.ghim = true;
            string tb = $"Ghim vạt {l.ten.ToLowerInvariant()} xuống {(loai == LoaiMau.Ech ? "khay sáp" : "khăn mổ")}, vạt giữ mở.";
            Ghi(tb);
            return new KetQua(true, tb);
        }

        public KetQua BanhVatTheoO(LopMo l, int c)
        {
            var vm = VatTheoO(l, c);
            if (vm == null) return new KetQua(false, "Chỗ này không phải vạt đang lật.");
            if (vm.banh) return new KetQua(false, "Đã có banh giữ vạt này.");
            vm.banh = true;
            string tb = $"Đặt banh giữ mở {l.ten.ToLowerInvariant()}.";
            Ghi(tb);
            return new KetQua(true, tb);
        }

        public KetQua DatLaiVatTheoO(LopMo l, int c)
        {
            var vm = VatTheoO(l, c);
            if (vm == null) return new KetQua(false, "Chỗ này không phải vạt đang lật.");
            foreach (var o in vm.oVat) { l.o[o] = TrangThaiO.Lien; l.vatCuaO[o] = -1; }
            vm.oVat.Clear();
            l.phienBan++;
            string tb = $"Đặt vạt {l.ten.ToLowerInvariant()} về chỗ cũ (đường rạch vẫn còn).";
            Ghi(tb);
            return new KetQua(true, tb);
        }

        /// <summary>Ống thổi đặt vào miệng ếch: thổi qua khe thanh quản, không cần mở ngực.</summary>
        public KetQua ThoiQuaThanhQuan()
        {
            if (loai != LoaiMau.Ech) return new KetQua(false, "Bệnh nhân đã thở máy qua nội khí quản.");
            var kq = ThoiPhoi();
            if (kq.ok) Ghi(kq.thongBao);
            return kq;
        }

        static bool GanMep(VatMo vm, float u, float v)
        {
            var l = vm.lop;
            switch (vm.phiaBanLe)
            {
                case 0: return u < l.u0 + 0.02f && v > l.v0 && v < l.v1;
                case 1: return u > l.u1 - 0.02f && v > l.v0 && v < l.v1;
                case 2: return v < l.v0 + 0.02f;
                case 3: return v > l.v1 - 0.02f;
            }
            return false;
        }

        public KetQua GhimChi(int chi)
        {
            if (loai != LoaiMau.Ech) return new KetQua(false, "Bệnh nhân đã được cố định trên bàn mổ.");
            if (chi < 0 || chi > 3) return new KetQua(false, "");
            if (chiGhim[chi]) return new KetQua(false, "Chi này đã ghim.");
            chiGhim[chi] = true; soChiGhim++;
            string[] ten = { "chi trước phải", "chi trước trái", "chi sau phải", "chi sau trái" };
            string tb = $"Ghim {ten[chi]} lên khay sáp ({soChiGhim}/4).";
            Ghi(tb);
            return new KetQua(true, tb);
        }

        public KetQua HuyTuy()
        {
            if (loai != LoaiMau.Ech) return new KetQua(false, "Không áp dụng cho người.");
            if (!huyNao)
            {
                huyNao = true;
                sinhLy.coTheTho = false;
                string tb = "Kim mũi mác đưa qua lỗ chẩm, xoay lên phía trước hủy não. Hành tủy bị hủy nên ếch ngừng thở bằng thềm miệng; tim vẫn đập vì tim tự động.";
                Ghi(tb); return new KetQua(true, tb);
            }
            if (!huyTuy)
            {
                huyTuy = true;
                string tb = "Đổi hướng kim xuống ống sống hủy tủy. Cơ hai chân duỗi rồi mềm hẳn; từ giờ không còn phản xạ tủy.";
                for (int k = 0; k < 4; k++) chiCo[k] = 1f;
                Ghi(tb); return new KetQua(true, tb);
            }
            return new KetQua(false, "Não và tủy đã hủy.");
        }

        // ---------------------------------------------------------------- luật trên cơ quan, mạch, thần kinh

        public KetQua DungTrenCoQuan(DungCuMo dc, CoQuanMo cq, ThuocMo thuoc = ThuocMo.Ringer)
        {
            if (cq == null) return new KetQua(false, "");
            if (cq.catBo) return new KetQua(false, $"{cq.ten} đã cắt bỏ.");
            string ly = LyDoChuaLo(cq);
            if (ly != null && !cq.tachRoi) return new KetQua(false, ly);
            KetQua kq;
            switch (dc)
            {
                case DungCuMo.KepPanh:
                    if (!cq.coTheVen) return new KetQua(false, $"{cq.ten} dính chặt (cố định bởi mạch, dây chằng), không vén được.");
                    cq.ven = !cq.ven; phienBanCoQuan++;
                    kq = new KetQua(true, cq.ven ? $"Vén {cq.ten.ToLowerInvariant()} sang bên; lộ các cấu trúc phía sau." : $"Trả {cq.ten.ToLowerInvariant()} về vị trí.");
                    break;
                case DungCuMo.DaoMo:
                    if (cq.rach) return new KetQua(false, $"{cq.ten} đã có vết rạch.");
                    cq.rach = true; phienBanCoQuan++;
                    kq = RachCoQuan(cq);
                    break;
                case DungCuMo.Keo:
                    kq = CatBoCoQuan(cq);
                    break;
                case DungCuMo.OngTiem:
                    kq = HutDich(cq);
                    break;
                case DungCuMo.DienCuc:
                    kq = KichThichCoQuan(cq);
                    break;
                case DungCuMo.OngThoi:
                    if (loai != LoaiMau.Ech || cq.chuyenDong != ChuyenDong.Phoi) return new KetQua(false, "Ống thổi dùng thổi phồng phổi ếch qua khe thanh quản (đặt vào phổi hoặc vùng họng).");
                    kq = ThoiPhoi();
                    break;
                case DungCuMo.OngNhoGiot:
                    kq = NhoThuoc(thuoc, cq);
                    break;
                case DungCuMo.BongGac:
                    kq = ThamMau();
                    break;
                case DungCuMo.KepCamMau:
                    kq = KepCuongCoQuan(cq);
                    break;
                case DungCuMo.Banh:
                    return new KetQua(false, "Banh đặt ở mép vết mổ hoặc hai nửa xương ức, không đặt lên cơ quan.");
                default:
                    return new KetQua(false, "Dụng cụ này không dùng trên cơ quan.");
            }
            if (kq.ok) Ghi(kq.thongBao);
            return kq;
        }

        KetQua RachCoQuan(CoQuanMo cq)
        {
            var sb = new System.Text.StringBuilder($"Rạch {cq.ten.ToLowerInvariant()}.");
            if (cq.luuLuongRach > 0f)
            {
                var m = new MachMau { id = "rach_" + cq.id, ten = "Nhu mô " + cq.ten.ToLowerInvariant(), dongMach = false, banKinhMm = 1f, luuLuongCat = cq.luuLuongRach,
                    dut = true, diemDut = cq.tam, coQuanNuoi = cq.id, thoiGianDong = loai == LoaiMau.Ech ? 90f : -1f };
                mach.Add(m);
                sb.Append(" Nhu mô nhiều mạch nhỏ nên rỉ máu liên tục; ép gạc hoặc kẹp để cầm.");
            }
            var d = DichCoThe.Lay(cq.dich);
            if (d != null && cq.theTichDich > 0f && cq.chuyenDong != ChuyenDong.Tim)
            {
                float tran = cq.theTichDich * 0.6f;
                cq.theTichDich -= tran;
                if (d.id != "khi") { dichTran += tran; mauDichTran = Color.Lerp(mauDichTran.a < 0.01f ? d.mau : mauDichTran, d.mau, 0.6f); }
                sb.Append(d.id == "khi" ? " Khí rò ra theo nhịp thở, phổi phồng kém đi." : $" {d.ten} tràn ra: {d.mauSac}, pH {d.pH}.");
                if (d.id == "khi") sinhLy.phoiRo = Mathf.Clamp01(sinhLy.phoiRo + 0.5f);
                if (d.id == "dich_vi" || d.id == "nhu_tran") sb.Append(" Dịch tiêu hóa tràn vào khoang bụng (ngoài đời gây viêm phúc mạc).");
            }
            if (cq.chuyenDong == ChuyenDong.Tim)
            {
                var m = new MachMau { id = "rach_tim", ten = "Vết rạch thành tim", dongMach = true, banKinhMm = 3f, luuLuongCat = loai == LoaiMau.Ech ? 0.03f : 40f, dut = true, diemDut = cq.tam, coQuanNuoi = "tim" };
                mach.Add(m);
                sb.Append(" Máu phụt theo mỗi nhát bóp của tâm thất.");
            }
            return new KetQua(true, sb.ToString());
        }

        KetQua CatBoCoQuan(CoQuanMo cq)
        {
            if (!cq.coTheCatBo && !(cq.id == "tim" && loai == LoaiMau.Ech))
            {
                if (cq.id == "tim") return new KetQua(false, "Không cắt rời tim người trong mô phỏng này: cần tuần hoàn ngoài cơ thể, và bệnh nhân sẽ tử vong.");
                return new KetQua(false, $"{cq.ten} là cơ quan sống còn hoặc nằm sau phúc mạc, không cắt bỏ trong bài này.");
            }
            var sb = new System.Text.StringBuilder();
            int dut = 0, kep = 0;
            foreach (var m in mach)
            {
                if (m.coQuanNuoi != cq.id || m.id.StartsWith("rach_")) continue;
                if (m.kep) { kep++; continue; }
                if (!m.dut) { m.dut = true; m.diemDut = m.duong != null ? m.duong[0] : cq.tam; m.tuoiDut = 0f; dut++; }
            }
            if (cq.id == "tim")
            {
                cq.tachRoi = true; cq.catBo = false; phienBanCoQuan++;
                sinhLy.timTachRoi = true;
                sb.Append("Cắt các cuống mạch, nhấc tim ếch ra đĩa. Tim tách rời vẫn tự đập nhờ nút xoang; nhỏ Ringer để giữ ẩm thì đập được lâu.");
            }
            else
            {
                cq.catBo = true; phienBanCoQuan++;
                sb.Append($"Cắt bỏ {cq.ten.ToLowerInvariant()}.");
            }
            if (dut > 0) sb.Append($" Cắt khi chưa kẹp: {dut} mạch cuống đứt, máu chảy.");
            if (kep > 0) sb.Append($" {kep} mạch cuống đã kẹp trước nên không chảy máu.");
            var d = DichCoThe.Lay(cq.dich);
            if (d != null && d.id != "khi" && cq.id != "tim" && cq.theTichDich > 0) { dichTran += cq.theTichDich * 0.3f; mauDichTran = d.mau; }
            return new KetQua(true, sb.ToString());
        }

        KetQua KepCuongCoQuan(CoQuanMo cq)
        {
            int n = 0;
            foreach (var m in mach)
                if (m.coQuanNuoi == cq.id && !m.kep) { m.kep = true; n++; }
            if (n == 0) return new KetQua(false, $"{cq.ten} không có cuống mạch nào chưa kẹp.");
            return new KetQua(true, $"Kẹp {n} cuống mạch của {cq.ten.ToLowerInvariant()}: phần cắt sau chỗ kẹp sẽ không chảy máu.");
        }

        KetQua HutDich(CoQuanMo cq)
        {
            var d = DichCoThe.Lay(cq.dich);
            if (d == null || cq.theTichDich <= 0.0001f) return new KetQua(false, $"Trong {cq.ten.ToLowerInvariant()} không còn dịch để hút.");
            if (cq.chuyenDong == ChuyenDong.Tim)
            {
                float lay = loai == LoaiMau.Ech ? 0.05f : 10f;
                if (sinhLy.theTichMau < lay * 2f) return new KetQua(false, "Tim gần như rỗng máu.");
                sinhLy.theTichMau -= lay;
                dichVuaHut = d.id;
                return new KetQua(true, $"Hút {lay:0.##} mL {d.ten.ToLowerInvariant()} từ buồng tim: {d.mauSac}, pH {d.pH}. {d.ghiChu}");
            }
            float v = Mathf.Min(cq.theTichDich, loai == LoaiMau.Ech ? 0.1f : 20f);
            cq.theTichDich -= v;
            dichVuaHut = d.id;
            if (d.id == "khi") sinhLy.phoiRo = Mathf.Clamp01(sinhLy.phoiRo + 0.25f);
            return new KetQua(true, $"Hút {v:0.##} mL {d.ten.ToLowerInvariant()}: {d.mauSac}, pH {d.pH}. {d.ghiChu}");
        }

        public KetQua KichThichCoQuan(CoQuanMo cq)
        {
            switch (cq.chuyenDong)
            {
                case ChuyenDong.Tim: return sinhLy.KichThichTim();
                case ChuyenDong.NhuDong:
                    cq.coGiat = 1f;
                    return new KetQua(true, $"Cơ trơn {cq.ten.ToLowerInvariant()} co thắt tại chỗ, chậm và kéo dài (khác co giật nhanh của cơ vân).");
                case ChuyenDong.CoHoanh:
                    sinhLy.coHoanhCo = 1f;
                    return new KetQua(true, "Cơ hoành co dẹt xuống (kích thích trực tiếp cơ vân).");
                case ChuyenDong.Phoi:
                    return new KetQua(false, "Phổi không có cơ để co khi kích thích; phổi phồng xẹp là nhờ thay đổi áp suất.");
                default:
                    return new KetQua(false, $"{cq.ten} không phải mô kích thích được (không co, không dẫn truyền).");
            }
        }

        KetQua ThoiPhoi()
        {
            if (sinhLy.coTheTho) return new KetQua(false, "Ếch còn tự thở, không cần thổi.");
            sinhLy.phoiThoiPhong = 1f;
            return new KetQua(true, sinhLy.phoiRo > 0.3f
                ? "Thổi qua khe thanh quản: phổi rách nên chỉ phồng một phần, khí xì ra chỗ rạch."
                : "Thổi qua khe thanh quản: hai túi phổi phồng căng, mỏng trong, thấy mạng mao mạch; khí thoát dần qua thanh quản nên phổi xẹp lại sau ít giây.");
        }

        public KetQua NhoThuoc(ThuocMo t, CoQuanMo cq = null)
        {
            var s = sinhLy;
            bool nguoi = loai == LoaiMau.Nguoi;
            switch (t)
            {
                case ThuocMo.Ringer:
                    if (nguoi)
                    {
                        // Dịch tinh thể: khoảng 1/3 lượng truyền ở lại lòng mạch
                        float truyen = 500f, giu = truyen / 3f;
                        s.theTichMau = Mathf.Min(s.theTichMau0 * 1.05f, s.theTichMau + giu);
                        return new KetQua(true, $"Truyền nhanh 500 mL Ringer lactat. Dịch tinh thể thoát dần ra gian bào, chỉ khoảng {giu:0} mL ở lại lòng mạch.");
                    }
                    s.doAm = 1f; s.ach *= 0.4f; s.adr *= 0.4f; s.nhietDo = Mathf.MoveTowards(s.nhietDo, 22f, 4f);
                    return new KetQua(true, "Nhỏ Ringer ếch (NaCl 0,65 %, đẳng trương với dịch cơ thể lưỡng cư): mô ẩm lại, rửa bớt thuốc đã nhỏ.");
                case ThuocMo.RingerLanh:
                    if (nguoi) return new KetQua(false, "Không truyền dịch lạnh cho bệnh nhân (gây hạ thân nhiệt, rối loạn nhịp).");
                    s.doAm = 1f; s.nhietDo = Mathf.Max(6f, s.nhietDo - 8f);
                    return new KetQua(true, $"Nhỏ Ringer lạnh: mô tim còn khoảng {s.nhietDo:0} °C, nút xoang phát nhịp chậm lại (ếch là động vật biến nhiệt).");
                case ThuocMo.Adrenalin:
                    s.adr = Mathf.Min(2f, s.adr + 1f);
                    return new KetQua(true, nguoi ? "Tiêm tĩnh mạch adrenalin liều nhỏ: tim nhanh, co mạnh, huyết áp tăng; nhu động ruột giảm."
                        : "Nhỏ adrenalin: nhịp tim nhanh và co bóp mạnh hơn (thụ thể β1), nhu động ruột giảm.");
                case ThuocMo.Acetylcholin:
                    s.ach = Mathf.Min(2f, s.ach + 0.8f);
                    return new KetQua(true, (s.atropin > 0.3f ? "Đã có atropin chặn thụ thể muscarin nên acetylcholin gần như không tác dụng lên tim. " : "")
                        + (nguoi ? "Acetylcholin: tim chậm, nhu động ruột tăng." : "Nhỏ acetylcholin (chất trung gian của dây X): tim chậm và yếu; liều cao làm tim ngừng ở thì tâm trương. Nhu động ruột tăng."));
                case ThuocMo.Atropin:
                    s.atropin = Mathf.Min(2f, s.atropin + 1f);
                    return new KetQua(true, "Atropin chặn thụ thể muscarin: bỏ tác dụng của acetylcholin và của kích thích dây X lên tim.");
                case ThuocMo.HCl:
                    if (nguoi) return new KetQua(false, "Không dùng acid trên người.");
                    return new KetQua(false, "Nhỏ HCl 0,3 % lên da chân ếch để thử phản xạ tủy (đặt ống nhỏ giọt lên chân).");
            }
            return new KetQua(false, "");
        }

        public KetQua NhoAcidChan(int chi)
        {
            if (loai != LoaiMau.Ech) return new KetQua(false, "");
            bool sau = chi >= 2;
            var tk = TK(chi == 2 ? "toa_phai" : chi == 3 ? "toa_trai" : null);
            string ten = chi == 2 ? "chân sau phải" : chi == 3 ? "chân sau trái" : chi == 0 ? "chân trước phải" : "chân trước trái";
            if (!sau) return new KetQua(true, $"Nhỏ HCl 0,3 % lên {ten}: " + (huyTuy ? "không co (tủy đã hủy)." : "chân trước co nhẹ (phản xạ tủy).") + KetQuaGhi(ten));
            if (huyTuy) return new KetQua(true, $"Nhỏ HCl 0,3 % lên {ten}: chân không co. Tủy sống (trung ương của phản xạ) đã hủy nên cung phản xạ bị đứt.");
            if (tk != null && !tk.LienToanBo) return new KetQua(true, $"Nhỏ HCl 0,3 % lên {ten}: chân không co. Dây thần kinh tọa bên này đã bị cắt, xung cảm giác và vận động không qua được.");
            if (chiGhim[chi])
            {
                chiCo[chi] = 1f;
                return new KetQua(true, $"Nhỏ HCl 0,3 % lên {ten}: chân co giật kéo ghim. Phản xạ tủy còn (não đã hủy không ảnh hưởng). Rửa acid bằng nước sau khi thử.");
            }
            chiCo[chi] = 1f;
            return new KetQua(true, $"Nhỏ HCl 0,3 % lên {ten}: chân co rút lại. Phản xạ tủy còn nguyên dù não đã hủy.");
        }

        static string KetQuaGhi(string _) => "";

        public KetQua DungTrenMach(DungCuMo dc, MachMau m)
        {
            if (m == null) return new KetQua(false, "");
            if (m.duong != null && m.lopId == "" && !DiemLo(m.duong[m.duong.Length / 2]) && !(m.dut && DiemLo(m.diemDut)))
                return new KetQua(false, $"{m.ten} còn bị các lớp phía trên che.");
            KetQua kq;
            switch (dc)
            {
                case DungCuMo.KepCamMau:
                    if (m.kep) return new KetQua(false, $"{m.ten} đã kẹp.");
                    m.kep = true;
                    kq = new KetQua(true, m.dut ? $"Kẹp {m.ten.ToLowerInvariant()}: máu ngừng chảy." : $"Kẹp {m.ten.ToLowerInvariant()} trước; cắt phía sau kẹp sẽ không chảy máu.");
                    break;
                case DungCuMo.Keo:
                case DungCuMo.DaoMo:
                    if (m.dut) return new KetQua(false, $"{m.ten} đã đứt.");
                    m.dut = true; m.tuoiDut = 0f; m.diemDut = m.duong != null ? m.duong[m.duong.Length / 2] : Vector3.zero;
                    kq = new KetQua(true, m.kep ? $"Cắt {m.ten.ToLowerInvariant()} phía sau kẹp: không chảy máu." :
                        $"Cắt {m.ten.ToLowerInvariant()}: " + (m.dongMach ? "máu đỏ tươi phụt thành tia theo nhịp tim." : "máu đỏ sẫm trào ra đều."));
                    break;
                case DungCuMo.OngTiem:
                    {
                        var d = DichCoThe.Lay(m.dongMach ? (loai == LoaiMau.Ech ? "mau_pha" : "mau_dm") : "mau_tm");
                        float lay = loai == LoaiMau.Ech ? 0.05f : 5f;
                        if (sinhLy.theTichMau < lay * 3f) return new KetQua(false, "Lòng mạch xẹp, không hút được.");
                        sinhLy.theTichMau -= lay;
                        dichVuaHut = d.id;
                        kq = new KetQua(true, $"Hút {lay:0.##} mL máu từ {m.ten.ToLowerInvariant()}: {d.mauSac}, pH {d.pH}. {d.ghiChu}");
                        break;
                    }
                case DungCuMo.BongGac:
                    kq = ThamMau();
                    if (m.dut && !m.kep && m.banKinhMm <= 1.6f) { m.epGac += 30f; kq = new KetQua(true, kq.thongBao + $" Ép gạc lên {m.ten.ToLowerInvariant()} giúp cục máu đông hình thành nhanh hơn."); }
                    break;
                default:
                    return new KetQua(false, "Dụng cụ này không dùng trên mạch máu.");
            }
            Ghi(kq.thongBao);
            return kq;
        }

        public KetQua DungTrenThanKinh(DungCuMo dc, DayThanKinh tk, float t)
        {
            if (tk == null) return new KetQua(false, "");
            var p = DiemTren(tk.duong, t);
            if (!DiemLo(p)) return new KetQua(false, $"{tk.ten} còn bị các lớp phía trên che.");
            if (!string.IsNullOrEmpty(tk.coQuanChe))
            {
                var che = CQ(tk.coQuanChe);
                if (che != null && !che.ven && !che.catBo) return new KetQua(false, $"{tk.ten} nằm sát thành lưng, sau {che.ten.ToLowerInvariant()}; vén {che.ten.ToLowerInvariant()} sang bên trước.");
            }
            KetQua kq;
            switch (dc)
            {
                case DungCuMo.DienCuc:
                    kq = KichThichThanKinh(tk, t);
                    break;
                case DungCuMo.Keo:
                case DungCuMo.DaoMo:
                    tk.diemCat.Add(t);
                    kq = new KetQua(true, $"Cắt {tk.ten.ToLowerInvariant()}. Kích thích đầu ngoại vi (phía cơ quan đích) vẫn gây đáp ứng; kích thích đầu trung ương thì không.");
                    break;
                default:
                    return new KetQua(false, "Dùng điện cực để kích thích, kéo để cắt dây thần kinh.");
            }
            Ghi(kq.thongBao);
            return kq;
        }

        KetQua KichThichThanKinh(DayThanKinh tk, float t)
        {
            if (!tk.LienTu(t)) return new KetQua(true, $"Kích thích {tk.ten.ToLowerInvariant()} ở đoạn phía trung ương của chỗ cắt: không có đáp ứng vì xung không truyền qua chỗ đứt.");
            switch (tk.dich)
            {
                case "tim":
                    if (sinhLy.atropin > 0.3f) return new KetQua(true, "Kích thích dây X: tim không chậm lại vì atropin đã chặn thụ thể muscarin.");
                    sinhLy.kichThichX = 8f;
                    return new KetQua(true, loai == LoaiMau.Ech
                        ? "Kích thích dây X: đầu tận cùng tiết acetylcholin, tim ếch chậm dần rồi ngừng ở thì tâm trương; kích thích kéo dài thì tim 'thoát' và đập lại chậm."
                        : "Kích thích dây X: nhịp tim giảm rõ (acetylcholin tác động nút xoang và nút nhĩ thất).");
                case "chi_sau_phai":
                case "chi_sau_trai":
                    {
                        int chi = tk.dich == "chi_sau_phai" ? 2 : 3;
                        chiCo[chi] = 1f;
                        return new KetQua(true, $"Kích thích {tk.ten.ToLowerInvariant()}: cơ chân sau {(chi == 2 ? "phải" : "trái")} co giật (dây tọa vận động cơ đùi, cẳng chân). Đáp ứng vẫn có dù tủy đã hủy vì kích thích đặt trực tiếp lên sợi trục.");
                    }
                case "co_hoanh_phai":
                case "co_hoanh_trai":
                    sinhLy.coHoanhCo = 1f;
                    return new KetQua(true, $"Kích thích {tk.ten.ToLowerInvariant()}: nửa cơ hoành cùng bên co giật xuống.");
            }
            return new KetQua(false, "");
        }

        public KetQua KichThichChi(int chi)
        {
            chiCo[chi] = 1f;
            string tb = "Đặt điện cực lên cơ chân: cơ vân co giật ngay (kích thích trực tiếp cơ, không cần thần kinh).";
            Ghi(tb);
            return new KetQua(true, tb);
        }

        public KetQua ThamMau()
        {
            float truoc = mauDong + dichTran;
            float hut = loai == LoaiMau.Ech ? 0.25f : 120f;
            float a = Mathf.Min(mauDong, hut);
            mauDong -= a;
            float b = Mathf.Min(dichTran, hut - a);
            dichTran -= b;
            if (truoc <= 0.0001f) return new KetQua(true, "Thấm gạc: vết mổ khô, không có máu đọng.");
            return new KetQua(true, $"Thấm gạc: lấy đi {(a + b):0.##} mL máu và dịch đọng.");
        }

        public static Vector3 DiemTren(Vector3[] duong, float t)
        {
            if (duong == null || duong.Length == 0) return Vector3.zero;
            if (duong.Length == 1) return duong[0];
            float x = Mathf.Clamp01(t) * (duong.Length - 1);
            int k = Mathf.Min((int)x, duong.Length - 2);
            return Vector3.Lerp(duong[k], duong[k + 1], x - k);
        }

        // ---------------------------------------------------------------- bước thời gian

        public void Buoc(float dt)
        {
            if (dt <= 0f) return;
            thoiGian += dt;
            var s = sinhLy;
            s.Buoc(dt, e => Ghi(e));

            // Chảy máu: lưu lượng tỉ lệ huyết áp; động mạch chảy theo nhịp, tĩnh mạch đều; mạch nhỏ tự đông
            float apTiLe = s.loai == LoaiMau.Nguoi ? s.huyetAp / 85f : s.huyetAp / 30f;
            float mat = 0f;
            foreach (var m in mach)
            {
                if (!m.dut || m.kep || m.dongCuc) continue;
                m.tuoiDut += dt + m.epGac * dt * 0.1f;
                m.epGac = Mathf.MoveTowards(m.epGac, 0f, dt * 10f);
                if (m.thoiGianDong > 0f && m.tuoiDut >= m.thoiGianDong) { m.dongCuc = true; continue; }
                float k = m.dongMach ? apTiLe * (0.5f + 0.9f * s.thatCo) : Mathf.Max(0.25f, apTiLe * 0.6f + 0.2f);
                if (s.timTachRoi && m.coQuanNuoi == "tim") k = 0.05f;
                if (m.thoiGianDong > 0f) k *= 1f - m.tuoiDut / m.thoiGianDong;
                mat += m.luuLuongCat * Mathf.Max(0f, k) * dt;
            }
            mat = Mathf.Min(mat, Mathf.Max(0f, s.theTichMau));
            s.theTichMau -= mat;
            mauDong += mat;

            // Vạt chưa ghim, không banh tự rơi về
            foreach (var vm in vat)
            {
                if (vm.oVat.Count == 0) continue;
                var l = vm.lop;
                if (l.o[vm.oVat[0]] != TrangThaiO.Lat) continue;
                vm.tuoi += dt;
                if (!vm.ghim && !vm.banh && vm.tuoi > l.thoiGianRoi)
                {
                    foreach (var c in vm.oVat) { l.o[c] = TrangThaiO.Lien; l.vatCuaO[c] = -1; }
                    l.phienBan++;
                    vm.oVat.Clear();
                    Ghi($"Vạt {l.ten.ToLowerInvariant()} chưa ghim nên đàn hồi rơi về chỗ cũ.");
                }
            }

            for (int k = 0; k < 4; k++) chiCo[k] = Mathf.MoveTowards(chiCo[k], 0f, dt * (huyTuy && k >= 2 ? 0.8f : 2.2f));
            foreach (var cq in coQuan) cq.coGiat = Mathf.MoveTowards(cq.coGiat, 0f, dt * 0.35f);
        }
    }
}
