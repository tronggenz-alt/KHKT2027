using System;
using UnityEngine;

namespace Khkt.Input
{
    /// <summary>
    /// Nguồn tay cầm chung: tay cầm serial thật, hoặc chuột-bàn phím khi không có tay cầm.
    /// Các script khác chỉ phụ thuộc lớp này, nên đổi nguồn (hoặc sau này làm bản Bluetooth/ESP32)
    /// không phải sửa phần tương tác.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public abstract class ControllerSource : MonoBehaviour
    {
        /// <summary>Mẫu mới nhất (cập nhật trong Update của nguồn, chạy trước các script khác).</summary>
        public ControllerState Latest { get; private set; }
        public abstract bool IsConnected { get; }
        /// <summary>Một dòng trạng thái cho bảng trợ giúp (đã nối ở đâu, bao nhiêu gói/giây, lỗi gì).</summary>
        public virtual string StatusText => IsConnected ? "đã nối" : "chưa nối";
        /// <summary>Tên ngắn của tay cầm đang nối, ví dụ "COM5" hoặc "192.168.1.50:4211".</summary>
        public virtual string ControllerName => "";

        /// <summary>Gọi trên luồng chính cho TỪNG mẫu (100 Hz), kể cả khi một khung hình nhận nhiều mẫu.</summary>
        public event Action<ControllerState> SampleReceived;

        ControllerButtons prevFrameButtons;
        ControllerButtons frameButtons;

        protected void Publish(in ControllerState s)
        {
            Latest = s;
            frameButtons |= s.Buttons;  // giữ cả lần bấm rất ngắn nằm giữa hai khung hình
            SampleReceived?.Invoke(s);
        }

        /// <summary>Nút vừa được bấm trong khung hình này.</summary>
        public bool ButtonDown(ControllerButtons b) => (frameButtons & b) != 0 && (prevFrameButtons & b) == 0;
        /// <summary>Nút vừa được thả trong khung hình này.</summary>
        public bool ButtonUp(ControllerButtons b) => (Latest.Buttons & b) == 0 && (prevFrameButtons & b) != 0;

        protected virtual void LateUpdate()
        {
            prevFrameButtons = Latest.Buttons;
            frameButtons = Latest.Buttons;
        }

        /// <summary>Rung liên tục, mức 0-255 (0 = tắt).</summary>
        public abstract void SetVibration(int level);
        /// <summary>Xung rung mức 0-255 trong ms. Motor cần khoảng 40 ms mới cảm nhận được.</summary>
        public abstract void Pulse(int level, int ms);
        public abstract void SetLeds(bool green, bool red);
        /// <summary>Đặt lại hướng theo trọng lực (dùng khi bộ lọc bị lệch sau va chạm mạnh).</summary>
        public abstract void ResetOrientation();
    }
}
