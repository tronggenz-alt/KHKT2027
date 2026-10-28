using System;
using System.Collections.Generic;
using UnityEngine;
using PhongThiNghiem.Loi;
using PhongThiNghiem.NhanVat;
using PhongThiNghiem.ThiNghiem;
using PhongThiNghiem.ToaNha;

namespace PhongThiNghiem.GiaoDien
{
    /// <summary>
    /// Quản lý toàn bộ giao diện người dùng:
    /// 1. Menu chính (Vào lab, Chọn thí nghiệm, Cài đặt, Giới thiệu, Thoát)
    /// 2. HUD khi di chuyển (Tên khu vực, Gợi ý thao tác, Phím tắt, Mini-map toạ độ)
    /// 3. Bảng điều khiển thí nghiệm tại bàn (khớp 100% BANG[id] của web)
    /// 4. Hộp thoại Cài đặt (Đồ hoạ, FPS, VSync, Độ nhạy chuột, FOV, Âm lượng lưu PlayerPrefs)
    /// 5. Xử lý Deep Link URL scheme phonglabhoa://mo?tn=<id>
    /// </summary>
    [ExecuteAlways]
    public class GiaoDienApp : MonoBehaviour
    {
        public static GiaoDienApp Instance { get; private set; }

        public enum ManHinh { MenuChinh, LabHUD, BangThiNghiem, CaiDat, GiớiThieu }

        [Header("Trạng thái hiển thị")]
        public ManHinh ManHinhHienTai = ManHinh.LabHUD;
        public bool MenuChinhDangMo = false;
        public bool CaiDatDangMo = false;
        public bool GioiThieuDangMo = false;
        public bool BangThiNghiemDangMo = false;
        public string TramHienTai = "";

        [Header("Thông tin HUD")]
        public string TenKhuVuc = "Sảnh đón tiếp";
        public string ThongBaoKhuBanner = "";
        public float ThoiGianBannerKhu = 0f;
        public string GoiYPhim = "";
        public bool HienFPS = true;

        [Header("Cài đặt")]
        public int ChatLuong = 2; // 0: Thấp, 1: Vừa, 2: Cao, 3: Tự động
        public int GioiHanFPS = 60; // 30, 60, 120, -1
        public bool VSync = true;
        public float DoNhayChuot = 1.0f;
        public bool DaoTrucY = false;
        public float FOV = 60f;
        public float AmLuong = 80f;

        // Bảng màu CSS chuẩn Icy Blue & Midnight Navy
        public static readonly Color MauIcyBlue = new Color(0.933f, 0.957f, 0.980f); // #eef4fa
        public static readonly Color MauMidnightNavy = new Color(0.059f, 0.169f, 0.282f); // #0f2b48
        public static readonly Color MauSkyBlue = new Color(0.220f, 0.741f, 0.973f); // #38bdf8
        public static readonly Color MauNenToi = new Color(0.059f, 0.141f, 0.220f, 0.92f); // #0f2438
        public static readonly Color MauVien = new Color(0.220f, 0.741f, 0.973f, 0.45f);
        public static readonly Color MauVangCanhBao = new Color(0.96f, 0.62f, 0.04f);

        // Biến đo FPS
        private float _fpsDem = 0f;
        private float _fpsCapNhat = 0f;
        private int _fpsSoMau = 0;

        // Style GUI tuỳ chỉnh
        private GUIStyle _styleTieuDe;
        private GUIStyle _styleNutChinh;
        private GUIStyle _styleNutPhu;
        private GUIStyle _styleNhan;
        private GUIStyle _styleHopThoai;
        private GUIStyle _styleBannerKhu;
        private GUIStyle _styleGoiY;
        private Texture2D _texNenToi;
        private Texture2D _texNutBinhThuong;
        private Texture2D _texNutChon;

        // Dữ liệu bảng điều khiển theo từng trạm
        private int _cdBaiChon = 0; // 0: HCl, 1: CH3COOH, 2: H2SO4
        private int _cdChiThiChon = 1; // 0: Không, 1: Phenolphthalein, 2: Methyl orange
        private string _cdGiaTriDoc = "19.95";
        private string _cdNongDoTinh = "0.100";
        private bool _cdCheDoOnThi = false;

        private int _nbOngChon = 1; // Ống 1..4
        private string _nbThuocThuChon = "NaOH";
        private string _nbTabChon = "ong"; // "ong" hoặc "ngon_lua"

        private string _pgKimLoaiTrai = "Zn";
        private string _pgKimLoaiPhai = "Cu";
        private float _pgNongDoTrai = 1.0f;
        private float _pgNongDoPhai = 1.0f;
        private bool _pgCauMuoi = true;

        private string _dpBaiChon = "cuso4_tro";
        private float _dpI = 2.0f;
        private int _dpT = 965;
        private int _dpTocDo = 1;
        private bool _dpDangChay = false;
        private string _dpGiaTriDoiChieu = "0.64";

        private int _tabBanHienTai = 0; // 0: Thao tac, 1: On thi THPT, 2: Thong tin
        private int _cauHoiHienTai = 0;
        private int _luaChonHocSinh = -1;
        private bool _daTraLoi = false;
        private int _soCauDung = 0;

        void Awake()
        {
            Instance = this;
            TaiCaiDatTuPlayerPrefs();
            ApDungCaiDat();
        }

        void OnEnable()
        {
            Application.deepLinkActivated += OnDeepLinkActivated;
            if (!string.IsNullOrEmpty(Application.absoluteURL))
            {
                XuLyDeepLink(Application.absoluteURL);
            }
        }

        void OnDisable()
        {
            Application.deepLinkActivated -= OnDeepLinkActivated;
        }

        void Start()
        {
            if (DieuKhienNhanVat.Instance != null)
            {
                DieuKhienNhanVat.Instance.OnQuaRanhGioiKhu += HienBannerKhu;
                DieuKhienNhanVat.Instance.OnGanTram += CapNhatGoiYTram;
                DieuKhienNhanVat.Instance.OnTrangThaiTaiBan += CapNhatTaiBan;
            }
        }

