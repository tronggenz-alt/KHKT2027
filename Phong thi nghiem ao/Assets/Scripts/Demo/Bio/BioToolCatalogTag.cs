// Tách từ BioCatalogLoader.cs: Unity chỉ lưu được thành phần khi tên lớp trùng tên file.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Khkt.Demo.Instruments;
using Khkt.Simulation.Chem;
using UnityEngine;

namespace Khkt.Demo.Bio
{
    /// <summary>
    /// Nhãn định danh mã dụng cụ chuẩn trong dung_cu.json được gắn lên từng GameObject tương tác ở Tầng 2.
    /// </summary>
    public class BioToolCatalogTag : MonoBehaviour
    {
        public string toolCode = "";
        public string roomCode = "";
    }
}
