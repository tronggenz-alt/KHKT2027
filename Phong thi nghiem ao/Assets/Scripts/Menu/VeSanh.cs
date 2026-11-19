using Khkt.Environment;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Khkt.Menu
{
    /// <summary>
    /// 19/11: rời phòng lab về sảnh đầu (cảnh ManHinhChinh). Dọn web giải phẫu, mô-đun G4, con trỏ, UiCapture,
    /// timeScale và phòng mạng trước khi nạp cảnh. Về bảng điều khiển (mặc định, MainMenu.Start khi HasIdentity)
    /// hoặc về hẳn màn chọn 2 luồng "Giải bài tập" / "Phòng Lab" (GameSession.VeManChonKhiVe).
    /// </summary>
    public static class VeSanh
    {
        /// <summary>Đang làm dở một bài: chuẩn độ đã xong ít nhất 1 bước mà chưa tới bước cuối, hoặc bài sinh học chưa có kết quả.</summary>
        public static bool DangLamDo()
        {
            var p = Object.FindAnyObjectByType<Khkt.Demo.LabProcedure>();
            if (p != null && p.Active && p.Current != Khkt.Demo.LabProcedure.Step.Xong && System.Array.IndexOf(p.Done, true) >= 0)
                return true;
            var b = Khkt.Demo.Bio.BioLabProcedure.Instance;
            return b != null && b.Active && b.LastResult == null && System.Array.IndexOf(b.Done, true) >= 0;
        }

        public static bool CoTheVe => GameSession.MenuAvailable;

        public static void Ve(bool veManChon)
        {
            if (Khkt.Demo.Bio.Portal.BioWebViewBridge.IsOpen) Khkt.Demo.Bio.Portal.BioWebViewBridge.Close();
            if (Khkt.Demo.Bio.GiaiPhau.GiaiPhauMoDunImpl.DangMo) Khkt.Demo.Bio.GiaiPhau.GiaiPhauMoDunImpl.Dong();
            GameSession.VeManChonKhiVe = veManChon;
            if (!Application.isBatchMode)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            GameSession.ReturnToMenu();   // UiCapture = 0, timeScale = 1, rời phòng mạng, nạp ManHinhChinh (Single → camera lab bị hủy)
        }
    }
}
