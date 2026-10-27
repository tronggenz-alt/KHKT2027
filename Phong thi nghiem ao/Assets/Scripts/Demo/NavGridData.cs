using UnityEngine;

namespace Khkt.Demo
{
    /// <summary>
    /// Lưới đi bộ của robot nướng sẵn lúc dựng cảnh (mọi khu đều có mặt). Lúc chạy các khu ở xa đã được gỡ khỏi bộ nhớ
    /// nên không tính lại được bằng va chạm; NavGrid.Build đọc lưới từ đây. walk[i * nz + k] = 1: ô đi được.
    /// </summary>
    public class NavGridData : MonoBehaviour
    {
        public int nx, nz;
        [HideInInspector] public byte[] walk;
    }
}
