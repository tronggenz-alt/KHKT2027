// Kiểu dữ liệu nhận từ trang web (JsonUtility). Tên trường = đúng khoá JSON do mo-phong.js phát ra.
// Unity KHÔNG tính hoá học: mọi con số (V, pH, E, m…) đến từ lõi JS; ở đây chỉ đọc để vẽ.
using System;

namespace PhongThiNghiem
{
    // ---------- giao thức
    [Serializable] public class LenhJS { public string lenh; public string du_lieu; }
    [Serializable] public class MoThiNghiemDL { public string id; public string ten; public string trang_thai; }
    [Serializable] public class GiaoDienDL { public string nen; public string nhan; public string the; public string chu; public bool toi; }
    [Serializable] public class CameraDL { public string lenh; }
    [Serializable] public class ChatLuongDL { public string q; }
    [Serializable] public class HieuUngDL { public string loai; public int so; public string id; }
    [Serializable] public class SuKienUnity { public string su_kien; public string lenh; public string tham_so; public string loai; public int so; public string thong_bao; }

    // ---------- dùng chung
    [Serializable] public class TTChung { public string id; public string che_do; }
    [Serializable] public class MauChiThi { public float[] rgba; public string mo_ta; public float ti_le; }

    // ---------- chuẩn độ
    [Serializable] public class AcidTT { public string cong_thuc; public string ten; }
    [Serializable]
    public class TTChuanDo
    {
        public string id; public float V; public float V_buret; public string khoa; public MauChiThi mau; public bool vet_mau;
        public float the_tich_dd; public AcidTT acid; public string ten_chi_thi;
    }

    // ---------- an toàn
    [Serializable] public class DiemNong { public string id; public int so; public string vi_tri; public bool da_tim; public bool la_viec_dung; }
    [Serializable] public class TTAnToan { public string id; public DiemNong[] diem_nong; public int so_tim; }

    // ---------- nhận biết ion
    [Serializable] public class MauDd { public string mo_ta; public float[] rgb; }
    [Serializable] public class KetTua { public string mau; public float[] rgb; public float luong; }
    [Serializable]
    public class OngTT
    {
        public int so; public MauDd mau_dd; public KetTua ket_tua; public bool co_khi; public bool co_ket_tua; public string quy; public string ten_ion;
    }
    [Serializable] public class NgonLuaTT { public string kim_loai; public float[] rgb; public string mau; }
    [Serializable] public class ThuocThuTT { public string id; public string ten; }
    [Serializable] public class TTNhanBiet { public string id; public string tab; public OngTT[] ong; public NgonLuaTT ngon_lua; public ThuocThuTT[] thuoc_thu; }

    // ---------- pin Galvani
    [Serializable] public class CapMau { public float[] trai; public float[] phai; }
    [Serializable]
    public class TTPin
    {
        public string id; public string trai; public string phai; public bool cau_muoi; public float E_pin; public string ben_cathode;
        public string chieu_electron; public float mon_anode; public float day_cathode; public CapMau mau_kim_loai; public CapMau mau_dd;
        public string muoi_trai; public string muoi_phai;
    }

    // ---------- điện phân
    [Serializable] public class KetQuaDP { public float m_cathode; public float m_anode_giam; public float V_khi_cathode; public float V_khi_anode; }
    [Serializable] public class KhiThoat { public bool cathode; public bool anode; }
    [Serializable] public class MauDdDP { public float[] rgb; public float dam; }
    [Serializable] public class DienCucDP { public string cathode; public string anode; }
    [Serializable]
    public class TTDienPhan
    {
        public string id; public string bai; public float I; public float t; public bool dang_chay; public KetQuaDP ket_qua; public KhiThoat khi_dang_thoat;
        public MauDdDP mau_dd; public MauChiThi chi_thi_cathode; public string quy_anode; public DienCucDP dien_cuc;
    }
}
