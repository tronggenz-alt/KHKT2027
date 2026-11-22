using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Khkt.EditorTools
{
    public static class VerifyBioIntegration
    {
        public static void Run()
        {
            Debug.Log("================================================================================");
            Debug.Log(">>> BẮT ĐẦU KIỂM TRA TÍCH HỢP PHÒNG SINH HỌC 3D & COMPILATION <<<");
            Debug.Log("================================================================================");

            bool success = true;

            // 1. Kiểm tra biên dịch Assembly-CSharp
            try
            {
                var bridgeType = Type.GetType("Khkt.Demo.Bio.Portal.BioWebViewBridge, Assembly-CSharp");
                if (bridgeType == null)
                {
                    Debug.LogError("[FAIL] Không tìm thấy Type Khkt.Demo.Bio.Portal.BioWebViewBridge trong Assembly-CSharp!");
                    success = false;
                }
                else
                {
                    Debug.Log("[PASS] Đã load thành công Type Khkt.Demo.Bio.Portal.BioWebViewBridge");

                    var openMethod = bridgeType.GetMethod("Open", BindingFlags.Public | BindingFlags.Static);
                    var closeMethod = bridgeType.GetMethod("Close", BindingFlags.Public | BindingFlags.Static);
                    var onClosedMethod = bridgeType.GetMethod("OnWebViewClosed", BindingFlags.Public | BindingFlags.Instance);

                    if (openMethod == null || closeMethod == null || onClosedMethod == null)
                    {
                        Debug.LogError("[FAIL] Thiếu method cần thiết trong BioWebViewBridge (Open, Close, OnWebViewClosed)!");
                        success = false;
                    }
                    else
                    {
                        Debug.Log("[PASS] Đầy đủ các method Open, Close, OnWebViewClosed trong BioWebViewBridge");
                    }
                }

                var portalType = Type.GetType("Khkt.Demo.Bio.Portal.BioPortal, Assembly-CSharp");
                if (portalType == null)
                {
                    Debug.LogError("[FAIL] Không tìm thấy Type Khkt.Demo.Bio.Portal.BioPortal!");
                    success = false;
                }
                else
                {
                    Debug.Log("[PASS] Đã load thành công Type Khkt.Demo.Bio.Portal.BioPortal");
                }

                var transitionType = Type.GetType("Khkt.Demo.Bio.Portal.BioPortalTransition, Assembly-CSharp");
                if (transitionType == null)
                {
                    Debug.LogError("[FAIL] Không tìm thấy Type Khkt.Demo.Bio.Portal.BioPortalTransition!");
                    success = false;
                }
                else
                {
                    Debug.Log("[PASS] Đã load thành công Type Khkt.Demo.Bio.Portal.BioPortalTransition");
                    var startMethod = transitionType.GetMethod("StartOpenTransition", BindingFlags.Public | BindingFlags.Instance);
                    var returnMethod = transitionType.GetMethod("OnReturnFromBioLab", BindingFlags.Public | BindingFlags.Instance);

                    if (startMethod == null || returnMethod == null)
                    {
                        Debug.LogError("[FAIL] Thiếu method StartOpenTransition hoặc OnReturnFromBioLab trong BioPortalTransition!");
                        success = false;
                    }
                    else
                    {
                        Debug.Log("[PASS] Đầy đủ method StartOpenTransition & OnReturnFromBioLab trong BioPortalTransition");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FAIL] Lỗi kiểm tra reflection: {ex.Message}");
                success = false;
            }

            // 2. Kiểm tra StreamingAssets/biology_lab
            string streamingPath = Application.streamingAssetsPath;
            string bioDir = Path.Combine(streamingPath, "biology_lab");

            string[] requiredFiles = new string[]
            {
                Path.Combine(bioDir, "biology-lab-3d.html"),
                Path.Combine(bioDir, "sinh-hoc/bp3d.bin.gz"),
                Path.Combine(bioDir, "vendor/three/three.module.js"),
                Path.Combine(bioDir, "vendor/mathjax.3.2.2.js"),
                Path.Combine(bioDir, "vendor/gsap.3.12.5.min.js"),
                Path.Combine(bioDir, "vendor/three/addons/controls/OrbitControls.js"),
                Path.Combine(bioDir, "vendor/three/addons/loaders/GLTFLoader.js"),
                Path.Combine(bioDir, "vendor/three/addons/renderers/CSS2DRenderer.js")
            };

            foreach (var file in requiredFiles)
            {
                if (!File.Exists(file))
                {
                    Debug.LogError($"[FAIL] Không tìm thấy file StreamingAssets: {file}");
                    success = false;
                }
                else
                {
                    var fi = new FileInfo(file);
                    Debug.Log($"[PASS] File StreamingAssets tồn tại: {Path.GetFileName(file)} ({fi.Length:N0} bytes)");
                }
            }

            // 3. Kiểm tra Plugin iOS
            string pluginPath = Path.Combine(Application.dataPath, "Plugins/iOS/BioWebViewPlugin.mm");
            string metaPath = Path.Combine(Application.dataPath, "Plugins/iOS/BioWebViewPlugin.mm.meta");

            if (!File.Exists(pluginPath))
            {
                Debug.LogError($"[FAIL] Không tìm thấy file plugin iOS: {pluginPath}");
                success = false;
            }
            else
            {
                Debug.Log($"[PASS] Đã tìm thấy iOS Plugin: BioWebViewPlugin.mm");
            }

            if (!File.Exists(metaPath))
            {
                Debug.LogError($"[FAIL] Không tìm thấy .meta cho plugin iOS: {metaPath}");
                success = false;
            }
            else
            {
                Debug.Log($"[PASS] Đã tìm thấy .meta cho iOS Plugin");
            }

            // 4. Kiểm tra HTML exit button
            string htmlPath = Path.Combine(bioDir, "biology-lab-3d.html");
            if (File.Exists(htmlPath))
            {
                string htmlContent = File.ReadAllText(htmlPath);
                if (htmlContent.Contains("nut-ve-lab") &&
                    htmlContent.Contains("dongTrangSinhHoc") &&
                    htmlContent.Contains("veralab://close"))
                {
                    Debug.Log("[PASS] File biology-lab-3d.html chứa đầy đủ exit button và callback đóng (nut-ve-lab, dongTrangSinhHoc, veralab://close)");
                }
                else
                {
                    Debug.LogError("[FAIL] File biology-lab-3d.html thiếu exit button hoặc handler đóng!");
                    success = false;
                }
            }

            // 5. Kết luận
            Debug.Log("================================================================================");
            if (success)
            {
                Debug.Log(">>> [SUCCESS] TẤT CẢ KIỂM TRA TÍCH HỢP HOÀN TOÀN HỢP LỆ VÀ SẴN SÀNG! <<<");
                Debug.Log("================================================================================");
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(0);
                }
            }
            else
            {
                Debug.LogError(">>> [FAILED] CÓ LỖI TRONG QUÁ TRÌNH KIỂM TRA TÍCH HỢP! <<<");
                Debug.Log("================================================================================");
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }
            }
        }
    }
}
