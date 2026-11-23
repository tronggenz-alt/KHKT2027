using System.Collections.Generic;
using UnityEngine;
using static Khkt.CoChe.JsonNho;

namespace Khkt.CoChe
{
    /// <summary>Một mũi tên cong (như co_che.py): tu/den = cap_e | lien_ket | nguyen_tu + danh sách atom map; kieu day_du | nua_dau | vong.</summary>
    public class MuiTen
    {
        public string TuLoai, DenLoai, Kieu;
        public int[] Tu, Den;
        public int SoE => Kieu == "nua_dau" ? 1 : 2;

        public static MuiTen TuJson(object o)
        {
            var d = D(o);
            var tu = D(Lay(d, "tu"));
            var den = D(Lay(d, "den"));
            return new MuiTen { TuLoai = S(tu, "loai"), DenLoai = S(den, "loai"), Kieu = S(d, "kieu", "day_du"), Tu = Map(Lay(tu, "nguyen_tu")), Den = Map(Lay(den, "nguyen_tu")) };
        }

        static int[] Map(object o)
        {
            if (o is List<object> l) { var a = new int[l.Count]; for (int i = 0; i < l.Count; i++) a[i] = I(l[i]); return a; }
            return new[] { I(o) };
        }
    }

    public struct LienKet3D { public string A, B; public int Bac; }

    public class TrangThai3D
    {
        public string Ten;
        public Dictionary<string, Vector3> ToaDo = new Dictionary<string, Vector3>();
        public List<LienKet3D> LienKet = new List<LienKet3D>();
        public Dictionary<string, int> GocTuDo = new Dictionary<string, int>();
        public Dictionary<string, int> DienTich = new Dictionary<string, int>();
    }

    public class Buoc3D
    {
        public int Tu, Den;
        public string Ten, GhiChu;
        public bool CoTS, BuocCham, ThuanNghich;
        public List<MuiTen> MuiTen = new List<MuiTen>();
    }

    public class NhanhSai3D
    {
        public int TuTrangThai;
        public string Ten, LyDo, KieuChan;
        public Dictionary<string, string> NguyenTuThem = new Dictionary<string, string>();
        public Dictionary<string, Vector3> ToaDo = new Dictionary<string, Vector3>();
        public List<MuiTen> MuiTen = new List<MuiTen>();
        public Vector3 TamChan, PhapTuyen;
        public float BanKinhChan;
    }

    /// <summary>.3d.json (HOP_DONG_APP.md mục 1). Toạ độ Å, hệ phải → Unity hệ trái: đổi dấu z.</summary>
    public class KichBan3D
    {
        public string Ten, GiaiThich, Loai, DieuKien;
        public Dictionary<string, string> NguyenTu = new Dictionary<string, string>();
        public List<TrangThai3D> TrangThai = new List<TrangThai3D>();
        public List<Buoc3D> Buoc = new List<Buoc3D>();
        public List<NhanhSai3D> NhanhSai = new List<NhanhSai3D>();

        static Vector3 V(object o) { var l = L(o); return l.Count < 3 ? Vector3.zero : new Vector3(F(l[0]), F(l[1]), -F(l[2])); }

        public static KichBan3D TuJson(Dictionary<string, object> d)
        {
            var kb = new KichBan3D { Ten = S(d, "ten"), GiaiThich = S(d, "giai_thich"), Loai = S(d, "loai"), DieuKien = S(d, "dieu_kien") };
            foreach (var kv in D(Lay(d, "nguyen_tu"))) kb.NguyenTu[kv.Key] = S(D(kv.Value), "e", "C");
            foreach (var o in L(Lay(d, "trang_thai")))
            {
                var s = D(o);
                var tt = new TrangThai3D { Ten = S(s, "ten") };
                foreach (var kv in D(Lay(s, "toa_do"))) tt.ToaDo[kv.Key] = V(kv.Value);
                foreach (var lo in L(Lay(s, "lien_ket")))
                {
                    var l = L(lo);
                    if (l.Count >= 3) tt.LienKet.Add(new LienKet3D { A = l[0] as string, B = l[1] as string, Bac = I(l[2]) });
                }
                foreach (var kv in D(Lay(s, "goc_tu_do"))) tt.GocTuDo[kv.Key] = I(kv.Value);
                foreach (var kv in D(Lay(s, "dien_tich"))) tt.DienTich[kv.Key] = I(kv.Value);
                kb.TrangThai.Add(tt);
            }
            foreach (var o in L(Lay(d, "buoc")))
            {
                var b = D(o);
                var bu = new Buoc3D { Tu = I(Lay(b, "tu")), Den = I(Lay(b, "den")), Ten = S(b, "ten"), GhiChu = S(b, "ghi_chu"),
                                      CoTS = B(b, "trang_thai_chuyen_tiep"), BuocCham = B(b, "buoc_cham"), ThuanNghich = B(b, "thuan_nghich") };
                foreach (var m in L(Lay(b, "mui_ten"))) bu.MuiTen.Add(global::Khkt.CoChe.MuiTen.TuJson(m));
                kb.Buoc.Add(bu);
            }
            foreach (var o in L(Lay(d, "nhanh_sai")))
            {
                var n = D(o);
                var mc = D(Lay(n, "man_chan"));
                var ns = new NhanhSai3D { TuTrangThai = I(Lay(n, "tu_trang_thai")), Ten = S(n, "ten"), LyDo = S(n, "ly_do"), KieuChan = S(n, "kieu_chan"),
                                          TamChan = V(Lay(mc, "tam")), PhapTuyen = V(Lay(mc, "phap_tuyen")), BanKinhChan = F(Lay(mc, "ban_kinh")) };
                foreach (var kv in D(Lay(n, "nguyen_tu_them"))) ns.NguyenTuThem[kv.Key] = S(D(kv.Value), "e", "C");
                foreach (var kv in D(Lay(n, "toa_do"))) ns.ToaDo[kv.Key] = V(kv.Value);
                foreach (var m in L(Lay(n, "mui_ten"))) ns.MuiTen.Add(global::Khkt.CoChe.MuiTen.TuJson(m));
                kb.NhanhSai.Add(ns);
            }
            return kb;
        }
    }

