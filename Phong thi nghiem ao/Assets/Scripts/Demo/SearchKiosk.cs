using System;
using UnityEngine;

namespace Khkt.Demo
{
    /// <summary>Máy tra cứu ở cửa phòng hóa chất: bấm vào là mở bảng tìm kiếm hóa chất.</summary>
    public class SearchKiosk : MonoBehaviour, IDemoUsable
    {
        /// <summary>Giao diện tìm kiếm đăng kí vào đây.</summary>
        public static event Action OpenRequested;

        public string UseHint => "Bấm chuột trái: tra cứu vị trí hóa chất";

        public void Use() => OpenRequested?.Invoke();
    }
}
