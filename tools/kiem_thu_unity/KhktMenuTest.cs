using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using Khkt.Demo;
using Khkt.Environment;
using Khkt.Environment.Tang2;
using Khkt.Menu;
using Khkt.Menu.Dashboard;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Chỉ có trong bản sao project: đi hết luồng màn hình chính, gồm cả máy chủ tài khoản giả đúng hợp đồng API.
public class KhktMenuTest : MonoBehaviour
{
    const string FakeUrl = "http://127.0.0.1:18765";
    int pass, fail, errors;
    string outDir;
    RenderTexture rt;
    Texture2D tex;
    HttpListener http;
    volatile int wrongTries;
    volatile int loginCalls;
    volatile string lastAuth = "";
    volatile int scoreMode;  // 0 có điểm, 1 chưa có bài, 2 máy chủ lỗi, 3 hết phiên

    void Check(string name, bool ok, string detail = "")
    {
        if (ok) pass++; else fail++;
        Debug.Log($"[KHKT-PLAY] [menu] {(ok ? "ĐẠT" : "HỎNG")}: {name} {detail}");
    }

    static bool started;

    void Awake()
    {
        // cảnh màn hình chính được tải lại mang theo bản sao của chính bài này
        if (started) { Destroy(gameObject); return; }
        started = true;
        DontDestroyOnLoad(gameObject);
        Application.logMessageReceived += (m, st, t) =>
        {
            if (t == LogType.Exception || t == LogType.Error)
            {
                errors++;
                Debug.Log("[KHKT-PLAY] [menu] LỖI LOG: " + m.Split('\n')[0]);
            }
        };
        outDir = Environment.GetEnvironmentVariable("KHKT_SHOTS") ?? Application.temporaryCachePath;
        rt = new RenderTexture(1600, 900, 24);
        tex = new Texture2D(1600, 900, TextureFormat.RGB24, false);
        StartFake();
    }

    IEnumerator Start()
    {
        string realFile = Environment.GetEnvironmentVariable("KHKT_MENU_TK");
        if (!string.IsNullOrEmpty(realFile)) yield return RealServer(realFile);
        else yield return Wrapper();
        try { http?.Stop(); } catch { }
#if UNITY_EDITOR
        UnityEditor.EditorApplication.Exit(0);
#else
        Application.Quit();
#endif
    }