        void Update()
        {
            // Đo FPS
            _fpsDem += Time.unscaledDeltaTime;
            _fpsSoMau++;
            if (_fpsDem >= 0.5f)
            {
                _fpsCapNhat = _fpsSoMau / _fpsDem;
                _fpsDem = 0f;
                _fpsSoMau = 0;
            }

            // Đếm lùi thời gian banner khu
            if (_thoiGianBannerKhuConLai > 0f)
            {
                _thoiGianBannerKhuConLai -= Time.deltaTime;
                if (_thoiGianBannerKhuConLai <= 0f) ThongBaoKhuBanner = "";
            }

            // Phím tắt mở/đóng Menu chính
#if UNITY_WEBGL && !UNITY_EDITOR
            bool moMenu = Input.GetKeyDown(KeyCode.Tab);
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (Cursor.lockState == CursorLockMode.Locked)
                {
                    if (DieKhienNhanVatCoSan()) DieuKhienNhanVat.Instance.KhoaChuot(false);
                }
                else
                {
                    moMenu = true;
                }
            }
            if (moMenu)
            {
                if (CaiDatDangMo) CaiDatDangMo = false;
                else if (GioiThieuDangMo) GioiThieuDangMo = false;
                else
                {
                    MenuChinhDangMo = !MenuChinhDangMo;
                    if (DieKhienNhanVatCoSan())
                    {
                        DieuKhienNhanVat.Instance.KhoaChuot(!MenuChinhDangMo && !BangThiNghiemDangMo);
                    }
                }
            }
#else
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (CaiDatDangMo)
                {
                    CaiDatDangMo = false;
                }
                else if (GioiThieuDangMo)
                {
                    GioiThieuDangMo = false;
                }
                else
                {
                    MenuChinhDangMo = !MenuChinhDangMo;
                    if (DieKhienNhanVatCoSan())
                    {
                        DieuKhienNhanVat.Instance.KhoaChuot(!MenuChinhDangMo && !BangThiNghiemDangMo);
                    }
                }
            }
#endif
        }

        private float _thoiGianBannerKhuConLai = 0f;

        public void HienBannerKhu(string tenKhu)
        {
            TenKhuVuc = tenKhu;
            ThongBaoKhuBanner = "Khu vực: " + tenKhu;
            _thoiGianBannerKhuConLai = 1.6f;
        }

        public void CapNhatGoiYTram(string goiY)
        {
            GoiYPhim = goiY;
        }

        public void CapNhatTaiBan(bool taiBan)
        {
            BangThiNghiemDangMo = taiBan;
            if (taiBan && DieuKhienNhanVat.Instance != null)
            {
                TramHienTai = DieuKhienNhanVat.Instance.TramHienTai;
            }
            else
            {
                TramHienTai = "";
            }
        }

        #region Xử lý Deep Link phonglabhoa://mo?tn=<id>

        public void OnDeepLinkActivated(string url)
        {
            XuLyDeepLink(url);
        }

        public bool XuLyDeepLink(string url)
        {
            if (string.IsNullOrEmpty(url)) return false;
            // Cú pháp phonglabhoa://mo?tn=chuan-do hoặc phonglabhoa://?tn=dien-phan
            try
            {
                var uri = new Uri(url);
                if (uri.Scheme != "phonglabhoa") return false;

                string query = uri.Query;
                if (string.IsNullOrEmpty(query)) return false;

                string[] parts = query.TrimStart('?').Split('&');
                foreach (var p in parts)
                {
                    var kv = p.Split('=');
                    if (kv.Length == 2 && kv[0] == "tn")
                    {
                        string idTram = kv[1].Trim();
                        return ChuyenToiTram(idTram);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[GiaoDienApp] Lỗi phân tích deep link: " + ex.Message);
            }
            return false;
        }

        public bool ChuyenToiTram(string idTram)
        {
            string[] tramtHopLe = { "chuan-do", "an-toan", "nhan-biet-ion", "pin-galvani", "dien-phan" };
            bool hopLe = false;
            foreach (var t in tramtHopLe)
            {
                if (t == idTram) { hopLe = true; break; }
            }
            if (!hopLe) return false;

            MenuChinhDangMo = false;
            CaiDatDangMo = false;
            GioiThieuDangMo = false;

            if (DieuKhienNhanVat.Instance != null)
            {
                DieuKhienNhanVat.Instance.VaoBan(idTram);
                return true;
            }
            return false;
        }

        #endregion

        #region Quản lý Cài đặt (PlayerPrefs)

        public void TaiCaiDatTuPlayerPrefs()
        {
            ChatLuong = PlayerPrefs.GetInt("ChatLuong", 2);
            GioiHanFPS = PlayerPrefs.GetInt("GioiHanFPS", 60);
            VSync = PlayerPrefs.GetInt("VSync", 1) == 1;
            HienFPS = PlayerPrefs.GetInt("HienFPS", 1) == 1;
            DoNhayChuot = PlayerPrefs.GetFloat("DoNhayChuot", 1.0f);
            DaoTrucY = PlayerPrefs.GetInt("DaoTrucY", 0) == 1;
            FOV = PlayerPrefs.GetFloat("FOV", 60f);
            AmLuong = PlayerPrefs.GetFloat("AmLuong", 80f);
        }

        public void LuuCaiDatVaoPlayerPrefs()
        {
            PlayerPrefs.SetInt("ChatLuong", ChatLuong);
            PlayerPrefs.SetInt("GioiHanFPS", GioiHanFPS);
            PlayerPrefs.SetInt("VSync", VSync ? 1 : 0);
            PlayerPrefs.SetInt("HienFPS", HienFPS ? 1 : 0);
            PlayerPrefs.SetFloat("DoNhayChuot", DoNhayChuot);
            PlayerPrefs.SetInt("DaoTrucY", DaoTrucY ? 1 : 0);
            PlayerPrefs.SetFloat("FOV", FOV);
            PlayerPrefs.SetFloat("AmLuong", AmLuong);
            PlayerPrefs.Save();
        }

        public void ApDungCaiDat()
        {
            // Chất lượng
            if (ChatLuong >= 0 && ChatLuong < QualitySettings.names.Length)
            {
                QualitySettings.SetQualityLevel(ChatLuong, true);
            }

            // VSync & FPS
            QualitySettings.vSyncCount = VSync ? 1 : 0;
            Application.targetFrameRate = GioiHanFPS;

            // Camera FOV
            if (Camera.main != null)
            {
                Camera.main.fieldOfView = FOV;
            }

            // Điều khiển nhân vật
            if (DieuKhienNhanVat.Instance != null)
            {
                DieuKhienNhanVat.Instance.DoNhayChuot = DoNhayChuot * 2.0f;
            }

            // Âm lượng
            AudioListener.volume = Mathf.Clamp01(AmLuong / 100f);
        }

        #endregion

        #region Render OnGUI Procedural UI

        private void KhoiTaoStyle()
        {
            if (_texNenToi == null)
            {
                _texNenToi = TaoTextureMau(MauNenToi);
                _texNutBinhThuong = TaoTextureMau(new Color(0.12f, 0.25f, 0.40f, 0.85f));
                _texNutChon = TaoTextureMau(MauSkyBlue);
            }

            if (_styleHopThoai == null)
            {
                _styleHopThoai = new GUIStyle(GUI.skin.box);
                _styleHopThoai.normal.background = _texNenToi;
                _styleHopThoai.normal.textColor = MauIcyBlue;
                _styleHopThoai.fontSize = 14;
                _styleHopThoai.padding = new RectOffset(16, 16, 16, 16);

                _styleTieuDe = new GUIStyle(GUI.skin.label);
                _styleTieuDe.normal.textColor = MauSkyBlue;
                _styleTieuDe.fontSize = 20;
                _styleTieuDe.fontStyle = FontStyle.Bold;
                _styleTieuDe.alignment = TextAnchor.MiddleCenter;

                _styleNutChinh = new GUIStyle(GUI.skin.button);
                _styleNutChinh.normal.background = _texNutChon;
                _styleNutChinh.normal.textColor = MauMidnightNavy;
                _styleNutChinh.fontSize = 14;
                _styleNutChinh.fontStyle = FontStyle.Bold;

                _styleNutPhu = new GUIStyle(GUI.skin.button);
                _styleNutPhu.normal.background = _texNutBinhThuong;
                _styleNutPhu.normal.textColor = MauIcyBlue;
                _styleNutPhu.fontSize = 13;

                _styleNhan = new GUIStyle(GUI.skin.label);
                _styleNhan.normal.textColor = MauIcyBlue;
                _styleNhan.fontSize = 13;

                _styleBannerKhu = new GUIStyle(GUI.skin.box);
                _styleBannerKhu.normal.background = _texNenToi;
                _styleBannerKhu.normal.textColor = MauSkyBlue;
                _styleBannerKhu.fontSize = 18;
                _styleBannerKhu.fontStyle = FontStyle.Bold;
                _styleBannerKhu.alignment = TextAnchor.MiddleCenter;

                _styleGoiY = new GUIStyle(GUI.skin.box);
                _styleGoiY.normal.background = _texNenToi;
                _styleGoiY.normal.textColor = MauVangCanhBao;
                _styleGoiY.fontSize = 15;
                _styleGoiY.fontStyle = FontStyle.Bold;
                _styleGoiY.alignment = TextAnchor.MiddleCenter;
            }
        }

        private Texture2D TaoTextureMau(Color col)
        {
            var tex = new Texture2D(2, 2);
            var cols = new Color[] { col, col, col, col };
            tex.SetPixels(cols);
            tex.Apply();
            return tex;
        }

        void OnGUI()
        {
            KhoiTaoStyle();

            // 1. HUD luôn hiển thị khi không mở Menu Chính
            if (!MenuChinhDangMo)
            {
                VeHUD();
            }

            // 2. Banner khu vực khi bước qua ranh giới
            if (!string.IsNullOrEmpty(ThongBaoKhuBanner))
            {
                float rong = 360f;
                float cao = 44f;
                GUI.Box(new Rect((Screen.width - rong) / 2f, 24f, rong, cao), ThongBaoKhuBanner, _styleBannerKhu);
            }

            // 3. Gợi ý phím khi đứng gần bàn
            if (!string.IsNullOrEmpty(GoiYPhim) && !BangThiNghiemDangMo && !MenuChinhDangMo)
            {
                float rong = 340f;
                float cao = 40f;
                GUI.Box(new Rect((Screen.width - rong) / 2f, Screen.height - 110f, rong, cao), GoiYPhim, _styleGoiY);
            }

            // 4. Bảng thí nghiệm tại bàn
            if (BangThiNghiemDangMo && !MenuChinhDangMo)
            {
                VeBangThiNghiem();
            }

            // 5. Menu chính
            if (MenuChinhDangMo)
            {
                if (CaiDatDangMo)
                {
                    VeMenuCaiDat();
                }
                else if (GioiThieuDangMo)
                {
                    VeHopThoaiGioiThieu();
                }
                else
                {
                    VeMenuChinh();
                }
            }
        }

        private void VeHUD()
        {
            // Góc trên bên trái: Tên khu & Nút Menu
            GUI.Box(new Rect(16, 16, 260, 75), "", _styleHopThoai);
            GUI.Label(new Rect(26, 22, 240, 24), "🏛 " + TenKhuVuc, _styleNhan);

#if UNITY_WEBGL && !UNITY_EDITOR
            if (GUI.Button(new Rect(26, 50, 140, 30), "☰ Menu (Tab/Esc)", _styleNutPhu))
#else
            if (GUI.Button(new Rect(26, 50, 110, 30), "☰ Menu (Esc)", _styleNutPhu))
#endif
            {
                MenuChinhDangMo = true;
                if (DieKhienNhanVatCoSan()) DieuKhienNhanVat.Instance.KhoaChuot(false);
            }

            // Góc trên bên phải: Đồng hồ FPS
            if (HienFPS)
            {
                GUI.Box(new Rect(Screen.width - 130, 16, 114, 40), "", _styleHopThoai);
                GUI.Label(new Rect(Screen.width - 120, 24, 100, 24), $"FPS: {Mathf.RoundToInt(_fpsCapNhat)}", _styleNhan);
            }

            // Góc dưới bên trái: Bảng phím tắt thu gọn
            if (!BangThiNghiemDangMo)
            {
                GUI.Box(new Rect(16, Screen.height - 115, 260, 95), "", _styleHopThoai);
                GUI.Label(new Rect(24, Screen.height - 110, 240, 20), "<b>Điều khiển:</b>", _styleNhan);
                GUI.Label(new Rect(24, Screen.height - 90, 240, 20), "• WASD: Di chuyển · Shift: Chạy", _styleNhan);
                GUI.Label(new Rect(24, Screen.height - 70, 240, 20), "• F: Ngôi 1/3 · V: Toàn cảnh", _styleNhan);
#if UNITY_WEBGL && !UNITY_EDITOR
                GUI.Label(new Rect(24, Screen.height - 50, 240, 20), "• Esc: Thả chuột · Tab: Menu", _styleNhan);
#else
                GUI.Label(new Rect(24, Screen.height - 50, 240, 20), "• Alt: Hiện chuột · Space: Nhảy", _styleNhan);
#endif
            }
        }

        private void VeMenuChinh()
        {
            float rong = 380f;
#if UNITY_WEBGL && !UNITY_EDITOR
            float cao = 470f;
#else
            float cao = 420f;
#endif
            Rect hop = new Rect((Screen.width - rong) / 2f, (Screen.height - cao) / 2f, rong, cao);
            GUI.Box(hop, "", _styleHopThoai);

            float y = hop.y + 24f;
            GUI.Label(new Rect(hop.x, y, rong, 32), "🧪 PHÒNG LAB HOÁ 3D", _styleTieuDe);
            y += 45f;

            if (GUI.Button(new Rect(hop.x + 40, y, rong - 80, 42), "Vào phòng lab (Tiếp tục)", _styleNutChinh))
            {
                MenuChinhDangMo = false;
                if (DieKhienNhanVatCoSan()) DieuKhienNhanVat.Instance.KhoaChuot(!BangThiNghiemDangMo);
            }
            y += 54f;

            GUI.Label(new Rect(hop.x + 40, y, rong - 80, 20), "Chọn nhanh thí nghiệm:", _styleNhan);
            y += 24f;

            string[] dsTram = { "chuan-do", "nhan-biet-ion", "pin-galvani", "dien-phan", "an-toan" };
            string[] tenTram = { "1. Chuẩn độ Acid - Base", "2. Nhận biết Ion trong dd", "3. Pin điện hoá Galvani", "4. Điện phân dung dịch", "5. An toàn phòng thí nghiệm" };

            for (int i = 0; i < dsTram.Length; i++)
            {
                if (GUI.Button(new Rect(hop.x + 40, y, rong - 80, 28), tenTram[i], _styleNutPhu))
                {
                    ChuyenToiTram(dsTram[i]);
                }
                y += 32f;
            }

            y += 10f;
            if (GUI.Button(new Rect(hop.x + 40, y, (rong - 90) / 2f, 36), "⚙ Cài đặt", _styleNutPhu))
            {
                CaiDatDangMo = true;
            }
            if (GUI.Button(new Rect(hop.x + 40 + (rong - 70) / 2f, y, (rong - 90) / 2f, 36), "ℹ Giới thiệu", _styleNutPhu))
            {
                GioiThieuDangMo = true;
            }
            y += 44f;

#if UNITY_WEBGL && !UNITY_EDITOR
            if (GUI.Button(new Rect(hop.x + 40, y, rong - 80, 36), "⚡ Mở cửa sổ mượt", _styleNutChinh))
            {
                PhongThiNghiem.HeThong.CauNoiTrang.BaoMoCuaSoMuot();
            }
            y += 44f;

            if (GUI.Button(new Rect(hop.x + 40, y, rong - 80, 36), "Thoát phòng lab", _styleNutPhu))
            {
                PhongThiNghiem.HeThong.CauNoiTrang.BaoThoat();
            }
#else
            if (GUI.Button(new Rect(hop.x + 40, y, rong - 80, 36), "Thoát ứng dụng", _styleNutPhu))
            {
                Application.Quit();
            }
#endif
        }

        private void VeMenuCaiDat()
        {
            float rong = 440f;
#if UNITY_WEBGL && !UNITY_EDITOR
            float cao = 360f;
#else
            float cao = 480f;
#endif
            Rect hop = new Rect((Screen.width - rong) / 2f, (Screen.height - cao) / 2f, rong, cao);
            GUI.Box(hop, "", _styleHopThoai);

            float y = hop.y + 20f;
            GUI.Label(new Rect(hop.x, y, rong, 30), "⚙ CÀI ĐẶT HỆ THỐNG", _styleTieuDe);
            y += 40f;

            // Đồ hoạ
            GUI.Label(new Rect(hop.x + 30, y, 160, 24), "Chất lượng đồ hoạ:", _styleNhan);
            string[] dsChatLuong = { "Thấp", "Vừa", "Cao" };
            for (int i = 0; i < dsChatLuong.Length; i++)
            {
                var style = (ChatLuong == i) ? _styleNutChinh : _styleNutPhu;
                if (GUI.Button(new Rect(hop.x + 190 + i * 75, y, 70, 26), dsChatLuong[i], style))
                {
                    ChatLuong = i;
                    ApDungCaiDat();
                }
            }
            y += 40f;

#if !UNITY_WEBGL || UNITY_EDITOR
            // Giới hạn FPS
            GUI.Label(new Rect(hop.x + 30, y, 160, 24), "Giới hạn khung hình:", _styleNhan);
            int[] dsFPS = { 30, 60, 120, -1 };
            string[] tenFPS = { "30", "60", "120", "Max" };
            for (int i = 0; i < dsFPS.Length; i++)
            {
                var style = (GioiHanFPS == dsFPS[i]) ? _styleNutChinh : _styleNutPhu;
                if (GUI.Button(new Rect(hop.x + 190 + i * 55, y, 50, 26), tenFPS[i], style))
                {
                    GioiHanFPS = dsFPS[i];
                    ApDungCaiDat();
                }
            }
            y += 40f;

            // VSync & FPS Counter
            VSync = GUI.Toggle(new Rect(hop.x + 30, y, 180, 24), VSync, " Đồng bộ dọc (VSync)");
            HienFPS = GUI.Toggle(new Rect(hop.x + 230, y, 180, 24), HienFPS, " Hiện đồng hồ FPS");
            y += 40f;
#else
            HienFPS = GUI.Toggle(new Rect(hop.x + 30, y, 180, 24), HienFPS, " Hiện đồng hồ FPS");
            y += 35f;
#endif

            // Độ nhạy chuột
            GUI.Label(new Rect(hop.x + 30, y, 180, 24), $"Độ nhạy chuột: {DoNhayChuot:F1}", _styleNhan);
            DoNhayChuot = GUI.HorizontalSlider(new Rect(hop.x + 210, y + 6, 190, 20), DoNhayChuot, 0.2f, 3.0f);
            y += 35f;

            // Góc nhìn FOV
            GUI.Label(new Rect(hop.x + 30, y, 180, 24), $"Góc nhìn FOV: {Mathf.RoundToInt(FOV)}°", _styleNhan);
            FOV = GUI.HorizontalSlider(new Rect(hop.x + 210, y + 6, 190, 20), FOV, 50f, 90f);
            y += 35f;

            // Âm lượng
            GUI.Label(new Rect(hop.x + 30, y, 180, 24), $"Âm lượng: {Mathf.RoundToInt(AmLuong)}%", _styleNhan);
            AmLuong = GUI.HorizontalSlider(new Rect(hop.x + 210, y + 6, 190, 20), AmLuong, 0f, 100f);
            y += 45f;

            // Nút Lưu / Quay lại
            if (GUI.Button(new Rect(hop.x + 50, y, 150, 36), "Lưu cài đặt", _styleNutChinh))
            {
                LuuCaiDatVaoPlayerPrefs();
                ApDungCaiDat();
                CaiDatDangMo = false;
            }
            if (GUI.Button(new Rect(hop.x + 230, y, 150, 36), "Đóng", _styleNutPhu))
            {
                CaiDatDangMo = false;
            }
        }

        private void VeHopThoaiGioiThieu()
        {
            float rong = 480f;
            float cao = 410f;
            Rect hop = new Rect((Screen.width - rong) / 2f, (Screen.height - cao) / 2f, rong, cao);
            GUI.Box(hop, "", _styleHopThoai);

            float y = hop.y + 20f;
            GUI.Label(new Rect(hop.x, y, rong, 30), "ℹ GIỚI THIỆU PHÒNG LAB", _styleTieuDe);
            y += 42f;

            GUI.Label(new Rect(hop.x + 24, y, rong - 48, 22), "<b>Phòng Thí Nghiệm Hoá Học 3D</b>", _styleNhan);
            y += 24f;
            GUI.Label(new Rect(hop.x + 24, y, rong - 48, 22), "Phiên bản: 2.0 (Unity 6 URP Standalone Desktop)", _styleNhan);
            y += 24f;
            GUI.Label(new Rect(hop.x + 24, y, rong - 48, 44), "Mô phỏng phản ứng hoá học chính xác thời gian thực, tích hợp Jint JavaScript Engine đồng bộ với lõi Web.", _styleNhan);
            y += 48f;
            GUI.Label(new Rect(hop.x + 24, y, rong - 48, 22), "Thực hiện bởi: Đội ngũ Antigravity & AI Pair Programming.", _styleNhan);
            y += 24f;
            GUI.Label(new Rect(hop.x + 24, y, rong - 48, 22), "Mô hình 3D: Nghệ sĩ Sketchfab (CC BY 4.0 / CC0). Chi tiết tại GHI_CONG.md.", _styleNhan);
            y += 24f;
            GUI.Label(new Rect(hop.x + 24, y, rong - 48, 22), "Bản quyền & Giấy phép: MIT License / Creative Commons.", _styleNhan);
            y += 45f;

            if (GUI.Button(new Rect(hop.x + (rong - 140) / 2f, y, 140, 36), "Đóng", _styleNutChinh))
            {
                GioiThieuDangMo = false;
            }
        }

        #endregion

        #region Render Bảng Thí Nghiệm Tại Bàn (Khớp 100% BANG[id])

        private void VeBangThiNghiem()
        {
            float rong = 420f;
            float cao = Screen.height - 60f;
            Rect hop = new Rect(Screen.width - rong - 20, 30, rong, cao);
            GUI.Box(hop, "", _styleHopThoai);

            float y = hop.y + 16f;

            // Tiêu đề trạm + Nút rời bàn [Q]
            GUI.Label(new Rect(hop.x + 16, y, rong - 110, 26), LayTenTram(TramHienTai), _styleTieuDe);
            if (GUI.Button(new Rect(hop.x + rong - 90, y, 74, 28), "Rời bàn (Q)", _styleNutPhu))
            {
                if (DieuKhienNhanVat.Instance != null) DieuKhienNhanVat.Instance.RoiBan();
            }
            y += 34f;

            // 3 Tab điều hướng chức năng tại bàn
            float tabW = (rong - 32f) / 3f;
            if (GUI.Button(new Rect(hop.x + 16, y, tabW, 28), "Thao tác", _tabBanHienTai == 0 ? _styleNutChinh : _styleNutPhu)) _tabBanHienTai = 0;
            if (GUI.Button(new Rect(hop.x + 16 + tabW, y, tabW, 28), "Ôn thi THPT", _tabBanHienTai == 1 ? _styleNutChinh : _styleNutPhu)) _tabBanHienTai = 1;
            if (GUI.Button(new Rect(hop.x + 16 + tabW * 2, y, tabW, 28), "Thông tin", _tabBanHienTai == 2 ? _styleNutChinh : _styleNutPhu)) _tabBanHienTai = 2;
            y += 34f;

            if (_tabBanHienTai == 1)
            {
                VeTabOnThiTHPT(hop, ref y);
                return;
            }

            if (_tabBanHienTai == 2)
            {
                VeTabThongTin(hop, ref y);
                return;
            }

            switch (TramHienTai)
            {
                case "chuan-do":
                    VeBangChuanDo(hop, ref y);
                    break;
                case "nhan-biet-ion":
                    VeBangNhanBietIon(hop, ref y);
                    break;
                case "pin-galvani":
                    VeBangPinGalvani(hop, ref y);
                    break;
                case "dien-phan":
                    VeBangDienPhan(hop, ref y);
                    break;
                case "an-toan":
                    VeBangAnToan(hop, ref y);
                    break;
                default:
                    GUI.Label(new Rect(hop.x + 20, y, rong - 40, 30), "Đang chuẩn bị thí nghiệm...", _styleNhan);
                    break;
            }
        }

        private void VeTabOnThiTHPT(Rect hop, ref float y)
        {
            float rong = hop.width - 32f;
            float x = hop.x + 16f;

            string id = TramHienTai;
            if (id == "nhan-biet-ion") id = "nhan-biet";

            var ds = CauHoiOnThiTHPT.LayCauHoi(id);
            if (ds == null || ds.Count == 0)
            {
                GUI.Label(new Rect(x, y, rong, 24), "Không có câu hỏi ôn thi cho trạm này.", _styleNhan);
                return;
            }

            if (_cauHoiHienTai >= ds.Count) _cauHoiHienTai = 0;
            var ch = ds[_cauHoiHienTai];

            GUI.Label(new Rect(x, y, rong, 20), $"<b>Câu {_cauHoiHienTai + 1}/{ds.Count}</b> ({ch.NguonDeThi})", _styleNhan);
            y += 24f;

            // Nội dung câu hỏi
            GUI.Label(new Rect(x, y, rong, 56), ch.NoiDung, _styleNhan);
            y += 60f;

            // 4 Lựa chọn A, B, C, D
            for (int i = 0; i < ch.LuaChon.Length; i++)
            {
                bool daChon = (_luaChonHocSinh == i);
                GUIStyle styleNut = _styleNutPhu;

                if (_daTraLoi)
                {
                    if (i == ch.DapAnDung) GUI.backgroundColor = new Color(0.2f, 0.8f, 0.4f); // Xanh lá
                    else if (daChon) GUI.backgroundColor = new Color(0.9f, 0.3f, 0.3f);     // Đỏ
                }
                else if (daChon)
                {
                    styleNut = _styleNutChinh;
                }

                if (GUI.Button(new Rect(x, y, rong, 34), ch.LuaChon[i], styleNut))
                {
                    if (!_daTraLoi)
                    {
                        _luaChonHocSinh = i;
                        _daTraLoi = true;
                        if (i == ch.DapAnDung) _soCauDung++;
                    }
                }
                GUI.backgroundColor = Color.white;
                y += 38f;
            }

            // Giải thích chi tiết sau khi trả lời
            if (_daTraLoi)
            {
                y += 4f;
                bool dung = (_luaChonHocSinh == ch.DapAnDung);
                string kqText = dung ? "✔ <b>CHÍNH XÁC!</b>" : "✖ <b>CHƯA CHÍNH XÁC!</b>";
                GUI.color = dung ? new Color(0.3f, 0.9f, 0.4f) : new Color(1.0f, 0.4f, 0.4f);
                GUI.Label(new Rect(x, y, rong, 20), kqText, _styleNhan);
                GUI.color = Color.white;
                y += 22f;

                GUI.Label(new Rect(x, y, rong, 60), "<b>Giải thích:</b> " + ch.GiaiThich, _styleNhan);
                y += 64f;

                if (GUI.Button(new Rect(x, y, rong * 0.48f, 32), "Câu tiếp theo ➜", _styleNutChinh))
                {
                    _cauHoiHienTai = (_cauHoiHienTai + 1) % ds.Count;
                    _luaChonHocSinh = -1;
                    _daTraLoi = false;
                }

                if (GUI.Button(new Rect(x + rong * 0.52f, y, rong * 0.48f, 32), "↺ Làm lại", _styleNutPhu))
                {
                    _luaChonHocSinh = -1;
                    _daTraLoi = false;
                }
                y += 38f;
            }
        }

        private void VeTabThongTin(Rect hop, ref float y)
        {
            float rong = hop.width - 32f;
            float x = hop.x + 16f;

            string id = TramHienTai;
            if (id == "nhan-biet-ion") id = "nhan-biet";

            switch (id)
            {
                case "chuan-do":
                    GUI.Label(new Rect(x, y, rong, 20), "<b>MỤC TIÊU BÀI HỌC (CT GDPT 2018):</b>", _styleNhan); y += 22f;
                    GUI.Label(new Rect(x, y, rong, 40), "Xác định nồng độ dung dịch acid mạnh (HCl) bằng chuẩn độ thể tích với dung dịch chuẩn NaOH 0.100 M.", _styleNhan); y += 44f;
                    GUI.Label(new Rect(x, y, rong, 20), "<b>PHƯƠNG TRÌNH HOÁ HỌC:</b>", _styleNhan); y += 22f;
                    GUI.Label(new Rect(x, y, rong, 22), "HCl + NaOH ➔ NaCl + H₂O", _styleTieuDe); y += 28f;
                    GUI.Label(new Rect(x, y, rong, 20), "<b>CHỈ THỊ PHENOLPHTHALEIN:</b>", _styleNhan); y += 22f;
                    GUI.Label(new Rect(x, y, rong, 36), "Không màu (pH < 8.2) ➔ Hồng nhạt (pH ≥ 8.3). Bước nhảy pH điểm tương đương từ 4.30 đến 9.70.", _styleNhan); y += 40f;
                    break;

                case "nhan-biet":
                    GUI.Label(new Rect(x, y, rong, 20), "<b>MỤC TIÊU BÀI HỌC (CT GDPT 2018):</b>", _styleNhan); y += 22f;
                    GUI.Label(new Rect(x, y, rong, 40), "Nhận biết các cation kim loại trong dung dịch bằng phản ứng tạo kết tủa màu đặc trưng và thử ngọn lửa.", _styleNhan); y += 44f;
                    GUI.Label(new Rect(x, y, rong, 20), "<b>PHƯƠNG TRÌNH PHẢN ỨNG ĐẶC TRƯNG:</b>", _styleNhan); y += 22f;
                    GUI.Label(new Rect(x, y, rong, 20), "• Fe³⁺ + 3OH⁻ ➔ Fe(OH)₃ ↓ (nâu đỏ)", _styleNhan); y += 20f;
                    GUI.Label(new Rect(x, y, rong, 20), "• Fe²⁺ + 2OH⁻ ➔ Fe(OH)₂ ↓ (trắng xanh) ➔ Fe(OH)₃ (nâu đỏ)", _styleNhan); y += 20f;
                    GUI.Label(new Rect(x, y, rong, 20), "• Cu²⁺ + 2OH⁻ ➔ Cu(OH)₂ ↓ (xanh lam) ➔ [Cu(NH₃)₄]²⁺ (xanh thẫm)", _styleNhan); y += 20f;
                    GUI.Label(new Rect(x, y, rong, 20), "• Al³⁺ + 3OH⁻ ➔ Al(OH)₃ ↓ (keo trắng, tan trong OH⁻ dư)", _styleNhan); y += 26f;
                    break;

                case "pin-galvani":
                    GUI.Label(new Rect(x, y, rong, 20), "<b>MỤC TIÊU BÀI HỌC (CT GDPT 2018):</b>", _styleNhan); y += 22f;
                    GUI.Label(new Rect(x, y, rong, 40), "Thiết lập pin điện hoá Zn - Cu, đo sức điện động chuẩn E°pin và tìm hiểu bản chất chuyển dịch electron.", _styleNhan); y += 44f;
                    GUI.Label(new Rect(x, y, rong, 20), "<b>PHẢN ỨNG TOÀN PHẦN CỦA PIN:</b>", _styleNhan); y += 22f;
                    GUI.Label(new Rect(x, y, rong, 22), "Zn(s) + Cu²⁺(aq) ➔ Zn²⁺(aq) + Cu(s)", _styleTieuDe); y += 28f;
                    GUI.Label(new Rect(x, y, rong, 20), "• Anode (-) Zn: Zn ➔ Zn²⁺ + 2e (quá trình oxi hoá)", _styleNhan); y += 20f;
                    GUI.Label(new Rect(x, y, rong, 20), "• Cathode (+) Cu: Cu²⁺ + 2e ➔ Cu (quá trình khử)", _styleNhan); y += 20f;
                    GUI.Label(new Rect(x, y, rong, 20), "• Sức điện động chuẩn: E°pin = +1.10 V", _styleNhan); y += 26f;
                    break;

                case "dien-phan":
                    GUI.Label(new Rect(x, y, rong, 20), "<b>MỤC TIÊU BÀI HỌC (CT GDPT 2018):</b>", _styleNhan); y += 22f;
                    GUI.Label(new Rect(x, y, rong, 40), "Khảo sát định luật Faraday và quá trình oxi hoá khử trên điện cực trơ khi điện phân dung dịch CuSO₄.", _styleNhan); y += 44f;
                    GUI.Label(new Rect(x, y, rong, 20), "<b>PHƯƠNG TRÌNH ĐIỆN PHÂN:</b>", _styleNhan); y += 22f;
                    GUI.Label(new Rect(x, y, rong, 22), "2CuSO₄ + 2H₂O ➔ 2Cu ↓ + O₂ ↑ + 2H₂SO₄", _styleTieuDe); y += 28f;
                    GUI.Label(new Rect(x, y, rong, 20), "• Cathode (-): Cu²⁺ + 2e ➔ Cu ↓ | Anode (+): 2H₂O ➔ O₂ ↑ + 4H⁺ + 4e", _styleNhan); y += 20f;
                    GUI.Label(new Rect(x, y, rong, 20), "• Định luật Faraday: m = (A · I · t) / (n · F)", _styleNhan); y += 26f;
                    break;

                case "an-toan":
                default:
                    GUI.Label(new Rect(x, y, rong, 20), "<b>QUY TẮC AN TOÀN PHÒNG THÍ NGHIỆM:</b>", _styleNhan); y += 22f;
                    GUI.Label(new Rect(x, y, rong, 36), "1. Luôn mang kính bảo hộ và áo blouse trong suốt thời gian làm thí nghiệm.", _styleNhan); y += 38f;
                    GUI.Label(new Rect(x, y, rong, 36), "2. Pha loãng H₂SO₄ đặc: LUÔN rót từ từ axit vào nước, KHÔNG BAO GIỜ làm ngược lại.", _styleNhan); y += 38f;
                    GUI.Label(new Rect(x, y, rong, 36), "3. Khi dính hoá chất: rửa ngay lập tức dưới vòi nước chảy liên tục ít nhất 15 phút.", _styleNhan); y += 38f;
                    break;
            }
        }

        private string LayTenTram(string id)
        {
            switch (id)
            {
                case "chuan-do": return "Chuẩn độ Acid - Base";
                case "nhan-biet-ion": return "Nhận biết Ion";
                case "pin-galvani": return "Pin Galvani";
                case "dien-phan": return "Điện phân dung dịch";
                case "an-toan": return "An toàn phòng Lab";
                default: return "Bàn thí nghiệm";
            }
        }

        // 1. CHUẨN ĐỘ
        private void VeBangChuanDo(Rect hop, ref float y)
        {
            float rong = hop.width - 32f;
            float x = hop.x + 16f;

            GUI.Label(new Rect(x, y, rong, 20), "<b>1. Dung dịch acid (20.00 mL):</b>", _styleNhan);
            y += 22f;
            string[] dsBai = { "HCl", "CH3COOH", "H2SO4" };
            string[] maBai = { "hcl_naoh", "ch3cooh_naoh", "h2so4_naoh" };
            for (int i = 0; i < dsBai.Length; i++)
            {
                var st = (_cdBaiChon == i) ? _styleNutChinh : _styleNutPhu;
                if (GUI.Button(new Rect(x + i * 90, y, 82, 26), dsBai[i], st))
                {
                    _cdBaiChon = i;
                    GuiLenhLoi("chuan-do", "chon_bai", $"{{\"id\":\"{maBai[i]}\"}}");
                }
            }
            y += 34f;

            GUI.Label(new Rect(x, y, rong, 20), "<b>2. Chỉ thị màu:</b>", _styleNhan);
            y += 22f;
            string[] dsCt = { "Không", "Phenolphthalein", "Methyl orange" };
            string[] maCt = { null, "phenolphthalein", "methyl_orange" };
            for (int i = 0; i < dsCt.Length; i++)
            {
                var st = (_cdChiThiChon == i) ? _styleNutChinh : _styleNutPhu;
                if (GUI.Button(new Rect(x + i * 125, y, 118, 26), dsCt[i], st))
                {
                    _cdChiThiChon = i;
                    string val = maCt[i] == null ? "null" : $"\"{maCt[i]}\"";
                    GuiLenhLoi("chuan-do", "chon_chi_thi", $"{{\"id\":{val}}}");
                }
            }
            y += 36f;

            GUI.Label(new Rect(x, y, rong, 20), "<b>3. Điều khiển Burette NaOH 0.100 M:</b>", _styleNhan);
            y += 22f;
            if (GUI.Button(new Rect(x, y, 90, 30), "Chảy nhanh", _styleNutPhu))
            {
                GuiLenhLoi("chuan-do", "mo_khoa", "{\"kieu\":\"nhanh\"}");
            }
            if (GUI.Button(new Rect(x + 96, y, 90, 30), "Từng giọt", _styleNutPhu))
            {
                GuiLenhLoi("chuan-do", "mo_khoa", "{\"kieu\":\"giot\"}");
            }
            if (GUI.Button(new Rect(x + 192, y, 90, 30), "Đóng khoá", _styleNutChinh))
            {
                GuiLenhLoi("chuan-do", "dong_khoa", "{}");
            }
            y += 36f;

            if (GUI.Button(new Rect(x, y, 90, 28), "+1 giọt", _styleNutPhu))
            {
                GuiLenhLoi("chuan-do", "nho_giot", "{}");
            }
            if (GUI.Button(new Rect(x + 96, y, 90, 28), "+1 mL", _styleNutPhu))
            {
                GuiLenhLoi("chuan-do", "them", "{\"ml\":1}");
            }
            if (GUI.Button(new Rect(x + 192, y, 90, 28), "Lắc bình", _styleNutPhu))
            {
                GuiLenhLoi("chuan-do", "lac", "{}");
            }
            y += 38f;

            GUI.Label(new Rect(x, y, rong, 20), "<b>4. Đọc burette (mL):</b>", _styleNhan);
            y += 22f;
            _cdGiaTriDoc = GUI.TextField(new Rect(x, y, 120, 26), _cdGiaTriDoc);
            if (GUI.Button(new Rect(x + 130, y, 110, 26), "Ghi số đọc", _styleNutPhu))
            {
                GuiLenhLoi("chuan-do", "doc_buret", $"{{\"gia_tri\":\"{_cdGiaTriDoc}\"}}");
            }
            y += 34f;

            GUI.Label(new Rect(x, y, rong, 20), "<b>5. Tính nồng độ acid C(M):</b>", _styleNhan);
            y += 22f;
            _cdNongDoTinh = GUI.TextField(new Rect(x, y, 120, 26), _cdNongDoTinh);
            if (GUI.Button(new Rect(x + 130, y, 110, 26), "Kiểm tra", _styleNutChinh))
            {
                GuiLenhLoi("chuan-do", "tinh_nong_do", $"{{\"C\":\"{_cdNongDoTinh}\"}}");
            }
            y += 36f;

            // Chế độ ôn thi
            bool onThiMoi = GUI.Toggle(new Rect(x, y, 180, 22), _cdCheDoOnThi, " Chế độ Ôn thi (ẩn pH)");
            if (onThiMoi != _cdCheDoOnThi)
            {
                _cdCheDoOnThi = onThiMoi;
                GuiLenhLoi("chuan-do", _cdCheDoOnThi ? "bat_on_thi" : "tat_on_thi", "{}");
            }
        }

        // 2. NHẬN BIẾT ION
        private void VeBangNhanBietIon(Rect hop, ref float y)
        {
            float rong = hop.width - 32f;
            float x = hop.x + 16f;

            // Tab chọn: Ống nghiệm hoặc Ngọn lửa
            if (GUI.Button(new Rect(x, y, 130, 28), "🧪 Ống nghiệm", _nbTabChon == "ong" ? _styleNutChinh : _styleNutPhu))
            {
                _nbTabChon = "ong";
                GuiLenhLoi("nhan-biet-ion", "chon_tab", "{\"tab\":\"ong\"}");
            }
            if (GUI.Button(new Rect(x + 140, y, 130, 28), "🔥 Thử ngọn lửa", _nbTabChon == "ngon_lua" ? _styleNutChinh : _styleNutPhu))
            {
                _nbTabChon = "ngon_lua";
                GuiLenhLoi("nhan-biet-ion", "chon_tab", "{\"tab\":\"ngon_lua\"}");
            }
            y += 36f;

            if (_nbTabChon == "ong")
            {
                GUI.Label(new Rect(x, y, rong, 20), "<b>Chọn ống nghiệm:</b>", _styleNhan);
                y += 22f;
                for (int i = 1; i <= 4; i++)
                {
                    var st = (_nbOngChon == i) ? _styleNutChinh : _styleNutPhu;
                    if (GUI.Button(new Rect(x + (i - 1) * 65, y, 58, 26), $"Ống {i}", st))
                    {
                        _nbOngChon = i;
                    }
                }
                y += 34f;

                GUI.Label(new Rect(x, y, rong, 20), "<b>Chọn thuốc thử:</b>", _styleNhan);
                y += 22f;
                string[] dsTt = { "NaOH", "NH3", "HCl", "BaCl2", "AgNO3" };
                for (int i = 0; i < dsTt.Length; i++)
                {
                    var st = (_nbThuocThuChon == dsTt[i]) ? _styleNutChinh : _styleNutPhu;
                    if (GUI.Button(new Rect(x + i * 55, y, 50, 26), dsTt[i], st))
                    {
                        _nbThuocThuChon = dsTt[i];
                    }
                }
                y += 34f;

                GUI.Label(new Rect(x, y, rong, 20), "<b>Thao tác:</b>", _styleNhan);
                y += 22f;
                if (GUI.Button(new Rect(x, y, 90, 28), "Nhỏ vài giọt", _styleNutChinh))
                {
                    GuiLenhLoi("nhan-biet-ion", "nho", $"{{\"so\":{_nbOngChon},\"thuoc_thu\":\"{_nbThuocThuChon}\",\"kieu\":\"giot\"}}");
                }
                if (GUI.Button(new Rect(x + 96, y, 80, 28), "Nhỏ dư", _styleNutPhu))
                {
                    GuiLenhLoi("nhan-biet-ion", "nho", $"{{\"so\":{_nbOngChon},\"thuoc_thu\":\"{_nbThuocThuChon}\",\"kieu\":\"du\"}}");
                }
                if (GUI.Button(new Rect(x + 182, y, 80, 28), "Đun nóng", _styleNutPhu))
                {
                    GuiLenhLoi("nhan-biet-ion", "dun", $"{{\"so\":{_nbOngChon}}}");
                }
                if (GUI.Button(new Rect(x + 268, y, 80, 28), "Thử quỳ", _styleNutPhu))
                {
                    GuiLenhLoi("nhan-biet-ion", "thu_quy", $"{{\"so\":{_nbOngChon}}}");
                }
                y += 38f;

                if (GUI.Button(new Rect(x, y, 120, 30), "Kiểm tra kết quả", _styleNutChinh))
                {
                    GuiLenhLoi("nhan-biet-ion", "kiem_tra", "{}");
                }
                if (GUI.Button(new Rect(x + 130, y, 110, 30), "Bộ ống mới", _styleNutPhu))
                {
                    GuiLenhLoi("nhan-biet-ion", "bo_moi", "{}");
                }
            }
            else
            {
                GUI.Label(new Rect(x, y, rong, 20), "<b>Nhúng que platinum vào muối:</b>", _styleNhan);
                y += 22f;
                string[] dsKl = { "Na", "K", "Ca", "Ba", "Cu" };
                for (int i = 0; i < dsKl.Length; i++)
                {
                    if (GUI.Button(new Rect(x + i * 55, y, 50, 28), dsKl[i], _styleNutPhu))
                    {
                        GuiLenhLoi("nhan-biet-ion", "thu_lua", $"{{\"kim_loai\":\"{dsKl[i]}\"}}");
                    }
                }
                y += 38f;
                if (GUI.Button(new Rect(x, y, 120, 30), "Rút dây ra", _styleNutChinh))
                {
                    GuiLenhLoi("nhan-biet-ion", "tat_lua", "{}");
                }
            }
        }

        // 3. PIN GALVANI
        private void VeBangPinGalvani(Rect hop, ref float y)
        {
            float rong = hop.width - 32f;
            float x = hop.x + 16f;

            GUI.Label(new Rect(x, y, rong, 20), "<b>1. Chọn điện cực:</b>", _styleNhan);
            y += 22f;
            GUI.Label(new Rect(x, y, 80, 24), "Cốc trái:", _styleNhan);
            string[] dsKl = { "Zn", "Fe", "Al", "Cu", "Ag" };
            for (int i = 0; i < dsKl.Length; i++)
            {
                var st = (_pgKimLoaiTrai == dsKl[i]) ? _styleNutChinh : _styleNutPhu;
                if (GUI.Button(new Rect(x + 70 + i * 45, y, 40, 24), dsKl[i], st))
                {
                    _pgKimLoaiTrai = dsKl[i];
                    GuiLenhLoi("pin-galvani", "chon_kim_loai", $"{{\"ben\":\"trai\",\"kim_loai\":\"{dsKl[i]}\"}}");
                }
            }
            y += 30f;

            GUI.Label(new Rect(x, y, 80, 24), "Cốc phải:", _styleNhan);
            for (int i = 0; i < dsKl.Length; i++)
            {
                var st = (_pgKimLoaiPhai == dsKl[i]) ? _styleNutChinh : _styleNutPhu;
                if (GUI.Button(new Rect(x + 70 + i * 45, y, 40, 24), dsKl[i], st))
                {
                    _pgKimLoaiPhai = dsKl[i];
                    GuiLenhLoi("pin-galvani", "chon_kim_loai", $"{{\"ben\":\"phai\",\"kim_loai\":\"{dsKl[i]}\"}}");
                }
            }
            y += 36f;

            GUI.Label(new Rect(x, y, rong, 20), "<b>2. Thao tác Cầu muối:</b>", _styleNhan);
            y += 22f;
            string nutCau = _pgCauMuoi ? "Rút cầu muối (Ngắt mạch)" : "Lắp cầu muối";
            if (GUI.Button(new Rect(x, y, 200, 30), nutCau, _styleNutChinh))
            {
                _pgCauMuoi = !_pgCauMuoi;
                GuiLenhLoi("pin-galvani", _pgCauMuoi ? "lap_cau_muoi" : "rut_cau_muoi", "{}");
            }
            if (GUI.Button(new Rect(x + 210, y, 100, 30), "Ghi số đo", _styleNutPhu))
            {
                GuiLenhLoi("pin-galvani", "ghi_so_do", "{}");
            }
            y += 40f;

            GUI.Label(new Rect(x, y, rong, 20), "<b>3. Nồng độ ion cốc trái (Nernst):</b>", _styleNhan);
            y += 22f;
            _pgNongDoTrai = GUI.HorizontalSlider(new Rect(x, y, 220, 20), _pgNongDoTrai, 0.001f, 1.0f);
            GUI.Label(new Rect(x + 230, y - 2, 80, 20), $"{_pgNongDoTrai:F3} M", _styleNhan);
            y += 28f;

            GUI.Label(new Rect(x, y, rong, 20), "<b>4. Nồng độ ion cốc phải:</b>", _styleNhan);
            y += 22f;
            _pgNongDoPhai = GUI.HorizontalSlider(new Rect(x, y, 220, 20), _pgNongDoPhai, 0.001f, 1.0f);
            GUI.Label(new Rect(x + 230, y - 2, 80, 20), $"{_pgNongDoPhai:F3} M", _styleNhan);
        }

        // 4. ĐIỆN PHÂN
        private void VeBangDienPhan(Rect hop, ref float y)
        {
            float rong = hop.width - 32f;
            float x = hop.x + 16f;

            GUI.Label(new Rect(x, y, rong, 20), "<b>1. Chọn bài điện phân:</b>", _styleNhan);
            y += 22f;
            string[] dsBai = { "CuSO4 trơ", "NaCl màng ngăn", "CuSO4 anode Cu" };
            string[] maBai = { "cuso4_tro", "nacl_mang_ngan", "cuso4_anode_cu" };
            for (int i = 0; i < dsBai.Length; i++)
            {
                var st = (_dpBaiChon == maBai[i]) ? _styleNutChinh : _styleNutPhu;
                if (GUI.Button(new Rect(x, y, 160, 24), dsBai[i], st))
                {
                    _dpBaiChon = maBai[i];
                    GuiLenhLoi("dien-phan", "chon_bai", $"{{\"id\":\"{maBai[i]}\"}}");
                }
                y += 26f;
            }
            y += 6f;

            GUI.Label(new Rect(x, y, rong, 20), $"<b>2. Dòng điện I = {_dpI:F1} A:</b>", _styleNhan);
            y += 22f;
            _dpI = GUI.HorizontalSlider(new Rect(x, y, 220, 20), _dpI, 1.0f, 5.0f);
            y += 26f;

            GUI.Label(new Rect(x, y, rong, 20), "<b>3. Thời gian t (giây):</b>", _styleNhan);
            y += 22f;
            int[] dsT = { 965, 1930, 3860 };
            for (int i = 0; i < dsT.Length; i++)
            {
                var st = (_dpT == dsT[i]) ? _styleNutChinh : _styleNutPhu;
                if (GUI.Button(new Rect(x + i * 80, y, 72, 26), $"{dsT[i]} s", st))
                {
                    _dpT = dsT[i];
                    GuiLenhLoi("dien-phan", "dat_t", $"{{\"t\":{dsT[i]}}}");
                }
            }
            y += 36f;

            // Nguồn điện & Tua nhanh
            string nutNguon = _dpDangChay ? "Tắt nguồn" : "Bật nguồn điện";
            if (GUI.Button(new Rect(x, y, 130, 30), nutNguon, _styleNutChinh))
            {
                _dpDangChay = !_dpDangChay;
                GuiLenhLoi("dien-phan", _dpDangChay ? "bat_nguon" : "tat_nguon", "{}");
            }

            int[] dsTua = { 1, 5, 10, 50 };
            for (int i = 0; i < dsTua.Length; i++)
            {
                var st = (_dpTocDo == dsTua[i]) ? _styleNutChinh : _styleNutPhu;
                if (GUI.Button(new Rect(x + 140 + i * 42, y, 38, 30), $"×{dsTua[i]}", st))
                {
                    _dpTocDo = dsTua[i];
                    GuiLenhLoi("dien-phan", "tua", $"{{\"toc_do\":{dsTua[i]}}}");
                }
            }
            y += 40f;

            GUI.Label(new Rect(x, y, rong, 20), "<b>4. Đối chiếu Faraday:</b>", _styleNhan);
            y += 22f;
            _dpGiaTriDoiChieu = GUI.TextField(new Rect(x, y, 110, 26), _dpGiaTriDoiChieu);
            if (GUI.Button(new Rect(x + 120, y, 100, 26), "Kiểm tra", _styleNutPhu))
            {
                GuiLenhLoi("dien-phan", "doi_chieu", $"{{\"gia_tri\":\"{_dpGiaTriDoiChieu}\"}}");
            }
        }

        // 5. AN TOÀN
        private void VeBangAnToan(Rect hop, ref float y)
        {
            float rong = hop.width - 32f;
            float x = hop.x + 16f;

            GUI.Label(new Rect(x, y, rong, 20), "<b>Nhận diện 9 biểu tượng nguy hại GHS:</b>", _styleNhan);
            y += 24f;
            string[] dsGhs = { "GHS01 Nổ", "GHS02 Cháy", "GHS03 Ôxi hoá", "GHS04 Khí nén", "GHS05 Ăn mòn", "GHS06 Độc cấp", "GHS07 Kích ứng", "GHS08 Mãn tính", "GHS09 Môi trường" };
            for (int i = 0; i < dsGhs.Length; i++)
            {
                int hang = i / 2;
                int cot = i % 2;
                GUI.Label(new Rect(x + cot * 160, y + hang * 22, 155, 20), "• " + dsGhs[i], _styleNhan);
            }
            y += 115f;

            GUI.Label(new Rect(x, y, rong, 20), "<b>Quy trình xử lý sự cố hoá chất:</b>", _styleNhan);
            y += 24f;
            string[] dsSuCo = { "Axit dính vào da: Rửa ngay bằng nước chảy 15 phút, rửa dung dịch NaHCO3 2%.", "Bỏng kiềm: Rửa nước nhiều lần, rửa bằng axit axetic 1% hoặc chanh loãng.", "Cháy photpho: Dùng cát ẩm hoặc bình cứu hoả bột, không dùng nước." };
            for (int i = 0; i < dsSuCo.Length; i++)
            {
                GUI.Label(new Rect(x, y, rong, 38), $"• {dsSuCo[i]}", _styleNhan);
                y += 40f;
            }
        }

        private void GuiLenhLoi(string idTram, string lenh, string thamSoJson)
        {
            if (QuanLyTram.Instance != null)
            {
                QuanLyTram.Instance.GuiThaoTac(lenh, thamSoJson);
            }
        }

        private bool DieKhienNhanVatCoSan()
        {
            return DieuKhienNhanVat.Instance != null;
        }

        #endregion
    }
}
