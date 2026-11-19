using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Khkt.Environment;
using Khkt.Rendering;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Khkt.CoChe
{
    /// <summary>
    /// Kiểm thử mô-đun cơ chế trong PlayMode (batchmode, có đồ hoạ) – gọi từ Editor/KhktCoCheVerify.Run:
    ///   giải gói thử do Python tạo + 8 gói thật; từ chối gói sửa 1 byte; phát hết các bước ở mức Thấp và Cao; chuyển 3D ⇄ Giấy;
    ///   nhánh sai có màn chắn; bảng giải chuỗi và Hỏi AI với máy chủ giả; nút Hỏi AI mờ khi không có máy chủ; đo FPS bài
    ///   propellane ở mức Cao; bảng "Đề mới": chọn ảnh (hộp chọn giả), Phân tích (máy chủ giả trả JSON bài thường) mở bài như bài mẫu,
    ///   lỗi 422 hiện thông điệp máy chủ; chụp ảnh từng bài ở cả hai chế độ vào Path.GetTempPath()/vera_coche_shots. Log [CC_PASS]/[CC_FAIL].
    /// </summary>
    public class KhktCoCheRunner : MonoBehaviour
    {
        const int ShotW = 1920, ShotH = 1080;
        int passes, failures, runtimeErrors, shots;
        readonly List<string> failed = new List<string>(), errLines = new List<string>();
        string outDir;
        RenderTexture rt;
        Texture2D readTex;
        public float FpsDo { get; private set; }

        void Awake()
        {
            DontDestroyOnLoad(gameObject);
            outDir = System.Environment.GetEnvironmentVariable("KHKT_COCHE_SHOTS");
            if (string.IsNullOrEmpty(outDir)) outDir = Path.Combine(Path.GetTempPath(), "vera_coche_shots");
            Directory.CreateDirectory(outDir);
            Application.runInBackground = true;
            Application.logMessageReceived += OnLog;
        }

        void OnDestroy() => Application.logMessageReceived -= OnLog;

        void OnLog(string c, string st, LogType t)
        {
            if (t != LogType.Error && t != LogType.Exception && t != LogType.Assert) return;
            if (c != null && (c.StartsWith("[CC_") || c.StartsWith("[W6_"))) return;
            if (!string.IsNullOrEmpty(st) && st.Contains("SearchInit.IndexationOnStartup")) return;
            runtimeErrors++;
            errLines.Add($"{t}: {c} | {(st ?? "").Split('\n').FirstOrDefault()}");
        }

        void Check(bool ok, string ten, string ct = "")
        {
            if (ok) { passes++; Debug.Log($"[CC_PASS] {ten}" + (ct == "" ? "" : " | " + ct)); }
            else { failures++; failed.Add(ten); Debug.LogError($"[CC_FAIL] {ten}" + (ct == "" ? "" : " | " + ct)); }
        }

        static IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }

        IEnumerator Start()
        {
            StartCoroutine(Watchdog(420f));
            rt = new RenderTexture(ShotW, ShotH, 24, RenderTextureFormat.ARGB32);
            rt.Create();
            readTex = new Texture2D(ShotW, ShotH, TextureFormat.RGBA32, false);
            var mucCu = HardwareProfile.Muc;
            bool tuDongCu = HardwareProfile.TuDong;
            string urlCu = PlayerPrefs.GetString(MayChuCoChe.KhoaUrl, "");
            yield return Frames(30);
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 20f && FindAnyObjectByType<DesktopViewer>() == null) yield return null;
            yield return Frames(10);

            // 1. gói: gói thử cố định của Python, sửa 1 byte bị từ chối
            TestGoi();

            // 2. mở mô-đun với máy chủ giả
            MayChuCoChe.GiaLap = MayChuGia;
            int uiCapCu = DesktopViewer.UiCapture;
            CoCheMoDun.Mo();
            yield return Frames(5);
            var md = CoCheMoDun.Instance;
            Check(CoCheMoDun.DangMo && md != null, "Mo() mở lớp phủ cơ chế");
            Check(DesktopViewer.UiCapture == uiCapCu + 1, "Mở lớp phủ khoá điều khiển lab (UiCapture+1)");
            md.Cam.targetTexture = rt;                                     // canvas + 3D dựng theo khung 1920×1080
            yield return Frames(5);
            Check(md.DanhSachMa.Count == 8, "Danh sách có 8 bài", string.Join(",", md.DanhSachMa));
            int napDuoc = 0;
            foreach (var ma in md.DanhSachMa) { try { var b = md.Nap(ma); if (b.KichBan.Buoc.Count > 0 && b.SoDo.NutDs.Count > 0) napDuoc++; } catch (Exception e) { Debug.LogError("[CC_FAIL] nạp " + ma + ": " + e.Message); } }
            Check(napDuoc == md.DanhSachMa.Count && napDuoc > 0, "Giải được mọi gói .verapack thật", $"{napDuoc}/{md.DanhSachMa.Count}");
            float tc = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - tc < 3f && !md.CoMayChu) yield return null;
            Check(md.CoMayChu, "Kiểm tra máy chủ (giả) thành công");

            // 3. phát hết các bước ở hai mức Thấp và Cao, chuyển chế độ, chụp ảnh ở mức Cao
            foreach (var muc in new[] { MucDoHoa.Thap, MucDoHoa.Cao })
            {
                HardwareProfile.Dat(muc);
                yield return Frames(3);
                int buocOk = 0, buocTong = 0, hatQua = 0;
                foreach (var ma in md.DanhSachMa)
                {
                    md.MoBai(ma);
                    var tp = md.TrinhPhat;
                    for (int i = 0; i < tp.SoBuoc; i++)
                    {
                        tp.DenBuoc(i);
                        bool coMuiTen = tp.BuocHienTai.MuiTen.Count > 0;
                        tp.DatT(0.3f);
                        yield return null;
                        bool hatOk = !coMuiTen || tp.SoHatDangVe > 0;
                        if (tp.SoHatDangVe > HardwareProfile.SoHatToiDa) hatQua++;
                        tp.DatT(1f);
                        yield return null;
                        buocTong++;
                        if (hatOk && tp.SoNguyenTuDangVe > 0) buocOk++;
                    }
                    if (tp.KichBan.NhanhSai.Count > 0)
                    {
                        tp.PhatNhanhSai();
                        tp.DatTNhanh(0.8f);
                        yield return null;
                        Check(tp.DangNhanhSai && tp.SoNguyenTuDangVe > 0, $"Nhánh sai {ma} phát được ({HardwareProfile.TenMuc(muc)})", tp.NhanhHienTai.Ten);
                        if (muc == MucDoHoa.Cao) { yield return null; Shot($"{ma}_nhanh_sai"); }
                        tp.DenBuoc(0);
                    }
                    if (muc == MucDoHoa.Cao)
                    {
                        tp.DenBuoc(Mathf.Min(1, tp.SoBuoc - 1));
                        tp.DatT(0.42f);
                        yield return null;
                        Shot($"{ma}_3d");
                        md.DatCheDo(true);
                        float tt = Time.realtimeSinceStartup;
                        while (md.DangChuyen && Time.realtimeSinceStartup - tt < 3f) yield return null;
                        yield return Frames(2);
                        Shot($"{ma}_giay");
                        Check(md.CheDoGiay && !md.DangChuyen, $"Chuyển sang Giấy: {ma}", $"{(Time.realtimeSinceStartup - tt):0.00} s");
                        int soDinh = md.Giay.SoDinh();
                        Check(soDinh >= 4 * md.Giay.DuLieu.NetDs.Count && md.Giay.DuLieu.NetDs.Count > 0, $"Sơ đồ giấy dựng đủ nét: {ma}", $"{soDinh} đỉnh, {md.Giay.DuLieu.NetDs.Count} nét, {md.Giay.DuLieu.Mt.Count} mũi tên cong");
                        md.DatCheDo(false);
                        tt = Time.realtimeSinceStartup;
                        while (md.DangChuyen && Time.realtimeSinceStartup - tt < 3f) yield return null;
                        Check(!md.CheDoGiay && md.TrinhPhat.Phang > 0.99f, $"Chuyển về 3D: {ma}");
                    }
                }
                Check(buocOk == buocTong && buocTong > 0, $"Phát hết các bước, mức {HardwareProfile.TenMuc(muc)}", $"{buocOk}/{buocTong} bước");
                Check(hatQua == 0, $"Số hạt electron ≤ SoHatToiDa ({HardwareProfile.SoHatToiDa}), mức {HardwareProfile.TenMuc(muc)}");
            }

            // 4. ô Giải thích mặc định đóng; Hỏi AI (máy chủ giả)
            md.MoBai("sn2_ch3br");
            Check(!md.GiaiThichMo, "Ô Giải thích mặc định đóng");
            md.BatGiaiThich(true);
            md.HoiAI();
            float ta = Time.realtimeSinceStartup;
            while (string.IsNullOrEmpty(md.TraLoiAICuoi) && Time.realtimeSinceStartup - ta < 3f) yield return null;
            Check(md.TraLoiAICuoi.Contains("phía sau"), "Hỏi AI nhận lời giải thích (máy chủ giả)");
            yield return null;
            Shot("sn2_ch3br_hoi_dap");
            // 4b. bảng bên: các bước (bấm dòng → nhảy bước), giải thích chi tiết (máy chủ giả), xuất ảnh, báo sai
            md.MoBangBen(0);
            yield return Frames(2);
            Check(md.TabBen == 0 && md.SoDongBuoc == md.TrinhPhat.SoBuoc && md.SoDongBuoc > 0, "Bảng bên: danh sách các bước đủ dòng", $"{md.SoDongBuoc}/{md.TrinhPhat.SoBuoc}");
            var dong2 = md.GetComponentsInChildren<Button>(true).FirstOrDefault(b => b.name == "Dòng bước " + md.TrinhPhat.SoBuoc);
            dong2?.onClick.Invoke();
            Check(dong2 != null && md.TrinhPhat.ChiSoBuoc == md.TrinhPhat.SoBuoc - 1, "Bấm dòng bước nhảy tới bước đó");
            Shot("sn2_ch3br_cac_buoc");
            md.MoBangBen(1);
            float tgct = Time.realtimeSinceStartup;
            while (string.IsNullOrEmpty(md.GiaiThichChiTietCuoi) && Time.realtimeSinceStartup - tgct < 3f) yield return null;
            Check(md.GiaiThichChiTietCuoi.Contains("Nucleophin tấn công"), "Giải thích chi tiết từ /co-che/giai-thich-chi-tiet");
            yield return null;
            Shot("sn2_ch3br_giai_thich");
            md.BatGiaiThich(false);
            yield return md.XuatAnhGiay();
            bool coAnh = md.DuongAnhXuatCuoi != "" && File.Exists(md.DuongAnhXuatCuoi) && new FileInfo(md.DuongAnhXuatCuoi).Length > 5000;
            Check(coAnh && md.CheDoGiay, "Xuất ảnh PNG sơ đồ giấy", md.DuongAnhXuatCuoi);
            if (coAnh) { File.Copy(md.DuongAnhXuatCuoi, Path.Combine(outDir, "xuat_anh_giay.png"), true); File.Delete(md.DuongAnhXuatCuoi); }
            var hop = md.GetComponentInChildren<Khkt.Menu.HopBaoSai>(true);
            md.GetComponentsInChildren<Button>(true).FirstOrDefault(b => b.name == "Nút Báo sai")?.onClick.Invoke();
            yield return Frames(3);
            Check(hop != null && hop.DangMo, "Nút Báo sai mở hộp báo sai");
            Shot("bao_sai");
            hop?.Gui("Mũi tên bước 1 vẽ sai chỗ");
            float tbs = Time.realtimeSinceStartup;
            while ((hop == null || hop.KetQuaCuoi == "") && Time.realtimeSinceStartup - tbs < 3f) yield return null;
            Check(soBaoSai >= 1 && hop != null && hop.KetQuaCuoi.Contains("ok"), "Gửi báo sai tới /co-che/bao-sai", $"{soBaoSai} lần, {hop?.KetQuaCuoi}, {MayChuCoChe.GiaLap != null}");
            yield return new WaitForSecondsRealtime(1.6f);
            md.DatCheDo(false);
            while (md.DangChuyen) yield return null;

            // 5. bảng giải chuỗi (máy chủ giả)
            md.BatBangChuoi(true);
            md.GiaiChuoi(CoCheMoDun.ChuoiMau[0]);
            float tb = Time.realtimeSinceStartup;
            while (md.SoTheChuoi == 0 && Time.realtimeSinceStartup - tb < 3f) yield return null;
            Check(md.SoTheChuoi == 4, "Bảng giải chuỗi hiện 4 thẻ bước", md.SoTheChuoi.ToString());
            yield return null;
            Shot("bang_giai_chuoi");
            md.BatBangChuoi(false);

            // 5b. đề mới: chọn ảnh (hộp chọn giả) → Phân tích (máy chủ giả) → mở bài; lỗi máy chủ hiện rõ
            {
                string anhThu = Path.Combine(outDir, "anh_de_thu.png");
                var texDe = new Texture2D(320, 240);
                File.WriteAllBytes(anhThu, texDe.EncodeToPNG());
                Destroy(texDe);
                jsonDeGia = GoiCoChe.GiaiJson(File.ReadAllBytes(Path.Combine(GoiCoChe.ThuMuc, "sn2_ch3br.verapack")))
                                    .Replace("\"ma\":\"sn2_ch3br\"", "\"ma\":\"de_thu\"");
                jsonDeGia = jsonDeGia.Substring(0, jsonDeGia.LastIndexOf('}')) + ",\"thong_tin\":{\"mo_hinh\":\"gia\",\"giay\":1.0,\"so_lan_sua\":1}}";
                // KHKT_CO_CHE_DE_JSON = tệp kết quả THẬT của /co-che/giai-de (vd do máy chủ giải ảnh đề) → hiện và chụp chính kết quả đó
                string deThat = System.Environment.GetEnvironmentVariable("KHKT_CO_CHE_DE_JSON");
                bool coDeThat = !string.IsNullOrEmpty(deThat) && File.Exists(deThat);
                if (coDeThat) jsonDeGia = File.ReadAllText(deThat);
                md.BatDeMoi(true);
                yield return null;
                Check(md.DeMoiMo && !md.TrinhPhat.NhanDieuKhien, "Bảng Đề mới mở, phím ← → Space không điều khiển phim khi gõ");
                ChonTepHeThong.GiaLap = () => (anhThu, null);
                md.ChonAnhDe();
                float tc2 = Time.realtimeSinceStartup;
                while (md.AnhDe == null && Time.realtimeSinceStartup - tc2 < 3f) yield return null;
                ChonTepHeThong.GiaLap = null;
                Check(md.AnhDe != null && md.TenAnhDe == "anh_de_thu.png", "Chọn ảnh đề (hộp chọn giả) gắn ảnh", md.TenAnhDe);
                yield return null;
                Shot("bang_de_moi");
                guiAnhDe = false;
                md.PhanTichDe("Viết cơ chế CH3Br + NaOH loãng");
                float td = Time.realtimeSinceStartup;
                while ((md.DangGiaiDe || md.TrangThaiDeCuoi.StartsWith("Đang")) && Time.realtimeSinceStartup - td < 5f) yield return null;
                yield return Frames(2);
                Check(md.BaiHienTai != null && (coDeThat || md.BaiHienTai.Ma == "de_thu") && md.BaiHienTai.KichBan.Buoc.Count > 0 && md.DeDaGiai.Count == 1 && !md.DeMoiMo,
                      "Đề mới: kết quả máy chủ mở như một bài", md.TrangThaiDeCuoi);
                Check(guiAnhDe, "Đề mới: ảnh đề được gửi kèm (anh_base64)");
                Check(md.TrinhPhat.NhanDieuKhien, "Đóng bảng Đề mới trả điều khiển phim 3D");
                md.TrinhPhat.DenBuoc(Mathf.Min(1, md.TrinhPhat.SoBuoc - 1));
                md.TrinhPhat.DatT(0.45f);
                yield return null;
                Shot("de_moi_ket_qua");
                md.DatCheDo(true);
                float tg = Time.realtimeSinceStartup;
                while (md.DangChuyen && Time.realtimeSinceStartup - tg < 3f) yield return null;
                yield return Frames(2);
                Check(md.CheDoGiay && md.Giay.DuLieu.NetDs.Count > 0, "Đề mới: sơ đồ giấy có nét", $"{md.Giay.DuLieu.NetDs.Count} nét");
                Shot("de_moi_giay");
                md.DatCheDo(false);
                tg = Time.realtimeSinceStartup;
                while (md.DangChuyen && Time.realtimeSinceStartup - tg < 3f) yield return null;
                md.BatDeMoi(true);
                md.DatAnhDe(null, "");
                md.PhanTichDe("LOI_THU");
                td = Time.realtimeSinceStartup;
                while ((md.DangGiaiDe || md.TrangThaiDeCuoi.StartsWith("Đang")) && Time.realtimeSinceStartup - td < 5f) yield return null;
                Check(md.TrangThaiDeCuoi.Contains("chưa nhất quán") && md.TrangThaiDeCuoi.Contains("Bước 1"), "Đề mới: lỗi 422 hiện thông điệp + chi tiết của máy chủ", md.TrangThaiDeCuoi);
                yield return null;
                Shot("de_moi_loi");
                md.BatDeMoi(false);
                md.MoBai("sn2_ch3br");
            }

            // 6. đo FPS bài propellane ở mức Cao
            HardwareProfile.Dat(MucDoHoa.Cao);
            int vsyncCu = QualitySettings.vSyncCount, fpsCu = Application.targetFrameRate;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            md.Cam.targetTexture = null;
            md.MoBai("hsg_propellane_a4");
            md.TrinhPhat.ThoiGianBuoc = 1.2f;
            md.TrinhPhat.DenBuoc(0, true);
            yield return Frames(20);
            int dem = 0;
            float tf = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - tf < 4f)
            {
                if (!md.TrinhPhat.DangPhat) md.TrinhPhat.DenBuoc(0, true);
                dem++;
                yield return null;
            }
            FpsDo = dem / (Time.realtimeSinceStartup - tf);
            Check(FpsDo >= 60f, "Propellane mức Cao ≥ 60 FPS", $"{FpsDo:0.0} FPS, {SystemInfo.graphicsDeviceName}, {Screen.width}×{Screen.height}");
            QualitySettings.vSyncCount = vsyncCu;
            Application.targetFrameRate = fpsCu;
            md.Cam.targetTexture = rt;

            // 7. không có máy chủ: nút Hỏi AI mờ, ghi rõ lý do
            CoCheMoDun.Dong();
            MayChuCoChe.GiaLap = null;
            MayChuCoChe.Url = "http://127.0.0.1:9";
            CoCheMoDun.Mo();
            md.Cam.targetTexture = rt;
            float tk = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - tk < 6f && md.LyDoMayChu == "") yield return null;
            yield return Frames(3);
            var nutAI = md.GetComponentsInChildren<Button>(true).FirstOrDefault(b => b.name == "Hỏi AI");
            Check(!md.CoMayChu && nutAI != null && !nutAI.interactable && md.LyDoMayChu.Length > 0, "Không có máy chủ: nút Hỏi AI mờ và có lý do", md.LyDoMayChu);
            Shot("khong_may_chu");

            // 8. đóng
            md.Cam.targetTexture = null;
            CoCheMoDun.Dong();
            yield return Frames(3);
            Check(!CoCheMoDun.DangMo && DesktopViewer.UiCapture == uiCapCu, "Dong() đóng lớp phủ, trả điều khiển lab");

            // khôi phục
            if (string.IsNullOrEmpty(urlCu)) PlayerPrefs.DeleteKey(MayChuCoChe.KhoaUrl); else PlayerPrefs.SetString(MayChuCoChe.KhoaUrl, urlCu);
            if (tuDongCu) HardwareProfile.DatTuDong(); else HardwareProfile.Dat(mucCu);

            Debug.Log($"[CC_SUMMARY] passes={passes} failures={failures} runtimeErrors={runtimeErrors} shots={shots} fps={FpsDo:0.0}");
            if (failed.Count > 0) Debug.LogError("[CC_FAILED_LIST] " + string.Join(" || ", failed));
            if (errLines.Count > 0) Debug.LogError("[CC_RUNTIME_ERR_LIST] " + string.Join(" || ", errLines.Take(20)));
            bool ok = failures == 0 && runtimeErrors == 0;
            Debug.Log(ok ? "[CC_RESULT] ALL PASSED" : "[CC_RESULT] FAILED");