    IEnumerator Wrapper()
    {
        float t0 = Time.realtimeSinceStartup;
        PlayerPrefs.DeleteKey("vera.may_chu");
        string sessionFile = Path.Combine(Application.persistentDataPath, "phien_dang_nhap.txt");
        if (File.Exists(sessionFile)) File.Delete(sessionFile);
        GameSession.SignOut();
        yield return Frames(5);

        var menu = FindAnyObjectByType<MainMenu>();
        Check("mở ứng dụng vào màn hình chính, chưa tải lab", menu != null && SceneManager.GetActiveScene().name == GameSession.MenuScene && FindAnyObjectByType<DesktopViewer>() == null);
        Check("màn đầu là Đăng nhập", menu.Current == MainMenu.Screen.Login);
        Check("con trỏ tự do", Cursor.lockState == CursorLockMode.None);
        Check("ô mật khẩu ẩn ký tự", Field("Ô đăng nhập: mật khẩu").contentType == InputField.ContentType.Password);
        yield return ShotDone("menu_dang_nhap");
        // ảnh trắng vẫn qua hết các mục về logic: phải đếm điểm ảnh thật
        Check("giao diện thật sự vẽ ra: có nút màu nhấn", CountNear(new Color(0x0b / 255f, 0x63 / 255f, 0xd1 / 255f)) > 5000, CountNear(new Color(0x0b / 255f, 0x63 / 255f, 0xd1 / 255f)).ToString());
        Check("giao diện thật sự vẽ ra: có chữ tối", CountNear(new Color(0x1d / 255f, 0x1d / 255f, 0x1f / 255f)) > 800, CountNear(new Color(0x1d / 255f, 0x1d / 255f, 0x1f / 255f)).ToString());
        Check("giao diện thật sự vẽ ra: có ảnh lab bên phải", ColorsInRightHalf() > 200, ColorsInRightHalf().ToString());

        // Bỏ trống
        Click("Nút Đăng nhập");
        yield return Frames(2);
        Check("bỏ trống thì nhắc", Status("Thông báo đăng nhập").Contains("Nhập tên"));

        // Máy chủ không chạy (cổng không có ai nghe)
        GameSession.ServerUrl = "http://127.0.0.1:18799";
        Field("Ô đăng nhập: tên").text = "hs1";
        Field("Ô đăng nhập: mật khẩu").text = "sai";
        Click("Nút Đăng nhập");
        yield return WaitStatus("Thông báo đăng nhập", "Không kết nối");
        Check("máy chủ tắt thì báo và gợi ý dùng thử", Status("Thông báo đăng nhập").Contains("dùng thử"));
        Check("mất kết nối không làm kẹt ở màn hình", menu.Current == MainMenu.Screen.Login);
        yield return ShotDone("menu_mat_ket_noi");

        // Cài đặt
        Click("Nút mở Cài đặt");
        yield return Frames(2);
        Check("mở Cài đặt", menu.Current == MainMenu.Screen.Settings);
        Field("Ô địa chỉ máy chủ").text = "ftp://sai";
        Click("Nút Lưu địa chỉ");
        yield return Frames(2);
        Check("từ chối địa chỉ sai", Status("Thông báo cài đặt").Contains("http://"));
        Field("Ô địa chỉ máy chủ").text = FakeUrl + "/";
        Click("Nút Kiểm tra kết nối");
        yield return WaitStatus("Thông báo cài đặt", "Kết nối được");
        Check("kiểm tra kết nối được", Status("Thông báo cài đặt").Contains("Kết nối được"));
        Click("Nút Lưu địa chỉ");
        yield return Frames(2);
        Check("lưu địa chỉ, bỏ dấu / cuối", GameSession.ServerUrl == FakeUrl, GameSession.ServerUrl);
        yield return ShotDone("menu_cai_dat");
        Click("Nút quay lại từ cài đặt");
        yield return Frames(2);

        // Sai mật khẩu, sai quá nhiều
        Field("Ô đăng nhập: mật khẩu").text = "sai";
        Click("Nút Đăng nhập");
        yield return WaitStatus("Thông báo đăng nhập", "Sai tên");
        Check("401 báo sai thông tin", Status("Thông báo đăng nhập").Contains("Sai tên đăng nhập"));
        Field("Ô đăng nhập: mật khẩu").text = "sai";
        wrongTries = 5;
        Click("Nút Đăng nhập");
        yield return WaitStatus("Thông báo đăng nhập", "quá nhiều");
        Check("429 báo đợi đúng số giây", Status("Thông báo đăng nhập").Contains("30 giây"));
        wrongTries = 0;

        // Đăng ký
        Click("Nút chuyển Tạo tài khoản");
        yield return Frames(2);
        Field("Ô đăng ký: mã lớp").text = "SAI";
        Field("Ô đăng ký: họ tên").text = "Nguyễn Văn A";
        Field("Ô đăng ký: tên đăng nhập").text = "hs2";
        Field("Ô đăng ký: mật khẩu").text = "matkhau1";
        Field("Ô đăng ký: nhập lại").text = "khac";
        Click("Nút Tạo tài khoản");
        yield return Frames(2);
        Check("mật khẩu không khớp thì nhắc", Status("Thông báo đăng ký").Contains("chưa khớp"));
        Field("Ô đăng ký: nhập lại").text = "matkhau1";
        Click("Nút Tạo tài khoản");
        yield return WaitStatus("Thông báo đăng ký", "Mã lớp");
        Check("hiện nguyên lỗi máy chủ trả về", Status("Thông báo đăng ký").Contains("Mã lớp không tồn tại"));
        yield return ShotDone("menu_dang_ky");
        Field("Ô đăng ký: mã lớp").text = "10A1";
        Click("Nút Tạo tài khoản");
        loginCalls = 0;
        yield return WaitScreen(menu, MainMenu.Screen.Home);
        Check("đăng ký xong có phiên ngay, không gọi lại đăng nhập", menu.Current == MainMenu.Screen.Home && GameSession.User != null && loginCalls == 0, loginCalls.ToString());
        yield return DashboardChecks(menu);
        yield return ShotDone("menu_trang_chinh");
        Check("có lưu mã phiên, không có mật khẩu", File.Exists(sessionFile) && !File.ReadAllText(sessionFile).Contains("matkhau1"));
        Click("Nút Đăng xuất");
        yield return WaitScreen(menu, MainMenu.Screen.Login);
        Check("đăng xuất gửi đúng Bearer và xóa phiên", lastAuth == "Bearer PHIEN123" && !File.Exists(sessionFile) && GameSession.Token == null, lastAuth);
        Check("đăng xuất xóa điểm khỏi bộ nhớ", !StudentData.HasData && StudentData.Results.Count == 0);
        yield return TeacherChecks(menu);

        // Bắt buộc đổi mật khẩu
        Field("Ô đăng nhập: tên").text = "doimk";
        Field("Ô đăng nhập: mật khẩu").text = "dung";
        Click("Nút Đăng nhập");
        yield return WaitScreen(menu, MainMenu.Screen.ChangePassword);
        Check("phai_doi_mat_khau thì bắt đổi trước", menu.Current == MainMenu.Screen.ChangePassword);
        yield return ShotDone("menu_doi_mat_khau");
        Field("Ô đổi: mật khẩu hiện tại").text = "cu_sai";
        Field("Ô đổi: mật khẩu mới").text = "moi12345";
        Field("Ô đổi: nhập lại").text = "moi12345";
        Click("Nút Đổi mật khẩu");
        yield return WaitStatus("Thông báo đổi mật khẩu", "không đúng");
        Check("sai mật khẩu cũ (400): ở lại, hiện lỗi máy chủ", menu.Current == MainMenu.Screen.ChangePassword && Status("Thông báo đổi mật khẩu").Contains("không đúng"));
        Field("Ô đổi: mật khẩu hiện tại").text = "dung";
        Click("Nút Đổi mật khẩu");
        yield return WaitScreen(menu, MainMenu.Screen.Home);
        Check("đổi xong vào chọn chế độ", menu.Current == MainMenu.Screen.Home && !GameSession.User.phai_doi_mat_khau);

        // Mở lại ứng dụng: dùng lại phiên đã lưu
        GameSession.Token = null;
        GameSession.User = null;
        SceneManager.LoadScene(GameSession.MenuScene);
        yield return Frames(3);
        menu = FindAnyObjectByType<MainMenu>();
        yield return WaitScreen(menu, MainMenu.Screen.Home);
        Check("phiên còn hạn thì vào thẳng chọn chế độ", menu.Current == MainMenu.Screen.Home && GameSession.User != null, GameSession.DisplayName);

        // Vào lab bằng tài khoản, chế độ tự do
        Click("Nút Vào phòng thí nghiệm");
        yield return Frames(2);
        Check("hiện màn tải", menu == null || menu.Current == MainMenu.Screen.Loading);
        Shot("menu_dang_tai");
        yield return WaitLab();
        yield return Frames(30);
        var intro = FindAnyObjectByType<IntroScreen>();
        Check("vào lab là dùng được ngay, không có bảng chọn", intro != null && !intro.IsOpen && DesktopViewer.UiCapture == 0);
        Check("đã hết cờ chờ vào lab", !GameSession.EnterLabPending);
        ShotMain("menu_vao_lab");

        // Yêu cầu AI trong lab mang mã phiên; mã hết hạn thì bỏ, chơi tiếp dùng thử
        Check("đang chơi vẫn dùng địa chỉ đã cài", GameSession.ServerUrl == FakeUrl);
        typeof(AiClient).GetProperty("Reachable").GetSetMethod(true).Invoke(AiClient.Instance, new object[] { true });
        lastAuth = "";
        AiClient.Instance.Log("thu", "Kiểm thử mã phiên");
        float tw = Time.realtimeSinceStartup;
        while (lastAuth == "" && Time.realtimeSinceStartup - tw < 8f) yield return null;
        Check("yêu cầu trong lab gửi kèm mã phiên", lastAuth == "Bearer PHIEN123", lastAuth);
        string keep = GameSession.Token;
        var keepUser = GameSession.User;
        GameSession.Token = "HET_HAN";
        AiClient.Instance.Log("thu", "Kiểm thử hết phiên");
        tw = Time.realtimeSinceStartup;
        while (GameSession.Token != null && Time.realtimeSinceStartup - tw < 8f) yield return null;
        yield return Frames(2);
        Check("401 trong lab: bỏ mã, chuyển dùng thử, không lỗi", GameSession.Token == null && GameSession.GuestActive && GameSession.User == null);
        GameSession.Token = keep;
        GameSession.User = keepUser;
        GameSession.GuestActive = false;

        // Esc menu có nút về
        intro.Open();
        yield return Frames(3);
        ShotMain("menu_esc_trong_lab");
        Check("bảng Esc có nút về màn hình chính", FindButton("Về màn hình chính") != null);
        Click("Về màn hình chính");
        yield return WaitScene(GameSession.MenuScene);
        yield return Frames(3);
        menu = FindAnyObjectByType<MainMenu>();
        Check("về màn hình chính, vẫn đăng nhập", menu != null && menu.Current == MainMenu.Screen.Home && GameSession.User != null);
        Check("thả con trỏ, không kẹt giao diện", Cursor.lockState == CursorLockMode.None && DesktopViewer.UiCapture == 0);
        Check("đã tắt lab", FindAnyObjectByType<DesktopViewer>() == null);

        // Đăng xuất, dùng thử
        Click("Nút Đăng xuất");
        yield return WaitScreen(menu, MainMenu.Screen.Login);
        Click("Nút Dùng thử");
        yield return Frames(2);
        Check("dùng thử không gọi máy chủ, vào chọn chế độ", menu.Current == MainMenu.Screen.Home && GameSession.GuestActive && GameSession.User == null);
        yield return WaitAnim();
        Check("dùng thử: không có mục điểm, có mục hướng dẫn", FindButton("Mục: Điểm số") == null && FindButton("Mục: Hướng dẫn") != null && Status("Số: Điểm trung bình") == "");
        yield return ShotDone("menu_dung_thu");
        Click("Nút Vào phòng thí nghiệm");
        yield return WaitLab();
        yield return Frames(30);
        intro = FindAnyObjectByType<IntroScreen>();
        Check("dùng thử: vào lab, không có bảng chọn", intro != null && !intro.IsOpen && DesktopViewer.UiCapture == 0);
        Check("đứng ở bàn thí nghiệm, không còn thẻ Chuẩn độ, Khám phá", FindButton("Nút Chuẩn độ") == null && FindButton("Nút Khám phá") == null);

        // Lần vào lab thứ ba: lộ lỗi trạng thái còn sót giữa các lần đổi cảnh
        intro.Open();
        yield return Frames(2);
        Click("Về màn hình chính");
        yield return WaitScene(GameSession.MenuScene);
        yield return Frames(3);
        Click("Nút Vào phòng thí nghiệm");
        yield return WaitLab();
        yield return Frames(30);
        intro = FindAnyObjectByType<IntroScreen>();
        Check("vào lab được lần thứ ba", intro != null && !intro.IsOpen);
        Check("chỉ một EventSystem", FindObjectsByType<UnityEngine.EventSystems.EventSystem>().Length == 1);
        var ht = Khkt.Input.HeadTracking.Instance;
        Check("HeadTracking trỏ tới bản mới", ht != null && ht.gameObject.scene.name == GameSession.LabScene);
        ShotMain("menu_lan_ba");

        Check("không có lỗi nào trong log", errors == 0, $"{errors} lỗi");
        PlayerPrefs.DeleteKey("vera.may_chu");
        Debug.Log($"[KHKT-PLAY] [menu] Tổng: đạt {pass}, hỏng {fail}, {Time.realtimeSinceStartup - t0:0} s");
    }