    /// <summary>.giay.json (HOP_DONG_APP.md mục 2): toạ độ 2D, độ dài liên kết = 1, y hướng lên.</summary>
    public class SoDoGiayDuLieu
    {
        public float X0, X1, Y0, Y1;
        public class Nut { public string Id; public Vector2 P; public string Nhan = ""; }
        public class Net { public string A, B; public string Kieu = "1"; public Vector2? Phia; }
        public class MuiTenCong { public Vector2 Tu, Qua, Den; public string Kieu; public bool Gach, Cheo; }
        /// <summary>(18/11) Cặp electron tự do: tâm P, hướng ra GocDo (độ, 0 = phải, 90 = lên).</summary>
        public class CapE { public Vector2 P; public float GocDo; }
        /// <summary>(18/11) Khối giai đoạn: tiêu đề nghiêng gạch lượn sóng tại Xy, khung nền, các bước 3D thuộc khối; Nen = "sai" | "chon" | null.</summary>
        public class GiaiDoan { public string TieuDe, Nen; public Vector2 Xy; public float X0, X1, Y0, Y1; public List<int> Buoc = new List<int>(); public int NhanhSai = -1; }
        public class MuiTenPU { public float X1, X2, Y; public string Tren, Duoi; public bool ThuanNghich; }
        public class Chu { public Vector2 P; public string T; }
        public Dictionary<string, Nut> NutDs = new Dictionary<string, Nut>();
        public List<Net> NetDs = new List<Net>();
        public List<Vector2> Goc = new List<Vector2>();
        public List<MuiTenCong> Mt = new List<MuiTenCong>();
        public List<MuiTenPU> Pu = new List<MuiTenPU>();
        public List<Chu> Ten = new List<Chu>(), Dt = new List<Chu>(), ChuKhac = new List<Chu>();
        // (18/11) giấy kiểu đáp án bài thi – HOP_DONG_APP.md mục 2b
        public List<CapE> CapEDs = new List<CapE>();
        public List<Chu> DtVong = new List<Chu>();
        public List<GiaiDoan> GiaiDoanDs = new List<GiaiDoan>();
        public string Phong = "", KieuCongThuc = "";
        public bool CoChan => Phong == "co_chan";

        static Vector2 V2(object o) { var l = L(o); return l.Count < 2 ? Vector2.zero : new Vector2(F(l[0]), F(l[1])); }

