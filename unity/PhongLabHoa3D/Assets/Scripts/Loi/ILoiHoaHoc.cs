using System;

namespace PhongThiNghiem.Loi
{
    public class SuKienHieuUng
    {
        public string Ten;
        public string DuLieuJson;
    }

    public class ThaoTacYeuCau
    {
        public string Lenh;
        public string ThamSoJson;
    }

    public interface ILoiHoaHoc : IDisposable
    {
        event Action<string> OnTrangThai;
        event Action<string> OnSoTay;
        event Action<SuKienHieuUng> OnHieuUng;

        bool DangMoPhong { get; }
        string MoPhongId { get; }

        void Tao(string id, string cheDo = "huong_dan");
        void ThaoTac(string lenh, string thamSoJson = null);
        void ThaoTacDongBo(string lenh, string thamSoJson = null);
        void BatDauMoPhong();
        void TamDungMoPhong();
        string TrangThai();
        string SoTay();
        void CapNhatLuongChinh();
    }
}
