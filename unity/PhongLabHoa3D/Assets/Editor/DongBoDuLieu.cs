using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace PhongThiNghiem.Editor
{
    public static class DongBoDuLieu
    {
        private const string DUONG_DAN_NGUON_TUONG_DOI = "frontend/hoa-hoc/phong-thi-nghiem";

        [MenuItem("Phòng Lab/Đồng bộ dữ liệu từ web")]
        public static void DongBo()
        {
            string repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "../../Documents/Antigravity"));
            if (!Directory.Exists(Path.Combine(repoRoot, DUONG_DAN_NGUON_TUONG_DOI)))
            {
                repoRoot = Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Documents/Antigravity"));
            }

            string nguon = Path.Combine(repoRoot, DUONG_DAN_NGUON_TUONG_DOI);
            if (!Directory.Exists(nguon))
            {
                // Fallback nếu đang chạy trực tiếp từ repo
                repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
                nguon = Path.Combine(repoRoot, DUONG_DAN_NGUON_TUONG_DOI);
            }

            if (!Directory.Exists(nguon))
            {
                Debug.LogError($"[DongBoDuLieu] Không tìm thấy thư mục nguồn: {nguon}");
                return;
            }

            string dichStreaming = Path.Combine(Application.streamingAssetsPath, "ptn");
            Directory.CreateDirectory(dichStreaming);
            Directory.CreateDirectory(Path.Combine(dichStreaming, "thi-nghiem"));

            string dichResources = Path.Combine(Application.dataPath, "Resources", "ptn");
            Directory.CreateDirectory(dichResources);
            Directory.CreateDirectory(Path.Combine(dichResources, "thi-nghiem"));

            // 1. Sao chép JS cho StreamingAssets (dùng bởi macOS Standalone Jint)
            string[] tapTinJs = { "tinh-toan.js", "mo-phong.js" };
            foreach (var f in tapTinJs)
            {
                string src = Path.Combine(nguon, f);
                if (File.Exists(src))
                {
                    File.Copy(src, Path.Combine(dichStreaming, f), true);
                }
            }

            // 2. Sao chép các file JSON dùng chung vào cả StreamingAssets và Resources/ptn
            string[] tapTinJson = { "bang-ion.json", "ban-do.json" };
            foreach (var f in tapTinJson)
            {
                string src = Path.Combine(nguon, f);
                if (File.Exists(src))
                {
                    File.Copy(src, Path.Combine(dichStreaming, f), true);
                    File.Copy(src, Path.Combine(dichResources, f), true);
                }
            }

            // Nguyên tố tuần hoàn
            string nguyenToSrc = Path.Combine(repoRoot, "frontend", "hoa-hoc", "bang-tuan-hoan", "nguyen-to.json");
            if (File.Exists(nguyenToSrc))
            {
                File.Copy(nguyenToSrc, Path.Combine(dichStreaming, "nguyen-to.json"), true);
                File.Copy(nguyenToSrc, Path.Combine(dichResources, "nguyen-to.json"), true);
            }

            // Tòa nhà
            string toaNhaSrc = Path.Combine(Application.dataPath, "DuLieu", "toa-nha.json");
            if (!File.Exists(toaNhaSrc))
            {
                toaNhaSrc = Path.Combine(dichStreaming, "toa-nha.json");
            }
            if (File.Exists(toaNhaSrc))
            {
                File.Copy(toaNhaSrc, Path.Combine(dichStreaming, "toa-nha.json"), true);
                File.Copy(toaNhaSrc, Path.Combine(dichResources, "toa-nha.json"), true);
            }

            // Tài nguyên
            string taiNguyenSrc = Path.Combine(repoRoot, "TAI_NGUYEN.json");
            if (!File.Exists(taiNguyenSrc))
            {
                taiNguyenSrc = Path.Combine(Application.dataPath, "..", "TAI_NGUYEN.json");
            }
            if (File.Exists(taiNguyenSrc))
            {
                File.Copy(taiNguyenSrc, Path.Combine(dichStreaming, "TAI_NGUYEN.json"), true);
                File.Copy(taiNguyenSrc, Path.Combine(dichResources, "TAI_NGUYEN.json"), true);
            }

            // 3. Sao chép thi-nghiem/*.json
            string nguonTn = Path.Combine(nguon, "thi-nghiem");
            if (Directory.Exists(nguonTn))
            {
                foreach (var f in Directory.GetFiles(nguonTn, "*.json"))
                {
                    string ten = Path.GetFileName(f);
                    File.Copy(f, Path.Combine(dichStreaming, "thi-nghiem", ten), true);
                    File.Copy(f, Path.Combine(dichResources, "thi-nghiem", ten), true);
                }
            }

            AssetDatabase.Refresh();
            Debug.Log("[DongBoDuLieu] Đồng bộ dữ liệu vào StreamingAssets/ptn và Resources/ptn thành công!");
        }
    }
}

