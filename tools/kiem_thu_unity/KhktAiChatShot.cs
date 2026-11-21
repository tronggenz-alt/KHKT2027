using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Khkt.Demo;
using Khkt.Menu;
using UnityEngine;

// Chỉ trong bản sao kiểm thử (KHKT_TEST=anh_chat): hỏi trợ lý AI trong lab bằng khóa thật người dùng vừa nhập
// (tools/kiem_thu_unity/nhap_khoa_ai.ps1), chụp ảnh khung trò chuyện, lưu câu trả lời. Khóa không bao giờ được ghi log.
public static class KhktAiChatShot
{
    const string KeyFile = @"D:\KHKT_kiem_thu\khoa_ai_tam.bin";

    static void Log(string s) => Debug.Log("[KHKT-PLAY] [anh_chat] " + s);

    [Serializable] class Plain { public string nha_cung_cap, mo_hinh, khoa; }

    public static IEnumerator Run(Camera cam, string outDir, Action<string> shot)
    {
        if (!File.Exists(KeyFile)) { Log("LỖI  chưa có khóa: chạy tools/kiem_thu_unity/nhap_khoa_ai.ps1 trước"); yield break; }
        Plain p = null;
        try
        {
            var plain = Unprotect(File.ReadAllBytes(KeyFile), Encoding.UTF8.GetBytes("vera-anh-chat"));
            p = JsonUtility.FromJson<Plain>(Encoding.UTF8.GetString(plain));
            Array.Clear(plain, 0, plain.Length);
        }
        catch (Exception e) { Log("LỖI  không giải mã được khóa: " + e.GetType().Name); }
        if (p == null || string.IsNullOrEmpty(p.khoa)) { try { File.Delete(KeyFile); } catch { } yield break; }
        Log($"đã đọc khóa {ApiKeyStore.ProviderName(p.nha_cung_cap)}");

        string dir = Path.Combine(Application.temporaryCachePath, "khoa_ai_anh_" + DateTime.Now.Ticks);
        ApiKeyStore.Root = dir;
        GameSession.User = new AccountUser { id = 1, ten_dang_nhap = "hs_demo", ho_ten = "Học sinh", vai_tro = "hoc_sinh" };
        ApiKeyStore.Save(ApiKeyStore.Owner, new ApiKeyStore.Entry { provider = p.nha_cung_cap, model = p.mo_hinh, key = p.khoa });
        p = null;

        var chat = UnityEngine.Object.FindAnyObjectByType<RobotChatUI>();
        var ai = AiClient.Instance;
        if (chat == null || ai == null) { Log("LỖI  không thấy khung trò chuyện hoặc AiClient"); Cleanup(dir); yield break; }
        Log($"AI: {ai.Provider} · mô hình {(ai.Model.Length > 0 ? ai.Model : "tự chọn")}");

        string q = Environment.GetEnvironmentVariable("KHKT_CAU_HOI");
        if (string.IsNullOrEmpty(q)) q = "Nhỏ dung dịch NaOH vào dung dịch CuSO4 thì có hiện tượng gì? Bạn làm mẫu cho mình xem nhé.";
        chat.Open();
        yield return new WaitForSecondsRealtime(0.5f);
        var waitingField = typeof(RobotChatUI).GetField("waiting", BindingFlags.NonPublic | BindingFlags.Instance);
        var linesField = typeof(RobotChatUI).GetField("lines", BindingFlags.NonPublic | BindingFlags.Instance);
        chat.Send(q);
        Log("đã hỏi: " + q);
        yield return new WaitForSecondsRealtime(1.5f);
        shot("anh_chat_1_dang_nghi");
        float t0 = Time.realtimeSinceStartup;
        while (waitingField != null && (bool)waitingField.GetValue(chat) && Time.realtimeSinceStartup - t0 < 120) yield return null;
        Log($"trả lời sau {Time.realtimeSinceStartup - t0:0.0} s");
        if (ai.LastDirectError.Length > 0)
        {
            Log("lần 1 lỗi: " + ai.LastDirectError + " · hỏi lại sau 8 s");
            yield return new WaitForSecondsRealtime(8f);
            chat.Send(q);
            yield return new WaitForSecondsRealtime(1f);
            t0 = Time.realtimeSinceStartup;
            while (waitingField != null && (bool)waitingField.GetValue(chat) && Time.realtimeSinceStartup - t0 < 120) yield return null;
            Log($"lần 2 trả lời sau {Time.realtimeSinceStartup - t0:0.0} s");
        }
        yield return new WaitForSecondsRealtime(1f);
        if (!chat.IsOpen) chat.Open();
        shot("anh_chat_2_tra_loi");

        var lines = linesField?.GetValue(chat) as System.Collections.Generic.List<string>;
        string transcript = lines != null ? string.Join("\n", lines) : "";
        File.WriteAllText(Path.Combine(outDir, "cau_tra_loi.txt"), "AI: " + ai.Provider + " · " + ai.Model + "\n" + (ai.LastDirectError.Length > 0 ? "LỖI: " + ai.LastDirectError + "\n" : "") + "\n" + transcript, new UTF8Encoding(false));
        Log("hội thoại:\n" + transcript);
        if (ai.LastDirectError.Length > 0) Log("LỖI  " + ai.LastDirectError + " · giữ tệp khóa tạm (đã mã hóa) để chạy lại");
        else { try { File.Delete(KeyFile); } catch { } Log("đã xóa tệp khóa tạm"); }

        // Robot làm theo kế hoạch: chụp thêm sau một lúc
        yield return new WaitForSecondsRealtime(25f);
        if (!chat.IsOpen) chat.Open();
        shot("anh_chat_3_robot_lam");
        Cleanup(dir);
        Log("xong");
    }

    static void Cleanup(string dir)
    {
        ApiKeyStore.Forget();
        try { Directory.Delete(dir, true); } catch { }
        GameSession.User = null;
    }

    [StructLayout(LayoutKind.Sequential)] struct Blob { public int cbData; public IntPtr pbData; }
    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern bool CryptUnprotectData(ref Blob pDataIn, IntPtr ppszDataDescr, ref Blob pOptionalEntropy, IntPtr pvReserved, IntPtr pPromptStruct, int dwFlags, ref Blob pDataOut);
    [DllImport("kernel32.dll")] static extern IntPtr LocalFree(IntPtr hMem);

    static byte[] Unprotect(byte[] data, byte[] entropy)
    {
        var hIn = GCHandle.Alloc(data, GCHandleType.Pinned);
        var hEnt = GCHandle.Alloc(entropy, GCHandleType.Pinned);
        var outBlob = new Blob();
        try
        {
            var inBlob = new Blob { cbData = data.Length, pbData = hIn.AddrOfPinnedObject() };
            var entBlob = new Blob { cbData = entropy.Length, pbData = hEnt.AddrOfPinnedObject() };
            if (!CryptUnprotectData(ref inBlob, IntPtr.Zero, ref entBlob, IntPtr.Zero, IntPtr.Zero, 1, ref outBlob)) throw new System.Security.Cryptography.CryptographicException("DPAPI " + Marshal.GetLastWin32Error());
            var r = new byte[outBlob.cbData];
            Marshal.Copy(outBlob.pbData, r, 0, outBlob.cbData);
            return r;
        }
        finally { hIn.Free(); hEnt.Free(); if (outBlob.pbData != IntPtr.Zero) LocalFree(outBlob.pbData); }
    }
}
