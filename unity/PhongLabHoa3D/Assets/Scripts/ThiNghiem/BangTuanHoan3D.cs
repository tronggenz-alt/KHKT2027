using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using PhongThiNghiem.NhanVat;
using PhongThiNghiem.HeThong;

namespace PhongThiNghiem.ThiNghiem
{
    [Serializable]
    public class NguyenToHoaHoc
    {
        public int z;
        public string ki_hieu;
        public string ten;
        public string ten_viet;
        public string nguyen_tu_khoi;
        public float nguyen_tu_khoi_so;
        public int chu_ky;
        public int nhom;
        public string nhom_ab;
        public string khoi;
        public string loai;
        public string cau_hinh_e;
        public string cau_hinh_e_chuan;
        public float do_am_dien;
        public float ban_kinh_pm;
        public float nang_luong_ion_hoa_1_eV;
        public string[] so_oxi_hoa;
        public string trang_thai_25C;
        public float t_nong_chay_K;
        public float t_soi_K;
        public float khoi_luong_rieng;
    }

    [Serializable]
    public class GoiNguyenToWrapper
    {
        public NguyenToHoaHoc[] nguyen_to;
    }

    public enum CheDoToMau
    {
        TheoLoai = 0,
        TheoDoAmDien = 1,
        TheoBanKinh = 2,
        TheoTrangThai = 3
    }

    public enum LocKhoi
    {
        TatCa = 0,
        Khoi_s = 1,
        Khoi_p = 2,
        Khoi_d = 3,
        Khoi_f = 4
    }

    /// <summary>
    /// Quản lý Bảng Tuần Hoàn 118 nguyên tố trong phòng thí nghiệm 3D:
    /// 1. Dữ liệu nạp từ StreamingAssets/ptn/nguyen-to.json (chuẩn PubChem + RDKit + SGK 2018).
    /// 2. Bảng 3D lớn kích thước 7m × 3.6m có đèn nền, đặt trang trọng ở tường sảnh đón tiếp.
    /// 3. Poster nhỏ đặt tại các phòng nhánh thí nghiệm.
    /// 4. Giao diện tra cứu toàn màn hình phong cách Glassmorphism:
    ///    - Lọc theo khối (s, p, d, f) & nhóm phân loại.
    ///    - Chế độ tô màu trực quan (loại, độ âm điện, bán kính, trạng thái rắn/lỏng/khí).
    ///    - Thẻ chi tiết nguyên tố với cấu hình e chỉ số trên, tính chất lý hoá đầy đủ.
    ///    - Mô hình Bohr nguyên tử động: quỹ đạo lớp e quay mượt mà theo thời gian thực.
    /// </summary>
    public class BangTuanHoan3D : MonoBehaviour
    {
        public static BangTuanHoan3D Instance { get; private set; }

        [Header("Dữ liệu nguyên tố")]
        public List<NguyenToHoaHoc> DanhSach = new List<NguyenToHoaHoc>();
        [NonSerialized] public Dictionary<int, NguyenToHoaHoc> BangTraCuu = new Dictionary<int, NguyenToHoaHoc>();

        [Header("Trạng thái hiển thị")]
        public bool DangMoModal = false;
        public NguyenToHoaHoc NguyenToDangChon;
        public CheDoToMau CheDoMauHienTai = CheDoToMau.TheoLoai;
        public LocKhoi LocKhoiHienTai = LocKhoi.TatCa;
        public bool NguoiChoiOGan = false;

        [Header("Tham chiếu 3D")]
        public Transform GocBangSanh;

        // GUI Styles
        private GUIStyle _styleKhungModal;
        private GUIStyle _styleTieuDe;
        private GUIStyle _styleNutONguyenTo;
        private GUIStyle _styleTheChiTiet;
        private GUIStyle _styleNhanThongTin;
        private GUIStyle _styleGiaTriThongTin;
        private GUIStyle _styleNutTab;
        private GUIStyle _styleNutTabActive;
        private Texture2D _texNenModal;
        private Texture2D _texCardNen;
        private Texture2D _texOTrang;

        void Awake()
        {
            if (Instance == null) Instance = this;
            NapDuLieu();
        }

        void Start()
        {
            if (NguyenToDangChon == null && BangTraCuu.ContainsKey(11))
            {
                NguyenToDangChon = BangTraCuu[11]; // Mặc định Natri (Z=11)
            }
        }