    // ------------------------------------------------------------------ trang chính

    IEnumerator DashboardChecks(MainMenu menu)
    {
        LogicChecks();
        Check("có phông biểu tượng của Windows", MenuKit.IconFont != null);

        // Từ đăng nhập sang trang chính: đo khung hình trong lúc chuyển động (trong Editor, chỉ để tham khảo)
        float worst = 0f, sum = 0f;
        int n = 0;
        float t0 = Time.realtimeSinceStartup;
        while ((MenuAnim.Busy || StudentLoading()) && Time.realtimeSinceStartup - t0 < 8f)
        {
            worst = Mathf.Max(worst, Time.unscaledDeltaTime);
            sum += Time.unscaledDeltaTime;
            n++;
            yield return null;
        }
        Debug.Log($"[KHKT-PLAY] [menu] [đo] chuyển sang trang chính: {n} khung, trung bình {(n > 0 ? sum / n * 1000f : 0f):0.0} ms, tệ nhất {worst * 1000f:0.0} ms");
        Check("số liệu lấy đúng từ máy chủ: trung bình", Status("Số: Điểm trung bình") == "7.5", Status("Số: Điểm trung bình"));
        Check("số liệu lấy đúng: số bài kể cả chưa chấm", Status("Số: Bài đã làm") == "7", Status("Số: Bài đã làm"));
        Check("số liệu lấy đúng: tiến bộ 3 bài gần nhất so với 3 bài trước", Status("Số: Tiến bộ") == "+3.0", Status("Số: Tiến bộ"));
        Check("báo số bài chờ chấm", Status("Ghi chú: Bài đã làm").Contains("1 bài đang chờ"), Status("Ghi chú: Bài đã làm"));
        Check("kết quả gần đây hiện tối đa 5 dòng", RowCount() == 5, RowCount().ToString());
        Check("bài mới nhất chưa chấm hiện Chờ chấm, không hiện điểm 0", FindObjectsByType<Text>().Any(x => x.text == "Chờ chấm"));
        Check("hiện nhận xét mới nhất có nội dung", Status("Nội dung nhận xét").Contains("số đọc"), Status("Nội dung nhận xét"));
        Check("hiện đúng tên gọi", Status("Lời chào") == "Chào A", Status("Lời chào"));
        yield return ShotDone("trang_chinh_tong_quan");
        Check("trang chính thật sự vẽ ra: có thẻ màu nhấn", CountNear(new Color(0x0b / 255f, 0x63 / 255f, 0xd1 / 255f)) > 20000, CountNear(new Color(0x0b / 255f, 0x63 / 255f, 0xd1 / 255f)).ToString());
        Check("trang chính thật sự vẽ ra: có chữ tối", CountNear(new Color(0x1d / 255f, 0x1d / 255f, 0x1f / 255f)) > 200, CountNear(new Color(0x1d / 255f, 0x1d / 255f, 0x1f / 255f)).ToString());

        // Chuyển trang: vệt chọn trượt đúng chỗ
        var shell = FindAnyObjectByType<DashboardShell>();
        var pill = (RectTransform)shell.transform.Find("Thanh bên/Vệt chọn");
        Click("Mục: Bài tập");
        yield return WaitAnim();
        var navItem = (RectTransform)FindButton("Mục: Bài tập").transform;
        Check("chuyển trang và vệt chọn tới đúng mục", shell.CurrentPage == "bai_tap" && Vector2.Distance(pill.anchoredPosition, navItem.anchoredPosition) < 0.5f, pill.anchoredPosition.ToString());
        yield return ShotDone("trang_chinh_chua_lam");

        // Bấm dồn dập giữa chừng chuyển động: không được lệch vị trí, không kẹt
        var greet = (RectTransform)shell.transform.Find("Vùng nội dung/Trang: Tổng quan/Lời chào");
        Vector2 greetHome = new Vector2(56, -44);
        foreach (var name in new[] { "Mục: Tổng quan", "Mục: Điểm số", "Mục: Tổng quan", "Mục: Cải thiện", "Mục: Tổng quan", "Mục: Hướng dẫn", "Mục: Tổng quan" })
        {
            Click(name);
            yield return Frames(2);
        }
        yield return WaitLoaded();
        yield return WaitAnim();
        Check("bấm dồn dập: trang về đúng vị trí", shell.CurrentPage == "tong_quan" && Vector2.Distance(greet.anchoredPosition, greetHome) < 0.5f && greet.GetComponent<CanvasGroup>().alpha > 0.99f, greet.anchoredPosition.ToString());
        navItem = (RectTransform)FindButton("Mục: Tổng quan").transform;
        Check("bấm dồn dập: vệt chọn về đúng mục", Vector2.Distance(pill.anchoredPosition, navItem.anchoredPosition) < 0.5f, pill.anchoredPosition.ToString());

        // Giảm chuyển động: không còn gì chạy, trang vẫn hiện đủ
        ReducedMotion.IsEnabled = true;
        Click("Mục: Điểm số");
        Check("giảm chuyển động: đổi trang tức thì", !MenuAnim.Busy && shell.CurrentPage == "diem_so");
        Click("Mục: Tổng quan");
        ReducedMotion.IsEnabled = false;
        yield return WaitLoaded();

        // Máy chủ lỗi khi đã có số: giữ số cũ, báo rõ, vẫn vào lab được
        scoreMode = 2;
        Click("Mục: Điểm số");
        yield return Frames(1);
        Click("Mục: Tổng quan");
        yield return WaitLoaded();
        Check("mất kết nối: giữ số cũ", Status("Số: Điểm trung bình") == "7.5" && Status("Thông báo trang chính").Contains("lần tải trước"), Status("Thông báo trang chính"));
        Check("mất kết nối: vẫn có nút vào lab", FindButton("Nút Vào phòng thí nghiệm") != null);
        yield return ShotDone("trang_chinh_mat_ket_noi");

        // Chưa có số mà máy chủ lỗi: báo lỗi, không hiện số giả, thử lại được
        Khkt.Cache.ApiCache.ClearUser();
        StudentData.Clear();
        Click("Mục: Điểm số");
        yield return Frames(1);
        Click("Mục: Tổng quan");
        yield return WaitLoaded();
        Check("lần đầu lỗi: không hiện số giả", Status("Số: Điểm trung bình") == "–" && Status("Thông báo trang chính").Contains("Không tải được"), Status("Số: Điểm trung bình") + " | " + Status("Thông báo trang chính"));
        scoreMode = 1;
        Click("Nút Thử lại");
        yield return WaitLoaded();
        yield return WaitAnim();
        Check("thử lại được, chưa có bài thì mời làm bài đầu tiên", Status("Chưa có kết quả").Contains("bài đầu tiên") && Status("Số: Bài đã làm") == "0" && Status("Số: Điểm trung bình") == "–", Status("Chưa có kết quả"));
        Check("ít bài thì nói cần thêm, không tự đưa ra số tiến bộ", Status("Số: Tiến bộ") == "–" && Status("Ghi chú: Tiến bộ").Contains("Cần thêm 6"), Status("Ghi chú: Tiến bộ"));
        yield return ShotDone("trang_chinh_chua_co_bai");

        // Hết phiên giữa chừng
        scoreMode = 3;
        Click("Mục: Điểm số");
        yield return Frames(1);
        Click("Mục: Tổng quan");
        yield return WaitScreen(menu, MainMenu.Screen.Login);
        yield return WaitAnim();
        Check("hết phiên: về đăng nhập, báo rõ lý do, bỏ trạng thái khách", menu.Current == MainMenu.Screen.Login && Status("Thông báo đăng nhập").Contains("hết hạn") && !GameSession.GuestActive && GameSession.User == null, Status("Thông báo đăng nhập"));

        // Đăng nhập lại: trở về trạng thái có điểm cho phần còn lại của bài thử
        scoreMode = 0;
        Field("Ô đăng nhập: tên").text = "hs2";
        Field("Ô đăng nhập: mật khẩu").text = "dung";
        Click("Nút Đăng nhập");
        yield return WaitScreen(menu, MainMenu.Screen.Home);
        yield return WaitLoaded();
        Check("đăng nhập lại: tải đúng điểm", Status("Số: Bài đã làm") != "" && StudentData.HasData);
    }

