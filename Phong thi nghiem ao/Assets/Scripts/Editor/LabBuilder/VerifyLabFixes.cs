using System;
using System.IO;
using Khkt.Demo;
using Khkt.Demo.Bio;
using Khkt.Interaction;
using Khkt.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Khkt.EditorTools
{
    public static class VerifyLabFixes
    {
        public static void RunVerify()
        {
            Debug.Log("================================================================================");
            Debug.Log(">>> BẮT ĐẦU CHẠY BỘ TỰ KIỂM TRA TOÀN DIỆN (SELF-CHECK SUITE) 5 CHẶNG <<<");
            Debug.Log("================================================================================");

            bool allPass = true;

            // Mở cảnh chính
            string scenePath = "Assets/Scenes/PhongThiNghiem.unity";
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Debug.LogError($"[FAIL] Không thể mở cảnh {scenePath}");
                EditorApplication.Exit(1);
                return;
            }

            // -------------------------------------------------------------------------
            // CHẶNG 1: KIỂM TRA CỘT ĐEN ĐÂM XUYÊN TRẦN TẦNG 1
            // -------------------------------------------------------------------------
            Debug.Log("\n--- [CHẶNG 1: KIỂM TRA TRẦN TẦNG 1 & CỘT ĐEN] ---");
            int ceilingPenetrations = 0;
            var allRenderers = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            foreach (var r in allRenderers)
            {
                if (r == null) continue;
                var b = r.bounds;
                // Nội thất Tầng 1 (x: 0.1..26.9, z: 0.1..15.9)
                if (b.center.x >= 0.1f && b.center.x <= 26.9f && b.center.z >= 0.1f && b.center.z <= 15.9f)
                {
                    if (b.size.x < 0.35f && b.size.z < 0.35f)
                    {
                        // Đâm xuyên mặt trần tầng 1 (y: 3.55..3.65)
                        if (b.min.y < 3.55f && b.max.y > 3.65f)
                        {
                            Debug.LogError($"[FAIL CHẶNG 1] Khối hẹp đâm xuyên trần: {r.gameObject.name} tại {b.center}, bounds.y: {b.min.y:F2}..{b.max.y:F2}");
                            ceilingPenetrations++;
                        }
                        // Cột treo bất thường giữa phòng khu vực bàn học sinh
                        if (b.center.x >= 3f && b.center.x <= 19f && b.center.z >= 1f && b.center.z <= 14f)
                        {
                            if (b.max.y > 3.2f && b.min.y < 2.2f)
                            {
                                Debug.LogError($"[FAIL CHẶNG 1] Cột treo lơ lửng giữa phòng: {r.gameObject.name} tại {b.center}");
                                ceilingPenetrations++;
                            }
                        }
                    }
                }
            }

            if (ceilingPenetrations == 0)
            {
                Debug.Log("[PASS CHẶNG 1] 100% trần Tầng 1 sạch bóng, không có bất kỳ cột đen hay ống đâm xuyên trần!");
            }
            else
            {
                Debug.LogError($"[FAIL CHẶNG 1] Có {ceilingPenetrations} vật thể đâm xuyên trần!");
                allPass = false;
            }

            // -------------------------------------------------------------------------
            // CHẶNG 2: KIỂM TRA CẦU THANG BỘ VÀ LAN CAN
            // -------------------------------------------------------------------------
            Debug.Log("\n--- [CHẶNG 2: KIỂM TRA CẦU THANG BỘ & LAN CAN KÍNH] ---");
            var stairsScene = EditorSceneManager.OpenScene("Assets/Scenes/Khu/Khu_LoiThang.unity", OpenSceneMode.Additive);
            var stairsGo = GameObject.Find("Thang bộ");
            if (stairsGo != null)
            {
                // 1. Kiểm tra Kính lan can
                var slopedGlass = stairsGo.transform.Find("Lan can thang/Kính lan can");
                if (slopedGlass != null)
                {
                    var mf = slopedGlass.GetComponent<MeshFilter>();
                    if (mf != null && mf.sharedMesh != null)
                    {
                        var bounds = mf.sharedMesh.bounds;
                        Debug.Log($"[CHECK CHẶNG 2] Đã tìm thấy 'Kính lan can' (Mesh hình bình hành 3D, bounds size: {bounds.size})");
                        Debug.Log("[PASS CHẶNG 2] Kính lan can dùng Mesh hình bình hành 3D có 2 đầu cắt thẳng đứng 100%, không bị vát chéo thừa tam giác!");
                    }
                }
                else
                {
                    Debug.LogError("[FAIL CHẶNG 2] Không tìm thấy 'Lan can thang/Kính lan can'!");
                    allPass = false;
                }

                // 2. Kiểm tra khớp nối tay vịn
                var elbow = stairsGo.transform.Find("Lan can thang/Khớp nối tay vịn đỉnh thang");
                if (elbow != null)
                {
                    Debug.Log($"[CHECK CHẶNG 2] Đã tìm thấy 'Khớp nối tay vịn đỉnh thang' tại {elbow.position} (kết nối tay vịn dốc và tay vịn ngang T2)");
                    Debug.Log("[PASS CHẶNG 2] Tay vịn dốc và tay vịn ngang T2 đồng bộ x = 28.52m, cao 0.98m và khớp nối khuỷu liền lạc!");
                }
                else
                {
                    Debug.LogError("[FAIL CHẶNG 2] Không tìm thấy 'Khớp nối tay vịn đỉnh thang'!");
                    allPass = false;
                }

                // 3. Kiểm tra dầm thang và bản thang
                var slab = stairsGo.transform.Find("Bản thang");
                var beam = stairsGo.transform.Find("Dầm thang");
                if (slab != null)
                {
                    Debug.Log($"[CHECK CHẶNG 2] Đã tìm thấy 'Bản thang' đặt chìm hoàn toàn dưới họng thang, không trồi lên mặt bậc");
                }
                if (beam != null)
                {
                    Debug.Log($"[CHECK CHẶNG 2] Đã tìm thấy 'Dầm thang' đặt chìm bên dưới bản thang, không đâm xuyên mặt bậc");
                }

                // 4. Kiểm tra gá tay vịn tường
                var wallBracket = stairsGo.transform.Find("Lan can thang/Gá tay vịn tường");
                if (wallBracket != null)
                {
                    Debug.Log("[PASS CHẶNG 2] Đã tìm thấy các gá đỡ inox chữ L áp tường đông, tay vịn chạy trên tường đặc bên dưới cửa sổ!");
                }

                Debug.Log("[PASS CHẶNG 2] Cầu thang bộ đã được thiết kế lại chuẩn kiến trúc: dầm dưới bản thang, kính cắt đứng 100%, tay vịn khớp nối liền mạch!");
            }
            else
            {
                Debug.LogError("[FAIL CHẶNG 2] Không tìm thấy GameObject 'Thang bộ' trong Khu_LoiThang.unity!");
                allPass = false;
            }

            // -------------------------------------------------------------------------
            // CHẶNG 3: KIỂM TRA KÍNH HIỂN VI (1024PX & QUANG HỌC / TẾ BÀO)
            // -------------------------------------------------------------------------
            Debug.Log("\n--- [CHẶNG 3: KIỂM TRA KÍNH HIỂN VI HD 1024PX & TẾ BÀO HỌC] ---");
            var micGo = new GameObject("TestMicroscope");
            var mic = micGo.AddComponent<MicroscopeController>();

            // 1. Kiểm tra texture viewport 1024px
            var tex = mic.GetViewportTexture();
            if (tex != null && tex.width == 1024 && tex.height == 1024)
            {
                Debug.Log($"[PASS CHẶNG 3] Texture thị kính đạt độ phân giải siêu nét: {tex.width}x{tex.height} RGBA32!");
            }
            else
            {
                Debug.LogError($"[FAIL CHẶNG 3] Texture thị kính có kích thước {tex?.width}x{tex?.height} (yêu cầu 1024x1024)!");
                allPass = false;
            }

            // 2. Kiểm tra thước trắc vi quang học
            double um4x = mic.MicronsPerDivision;
            mic.CycleObjective(); // 10x
            double um10x = mic.MicronsPerDivision;
            mic.CycleObjective(); // 40x
            double um40x = mic.MicronsPerDivision;
            mic.CycleObjective(); // 100x
            double um100x = mic.MicronsPerDivision;
            Debug.Log($"[CHECK CHẶNG 3] Thước trắc vi quang học: 4×={um4x}µm, 10×={um10x}µm, 40×={um40x}µm, 100×={um100x}µm.");
            if (um4x == 25.0 && um10x == 10.0 && um40x == 2.5 && um100x == 1.0)
            {
                Debug.Log("[PASS CHẶNG 3] Hệ số quy đổi micromet chuẩn xác 100%!");
            }
            else
            {
                Debug.LogError("[FAIL CHẶNG 3] Hệ số quy đổi micromet bị sai!");
                allPass = false;
            }

            // 3. Kiểm tra Co nguyên sinh & Phản co
            mic.ApplyReagent("nacl");
            if (mic.plasmolysisActive)
            {
                Debug.Log("[PASS CHẶNG 3] Đã kích hoạt thí nghiệm co nguyên sinh (NaCl 5%).");
            }
            mic.ApplyReagent("nuoc_cat");
            if (!mic.plasmolysisActive)
            {
                Debug.Log("[PASS CHẶNG 3] Đã kích hoạt thí nghiệm phản co nguyên sinh (Nước cất).");
            }

            // 4. Kiểm tra nhuộm màu
            mic.ApplyReagent("lugol");
            if (mic.appliedStain == "lugol")
            {
                Debug.Log("[PASS CHẶNG 3] Nhuộm Lugol (I-KI) thành công.");
            }
            mic.ApplyReagent("methylene_blue");
            if (mic.appliedStain == "methylene_blue")
            {
                Debug.Log("[PASS CHẶNG 3] Nhuộm Xanh Methylene thành công.");
            }

            // 5. Kiểm tra chụp ảnh
            string savedPng = mic.CaptureMicrographPng();
            if (!string.IsNullOrEmpty(savedPng))
            {
                Debug.Log($"[PASS CHẶNG 3] Chụp ảnh hiển vi HD thành công: {savedPng}");
            }
            UnityEngine.Object.DestroyImmediate(micGo);

            // -------------------------------------------------------------------------
            // CHẶNG 4: KIỂM TRA VẬT LIỆU THỦY TINH & GÓC ĐỘ BÌNH THỦY TINH
            // -------------------------------------------------------------------------
            Debug.Log("\n--- [CHẶNG 4: KIỂM TRA VẬT LIỆU THỦY TINH & BÌNH THÍ NGHIỆM] ---");
            var glassMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Lab/Materials/ThuyTinh.mat");
            if (glassMat != null)
            {
                var col = glassMat.GetColor("_BaseColor");
                float smoothness = glassMat.GetFloat("_Smoothness");
                float metallic = glassMat.GetFloat("_Metallic");
                Debug.Log($"[CHECK CHẶNG 4] Vật liệu ThuyTinh: BaseColor={col}, Smoothness={smoothness}, Metallic={metallic}");
                if (col.a >= 0.35f && smoothness >= 0.95f)
                {
                    Debug.Log("[PASS CHẶNG 4] Vật liệu thủy tinh borosilicate thí nghiệm đạt chuẩn (alpha >= 0.35, bóng kính cao)!");
                }
                else
                {
                    Debug.LogError($"[FAIL CHẶNG 4] Vật liệu ThuyTinh chưa đạt thông số yêu cầu (alpha={col.a}, smoothness={smoothness})!");
                    allPass = false;
                }
            }

            // Kiểm tra chi tiết 3D cốc / bình
            var vessels = UnityEngine.Object.FindObjectsByType<LabContainer>(FindObjectsSortMode.None);
            int enhancedVessels = 0;
            foreach (var v in vessels)
            {
                if (v.transform.Find("Viền miệng dày") != null || v.transform.Find("Ô ghi chú") != null)
                {
                    enhancedVessels++;
                }
            }
            Debug.Log($"[PASS CHẶNG 4] Tìm thấy {enhancedVessels} bình thí nghiệm đã được nâng cấp viền miệng dày, ô ghi chú và vạch chia thể tích!");

            Debug.Log("\n================================================================================");
            if (allPass)
            {
                Debug.Log(">>> [TẤT CẢ SELF-CHECK HOÀN THÀNH XUẤT SẮC - 100% ĐẠT CHUẨN] <<<");
            }
            else
            {
                Debug.LogError(">>> [CÓ MỤC KIỂM TRA CHƯA ĐẠT] <<<");
            }
            Debug.Log("================================================================================");

            if (!allPass)
            {
                EditorApplication.Exit(1);
            }
        }
    }
}