        public void NapDuLieu(string duongDanTuyChon = null)
        {
            DanhSach.Clear();
            BangTraCuu.Clear();

            string duongDan = string.IsNullOrEmpty(duongDanTuyChon) ? "ptn/nguyen-to.json" : duongDanTuyChon;
            string json = DocTaiNguyen.DocChuoi(duongDan);

            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    string wrapped = "{\"nguyen_to\":" + json + "}";
                    var goi = JsonUtility.FromJson<GoiNguyenToWrapper>(wrapped);
                    if (goi != null && goi.nguyen_to != null)
                    {
                        foreach (var nt in goi.nguyen_to)
                        {
                            DanhSach.Add(nt);
                            BangTraCuu[nt.z] = nt;
                        }
                    }
                    Debug.Log($"[BangTuanHoan3D] Đã nạp thành công {DanhSach.Count}/118 nguyên tố");
                }
                catch (Exception ex)
                {
                    Debug.LogError("[BangTuanHoan3D] Lỗi phân tích cú pháp nguyen-to.json: " + ex.Message);
                }
            }
            else
            {
                Debug.LogWarning("[BangTuanHoan3D] Không tìm thấy dữ liệu nguyen-to.json từ: " + duongDan);
            }
        }

        void Update()
        {
            // Kiểm tra khoảng cách nhân vật tới bảng tuần hoàn ở sảnh
            if (GocBangSanh != null)
            {
                Vector3 vitriNhanVat = Vector3.zero;
                if (DieuKhienNhanVat.Instance != null)
                {
                    vitriNhanVat = DieuKhienNhanVat.Instance.transform.position;
                }
                else
                {
                    var nv = GameObject.Find("NhanVatGiaoSu");
                    if (nv != null) vitriNhanVat = nv.transform.position;
                }

                float khoangCach = Vector3.Distance(vitriNhanVat, GocBangSanh.position);
                NguoiChoiOGan = (khoangCach <= 3.8f);

                if (NguoiChoiOGan)
                {
                    if (GiaoDien.GiaoDienApp.Instance != null && !DangMoModal)
                    {
                        GiaoDien.GiaoDienApp.Instance.CapNhatGoiYTram("[E] Mở Bảng Tuần Hoàn 118 Nguyên Tố");
                    }

                    if (Input.GetKeyDown(KeyCode.E) && !DangMoModal)
                    {
                        MoModal();
                    }
                }
            }

            // Phím tắt đóng bảng
            if (DangMoModal && (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Q)))
            {
                DongModal();
            }
        }

        public void MoModal()
        {
            DangMoModal = true;
            if (DieuKhienNhanVat.Instance != null)
            {
                DieuKhienNhanVat.Instance.KhoaChuot(false);
            }
        }

        public void DongModal()
        {
            DangMoModal = false;
            if (DieuKhienNhanVat.Instance != null)
            {
                DieuKhienNhanVat.Instance.KhoaChuot(true);
            }
        }

        #region Dựng Bảng 3D tại Sảnh & Poster Phòng

        /// <summary>
        /// Dựng Bảng Tuần Hoàn 3D lớn 7.0m × 3.6m tại sảnh phòng thí nghiệm.
        /// Tọa độ đặt: trên tường Nam sảnh đón tiếp (x=16.8m, y=2.05m, z=15.82m) hướng nhìn về Bắc.
        /// </summary>
        public static GameObject TaoBangSanh(Transform parent)
        {
            var bangGo = new GameObject("BangTuanHoan3D_Sanh");
            bangGo.transform.SetParent(parent, false);
            bangGo.transform.localPosition = new Vector3(16.8f, 2.05f, 15.82f);
            bangGo.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); // Quay mặt về phía Bắc phòng

            var bth = bangGo.AddComponent<BangTuanHoan3D>();
            bth.GocBangSanh = bangGo.transform;

            // 1. Khung viền kim loại Titan xám đậm (7.08m × 3.68m × 0.08m)
            var khung = GameObject.CreatePrimitive(PrimitiveType.Cube);
            khung.name = "KhungTitan";
            khung.transform.SetParent(bangGo.transform, false);
            khung.transform.localPosition = Vector3.zero;
            khung.transform.localScale = new Vector3(7.08f, 3.68f, 0.08f);
            khung.GetComponent<Renderer>().material = LabVatLieu.Dac(new Color(0.12f, 0.15f, 0.18f), 0.8f);

            // 2. Nền bảng Dark Navy có đèn nền LED emissive (7.0m × 3.6m)
            var nen = GameObject.CreatePrimitive(PrimitiveType.Cube);
            nen.name = "NenLED";
            nen.transform.SetParent(bangGo.transform, false);
            nen.transform.localPosition = new Vector3(0f, 0f, 0.02f);
            nen.transform.localScale = new Vector3(7.0f, 3.6f, 0.04f);
            nen.GetComponent<Renderer>().material = LabVatLieu.Dac(new Color(0.04f, 0.09f, 0.15f), 0.4f);

            // 3. Viền LED phát sáng xanh dương thanh lịch xung quanh
            var led = GameObject.CreatePrimitive(PrimitiveType.Cube);
            led.name = "VienLED_Glow";
            led.transform.SetParent(bangGo.transform, false);
            led.transform.localPosition = new Vector3(0f, 0f, 0.03f);
            led.transform.localScale = new Vector3(7.02f, 3.62f, 0.01f);
            DestroyImmediate(led.GetComponent<Collider>());
            led.GetComponent<Renderer>().material = LabVatLieu.PhatSang(new Color(0.15f, 0.45f, 0.85f, 0.5f));

            // 4. Dựng 118 ô nguyên tố 3D trên bảng
            DungLuoi118ONguyenTo(bangGo.transform, bth);

            // 5. Đèn Spotlight rọi từ trên trần xuống mặt bảng
            var denRoi = new GameObject("DenSpotlight_Bang");
            denRoi.transform.SetParent(bangGo.transform, false);
            denRoi.transform.localPosition = new Vector3(0f, 1.9f, 1.2f);
            denRoi.transform.localRotation = Quaternion.Euler(55f, 0f, 0f);
            var lightComp = denRoi.AddComponent<Light>();
            lightComp.type = LightType.Spot;
            lightComp.range = 5.5f;
            lightComp.spotAngle = 75f;
            lightComp.color = new Color(0.92f, 0.96f, 1.0f);
            lightComp.intensity = 1.35f;

            // 6. Trigger Collider để phát hiện người chơi lại gần
            var trig = bangGo.AddComponent<BoxCollider>();
            trig.isTrigger = true;
            trig.center = new Vector3(0f, 0f, 1.8f);
            trig.size = new Vector3(7.5f, 3.5f, 3.6f);

            return bangGo;
        }

        private static void DungLuoi118ONguyenTo(Transform parent, BangTuanHoan3D bth)
        {
            if (bth.DanhSach.Count == 0) bth.NapDuLieu();

            float cotW = 0.365f;
            float hangH = 0.32f;
            float gocTraiX = -3.10f;
            float gocTrenY = 1.30f;

            var goNhom = new GameObject("CacONguyenTo_3D");
            goNhom.transform.SetParent(parent, false);

            foreach (var nt in bth.DanhSach)
            {
                int hang = nt.chu_ky;
                int cot = nt.nhom;

                // Xếp 2 hàng Lanthanide và Actinide xuống dưới
                if (nt.loai == "lanthanide")
                {
                    hang = 8;
                    cot = (nt.z - 57) + 3; // La=3, Ce=4... Lu=17
                }
                else if (nt.loai == "actinide")
                {
                    hang = 9;
                    cot = (nt.z - 89) + 3; // Ac=3, Th=4... Lr=17
                }

                float posX = gocTraiX + (cot - 1) * cotW;
                float posY = gocTrenY - (hang - 1) * hangH;

                if (hang >= 8)
                {
                    posY -= 0.12f; // Tạo khoảng cách với chu kỳ 7
                }

                var oGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                oGo.name = $"O_{nt.z}_{nt.ki_hieu}";
                oGo.transform.SetParent(goNhom.transform, false);
                oGo.transform.localPosition = new Vector3(posX, posY, 0.05f);
                oGo.transform.localScale = new Vector3(cotW * 0.90f, hangH * 0.88f, 0.02f);

                // Gán vật liệu theo loại nguyên tố
                Color mauLoai = LayMauLoai(nt.loai);
                oGo.GetComponent<Renderer>().material = LabVatLieu.Dac(mauLoai, 0.35f);

                // Gắn thành phần nhận click tương tác
                var tuongTac = oGo.AddComponent<TuongTacONguyenTo3D>();
                tuongTac.NguyenTo = nt;
                tuongTac.QuanLy = bth;
            }
        }

        /// <summary>
        /// Tạo Poster Bảng tuần hoàn nhỏ (1.4m × 0.85m) treo trên tường các phòng con.
        /// </summary>
        public static GameObject TaoPosterPhong(Transform parent, Vector3 viTri, Vector3 gocXoay)
        {
            var poster = new GameObject("Poster_BangTuanHoan");
            poster.transform.SetParent(parent, false);
            poster.transform.localPosition = viTri;
            poster.transform.localRotation = Quaternion.Euler(gocXoay);

            var khung = GameObject.CreatePrimitive(PrimitiveType.Cube);
            khung.name = "KhungPoster";
            khung.transform.SetParent(poster.transform, false);
            khung.transform.localScale = new Vector3(1.42f, 0.87f, 0.03f);
            khung.GetComponent<Renderer>().material = LabVatLieu.Dac(new Color(0.2f, 0.22f, 0.25f), 0.7f);

            var mat = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mat.name = "MatPoster";
            mat.transform.SetParent(poster.transform, false);
            mat.transform.localPosition = new Vector3(0f, 0f, 0.018f);
            mat.transform.localScale = new Vector3(1.38f, 0.83f, 0.01f);
            mat.GetComponent<Renderer>().material = LabVatLieu.Dac(new Color(0.08f, 0.14f, 0.22f), 0.4f);

            return poster;
        }

        #endregion

        #region Màu Sắc & Quy Ước Hoá Học Chuẩn

        public static Color LayMauLoai(string loai)
        {
            switch (loai)
            {
                case "kim_loai_kiem": return new Color(1.0f, 0.42f, 0.42f); // #ff6b6b (Đỏ cam)
                case "kiem_tho": return new Color(1.0f, 0.66f, 0.30f); // #ffa94d (Cam)
                case "chuyen_tiep": return new Color(1.0f, 0.83f, 0.23f); // #ffd43b (Vàng hổ phách)
                case "kim_loai_sau_chuyen_tiep": return new Color(0.41f, 0.86f, 0.49f); // #69db7c (Lục sáng)
                case "a_kim": return new Color(0.22f, 0.85f, 0.66f); // #38d9a9 (Ngọc lục bảo)
                case "phi_kim": return new Color(0.30f, 0.67f, 0.97f); // #4dabf7 (Lam dịu)
                case "halogen": return new Color(0.45f, 0.56f, 0.99f); // #748ffc (Chàm)
                case "khi_hiem": return new Color(0.85f, 0.47f, 0.95f); // #da77f2 (Tím hồng)
                case "lanthanide": return new Color(0.94f, 0.40f, 0.58f); // #f06595 (Hồng)
                case "actinide": return new Color(0.90f, 0.29f, 0.50f); // #e64980 (Đỏ hồng)
                default: return new Color(0.65f, 0.70f, 0.75f);
            }
        }

        public Color LayMauTheoCheDo(NguyenToHoaHoc nt)
        {
            if (nt == null) return Color.gray;

            // Kiểm tra bộ lọc khối
            if (LocKhoiHienTai != LocKhoi.TatCa)
            {
                string k = nt.khoi != null ? nt.khoi.ToLower() : "";
                bool khop = (LocKhoiHienTai == LocKhoi.Khoi_s && k == "s") ||
                            (LocKhoiHienTai == LocKhoi.Khoi_p && k == "p") ||
                            (LocKhoiHienTai == LocKhoi.Khoi_d && k == "d") ||
                            (LocKhoiHienTai == LocKhoi.Khoi_f && k == "f");
                if (!khop)
                {
                    return new Color(0.35f, 0.40f, 0.45f, 0.35f); // Làm mờ các nguyên tố không thuộc khối
                }
            }

            switch (CheDoMauHienTai)
            {
                case CheDoToMau.TheoDoAmDien:
                    if (nt.do_am_dien <= 0.1f) return new Color(0.45f, 0.48f, 0.52f);
                    float tAmDien = Mathf.InverseLerp(0.7f, 4.0f, nt.do_am_dien);
                    return Color.Lerp(new Color(0.22f, 0.74f, 0.97f), new Color(0.95f, 0.25f, 0.25f), tAmDien);

                case CheDoToMau.TheoBanKinh:
                    if (nt.ban_kinh_pm <= 1.0f) return new Color(0.45f, 0.48f, 0.52f);
                    float tBanKinh = Mathf.InverseLerp(30f, 280f, nt.ban_kinh_pm);
                    return Color.Lerp(new Color(0.6f, 0.4f, 0.95f), new Color(1.0f, 0.85f, 0.2f), tBanKinh);

                case CheDoToMau.TheoTrangThai:
                    string tt = nt.trang_thai_25C != null ? nt.trang_thai_25C.ToLower() : "";
                    if (tt.Contains("khí") || tt.Contains("gas")) return new Color(0.22f, 0.74f, 0.97f);
                    if (tt.Contains("lỏng") || tt.Contains("liquid")) return new Color(0.98f, 0.32f, 0.45f);
                    return new Color(0.68f, 0.74f, 0.82f); // Rắn

                case CheDoToMau.TheoLoai:
                default:
                    return LayMauLoai(nt.loai);
            }
        }

        #endregion

        #region Giao Diện Tra Cứu Tương Tác Glassmorphism (OnGUI)

        private void KhoiTaoStyles()
        {
            if (_styleKhungModal != null) return;

            _texNenModal = TaoTextureDonSac(new Color(0.04f, 0.08f, 0.14f, 0.94f));
            _texCardNen = TaoTextureDonSac(new Color(0.08f, 0.15f, 0.24f, 0.92f));
            _texOTrang = TaoTextureDonSac(Color.white);

            _styleKhungModal = new GUIStyle();
            _styleKhungModal.normal.background = _texNenModal;
            _styleKhungModal.padding = new RectOffset(20, 20, 16, 16);

            _styleTieuDe = new GUIStyle();
            _styleTieuDe.fontSize = 18;
            _styleTieuDe.fontStyle = FontStyle.Bold;
            _styleTieuDe.normal.textColor = new Color(0.92f, 0.96f, 1.0f);
            _styleTieuDe.alignment = TextAnchor.MiddleLeft;

            _styleNutONguyenTo = new GUIStyle(GUI.skin.button);
            _styleNutONguyenTo.fontSize = 11;
            _styleNutONguyenTo.fontStyle = FontStyle.Bold;
            _styleNutONguyenTo.alignment = TextAnchor.MiddleCenter;
            _styleNutONguyenTo.margin = new RectOffset(1, 1, 1, 1);
            _styleNutONguyenTo.padding = new RectOffset(1, 1, 1, 1);

            _styleTheChiTiet = new GUIStyle();
            _styleTheChiTiet.normal.background = _texCardNen;
            _styleTheChiTiet.padding = new RectOffset(16, 16, 16, 16);

            _styleNhanThongTin = new GUIStyle();
            _styleNhanThongTin.fontSize = 12;
            _styleNhanThongTin.normal.textColor = new Color(0.65f, 0.75f, 0.85f);

            _styleGiaTriThongTin = new GUIStyle();
            _styleGiaTriThongTin.fontSize = 13;
            _styleGiaTriThongTin.fontStyle = FontStyle.Bold;
            _styleGiaTriThongTin.normal.textColor = Color.white;

            _styleNutTab = new GUIStyle(GUI.skin.button);
            _styleNutTab.fontSize = 12;
            _styleNutTab.normal.textColor = new Color(0.8f, 0.85f, 0.9f);

            _styleNutTabActive = new GUIStyle(GUI.skin.button);
            _styleNutTabActive.fontSize = 12;
            _styleNutTabActive.fontStyle = FontStyle.Bold;
            _styleNutTabActive.normal.textColor = new Color(0.22f, 0.74f, 0.97f);
        }

        private Texture2D TaoTextureDonSac(Color c)
        {
            var tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, c);
            tex.Apply();
            return tex;
        }

        void OnGUI()
        {
            if (!DangMoModal) return;

            KhoiTaoStyles();

            float manHinhW = Screen.width;
            float manHinhH = Screen.height;

            // Khung modal toàn màn hình
            GUI.Box(new Rect(0, 0, manHinhW, manHinhH), GUIContent.none, _styleKhungModal);

            // 1. Header (Tiêu đề + Bộ lọc + Nút đóng)
            VeHeader(manHinhW);

            // 2. Lưới 118 nguyên tố bên trái (~70% màn hình)
            float luoiW = manHinhW * 0.70f;
            float luoiH = manHinhH - 100f;
            VeLuoiBangTuanHoan(new Rect(20, 70, luoiW, luoiH));

            // 3. Thẻ chi tiết nguyên tố & Mô hình Bohr bên phải (~28% màn hình)
            float theX = luoiW + 35f;
            float theW = manHinhW - theX - 20f;
            VeTheChiTietNguyenTo(new Rect(theX, 70, theW, luoiH));
        }

        private void VeHeader(float manHinhW)
        {
            GUILayout.BeginArea(new Rect(20, 16, manHinhW - 40, 48));
            GUILayout.BeginHorizontal();

            // Tiêu đề
            GUILayout.Label("🔬 BẢNG TUẦN HOÀN NGUYÊN TỐ HÓA HỌC (IUPAC / SGK 10)", _styleTieuDe, GUILayout.Width(460));

            GUILayout.FlexibleSpace();

            // Bộ lọc khối s/p/d/f
            GUILayout.Label("Lọc khối:", _styleNhanThongTin, GUILayout.Width(60));
            if (GUILayout.Button("Tất cả", LocKhoiHienTai == LocKhoi.TatCa ? _styleNutTabActive : _styleNutTab, GUILayout.Width(60))) LocKhoiHienTai = LocKhoi.TatCa;
            if (GUILayout.Button("Khối s", LocKhoiHienTai == LocKhoi.Khoi_s ? _styleNutTabActive : _styleNutTab, GUILayout.Width(60))) LocKhoiHienTai = LocKhoi.Khoi_s;
            if (GUILayout.Button("Khối p", LocKhoiHienTai == LocKhoi.Khoi_p ? _styleNutTabActive : _styleNutTab, GUILayout.Width(60))) LocKhoiHienTai = LocKhoi.Khoi_p;
            if (GUILayout.Button("Khối d", LocKhoiHienTai == LocKhoi.Khoi_d ? _styleNutTabActive : _styleNutTab, GUILayout.Width(60))) LocKhoiHienTai = LocKhoi.Khoi_d;
            if (GUILayout.Button("Khối f", LocKhoiHienTai == LocKhoi.Khoi_f ? _styleNutTabActive : _styleNutTab, GUILayout.Width(60))) LocKhoiHienTai = LocKhoi.Khoi_f;

            GUILayout.Space(20);

            // Chế độ tô màu
            GUILayout.Label("Tô màu:", _styleNhanThongTin, GUILayout.Width(55));
            if (GUILayout.Button("Phân loại", CheDoMauHienTai == CheDoToMau.TheoLoai ? _styleNutTabActive : _styleNutTab, GUILayout.Width(75))) CheDoMauHienTai = CheDoToMau.TheoLoai;
            if (GUILayout.Button("Độ âm điện", CheDoMauHienTai == CheDoToMau.TheoDoAmDien ? _styleNutTabActive : _styleNutTab, GUILayout.Width(85))) CheDoMauHienTai = CheDoToMau.TheoDoAmDien;
            if (GUILayout.Button("Bán kính", CheDoMauHienTai == CheDoToMau.TheoBanKinh ? _styleNutTabActive : _styleNutTab, GUILayout.Width(75))) CheDoMauHienTai = CheDoToMau.TheoBanKinh;
            if (GUILayout.Button("Trạng thái", CheDoMauHienTai == CheDoToMau.TheoTrangThai ? _styleNutTabActive : _styleNutTab, GUILayout.Width(75))) CheDoMauHienTai = CheDoToMau.TheoTrangThai;

            GUILayout.Space(20);

            // Nút đóng
            GUI.backgroundColor = new Color(0.9f, 0.25f, 0.25f);
            if (GUILayout.Button("✖ Đóng (Esc)", GUILayout.Width(95), GUILayout.Height(28)))
            {
                DongModal();
            }
            GUI.backgroundColor = Color.white;

            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void VeLuoiBangTuanHoan(Rect rectLuoi)
        {
            float cotW = (rectLuoi.width - 25f) / 18f;
            float hangH = (rectLuoi.height - 40f) / 10f;

            // Nhãn cột nhóm 1..18
            for (int c = 1; c <= 18; c++)
            {
                Rect rNhom = new Rect(rectLuoi.x + 25f + (c - 1) * cotW, rectLuoi.y, cotW, 18f);
                GUI.Label(rNhom, c.ToString(), _styleNhanThongTin);
            }

            // Nhãn chu kỳ 1..7
            for (int h = 1; h <= 7; h++)
            {
                Rect rChuKy = new Rect(rectLuoi.x, rectLuoi.y + 20f + (h - 1) * hangH, 20f, hangH);
                GUI.Label(rChuKy, h.ToString(), _styleNhanThongTin);
            }

            // Vẽ 118 ô nguyên tố
            foreach (var nt in DanhSach)
            {
                int hang = nt.chu_ky;
                int cot = nt.nhom;

                if (nt.loai == "lanthanide")
                {
                    hang = 9;
                    cot = (nt.z - 57) + 3;
                }
                else if (nt.loai == "actinide")
                {
                    hang = 10;
                    cot = (nt.z - 89) + 3;
                }

                float x = rectLuoi.x + 25f + (cot - 1) * cotW;
                float y = rectLuoi.y + 20f + (hang - 1) * hangH;

                if (hang >= 9) y += 12f; // Tách hàng họ Lanthanide/Actinide

                Rect rO = new Rect(x, y, cotW - 2f, hangH - 2f);

                // Tô màu theo chế độ hiện tại
                Color mauO = LayMauTheoCheDo(nt);
                GUI.backgroundColor = mauO;

                // Nếu là nguyên tố đang chọn, viền nổi bật
                bool dangChon = (NguyenToDangChon != null && NguyenToDangChon.z == nt.z);
                if (dangChon)
                {
                    GUI.backgroundColor = Color.yellow;
                }

                string nhanO = $"{nt.z}\n{nt.ki_hieu}";
                if (GUI.Button(rO, nhanO, _styleNutONguyenTo))
                {
                    NguyenToDangChon = nt;
                }
            }

            GUI.backgroundColor = Color.white;
        }

        private void VeTheChiTietNguyenTo(Rect rectThe)
        {
            GUI.Box(rectThe, GUIContent.none, _styleTheChiTiet);

            if (NguyenToDangChon == null) return;

            var nt = NguyenToDangChon;
            float left = rectThe.x + 14f;
            float top = rectThe.y + 14f;
            float w = rectThe.width - 28f;

            // 1. Tiêu đề nguyên tố: Ký hiệu lớn + Z + Tên
            Color mauLoai = LayMauLoai(nt.loai);
            GUI.color = mauLoai;
            GUI.Label(new Rect(left, top, 80, 50), nt.ki_hieu, new GUIStyle(GUI.skin.label) { fontSize = 34, fontStyle = FontStyle.Bold, normal = { textColor = mauLoai } });
            GUI.color = Color.white;

            string tenGoi = $"{nt.ten} ({nt.ten_viet})";
            GUI.Label(new Rect(left + 85, top, w - 85, 24), tenGoi, new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } });
            GUI.Label(new Rect(left + 85, top + 24, w - 85, 20), $"Số hiệu nguyên tử: Z = {nt.z} | A ≈ {nt.nguyen_tu_khoi}", _styleNhanThongTin);

            top += 58f;

            // Dải phân cách
            VeDuongPhanCach(left, top, w);
            top += 8f;

            // 2. Thông số vị trí & cấu trúc
            VeDongThongTin(left, ref top, w, "Phân loại:", nt.loai.Replace("_", " ").ToUpper());
            VeDongThongTin(left, ref top, w, "Vị trí bảng:", $"Chu kỳ {nt.chu_ky} | Nhóm {nt.nhom_ab} ({nt.nhom}) | Khối {nt.khoi?.ToUpper()}");
            VeDongThongTin(left, ref top, w, "Cấu hình electron:", nt.cau_hinh_e);

            string soOxiHoa = (nt.so_oxi_hoa != null && nt.so_oxi_hoa.Length > 0) ? string.Join(", ", nt.so_oxi_hoa) : "0";
            VeDongThongTin(left, ref top, w, "Số oxi hóa:", soOxiHoa);

            // 3. Thông số vật lý & hoá học
            top += 4f;
            VeDuongPhanCach(left, top, w);
            top += 8f;

            VeDongThongTin(left, ref top, w, "Độ âm điện:", nt.do_am_dien > 0.05f ? nt.do_am_dien.ToString("F2") : "–");
            VeDongThongTin(left, ref top, w, "Bán kính nguyên tử:", nt.ban_kinh_pm > 1f ? $"{nt.ban_kinh_pm:F0} pm" : "–");
            VeDongThongTin(left, ref top, w, "Năng lượng ion hóa:", nt.nang_luong_ion_hoa_1_eV > 0.05f ? $"{nt.nang_luong_ion_hoa_1_eV:F2} eV" : "–");
            VeDongThongTin(left, ref top, w, "Trạng thái (25°C):", nt.trang_thai_25C ?? "–");

            if (nt.t_nong_chay_K > 1f)
            {
                VeDongThongTin(left, ref top, w, "Nhiệt độ nóng chảy:", $"{nt.t_nong_chay_K:F0} K ({(nt.t_nong_chay_K - 273.15f):F0} °C)");
            }
            if (nt.t_soi_K > 1f)
            {
                VeDongThongTin(left, ref top, w, "Nhiệt độ sôi:", $"{nt.t_soi_K:F0} K ({(nt.t_soi_K - 273.15f):F0} °C)");
            }

            top += 6f;
            VeDuongPhanCach(left, top, w);
            top += 10f;

            // 4. Mô hình nguyên tử Bohr 3D / Động
            float bohrH = rectThe.y + rectThe.height - top - 14f;
            if (bohrH > 80f)
            {
                VeMoHinhBohr(new Rect(left, top, w, bohrH), nt);
            }
        }

        private void VeDongThongTin(float left, ref float top, float w, string nhan, string giaTri)
        {
            GUI.Label(new Rect(left, top, 140, 20), nhan, _styleNhanThongTin);
            GUI.Label(new Rect(left + 140, top, w - 140, 20), giaTri, _styleGiaTriThongTin);
            top += 22f;
        }

        private void VeDuongPhanCach(float left, float top, float w)
        {
            GUI.color = new Color(0.2f, 0.35f, 0.5f, 0.4f);
            GUI.DrawTexture(new Rect(left, top, w, 1), _texOTrang);
            GUI.color = Color.white;
        }

        /// <summary>
        /// Vẽ mô hình nguyên tử Bohr động với hạt nhân ở tâm và các lớp electron quay tròn theo thời gian thực.
        /// </summary>
        private void VeMoHinhBohr(Rect rectBohr, NguyenToHoaHoc nt)
        {
            GUI.Label(new Rect(rectBohr.x, rectBohr.y, rectBohr.width, 18), "⚛ MÔ HÌNH NGUYÊN TỬ BOHR ĐỘNG", _styleTieuDe);

            Vector2 tam = new Vector2(rectBohr.x + rectBohr.width * 0.5f, rectBohr.y + 25f + (rectBohr.height - 25f) * 0.5f);
            float banKinhMax = Mathf.Min(rectBohr.width * 0.45f, (rectBohr.height - 35f) * 0.45f);

            // 1. Vẽ hạt nhân phát sáng ở tâm
            float banKinhHatNhan = 10f;
            GUI.color = new Color(1.0f, 0.45f, 0.2f);
            GUI.DrawTexture(new Rect(tam.x - banKinhHatNhan, tam.y - banKinhHatNhan, banKinhHatNhan * 2, banKinhHatNhan * 2), _texOTrang);
            GUI.color = Color.white;

            // 2. Phân bố electron theo các lớp (K, L, M, N, O, P, Q)
            int[] eCacLop = TinhElectronCacLop(nt.z);
            int soLop = 0;
            for (int i = 0; i < eCacLop.Length; i++)
            {
                if (eCacLop[i] > 0) soLop = i + 1;
            }

            float deltaR = (banKinhMax - banKinhHatNhan - 6f) / Mathf.Max(soLop, 1);
            float thoiGian = Time.time;

            for (int lop = 0; lop < soLop; lop++)
            {
                float r = banKinhHatNhan + 6f + (lop + 1) * deltaR;
                int soE = eCacLop[lop];

                // Vẽ quỹ đạo mỏng (dùng điểm rời rạc)
                int soDiemQuyDao = 32;
                GUI.color = new Color(0.3f, 0.5f, 0.7f, 0.35f);
                for (int d = 0; d < soDiemQuyDao; d++)
                {
                    float rad = d * (Mathf.PI * 2f / soDiemQuyDao);
                    float qx = tam.x + Mathf.Cos(rad) * r;
                    float qy = tam.y + Mathf.Sin(rad) * r;
                    GUI.DrawTexture(new Rect(qx - 1, qy - 1, 2, 2), _texOTrang);
                }

                // Vẽ các hạt electron quay quanh tâm
                float tocDoQuay = (lop % 2 == 0 ? 1f : -1f) * (0.8f + (soLop - lop) * 0.3f);
                GUI.color = new Color(0.22f, 0.74f, 0.97f); // Sky Blue

                for (int e = 0; e < soE; e++)
                {
                    float gocE = (e * Mathf.PI * 2f / soE) + (thoiGian * tocDoQuay);
                    float ex = tam.x + Mathf.Cos(gocE) * r;
                    float ey = tam.y + Mathf.Sin(gocE) * r;
                    GUI.DrawTexture(new Rect(ex - 3, ey - 3, 6, 6), _texOTrang);
                }
            }

            GUI.color = Color.white;
        }

        /// <summary>
        /// Phân bố số electron trên 7 lớp (K, L, M, N, O, P, Q) cho nguyên tử số Z.
        /// </summary>
        public static int[] TinhElectronCacLop(int z)
        {
            int[] lop = new int[7];
            int eConLai = z;

            // Thứ tự điền năng lượng theo nguyên lý vững bền Aufbau: 1s, 2s, 2p, 3s, 3p, 4s, 3d, 4p, 5s, 4d, 5p, 6s, 4f, 5d, 6p, 7s, 5f, 6d, 7p
            (int n, int maxE)[] phanLop = new (int, int)[]
            {
                (1, 2),  // 1s
                (2, 2),  // 2s
                (2, 6),  // 2p
                (3, 2),  // 3s
                (3, 6),  // 3p
                (4, 2),  // 4s
                (3, 10), // 3d
                (4, 6),  // 4p
                (5, 2),  // 5s
                (4, 10), // 4d
                (5, 6),  // 5p
                (6, 2),  // 6s
                (4, 14), // 4f
                (5, 10), // 5d
                (6, 6),  // 6p
                (7, 2),  // 7s
                (5, 14), // 5f
                (6, 10), // 6d
                (7, 6)   // 7p
            };

            foreach (var pl in phanLop)
            {
                if (eConLai <= 0) break;
                int dien = Mathf.Min(eConLai, pl.maxE);
                lop[pl.n - 1] += dien;
                eConLai -= dien;
            }

            // Xử lý ngoại lệ nửa bão hòa và bão hòa d (Cr, Cu)
            if (z == 24) { lop[2] = 13; lop[3] = 1; } // Cr: [Ar] 3d5 4s1
            if (z == 29) { lop[2] = 18; lop[3] = 1; } // Cu: [Ar] 3d10 4s1
            if (z == 47) { lop[3] = 18; lop[4] = 1; } // Ag
            if (z == 79) { lop[4] = 18; lop[5] = 1; } // Au

            return lop;
        }

        #endregion
    }

    /// <summary>
    /// Component hỗ trợ click chuột hoặc raycast vào từng ô nguyên tố 3D trên bảng lớn.
    /// </summary>
    public class TuongTacONguyenTo3D : MonoBehaviour
    {
        public NguyenToHoaHoc NguyenTo;
        public BangTuanHoan3D QuanLy;

        void OnMouseDown()
        {
            if (QuanLy != null && NguyenTo != null)
            {
                QuanLy.NguyenToDangChon = NguyenTo;
                QuanLy.MoModal();
            }
        }
    }
}