    IEnumerator TeacherChecks(MainMenu menu)
    {
        Field("Ô đăng nhập: tên").text = "gv";
        Field("Ô đăng nhập: mật khẩu").text = "dung";
        Click("Nút Đăng nhập");
        yield return WaitScreen(menu, MainMenu.Screen.Home);
        yield return WaitAnim();
        Check("giáo viên: không có mục điểm cá nhân, có Trợ lý và Hướng dẫn", FindButton("Mục: Điểm số") == null && FindButton("Mục: Trợ lý AI") != null && FindButton("Mục: Hướng dẫn") != null);
        Check("giáo viên: không có thẻ số liệu", Status("Số: Điểm trung bình") == "");
        yield return ShotDone("trang_chinh_giao_vien");
        Click("Nút Đăng xuất");
        yield return WaitScreen(menu, MainMenu.Screen.Login);
    }

    void LogicChecks()
    {
        var parsed = StudentData.Parse("[{\"id\":1,\"ten_bai\":\"A\",\"thoi_gian\":1,\"diem\":null,\"nhan_xet\":\"\"},{\"id\":2,\"ten_bai\":\"B\",\"thoi_gian\":2,\"diem\":0,\"nhan_xet\":\"\"}]");
        Check("đọc JSON: mới nhất lên đầu", parsed != null && parsed.Count == 2 && parsed[0].id == 2);
        Check("đọc JSON: chưa chấm khác điểm 0", parsed != null && parsed[0].Graded && parsed[0].diem == 0f && !parsed[1].Graded);
        Check("đọc JSON: dữ liệu hỏng trả null, không ném lỗi", StudentData.Parse("<html>") == null && StudentData.Parse("") == null && StudentData.Parse("[{\"id\":") == null);
        var few = StudentStats.From(parsed);
        Check("thống kê: bài chưa chấm không tính vào trung bình", few.done == 2 && few.graded == 1 && few.average == 0f && !few.hasGrowth);
        Check("thống kê: danh sách rỗng không chia cho 0", StudentStats.From(new System.Collections.Generic.List<StudentResult>()).average == 0f);
        var five = new System.Collections.Generic.List<StudentResult>();
        for (int i = 0; i < 5; i++) five.Add(new StudentResult { thoi_gian = i, diem = 8f });
        Check("thống kê: 5 bài đã chấm chưa đủ để so tiến bộ", !StudentStats.From(five).hasGrowth);
        five.Add(new StudentResult { thoi_gian = 9, diem = 2f });
        var six = StudentStats.From(five);
        Check("thống kê: 6 bài thì tính được, giảm là số âm", six.hasGrowth && Mathf.Abs(six.growth + 2f) < 0.001f, six.growth.ToString());
        Check("tên gọi lấy chữ cuối", MenuKit.GivenName(" Nguyễn Thị  Mai ") == "Mai" && MenuKit.Initial("Lê Bảo Trọng") == "T" && MenuKit.Initial("") == "?");
        Check("tiện ích chuyển động: kết thúc đúng 1", Mathf.Approximately(MenuAnim.Evaluate(MenuAnim.Ease.OutCubic, 1f), 1f) && Mathf.Approximately(MenuAnim.Evaluate(MenuAnim.Ease.OutBack, 1f), 1f) && Mathf.Approximately(MenuAnim.Evaluate(MenuAnim.Ease.InOutCubic, 1f), 1f));
    }