        public static SoDoGiayDuLieu TuJson(Dictionary<string, object> d)
        {
            var sd = new SoDoGiayDuLieu();
            var k = L(Lay(d, "khung"));
            if (k.Count == 4) { sd.X0 = F(k[0]); sd.X1 = F(k[1]); sd.Y0 = F(k[2]); sd.Y1 = F(k[3]); }
            foreach (var kv in D(Lay(d, "nt")))
            {
                var l = L(kv.Value);
                sd.NutDs[kv.Key] = new Nut { Id = kv.Key, P = V2(l), Nhan = l.Count > 2 ? l[2] as string ?? "" : "" };
            }
            foreach (var o in L(Lay(d, "lk")))
            {
                var l = L(o);
                if (l.Count < 2) continue;
                var n = new Net { A = l[0] as string, B = l[1] as string };
                if (l.Count > 2) n.Kieu = l[2] is double x ? ((int)x).ToString() : (l[2] as string ?? "1");
                if (l.Count > 3 && l[3] is List<object>) n.Phia = V2(l[3]);
                sd.NetDs.Add(n);
            }
            foreach (var o in L(Lay(d, "goc"))) sd.Goc.Add(V2(o));
            foreach (var o in L(Lay(d, "mt")))
            {
                var m = D(o);
                sd.Mt.Add(new MuiTenCong { Tu = V2(Lay(m, "tu")), Qua = V2(Lay(m, "qua")), Den = V2(Lay(m, "den")), Kieu = S(m, "kieu", "nua_dau"), Gach = B(m, "gach"), Cheo = B(m, "cheo") });
            }
            foreach (var o in L(Lay(d, "pu")))
            {
                var p = D(o);
                sd.Pu.Add(new MuiTenPU { X1 = F(Lay(p, "x1")), X2 = F(Lay(p, "x2")), Y = F(Lay(p, "y")), Tren = S(p, "tren", null), Duoi = S(p, "duoi", null), ThuanNghich = B(p, "thuan_nghich") });
            }
            void Doc(string khoa, List<Chu> ds)
            {
                foreach (var o in L(Lay(d, khoa))) { var l = L(o); if (l.Count >= 3) ds.Add(new Chu { P = new Vector2(F(l[0]), F(l[1])), T = l[2] as string }); }
            }
            Doc("ten", sd.Ten);
            Doc("dt", sd.Dt);
            Doc("chu", sd.ChuKhac);
            Doc("dt_vong", sd.DtVong);
            foreach (var o in L(Lay(d, "cap_e")))
            {
                var l = L(o);
                if (l.Count >= 3) sd.CapEDs.Add(new CapE { P = new Vector2(F(l[0]), F(l[1])), GocDo = F(l[2]) });
            }
            foreach (var o in L(Lay(d, "giai_doan")))
            {
                var g = D(o);
                var gd = new GiaiDoan { TieuDe = S(g, "tieu_de", ""), Nen = S(g, "nen", null), Xy = V2(Lay(g, "xy")) };
                var kh = L(Lay(g, "khung"));
                if (kh.Count == 4) { gd.X0 = F(kh[0]); gd.X1 = F(kh[1]); gd.Y0 = F(kh[2]); gd.Y1 = F(kh[3]); }
                foreach (var b in L(Lay(g, "buoc"))) gd.Buoc.Add(I(b));
                if (Lay(g, "nhanh_sai") != null) gd.NhanhSai = I(Lay(g, "nhanh_sai"));
                sd.GiaiDoanDs.Add(gd);
            }
            sd.Phong = S(d, "phong", "");
            sd.KieuCongThuc = S(d, "kieu_cong_thuc", "");
            if (sd.DtVong.Count > 0)
            {
                // có điện tích vòng tròn: bỏ ký tự điện tích cuối nhãn, bỏ "dt" kiểu cũ (HOP_DONG_APP.md mục 2b)
                foreach (var n in sd.NutDs.Values) n.Nhan = (n.Nhan ?? "").TrimEnd('⁺', '⁻', '²', '³');
                sd.Dt.Clear();
            }
            return sd;
        }
    }

    /// <summary>Một bài cơ chế đã giải gói: bài (đề, câu hỏi…) + kịch bản 3D + sơ đồ giấy.</summary>
    public class BaiCoChe
    {
        public string Ma, De, Ten;
        public int Lop;
        public List<string> MucTieu = new List<string>();
        public List<(string ten, string giaiThich)> CacBuoc = new List<(string, string)>();
        public List<(string hoi, List<string> luaChon, int dapAn, string giaiThich)> CauHoi = new List<(string, List<string>, int, string)>();
        public List<string> LoiHayGap = new List<string>();
        public KichBan3D KichBan;
        public SoDoGiayDuLieu SoDo;

        public static BaiCoChe TuJson(string json)
        {
            var d = D(JsonNho.Doc(json));
            var bai = D(Lay(d, "bai"));
            var kb = KichBan3D.TuJson(D(Lay(d, "kich_ban_3d")));
            var b = new BaiCoChe { Ma = S(d, "ma"), De = S(bai, "de"), Lop = I(Lay(bai, "lop")), KichBan = kb, Ten = kb.Ten,
                                   SoDo = SoDoGiayDuLieu.TuJson(D(Lay(d, "so_do_giay"))) };
            foreach (var o in L(Lay(bai, "muc_tieu"))) b.MucTieu.Add(o as string);
            foreach (var o in L(Lay(bai, "cac_buoc"))) { var x = D(o); b.CacBuoc.Add((S(x, "ten"), S(x, "giai_thich"))); }
            foreach (var o in L(Lay(bai, "loi_hay_gap"))) b.LoiHayGap.Add(o as string);
            foreach (var o in L(Lay(bai, "cau_hoi")))
            {
                var x = D(o);
                var lc = new List<string>();
                foreach (var c in L(Lay(x, "lua_chon"))) lc.Add(c as string);
                b.CauHoi.Add((S(x, "hoi"), lc, I(Lay(x, "dap_an")), S(x, "giai_thich")));
            }
            return b;
        }
    }
}
