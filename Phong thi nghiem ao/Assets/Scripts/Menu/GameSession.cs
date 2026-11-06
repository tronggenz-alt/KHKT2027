using Khkt.Environment;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Khkt.Menu
{
    /// <summary>
    /// Trạng thái phiên chơi giữ qua các lần đổi cảnh: ai đang dùng, địa chỉ máy chủ tài khoản.
    /// </summary>
    public static class GameSession
    {
        public const string MenuScene = "ManHinhChinh";
        public const string LabScene = "PhongThiNghiem";
        public const string DefaultServerUrl = "http://127.0.0.1:8000";
        const string ServerUrlKey = "vera.may_chu";

        /// <summary>Vừa bấm vào lab từ màn hình chính; IntroScreen đọc rồi xóa khi phòng lab tải xong.</summary>
        public static bool EnterLabPending { get; set; }
        public static AccountUser User { get; set; }
        public static string Token { get; set; }
        public static bool GuestActive { get; set; }

        public static bool HasIdentity => User != null || GuestActive;
        public static string DisplayName => User != null ? User.ho_ten : "Khách";

        public static string ServerUrl
        {
            get => PlayerPrefs.GetString(ServerUrlKey, DefaultServerUrl);
            set
            {
                PlayerPrefs.SetString(ServerUrlKey, value.Trim().TrimEnd('/'));
                PlayerPrefs.Save();
            }
        }

        public static bool MenuAvailable => Application.CanStreamedLevelBeLoaded(MenuScene);

        public static void ReturnToMenu()
        {
            EnterLabPending = false;
            if (Khkt.Network.RoomConnection.InRoom) Khkt.Network.RoomConnection.Instance.Leave();
            DesktopViewer.UiCapture = 0;
            Time.timeScale = 1f;
            SceneManager.LoadScene(MenuScene);
        }

        public static void SignOut()
        {
            User = null;
            Token = null;
            GuestActive = false;
        }
    }
}