    static int RowCount()
    {
        var list = GameObject.Find("Danh sách kết quả");
        return list != null ? list.transform.childCount : -1;
    }

    static bool StudentLoading()
    {
        var o = FindAnyObjectByType<DashboardShell>();
        return o != null && o.Overview.Loading;
    }

    static IEnumerator WaitLoaded()
    {
        float t0 = Time.realtimeSinceStartup;
        yield return null;
        while (StudentLoading() && Time.realtimeSinceStartup - t0 < 12f) yield return null;
    }

    static IEnumerator WaitAnim()
    {
        float t0 = Time.realtimeSinceStartup;
        yield return null;
        while (MenuAnim.Busy && Time.realtimeSinceStartup - t0 < 6f) yield return null;
        yield return null;
    }

    IEnumerator ShotDone(string name)
    {
        yield return WaitAnim();
        Shot(name);
    }

    // Máy chủ tài khoản thật ở cổng 8000 (server/, do phiên trang quản lý chạy). Tệp: dòng 1 tên, dòng 2 mật khẩu.
    IEnumerator RealServer(string file)
    {
        var lines = File.ReadAllLines(file);
        string user = lines[0], pw = lines[1];
        PlayerPrefs.DeleteKey("vera.may_chu");
        GameSession.SignOut();
        string sessionFile = Path.Combine(Application.persistentDataPath, "phien_dang_nhap.txt");
        if (File.Exists(sessionFile)) File.Delete(sessionFile);
        yield return Frames(5);
        var menu = FindAnyObjectByType<MainMenu>();
        Check("địa chỉ mặc định là máy chủ trực tuyến", GameSession.ServerUrl == GameSession.CloudServerUrl);
        GameSession.ServerUrl = GameSession.LocalServerUrl;  // bài này thử bản FastAPI ở cổng 8000

        Field("Ô đăng nhập: tên").text = user;
        Field("Ô đăng nhập: mật khẩu").text = "mat_khau_sai_unity";
        Click("Nút Đăng nhập");
        yield return WaitStatus("Thông báo đăng nhập", "Sai");
        Check("máy chủ thật: sai mật khẩu bị từ chối", Status("Thông báo đăng nhập").Contains("Sai") && GameSession.Token == null, Status("Thông báo đăng nhập"));

        Field("Ô đăng nhập: mật khẩu").text = pw;
        Click("Nút Đăng nhập");
        yield return WaitScreen(menu, MainMenu.Screen.Home);
        Check("máy chủ thật: đăng nhập được", menu.Current == MainMenu.Screen.Home && GameSession.User != null, GameSession.User != null ? $"{GameSession.User.ho_ten} / {GameSession.User.vai_tro} / {(GameSession.User.lop != null ? GameSession.User.lop.Length : -1)} lớp" : "");
        Shot("that_da_dang_nhap");

        // mở lại app: phiên lưu còn dùng được qua /auth/toi
        string token = GameSession.Token;
        GameSession.Token = null;
        GameSession.User = null;
        SceneManager.LoadScene(GameSession.MenuScene);
        yield return Frames(3);
        menu = FindAnyObjectByType<MainMenu>();
        yield return WaitScreen(menu, MainMenu.Screen.Home);
        Check("máy chủ thật: mở lại vẫn còn phiên", GameSession.Token == token && GameSession.User != null);

        // vào lab, sự kiện và AI gửi kèm mã phiên: máy chủ không được trả 401
        Click("Nút Vào phòng thí nghiệm");
        yield return WaitLab();
        yield return Frames(30);
        using (var r = UnityEngine.Networking.UnityWebRequest.Get(GameSession.ServerUrl + "/lab/sinh/tong-quan"))
        {
            AccountClient.Authorize(r);
            yield return r.SendWebRequest();
            AccountClient.CheckExpired(r);
            Check("máy chủ thật: đường dẫn của Unity nhận mã phiên", r.responseCode == 200 && GameSession.Token == token, r.responseCode.ToString());
        }
        var keepUser = GameSession.User;
        GameSession.Token = "ma_phien_sai_cua_unity";
        using (var r = UnityEngine.Networking.UnityWebRequest.Get(GameSession.ServerUrl + "/lab/sinh/tong-quan"))
        {
            AccountClient.Authorize(r);
            yield return r.SendWebRequest();
            AccountClient.CheckExpired(r);
            Check("máy chủ thật: mã sai trả 401, app bỏ mã và chuyển dùng thử", r.responseCode == 401 && GameSession.Token == null && GameSession.GuestActive, r.responseCode.ToString());
        }
        GameSession.GuestActive = false;
        GameSession.Token = token;
        GameSession.User = keepUser;
        GameSession.ReturnToMenu();
        yield return WaitScene(GameSession.MenuScene);
        yield return Frames(3);
        menu = FindAnyObjectByType<MainMenu>();

        Click("Nút Đăng xuất");
        yield return WaitScreen(menu, MainMenu.Screen.Login);
        Check("máy chủ thật: đăng xuất xóa phiên", GameSession.Token == null && !File.Exists(sessionFile));

        // đăng ký bằng mã lớp thử
        Click("Nút chuyển Tạo tài khoản");
        yield return Frames(2);
        string newUser = "thu.unity." + DateTime.Now.ToString("MMddHHmmss");
        Field("Ô đăng ký: mã lớp").text = "MA_LOP_SAI";
        Field("Ô đăng ký: họ tên").text = "Tài khoản thử Unity";
        Field("Ô đăng ký: tên đăng nhập").text = newUser;
        string newPw = "ThuUnity" + DateTime.Now.Ticks;
        Field("Ô đăng ký: mật khẩu").text = newPw;
        Field("Ô đăng ký: nhập lại").text = newPw;
        Click("Nút Tạo tài khoản");
        float t1 = Time.realtimeSinceStartup;
        while (Status("Thông báo đăng ký").StartsWith("Đang") && Time.realtimeSinceStartup - t1 < 8f) yield return null;
        yield return Frames(2);
        Check("máy chủ thật: mã lớp sai bị từ chối, hiện câu tiếng Việt", menu.Current == MainMenu.Screen.Register && Status("Thông báo đăng ký").Length > 0 && GameSession.Token == null, Status("Thông báo đăng ký"));
        Field("Ô đăng ký: mã lớp").text = "VERA2A";
        Click("Nút Tạo tài khoản");
        yield return WaitScreen(menu, MainMenu.Screen.Home);
        Check("máy chủ thật: đăng ký có phiên ngay", menu.Current == MainMenu.Screen.Home && GameSession.User != null && GameSession.Token != null, GameSession.User != null ? GameSession.User.ten_dang_nhap : Status("Thông báo đăng ký"));
        Shot("that_da_dang_ky");
        Click("Nút Đăng xuất");
        yield return WaitScreen(menu, MainMenu.Screen.Login);

        // trùng tên
        Click("Nút chuyển Tạo tài khoản");
        yield return Frames(2);
        Field("Ô đăng ký: mã lớp").text = "VERA2A";
        Field("Ô đăng ký: tên đăng nhập").text = newUser;
        Field("Ô đăng ký: mật khẩu").text = newPw;
        Field("Ô đăng ký: nhập lại").text = newPw;
        Click("Nút Tạo tài khoản");
        t1 = Time.realtimeSinceStartup;
        yield return Frames(2);
        while (Status("Thông báo đăng ký").StartsWith("Đang") && Time.realtimeSinceStartup - t1 < 8f) yield return null;
        Check("máy chủ thật: trùng tên hiện lỗi của máy chủ", menu.Current == MainMenu.Screen.Register && !Status("Thông báo đăng ký").StartsWith("Điền đủ") && Status("Thông báo đăng ký").Length > 0, Status("Thông báo đăng ký"));

        Check("không có lỗi nào trong log", errors == 0, $"{errors} lỗi");
        PlayerPrefs.DeleteKey("vera.may_chu");
        Debug.Log($"[KHKT-PLAY] [menu] Tổng máy chủ thật: đạt {pass}, hỏng {fail}");
    }

