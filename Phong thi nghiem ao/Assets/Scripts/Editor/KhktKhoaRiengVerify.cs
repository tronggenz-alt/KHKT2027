using System.Collections.Generic;
using System.Linq;
using Khkt.CoChe;
using Khkt.Menu;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Kiểm thử khóa AI riêng gửi kèm yêu cầu Giải bài tập (MayChuCoChe.TieuDeKhoaRieng, MayChuCoChe.DuocGuiKhoa), không gọi mạng:
///   ~/AppBuild/VERA_claude/dong_bo_va_chay.sh &lt;worktree&gt; KhktKhoaRiengVerify.Run khoa_rieng
/// Thoát 0 = đạt, 1 = có kiểm tra trượt (in danh sách trong log).
/// </summary>
public static class KhktKhoaRiengVerify
{
    public static void Run()
    {
        var loi = new List<string>();
        void Kiem(bool dat, string ten) { if (!dat) loi.Add(ten); }
        Dictionary<string, string> TieuDe(string duong, string url, ApiKeyStore.Entry e) =>
            MayChuCoChe.TieuDeKhoaRieng(duong, url, e).ToDictionary(kv => kv.Key, kv => kv.Value);

        foreach (var u in new[] { "http://127.0.0.1:8010", "http://localhost:8010", "http://192.168.1.20:8010", "http://10.0.0.5:8010",
                                  "http://172.20.3.4:8010", "http://169.254.7.8:8010", "http://may-mac.local:8010", "https://vera.example.com",
                                  "http://[::1]:8010", "http://[fe80::1]:8010", "http://[fd12::3]:8010" })
            Kiem(MayChuCoChe.DuocGuiKhoa(u), "phải được gửi khóa: " + u);
        foreach (var u in new[] { "http://203.0.113.5:8010", "http://vera.example.com:8010", "http://172.32.0.1:8010", "http://192.169.0.1:8010",
                                  "http://[2001:db8::1]:8010", "ftp://127.0.0.1/", "khong-phai-url", "" })
            Kiem(!MayChuCoChe.DuocGuiKhoa(u), "không được gửi khóa: " + u);

        const string Lan = "http://127.0.0.1:8010";
        var claude = new ApiKeyStore.Entry { provider = ApiKeyStore.Claude, key = " sk-ant-gia-0123456789abcdef ", model = " claude-sonnet-5-5 " };
        var td = TieuDe("/co-che/giai-de", Lan, claude);
        Kiem(td.Count == 3 && td["X-Vera-Nha-Cung-Cap"] == "claude" && td["X-Vera-Khoa-AI"] == "sk-ant-gia-0123456789abcdef"
             && td["X-Vera-Mo-Hinh"] == "claude-sonnet-5-5", "tiêu đề khóa Claude (cắt khoảng trắng, có mô hình)");

        var gemini = new ApiKeyStore.Entry { provider = ApiKeyStore.Gemini, key = "AIzaGia0123456789" };
        td = TieuDe("/co-che/hoi-dap", Lan, gemini);
        Kiem(td.Count == 2 && td["X-Vera-Nha-Cung-Cap"] == "gemini" && !td.ContainsKey("X-Vera-Mo-Hinh"), "tiêu đề khóa Gemini (mô hình trống thì không gửi)");

        foreach (var d in new[] { "/co-che/giai-chuoi", "/co-che/mo-hinh-3d", "/co-che/giai-thich-chi-tiet" })
            Kiem(TieuDe(d, Lan, claude).Count == 3, "đường có AI phải mang khóa: " + d);
        foreach (var d in new[] { "/co-che/suc-khoe", "/co-che/bai", "/co-che/bao-sai", "/co-che/giai-thich" })
            Kiem(TieuDe(d, Lan, claude).Count == 0, "đường không gọi AI đám mây không mang khóa: " + d);
        Kiem(TieuDe("/co-che/giai-de", "http://203.0.113.5:8010", claude).Count == 0, "máy chủ Internet qua http trơn: không gửi khóa");
        Kiem(TieuDe("/co-che/giai-de", Lan, null).Count == 0, "không có khóa");
        Kiem(TieuDe("/co-che/giai-de", Lan, new ApiKeyStore.Entry { provider = ApiKeyStore.Claude, key = "" }).Count == 0, "khóa rỗng");
        foreach (var p in new[] { ApiKeyStore.Groq, ApiKeyStore.OpenAi })
            Kiem(TieuDe("/co-che/giai-de", Lan, new ApiKeyStore.Entry { provider = p, key = "gia-0123456789abcdef" }).Count == 0,
                 "máy chủ cơ chế chưa gọi được " + p + ": không gửi");

        if (loi.Count > 0)
        {
            Debug.LogError("[KHOA_RIENG] TRƯỢT " + loi.Count + ":\n  " + string.Join("\n  ", loi));
            EditorApplication.Exit(1);
            return;
        }
        Debug.Log("[KHOA_RIENG] ĐẠT mọi kiểm tra khóa AI riêng gửi kèm Giải bài tập");
        EditorApplication.Exit(0);
    }
}