#if UNITY_EDITOR
            EditorApplication.Exit(ok ? 0 : 1);
#endif
        }

        IEnumerator Watchdog(float s)
        {
            yield return new WaitForSecondsRealtime(s);
            Debug.LogError($"[CC_TIMEOUT] passes={passes} failures={failures}");
#if UNITY_EDITOR
            EditorApplication.Exit(2);
#endif
        }

        void TestGoi()
        {
            string p = Path.Combine(GoiCoChe.ThuMuc, "mau_kiem_thu.bin");
            if (!File.Exists(p)) { Check(false, "Có gói thử Python mau_kiem_thu.bin", p); return; }
            var goi = File.ReadAllBytes(p);
            var khoa = new byte[32];
            for (int i = 0; i < 32; i++) khoa[i] = (byte)i;
            string json = "";
            try { json = GoiCoChe.GiaiJson(goi, khoa); } catch (Exception e) { json = "LỖI " + e.Message; }
            Check(json.Contains("Ặ ỷ ữ") && json.Contains("\"mau\""), "Giải đúng gói thử do Python tạo (AES-CBC + HMAC + gzip)", json.Length > 80 ? json.Substring(0, 80) : json);
            int tuChoi = 0;
            foreach (int vt in new[] { 0, 7, 30, 40, goi.Length - 40, goi.Length - 1 })
            {
                var sua = (byte[])goi.Clone();
                sua[vt] ^= 1;
                try { GoiCoChe.GiaiJson(sua, khoa); } catch (InvalidDataException) { tuChoi++; }
            }
            Check(tuChoi == 6, "Gói sửa 1 byte bị từ chối (6/6 vị trí)", tuChoi.ToString());
            bool saiKhoa = false;
            try { GoiCoChe.GiaiJson(goi, new byte[32]); } catch (InvalidDataException) { saiKhoa = true; }
            Check(saiKhoa, "Sai khóa bị từ chối");
        }

        static string jsonDeGia = "{}";
        static bool guiAnhDe;

        static int soBaoSai;

        static (long, string) MayChuGia(string pt, string duong, string than)
        {
            switch (duong)
            {
                case "/co-che/giai-de":
                    if (than.Contains("LOI_THU"))
                        return (422, "{\"detail\":{\"thong_diep\":\"AI đã sửa 3 lần nhưng cơ chế vẫn chưa nhất quán khi mô phỏng bằng RDKit.\",\"ma\":\"co_che_sai\",\"chi_tiet\":[\"Bước 1: sản phẩm không khớp\"]}}");
                    guiAnhDe = than.Contains("\"anh_base64\":\"");
                    return (200, jsonDeGia);
                case "/co-che/suc-khoe":
                    return (200, "{\"ok\":true,\"mo_hinh\":\"gia\",\"so_bai\":8}");
                case "/co-che/hoi-dap":
                    return (200, "{\"tra_loi\":\"OH⁻ tấn công C từ phía sau, đối diện Br (Bước 1).\",\"goi_y\":[],\"nguon\":\"ai\",\"mo_hinh\":\"gia\",\"giay\":0.1}");
                case "/co-che/giai-thich-chi-tiet":
                    return (200, "{\"tong_quan\":\"SN2 một giai đoạn.\",\"buoc\":[{\"so\":1,\"tieu_de\":\"Nucleophin tấn công\",\"noi_dung\":\"Cặp electron của O tấn công C.\"}],\"luu_y\":[\"Nhớ nghịch đảo cấu hình.\"],\"nguon\":\"ai\",\"mo_hinh\":\"gia\",\"giay\":0.1}");
                case "/co-che/bao-sai":
                    soBaoSai++;
                    return (200, "{\"ok\":true,\"ma\":\"gia\"}");
                case "/co-che/giai-thich":
                    return (200, "{\"tra_loi\":\"OH⁻ tấn công C từ phía sau, đối diện Br [1].\",\"nguon\":[{\"so\":1,\"tieu_de\":\"SN2 › Các bước\",\"nguon\":\"bai_demo/sn2_ch3br\"}],\"nha_cung_cap\":\"gia\",\"mo_hinh\":\"gia\",\"kiem_chung\":\"ai_theo_tai_lieu\",\"giay\":0.1}");
                case "/co-che/giai-chuoi":
                    var sb = new StringBuilder("{\"chuoi\":\"x\",\"da_kiem_chung\":true,\"chat\":[{\"ki_hieu\":\"C2H5OH\",\"ten\":\"ethanol\"}],\"buoc\":[");
                    sb.Append("{\"tu\":\"C2H4\",\"den\":\"C2H5OH\",\"phan_ung\":\"cộng H₂O (hydrat hoá)\",\"dieu_kien\":\"H₂O, H₂SO₄ loãng, t°\",\"loai\":\"AE\",\"ma_co_che\":\"cong_hbr_propen\",\"khop_mau\":\"cung_loai\",\"kiem_chung\":\"da_kiem_chung\"},");
                    sb.Append("{\"tu\":\"C2H5OH\",\"den\":\"CH3CHO\",\"phan_ung\":\"oxi hoá bằng CuO\",\"dieu_kien\":\"CuO, t°\",\"loai\":\"OXI_HOA\",\"ma_co_che\":null,\"kiem_chung\":\"da_kiem_chung\"},");
                    sb.Append("{\"tu\":\"CH3CHO\",\"den\":\"CH3COOH\",\"phan_ung\":\"oxi hoá aldehyde\",\"dieu_kien\":\"O₂, Mn²⁺\",\"loai\":\"OXI_HOA\",\"ma_co_che\":null,\"kiem_chung\":\"da_kiem_chung\"},");
                    sb.Append("{\"tu\":\"CH3COOH\",\"den\":\"CH3COOC2H5\",\"phan_ung\":\"ester hoá\",\"dieu_kien\":\"C₂H₅OH, H₂SO₄ đặc\",\"loai\":\"THE_ACYL\",\"ma_co_che\":\"este_hoa_fischer\",\"khop_mau\":\"dung_mau\",\"kiem_chung\":\"da_kiem_chung\"}]}");
                    return (200, sb.ToString());
            }
            return (404, "{}");
        }

        void Shot(string ten)
        {
            var md = CoCheMoDun.Instance;
            if (md == null) return;
            var cam = md.Cam;
            var cu = cam.targetTexture;
            cam.targetTexture = rt;
            // batchmode có màn 640×480: chữ dựng ở cỡ đó bị nhòe khi kết xuất 1920×1080 → ép dựng lại chữ theo cỡ ảnh
            foreach (var t in md.GetComponentsInChildren<Text>()) t.SetAllDirty();
            Canvas.ForceUpdateCanvases();
            cam.Render();
            RenderTexture.active = rt;
            readTex.ReadPixels(new Rect(0, 0, ShotW, ShotH), 0, 0);
            readTex.Apply();
            RenderTexture.active = null;
            cam.targetTexture = cu;
            string p = Path.Combine(outDir, ten + ".png");
            File.WriteAllBytes(p, readTex.EncodeToPNG());
            shots++;
            Debug.Log("[CC_SHOT] " + p);
        }
    }
}
