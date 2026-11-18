using System.Collections.Generic;
using Khkt.Demo;
using UnityEngine;
using UnityEngine.UI;

namespace Khkt.HoaLy
{
    /// <summary>
    /// Một trang mô phỏng trong lớp phủ Hóa lý. Lớp phủ dựng sẵn hai vùng: VungTrai (hình + đồ thị) và VungDieuKhien (cột phải);
    /// trang tự dựng nội dung vào đó. CapNhat(dt) chạy mỗi khung khi trang đang hiện (không cấp phát); số liệu chữ cập nhật ~5 lần/giây.
    /// </summary>
    public abstract class TrangHoaLy
    {
        public abstract string Ma { get; }
        public abstract string Ten { get; }
        public abstract string MoTa { get; }
        /// <summary>Tên tệp câu hỏi trong Resources/HoaLy.</summary>
        public abstract string TepCauHoi { get; }

        public RectTransform VungTrai { get; private set; }
        public RectTransform VungDieuKhien { get; private set; }
        public readonly List<Text> SoLieu = new List<Text>();
        protected float hen;   // hẹn giờ cập nhật chữ
        protected const float ChuKyChu = 0.2f;

        /// <summary>Số khung đã vẽ (cho kiểm thử).</summary>
        public int SoKhung { get; protected set; }

        public void Gan(RectTransform trai, RectTransform dieuKhien)
        {
            VungTrai = trai;
            VungDieuKhien = dieuKhien;
            Dung();
        }

        protected abstract void Dung();
        public abstract void CapNhat(float dt);
        public virtual void KhiHien() { hen = 0; }

        /// <summary>Buộc cập nhật chữ số liệu ở khung sau (sau khi đổi tham số).</summary>
        public void LamMoiChu() => hen = 0;

        protected bool DenLucCapNhatChu(float dt)
        {
            hen -= dt;
            if (hen > 0) return false;
            hen = ChuKyChu;
            return true;
        }

        // ---- dựng chung ----

        /// <summary>Ô số liệu thứ i trong lưới 2 cột bắt đầu ở y0 (trong VungDieuKhien).</summary>
        protected Text OSo(string nhan, int i, float y0, float rong = 268, float cao = 64)
        {
            float x = 20 + (i % 2) * (rong + 12), y = y0 - (i / 2) * (cao + 10);
            var t = GlassKit.Readout(VungDieuKhien, "Số liệu " + nhan, nhan, "—", new Vector2(x, y), new Vector2(rong, cao));
            SoLieu.Add(t);
            return t;
        }

        protected Text TieuMuc(string chu, float y) =>
            HoaLyUi.Chu(VungDieuKhien, "Mục " + chu, chu.ToUpperInvariant(), 12, GlassKit.Accent2, true, TextAnchor.UpperLeft, new Vector2(20, y), new Vector2(548, 18));

        /// <summary>Nhóm chip chọn một: trả về mảng nút; chon(i) được gọi khi bấm.</summary>
        protected Button[] NhomChip(string[] nhan, float y, float rong, System.Action<int> chon, int dangChon, int moiHang = 4, float caoChip = 40, int coChu = 14)
        {
            var ds = new Button[nhan.Length];
            float w = (rong - (moiHang - 1) * 8) / moiHang;
            for (int i = 0; i < nhan.Length; i++)
            {
                int ii = i;
                ds[i] = GlassKit.MakeChip(VungDieuKhien, "Chọn " + nhan[i], nhan[i], new Vector2(20 + (i % moiHang) * (w + 8), y - (i / moiHang) * (caoChip + 8)),
                    new Vector2(w, caoChip), () => { for (int j = 0; j < ds.Length; j++) GlassKit.SetChip(ds[j], j == ii); chon(ii); }, i == dangChon, coChu);
            }
            return ds;
        }

        protected static void DatChu(Text t, string s)
        {
            if (t != null && t.text != s) t.text = s;
        }
    }
}
