using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using PhongThiNghiem.ToaNha;
using PhongThiNghiem.NhanVat;
using PhongThiNghiem.ThiNghiem;

namespace PhongThiNghiem.Editor
{
    public static class ChupAnhNhanVatCanhBan
    {
        [MenuItem("Phòng Lab/Chụp ảnh nhân vật cạnh bàn thí nghiệm", false, 150)]
        public static void ChupAnh()
        {
            ThucHienChupAnh(false);
        }

        public static void ChupAnhBatch()
        {
            ThucHienChupAnh(true);
        }

        private static void ThucHienChupAnh(bool isBatchmode)
        {
            Debug.Log("[ChupAnhNhanVat] Bắt đầu thiết lập cảnh và chụp ảnh nhân vật cạnh bàn...");

            GameObject goLab = null;
            GameObject goCam = null;
            GameObject goLight = null;
            GameObject goNv = null;
            RenderTexture rt = null;

            try
            {
                // 0. Cấu hình URP để hiển thị vật liệu chuẩn
                var urpAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.RenderPipelineAsset>("Assets/Settings/URP_Asset.asset");
                if (urpAsset != null)
                {
                    UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline = urpAsset;
                    QualitySettings.renderPipeline = urpAsset;
                }

                // 1. Dựng phòng lab
                goLab = new GameObject("LabPhongThiNghiem");
                var lab = goLab.AddComponent<DungPhongLab>();
                goLab.AddComponent<QuanLyTram>();

                string banDoPath = Path.Combine(Application.streamingAssetsPath, "ptn/ban-do.json");
                lab.DungToanBoPhong(banDoPath);

                // 2. Chiếu sáng
                goLight = new GameObject("LabAnhSang");
                var light = goLight.AddComponent<Light>();
                light.type = LightType.Directional;
                light.color = new Color(1f, 0.98f, 0.95f);
                light.intensity = 1.3f;
                goLight.transform.rotation = Quaternion.Euler(45f, -30f, 0f);

                // 3. Nhân vật giáo sư
                goNv = new GameObject("NhanVatGiaoSu");
                // Đặt tại trạm chuẩn độ acid-base (diem_dung: 3.5, 12.7)
                goNv.transform.position = new Vector3(3.5f, 0.05f, 12.7f);
                // Quay mặt về phía bàn thí nghiệm (tam: 3.5, 11.5)
                Vector3 huongBan = (new Vector3(3.5f, 0.95f, 11.5f) - goNv.transform.position).normalized;
                huongBan.y = 0;
                goNv.transform.rotation = Quaternion.LookRotation(huongBan, Vector3.up);

                var hinh = goNv.AddComponent<DungNhanVat>();
                var dk = goNv.AddComponent<DieuKhienNhanVat>();
                hinh.DeoDoBaoHo();

                // 4. Camera chụp ảnh góc chéo 3/4 nhìn rõ cả người giáo sư và bàn thí nghiệm
                goCam = new GameObject("CameraChupAnh");
                var cam = goCam.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.Color;
                cam.backgroundColor = new Color(0.12f, 0.15f, 0.20f);
                cam.nearClipPlane = 0.1f;
                cam.farClipPlane = 100f;
                cam.fieldOfView = 50f;

                // Vị trí camera chếch sang phải và lùi lại 2.2m
                Vector3 viTriCam = new Vector3(4.8f, 1.45f, 13.8f);
                Vector3 diemNhin = new Vector3(3.5f, 1.05f, 12.0f);
                cam.transform.position = viTriCam;
                cam.transform.LookAt(diemNhin);

                // 5. Render vào RenderTexture
                int rong = 1280;
                int cao = 720;
                rt = new RenderTexture(rong, cao, 24, RenderTextureFormat.ARGB32);
                cam.targetTexture = rt;
                cam.Render();

                RenderTexture.active = rt;
                Texture2D tex = new Texture2D(rong, cao, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, rong, cao), 0, 0);
                tex.Apply();
                RenderTexture.active = null;
                cam.targetTexture = null;

                byte[] bytes = tex.EncodeToPNG();
                UnityEngine.Object.DestroyImmediate(tex);

                // 6. Lưu tệp
                string repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", ".."));
                string thuMucOutputs = Path.Combine(repoRoot, "outputs", "app-lab");
                Directory.CreateDirectory(thuMucOutputs);

                string fileDich = Path.Combine(thuMucOutputs, "nhan-vat-canh-ban.png");
                File.WriteAllBytes(fileDich, bytes);
                Debug.Log($"[ChupAnhNhanVat] Đã lưu ảnh thành công tại: {fileDich} ({bytes.Length / 1024} KB)");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ChupAnhNhanVat] Lỗi khi chụp ảnh: {ex.Message}\n{ex.StackTrace}");
                if (isBatchmode) EditorApplication.Exit(1);
            }
            finally
            {
                if (rt != null)
                {
                    rt.Release();
                    UnityEngine.Object.DestroyImmediate(rt);
                }
                if (goNv != null) UnityEngine.Object.DestroyImmediate(goNv);
                if (goCam != null) UnityEngine.Object.DestroyImmediate(goCam);
                if (goLight != null) UnityEngine.Object.DestroyImmediate(goLight);
                if (goLab != null) UnityEngine.Object.DestroyImmediate(goLab);

                if (isBatchmode) EditorApplication.Exit(0);
            }
        }
    }
}
