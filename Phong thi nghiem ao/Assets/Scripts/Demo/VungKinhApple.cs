using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Khkt.Demo
{
    public enum KinhLongCheDo
    {
        Regular = 0,
        Clear = 1
    }

    public enum KinhLongThichUngCheDo
    {
        KhoaTheoChuDe = 0,
        TuDong = 1
    }

    public enum KinhLongVaiTro
    {
        BangChinh = 0,
        The = 1,
        Nut = 2,
        VienNang = 3,
        ThanhDieuHuong = 4,
        NumTruot = 5,
        MenuBung = 6
    }

    /// <summary>
    /// Đánh dấu một vùng giao diện ngoài Phòng Lab sử dụng vật liệu Kính Lỏng Apple.
    /// Lưu chế độ (Regular / Clear), vai trò (nút, thẻ, thanh điều hướng...), chế độ thích ứng nền,
    /// và tự động báo bẩn cho bộ nhớ đệm mờ (KinhLongBlurFeature) khi hình học thay đổi.
    /// </summary>
    [DisallowMultipleComponent]
    public class VungKinhApple : MonoBehaviour
    {
        static readonly HashSet<VungKinhApple> _danhSach = new HashSet<VungKinhApple>();
        public static IReadOnlyCollection<VungKinhApple> DanhSach => _danhSach;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _danhSach.Clear();

        [Header("Cấu hình Kính Lỏng Apple")]
        public KinhLongCheDo CheDo = KinhLongCheDo.Regular;
        public KinhLongThichUngCheDo CheDoThichUng = KinhLongThichUngCheDo.KhoaTheoChuDe;
        public KinhLongVaiTro VaiTro = KinhLongVaiTro.Nut;
        public bool LaDieuHuong = false;
        [Range(0f, 120f)] public float BanKinhBo = 20f;
        [Range(0f, 0.7f)] public float DoToiCucBo = 0f;
        [Range(0f, 1f)] public float TrongSuotToiThieu = 0f;

        [HideInInspector] public float LuotMoHienTai = 1f;
        [HideInInspector] public bool DangThichUngToi = true;

        RectTransform _rt;
        Vector3 _viTriCu;
        Vector2 _kichThuocCu;
        Vector3 _tyLeCu;
        float _alphaCu = -1f;
        CanvasGroup _cg;

        public RectTransform Rect => _rt != null ? _rt : (_rt = transform as RectTransform);

        public static void BaoBan(int soKhung = 2)
        {
            Khkt.Rendering.KinhLongBlurCache.DanhDauBan(soKhung);
        }

        void OnEnable()
        {
            _danhSach.Add(this);
            _rt = transform as RectTransform;
            _cg = GetComponent<CanvasGroup>();
            GhiNhanHinhHoc();
            BaoBan(3);
        }

        void OnDisable()
        {
            _danhSach.Remove(this);
            BaoBan(3);
        }

        void OnRectTransformDimensionsChange()
        {
            if (isActiveAndEnabled) BaoBan(2);
        }

        void OnTransformParentChanged()
        {
            if (isActiveAndEnabled) BaoBan(2);
        }

        void LateUpdate()
        {
            if (_rt == null) _rt = transform as RectTransform;
            if (_rt == null) return;

            bool doi = _rt.position != _viTriCu
                    || _rt.rect.size != _kichThuocCu
                    || _rt.lossyScale != _tyLeCu;

            float alpha = _cg != null ? _cg.alpha : 1f;
            if (Mathf.Abs(alpha - _alphaCu) > 0.005f)
                doi = true;

            if (doi)
            {
                GhiNhanHinhHoc();
                BaoBan(2);
            }
        }

        void GhiNhanHinhHoc()
        {
            if (_rt != null)
            {
                _viTriCu = _rt.position;
                _kichThuocCu = _rt.rect.size;
                _tyLeCu = _rt.lossyScale;
            }
            _alphaCu = _cg != null ? _cg.alpha : 1f;
        }
    }
}