    // ------------------------------------------------------------------ tiện ích

    static IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }

    static InputField Field(string name) => FindObjectsByType<InputField>().First(f => f.name == name);

    static Button FindButton(string name) => FindObjectsByType<Button>().FirstOrDefault(b => b.name == name && b.gameObject.activeInHierarchy);

    void Click(string name)
    {
        var b = FindButton(name);
        if (b == null) { Check("tìm thấy nút " + name, false); return; }
        b.onClick.Invoke();
    }

    static string Status(string name)
    {
        var t = FindObjectsByType<Text>().FirstOrDefault(x => x.name == name);
        return t != null ? t.text : "";
    }

    static IEnumerator WaitStatus(string name, string contains)
    {
        float t0 = Time.realtimeSinceStartup;
        while (!Status(name).Contains(contains) && Time.realtimeSinceStartup - t0 < 10f) yield return null;
    }

    static IEnumerator WaitScreen(MainMenu m, MainMenu.Screen s)
    {
        float t0 = Time.realtimeSinceStartup;
        while (m != null && m.Current != s && Time.realtimeSinceStartup - t0 < 10f) yield return null;
    }

    static IEnumerator WaitScene(string name)
    {
        float t0 = Time.realtimeSinceStartup;
        while (SceneManager.GetActiveScene().name != name && Time.realtimeSinceStartup - t0 < 300f) yield return null;
    }

    static IEnumerator WaitLab()
    {
        yield return WaitScene(GameSession.LabScene);
        float t0 = Time.realtimeSinceStartup;
        while (FindAnyObjectByType<IntroScreen>() == null && Time.realtimeSinceStartup - t0 < 60f) yield return null;
    }

    void Shot(string name) => Capture(FindAnyObjectByType<MainMenu>().menuCamera, name);

    void ShotMain(string name) => Capture(FindAnyObjectByType<ModernUI>().viewCamera, name);

    void Capture(Camera c, string name)
    {
        Canvas.ForceUpdateCanvases();
        var old = c.targetTexture;
        c.targetTexture = rt;
        c.Render();
        RenderTexture.active = rt;
        tex.ReadPixels(new UnityEngine.Rect(0, 0, 1600, 900), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        c.targetTexture = old;
        File.WriteAllBytes(Path.Combine(outDir, name + ".png"), tex.EncodeToPNG());
    }

    int CountNear(Color c)
    {
        int n = 0;
        foreach (var p in tex.GetPixels32())
            if (Mathf.Abs(p.r / 255f - c.r) + Mathf.Abs(p.g / 255f - c.g) + Mathf.Abs(p.b / 255f - c.b) < 0.08f) n++;
        return n;
    }

    int ColorsInRightHalf()
    {
        var seen = new System.Collections.Generic.HashSet<int>();
        var px = tex.GetPixels32();
        for (int y = 0; y < 900; y += 3)
            for (int x = 800; x < 1600; x += 3)
            {
                var p = px[y * 1600 + x];
                seen.Add((p.r >> 3) << 10 | (p.g >> 3) << 5 | (p.b >> 3));
            }
        return seen.Count;
    }

    // ------------------------------------------------------------------ máy chủ giả theo hợp đồng API

    void StartFake()
    {
        http = new HttpListener();
        http.Prefixes.Add(FakeUrl + "/");
        http.Start();
        var th = new Thread(() =>
        {
            while (http != null && http.IsListening)
            {
                HttpListenerContext c;
                try { c = http.GetContext(); }
                catch { return; }
                try { Handle(c); }
                catch (Exception e) { Debug.Log("[KHKT-PLAY] [menu] máy chủ giả lỗi: " + e.Message); }
            }
        }) { IsBackground = true };
        th.Start();
    }

    const string UserJson = "{\"id\":7,\"ten_dang_nhap\":\"hs2\",\"ho_ten\":\"Nguyễn Văn A\",\"vai_tro\":\"hoc_sinh\",\"phai_doi_mat_khau\":false,\"lop\":[{\"id\":1,\"ten\":\"10A1\",\"nam_hoc\":\"2026-2027\"}]}";
    const string TeacherJson = "{\"id\":9,\"ten_dang_nhap\":\"gv\",\"ho_ten\":\"Phạm Thị C\",\"vai_tro\":\"giao_vien\",\"phai_doi_mat_khau\":false,\"lop\":[]}";
    // 7 bài: 6 đã chấm 5, 6, 7, 8, 9, 10 (trung bình 7,5; tiến bộ +3), bài mới nhất chưa chấm
    const string ScoresJson = "[" +
        "{\"id\":1,\"hoc_sinh_id\":7,\"lop\":\"10A1\",\"ma_bai\":\"a\",\"ten_bai\":\"Bài 1\",\"thoi_gian\":1790000000.5,\"tom_tat\":\"\",\"diem\":5,\"nhan_xet\":\"\"}," +
        "{\"id\":2,\"hoc_sinh_id\":7,\"lop\":\"10A1\",\"ma_bai\":\"b\",\"ten_bai\":\"Bài 2\",\"thoi_gian\":1790100000,\"tom_tat\":\"\",\"diem\":6.0,\"nhan_xet\":\"Cũ hơn\"}," +
        "{\"id\":3,\"hoc_sinh_id\":7,\"lop\":\"10A1\",\"ma_bai\":\"c\",\"ten_bai\":\"Bài 3\",\"thoi_gian\":1790200000,\"tom_tat\":\"\",\"diem\":7,\"nhan_xet\":\"\"}," +
        "{\"id\":4,\"hoc_sinh_id\":7,\"lop\":\"10A1\",\"ma_bai\":\"d\",\"ten_bai\":\"Nhuộm Gram\",\"thoi_gian\":1790300000,\"tom_tat\":\"\",\"diem\":8,\"nhan_xet\":\"\"}," +
        "{\"id\":5,\"hoc_sinh_id\":7,\"lop\":\"10A1\",\"ma_bai\":\"e\",\"ten_bai\":\"Quan sát tế bào hành\",\"thoi_gian\":1790400000,\"tom_tat\":\"\",\"diem\":9,\"nhan_xet\":\"\"}," +
        "{\"id\":6,\"hoc_sinh_id\":7,\"lop\":\"10A1\",\"ma_bai\":\"f\",\"ten_bai\":\"Chuẩn độ acid - base\",\"thoi_gian\":1790500000,\"tom_tat\":\"\",\"diem\":10,\"nhan_xet\":\"Thao tác buret tốt, cần ghi số đọc cẩn thận hơn.\"}," +
        "{\"id\":7,\"hoc_sinh_id\":7,\"lop\":\"10A1\",\"ma_bai\":\"g\",\"ten_bai\":\"Pha loãng dung dịch\",\"thoi_gian\":1790600000,\"tom_tat\":\"\",\"diem\":null,\"nhan_xet\":\"\"}]";
    const string MustChangeJson = "{\"id\":8,\"ten_dang_nhap\":\"doimk\",\"ho_ten\":\"Trần Thị B\",\"vai_tro\":\"hoc_sinh\",\"phai_doi_mat_khau\":true,\"lop\":[]}";

    void Handle(HttpListenerContext c)
    {
        string path = c.Request.Url.AbsolutePath;
        string body;
        using (var r = new StreamReader(c.Request.InputStream, Encoding.UTF8)) body = r.ReadToEnd();
        int code = 200;
        string reply = "{}";
        string auth = c.Request.Headers["Authorization"] ?? "";
        if (path == "/auth/dang-nhap")
        {
            loginCalls++;
            if (wrongTries > 0) { code = 429; c.Response.AddHeader("Retry-After", "30"); }
            else if (body.Contains("\"sai\"")) code = 401;
            else if (!body.Contains("\"kieu\":\"ung_dung\"")) code = 400;
            else reply = "{\"ma_phien\":\"PHIEN123\",\"het_han\":\"2026-12-01T00:00:00Z\",\"nguoi_dung\":" + (body.Contains("doimk") ? MustChangeJson : body.Contains("\"gv\"") ? TeacherJson : UserJson) + "}";
        }
        else if (path == "/auth/dang-ky")
        {
            if (!body.Contains("\"ma_lop\":\"10A1\"")) { code = 403; reply = "{\"detail\":\"Mã lớp không tồn tại\"}"; }
            else if (!body.Contains("\"kieu\":\"ung_dung\"")) code = 400;
            else reply = "{\"ma_phien\":\"PHIEN123\",\"het_han\":\"2026-12-01T00:00:00Z\",\"nguoi_dung\":" + UserJson + "}";
        }
        else if (path == "/api/nhat-ky")
        {
            lastAuth = auth;
            if (auth != "" && auth != "Bearer PHIEN123") code = 401;
        }
        else if (path == "/api/toi/ket-qua")
        {
            if (auth != "Bearer PHIEN123" || scoreMode == 3) code = 401;
            else if (scoreMode == 2) code = 500;
            else reply = scoreMode == 1 ? "[]" : ScoresJson;
        }
        else if (path == "/auth/toi")
        {
            if (auth == "Bearer PHIEN123") reply = UserJson;
            else code = 401;
        }
        else if (path == "/auth/dang-xuat" || path == "/auth/doi-mat-khau")
        {
            lastAuth = auth;
            if (auth != "Bearer PHIEN123") code = 401;
            else if (body.Contains("cu_sai")) { code = 400; reply = "{\"detail\":\"Mật khẩu hiện tại không đúng\"}"; }
        }
        else if (path != "/suc-khoe") code = 404;
        var bytes = Encoding.UTF8.GetBytes(reply);
        c.Response.StatusCode = code;
        c.Response.ContentType = "application/json";
        c.Response.OutputStream.Write(bytes, 0, bytes.Length);
        c.Response.Close();
    }
}
