// -----------------------------------------------------------------------------
// LabSceneBuilder.Floor2.Tools.cs — Gắn toàn bộ 108 dụng cụ và thiết bị tương tác
// của Tầng 2 (theo bảng Mục 2 HOP_DONG_GIAO_DIEN_BACKEND.md và data/sinh/dung_cu.json)
// vào các khu tương ứng khi dựng cảnh Tầng 2.
// -----------------------------------------------------------------------------
using UnityEngine;
using Khkt.Demo.Bio;

namespace Khkt.EditorTools
{
    public static partial class LabSceneBuilder
    {
        static void BuildFloor2InteractiveTools(Transform tang2)
        {
            if (tang2 == null) return;
            BioFloor2InteractiveTools.EnsureSpawnAllTools(tang2);
        }
    }
}
