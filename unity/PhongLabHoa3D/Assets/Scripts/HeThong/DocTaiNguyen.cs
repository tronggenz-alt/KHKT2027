using System.IO;
using UnityEngine;

namespace PhongThiNghiem.HeThong
{
    public static class DocTaiNguyen
    {
        public static string DocChuoi(string duongDanTuongDoi)
        {
            if (string.IsNullOrEmpty(duongDanTuongDoi)) return string.Empty;

            #if !UNITY_WEBGL || UNITY_EDITOR
            // 0. Nếu là đường dẫn tệp trực tiếp và tồn tại (ví dụ kiểm tra trong Editor)
            if (File.Exists(duongDanTuongDoi))
            {
                return File.ReadAllText(duongDanTuongDoi);
            }
            #endif

            // 1. Chuẩn hoá đường dẫn cho Resources (bỏ phần mở rộng, chuẩn hoá dấu gạch chéo)
            string duongDanChuan = duongDanTuongDoi.Replace('\\', '/').TrimStart('/');
            string duongDanResources = duongDanChuan;
            if (duongDanResources.EndsWith(".json", System.StringComparison.OrdinalIgnoreCase))
            {
                duongDanResources = duongDanResources.Substring(0, duongDanResources.Length - 5);
            }
            else if (duongDanResources.EndsWith(".txt", System.StringComparison.OrdinalIgnoreCase))
            {
                duongDanResources = duongDanResources.Substring(0, duongDanResources.Length - 4);
            }

            // Thử nạp qua Resources trực tiếp
            TextAsset textAsset = Resources.Load<TextAsset>(duongDanResources);

            if (textAsset == null)
            {
                if (!duongDanResources.StartsWith("ptn/", System.StringComparison.OrdinalIgnoreCase))
                {
                    textAsset = Resources.Load<TextAsset>("ptn/" + duongDanResources) ?? Resources.Load<TextAsset>("PTN/" + duongDanResources);
                }
                else
                {
                    string sub = duongDanResources.Substring(4);
                    textAsset = Resources.Load<TextAsset>(sub) ?? Resources.Load<TextAsset>("ptn/" + sub) ?? Resources.Load<TextAsset>("PTN/" + sub);
                }
            }

            // Thử tìm theo tên tệp không kèm thư mục trong ptn/ và PTN/
            if (textAsset == null)
            {
                string tenTep = Path.GetFileNameWithoutExtension(duongDanChuan);
                textAsset = Resources.Load<TextAsset>("ptn/" + tenTep) 
                         ?? Resources.Load<TextAsset>("PTN/" + tenTep)
                         ?? Resources.Load<TextAsset>("ptn/thi-nghiem/" + tenTep)
                         ?? Resources.Load<TextAsset>("PTN/thi-nghiem/" + tenTep);
            }

            if (textAsset != null)
            {
                return textAsset.text;
            }

            // 2. Fallback cho Editor và Standalone macOS qua File.ReadAllText
            #if !UNITY_WEBGL || UNITY_EDITOR
            string streamingPath = Path.Combine(Application.streamingAssetsPath, duongDanChuan);
            if (File.Exists(streamingPath))
            {
                return File.ReadAllText(streamingPath);
            }

            string ptnStreamingPath = Path.Combine(Application.streamingAssetsPath, "ptn", duongDanChuan);
            if (File.Exists(ptnStreamingPath))
            {
                return File.ReadAllText(ptnStreamingPath);
            }

            string duLieuPath = Path.Combine(Application.dataPath, duongDanChuan);
            if (File.Exists(duLieuPath))
            {
                return File.ReadAllText(duLieuPath);
            }

            string duLieuPath2 = Path.Combine(Application.dataPath, "DuLieu", Path.GetFileName(duongDanChuan));
            if (File.Exists(duLieuPath2))
            {
                return File.ReadAllText(duLieuPath2);
            }

            // Fallback repo root nếu chạy trong test runner
            string repoFallback = Path.Combine(Application.dataPath, "..", "..", duongDanChuan);
            if (File.Exists(repoFallback))
            {
                return File.ReadAllText(repoFallback);
            }
            #endif

            Debug.LogError($"[DocTaiNguyen] Không tìm thấy tài nguyên: {duongDanTuongDoi}");
            return null;
        }
    }
}
