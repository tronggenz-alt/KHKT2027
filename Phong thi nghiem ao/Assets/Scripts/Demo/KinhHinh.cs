using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Khkt.Demo
{
    /// <summary>
    /// Bộ điều khiển hình học & vật liệu cho một phần tử Kính Lỏng Apple (UI Image/RawImage).
    /// Truyền kích thước thật (px), mở rộng quad đủ cho bóng đổ thích ứng mà không làm sai vùng hit-test,
    /// và cập nhật đồng bộ thuộc tính lên shader Khkt/KinhLongApple.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Graphic))]
    public class KinhHinh : BaseMeshEffect
    {
        static readonly int IdKichThuoc = Shader.PropertyToID("_KichThuoc");
        static readonly int IdBanKinhBo = Shader.PropertyToID("_BanKinhBo");
        static readonly int IdLoaiMatCat = Shader.PropertyToID("_LoaiMatCat");
        static readonly int IdCheDo = Shader.PropertyToID("_CheDoKinh");
        static readonly int IdLaDieuHuong = Shader.PropertyToID("_LaDieuHuong");
        static readonly int IdLaNenSang = Shader.PropertyToID("_LaNenSang");
        static readonly int IdMucMo = Shader.PropertyToID("_MucMo");
        static readonly int IdVienKhucXa = Shader.PropertyToID("_VienKhucXa");
        static readonly int IdCuongDoKhucXa = Shader.PropertyToID("_CuongDoKhucXa");
        static readonly int IdCuongDoNen = Shader.PropertyToID("_CuongDoNen");
        static readonly int IdDoRuc = Shader.PropertyToID("_DoRuc");
        static readonly int IdToiCucBo = Shader.PropertyToID("_ToiCucBo");
        static readonly int IdCuongDoVienSang = Shader.PropertyToID("_CuongDoVienSang");
        static readonly int IdCuongDoVienToi = Shader.PropertyToID("_CuongDoVienToi");
        static readonly int IdBongCuongDo = Shader.PropertyToID("_BongCuongDo");
        static readonly int IdBongKichThuoc = Shader.PropertyToID("_BongKichThuoc");
        static readonly int IdBongDichY = Shader.PropertyToID("_BongDichY");
        static readonly int IdNgungTu = Shader.PropertyToID("_NgungTu");
        static readonly int IdRoiTam = Shader.PropertyToID("_RoiTam");
        static readonly int IdChamTam = Shader.PropertyToID("_ChamTam");
        static readonly int IdQuetVien = Shader.PropertyToID("_QuetVien");

        [Header("Cấu hình quang học & hình khối")]
        public KinhLongCheDo CheDo = KinhLongCheDo.Regular;
        public KinhLongMatCat LoaiMatCat = KinhLongMatCat.Squircle;
        public bool LaDieuHuong = false;
        public bool LaNenSang = false;
        public bool HinhVienNang = false;
        [Range(0f, 120f)] public float BanKinhBo = 22f;
        [Range(0f, 4f)] public float MucMo = 2.35f;
        [Range(2f, 40f)] public float VienKhucXa = 18f;
        [Range(0f, 2.5f)] public float CuongDoKhucXa = 1.0f;
        [Range(0f, 1f)] public float CuongDoNen = 0.48f;
        [Range(0.8f, 1.5f)] public float DoRuc = 1.18f;
        [Range(0f, 0.7f)] public float ToiCucBo = 0f;
        [Range(0f, 2f)] public float CuongDoVienSang = 1.0f;
        [Range(0f, 1f)] public float CuongDoVienToi = 0.24f;

        [Header("Bóng đổ thích ứng")]
        [Range(0f, 0.6f)] public float BongCuongDo = 0.18f;
        [Range(0f, 32f)] public float BongKichThuoc = 16f;
        public float BongDichY = 5f;
        [Range(0f, 36f)] public float LeBongMoRong = 18f;

        [Header("Tương tác động")]
        [Range(0f, 1f)] public float NgungTu = 1f;
        public Vector4 RoiTam = new Vector4(0.5f, 0.5f, 90f, 0f);
        public Vector4 ChamTam = new Vector4(0.5f, 0.5f, 110f, 0f);
        public float QuetVien = -1f;

        Material _matInstance;
        bool _soHuuMat;
        static Shader _shaderKinh;

        public Material VatLieu => _matInstance;

        public static KinhHinh Gan(Graphic g, KinhLongCheDo cheDo = KinhLongCheDo.Regular, float banKinhBo = 22f, bool laDieuHuong = false)
        {
            if (g == null) return null;
            var kh = g.GetComponent<KinhHinh>() ?? g.gameObject.AddComponent<KinhHinh>();
            kh.CheDo = cheDo;
            kh.BanKinhBo = banKinhBo;
            kh.LaDieuHuong = laDieuHuong;
            kh.CapNhatVatLieu();
            return kh;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            KinhLongQuangHoc.DamBaoKhoiTao();
            DamBaoVatLieu();
            CapNhatVatLieu();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (_soHuuMat && _matInstance != null)
            {
                Destroy(_matInstance);
                _matInstance = null;
                _soHuuMat = false;
            }
        }

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            if (isActiveAndEnabled)
            {
                CapNhatVatLieu();
                graphic?.SetVerticesDirty();
            }
        }

        public void DamBaoVatLieu()
        {
            if (_shaderKinh == null)
                _shaderKinh = Shader.Find("Khkt/KinhLongApple");
            if (_shaderKinh == null) return;

            if (_matInstance == null || _matInstance.shader != _shaderKinh)
            {
                if (_soHuuMat && _matInstance != null) Destroy(_matInstance);
                _matInstance = new Material(_shaderKinh)
                {
                    name = "KL_Apple_Inst",
                    hideFlags = HideFlags.HideAndDontSave
                };
                _soHuuMat = true;
            }

            if (graphic != null && graphic.material != _matInstance)
                graphic.material = _matInstance;
        }

        public void BatHopKinh(bool bat)
        {
            DamBaoVatLieu();
            if (_matInstance == null) return;
            if (bat) _matInstance.EnableKeyword("_HOP_KINH");
            else _matInstance.DisableKeyword("_HOP_KINH");
        }

        public void CapNhatVatLieu()
        {
            DamBaoVatLieu();
            if (_matInstance == null || graphic == null) return;

            Rect r = graphic.rectTransform.rect;
            float w = Mathf.Max(2f, r.width);
            float h = Mathf.Max(2f, r.height);
            float effR = HinhVienNang ? Mathf.Min(w, h) * 0.5f : Mathf.Min(BanKinhBo, Mathf.Min(w, h) * 0.5f);

            _matInstance.SetVector(IdKichThuoc, new Vector4(w, h, 0f, 0f));
            _matInstance.SetFloat(IdBanKinhBo, effR);
            _matInstance.SetFloat(IdLoaiMatCat, (float)LoaiMatCat);
            _matInstance.SetFloat(IdCheDo, (float)CheDo);
            _matInstance.SetFloat(IdLaDieuHuong, LaDieuHuong ? 1f : 0f);
            _matInstance.SetFloat(IdLaNenSang, LaNenSang ? 1f : 0f);
            _matInstance.SetFloat(IdMucMo, MucMo);
            _matInstance.SetFloat(IdVienKhucXa, VienKhucXa);
            _matInstance.SetFloat(IdCuongDoKhucXa, CuongDoKhucXa);
            _matInstance.SetFloat(IdCuongDoNen, CuongDoNen);
            _matInstance.SetFloat(IdDoRuc, DoRuc);
            _matInstance.SetFloat(IdToiCucBo, ToiCucBo);
            _matInstance.SetFloat(IdCuongDoVienSang, CuongDoVienSang);
            _matInstance.SetFloat(IdCuongDoVienToi, CuongDoVienToi);
            _matInstance.SetFloat(IdBongCuongDo, BongCuongDo);
            _matInstance.SetFloat(IdBongKichThuoc, BongKichThuoc);
            _matInstance.SetFloat(IdBongDichY, BongDichY);
            _matInstance.SetFloat(IdNgungTu, NgungTu);
            _matInstance.SetVector(IdRoiTam, RoiTam);
            _matInstance.SetVector(IdChamTam, ChamTam);
            _matInstance.SetFloat(IdQuetVien, QuetVien);
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || vh == null || vh.currentVertCount == 0) return;

            Rect r = graphic.rectTransform.rect;
            float w = Mathf.Max(2f, r.width);
            float h = Mathf.Max(2f, r.height);

            // Chỉ mở rộng đỉnh nếu có bóng đổ hoặc chống cắt răng cưa rìa; vẫn giữ UV [0..1] khớp đúng hình gốc
            float pad = (BongCuongDo > 0.001f && BongKichThuoc > 0.5f) ? LeBongMoRong : 2f;

            var verts = new List<UIVertex>(vh.currentVertCount);
            vh.GetUIVertexStream(verts);

            for (int i = 0; i < verts.Count; i++)
            {
                UIVertex v = verts[i];
                Vector3 pos = v.position;
                Vector2 uv0 = v.uv0;

                float dx = pos.x <= r.xMin + 0.01f ? -pad : (pos.x >= r.xMax - 0.01f ? pad : 0f);
                float dy = pos.y <= r.yMin + 0.01f ? (-pad - Mathf.Max(0f, BongDichY)) : (pos.y >= r.yMax - 0.01f ? pad : 0f);

                pos.x += dx;
                pos.y += dy;

                uv0.x = (pos.x - r.xMin) / w;
                uv0.y = (pos.y - r.yMin) / h;

                v.position = pos;
                v.uv0 = uv0;
                v.uv1 = new Vector4(w, h, r.xMin, r.yMin);
                verts[i] = v;
            }

            vh.Clear();
            vh.AddUIVertexTriangleStream(verts);
        }
    }
}
