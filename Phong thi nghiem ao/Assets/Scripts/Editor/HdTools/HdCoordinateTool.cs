using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Khkt.Demo;
using Khkt.Environment;
using Khkt.Interaction;
using Khkt.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Khkt.EditorTools
{
    /// <summary>
    /// Công cụ chụp và so sánh tọa độ của toàn bộ cảnh VERA Lab (cảnh lõi + 13 cảnh khu).
    /// Dùng để đảm bảo luật khóa tọa độ và bố cục: sai lệch vị trí tối đa 1 mm, góc 0.01°, tỉ lệ 0.1%.
    /// </summary>
    public static class HdCoordinateTool
    {
        public static string OutputPath => Path.GetFullPath(Path.Combine(Application.dataPath, "../../docs/do-hoa-hd/toa_do_goc.json"));

        [Serializable]
        public class ColliderInfo
        {
            public bool present;
            public string type;
            public float[] center;
            public float[] size;
            public bool isTrigger;
        }

        [Serializable]
        public class ObjectRecord
        {
            public string scene;
            public string path;
            public int siblingIndex;
            public float[] pos;     // [x, y, z]
            public float[] rot;     // [x, y, z, w]
            public float[] euler;   // [x, y, z]
            public float[] scale;   // [x, y, z]
            public ColliderInfo collider;
            public List<string> trackedComponents = new List<string>();
        }

        [Serializable]
        public class SnapshotData
        {
            public string createdAt;
            public string unityVersion;
            public string navGridHash;
            public int navGridNx;
            public int navGridNz;
            public int totalObjects;
            public List<ObjectRecord> records = new List<ObjectRecord>();
        }

        [MenuItem("KHKT/HD/Chụp tọa độ", priority = 200)]
        public static void CaptureMenu()
        {
            Capture();
            EditorUtility.DisplayDialog("Chụp tọa độ", $"Đã lưu mốc gốc vào:\n{OutputPath}", "Đóng");
        }

        [MenuItem("KHKT/HD/So tọa độ", priority = 201)]
        public static void CompareMenu()
        {
            int errors = Compare();
            if (errors == 0)
                EditorUtility.DisplayDialog("So tọa độ", "HOÀN HẢO: 0 lỗi so với mốc gốc!", "Đóng");
            else
                EditorUtility.DisplayDialog("So tọa độ", $"CẢNH BÁO: Phát hiện {errors} lỗi sai lệch so với mốc gốc! Xem chi tiết trong Console.", "Đóng");
        }

        public static void CaptureBatch()
        {
            try
            {
                Capture();
                Debug.Log("[HD-TOA-DO] Chụp mốc gốc thành công.");
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[HD-TOA-DO] Lỗi khi chụp mốc gốc: {ex}");
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }

        public static void CompareBatch()
        {
            try
            {
                int errors = Compare();
                if (errors == 0)
                {
                    Debug.Log("[HD-TOA-DO] KẾT QUẢ SO TỌA ĐỘ: 0 LỖI (ĐẠT).");
                    if (Application.isBatchMode) EditorApplication.Exit(0);
                }
                else
                {
                    Debug.LogError($"[HD-TOA-DO] KẾT QUẢ SO TỌA ĐỘ: {errors} LỖI (KHÔNG ĐẠT - LỖI CHẶN).");
                    if (Application.isBatchMode) EditorApplication.Exit(1);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[HD-TOA-DO] Lỗi khi so tọa độ: {ex}");
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }

        /// <summary>Chụp toàn bộ GameObjects trong cảnh lõi và 13 cảnh khu</summary>
        public static SnapshotData Capture()
        {
            var snapshot = new SnapshotData
            {
                createdAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                unityVersion = Application.unityVersion,
            };

            var scenes = GetAllScenePaths();
            OpenScenesAdditive(scenes);

            // 1. Tính mã băm lưới đi robot
            var navObj = GameObject.Find("Lưới đi robot (nướng sẵn)");
            if (navObj != null)
            {
                var navData = navObj.GetComponent<NavGridData>();
                if (navData != null && navData.walk != null)
                {
                    snapshot.navGridNx = navData.nx;
                    snapshot.navGridNz = navData.nz;
                    using (var sha = SHA256.Create())
                    {
                        var hash = sha.ComputeHash(navData.walk);
                        snapshot.navGridHash = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                    }
                    Debug.Log($"[HD-TOA-DO] Mã băm lưới đi robot: {snapshot.navGridHash} ({navData.nx}x{navData.nz})");
                }
            }

            // 2. Thu thập mọi GameObjects
            for (int i = 0; i < EditorSceneManager.sceneCount; i++)
            {
                var s = EditorSceneManager.GetSceneAt(i);
                if (!s.isLoaded) continue;
                string relScenePath = s.path;
                foreach (var rootGo in s.GetRootGameObjects())
                {
                    CollectObjects(rootGo.transform, relScenePath, snapshot.records);
                }
            }

            snapshot.totalObjects = snapshot.records.Count;

            // 3. Lưu file JSON
            string jsonDir = Path.GetDirectoryName(OutputPath);
            if (!Directory.Exists(jsonDir)) Directory.CreateDirectory(jsonDir);

            // Serialize đẹp
            string json = UnityEngine.JsonUtility.ToJson(snapshot, true);
            File.WriteAllText(OutputPath, json, Encoding.UTF8);

            Debug.Log($"[HD-TOA-DO] Đã chụp {snapshot.totalObjects} đối tượng vào {OutputPath}");
            return snapshot;
        }

        /// <summary>So sánh hiện trạng với mốc gốc, trả về số lượng lỗi</summary>
        public static int Compare()
        {
            if (!File.Exists(OutputPath))
            {
                Debug.LogError($"[HD-TOA-DO] Không tìm thấy file mốc gốc: {OutputPath}");
                return 1;
            }

            string json = File.ReadAllText(OutputPath, Encoding.UTF8);
            var snapshot = UnityEngine.JsonUtility.FromJson<SnapshotData>(json);

            var scenes = GetAllScenePaths();
            OpenScenesAdditive(scenes);

            int errorCount = 0;

            // 1. So mã băm lưới đi robot
            var navObj = GameObject.Find("Lưới đi robot (nướng sẵn)");
            if (navObj == null)
            {
                Debug.LogError("[HD-TOA-DO] LỖI: Không tìm thấy GameObject 'Lưới đi robot (nướng sẵn)'!");
                errorCount++;
            }
            else
            {
                var navData = navObj.GetComponent<NavGridData>();
                if (navData == null || navData.walk == null)
                {
                    Debug.LogError("[HD-TOA-DO] LỖI: Không tìm thấy NavGridData hợp lệ trên 'Lưới đi robot (nướng sẵn)'!");
                    errorCount++;
                }
                else
                {
                    using (var sha = SHA256.Create())
                    {
                        var hash = BitConverter.ToString(sha.ComputeHash(navData.walk)).Replace("-", "").ToLowerInvariant();
                        if (hash != snapshot.navGridHash)
                        {
                            Debug.LogError($"[HD-TOA-DO] LỖI: Mã băm lưới đi robot thay đổi! Gốc: {snapshot.navGridHash}, Hiện tại: {hash}");
                            errorCount++;
                        }
                    }
                }
            }

            // 2. Thu thập hiện trạng
            var currentRecords = new List<ObjectRecord>();
            for (int i = 0; i < EditorSceneManager.sceneCount; i++)
            {
                var s = EditorSceneManager.GetSceneAt(i);
                if (!s.isLoaded) continue;
                string relScenePath = s.path;
                foreach (var rootGo in s.GetRootGameObjects())
                {
                    CollectObjects(rootGo.transform, relScenePath, currentRecords);
                }
            }

            // Tạo từ điển cho hiện trạng
            var currentMap = new Dictionary<string, ObjectRecord>();
            foreach (var rec in currentRecords)
            {
                string key = $"{rec.scene}::{rec.path}";
                currentMap[key] = rec;
            }

            // Kiểm tra các chi tiết HD_* không được có Collider theo luật 1.2
            foreach (var rec in currentRecords)
            {
                if (IsHdDetail(rec.path) && rec.collider != null && rec.collider.present)
                {
                    Debug.LogError($"[HD-TOA-DO] LỖI LUẬT 1.2: Chi tiết thêm HD_* có collider! Vật: {rec.path} ({rec.collider.type})");
                    errorCount++;
                }
            }

            // 3. Đối chiếu từng bản ghi gốc
            int checkedCount = 0;
            foreach (var baseRec in snapshot.records)
            {
                // Bỏ qua vật bị loại trừ theo quy tắc (đèn, đầu dò, volume, HD_*)
                if (ShouldIgnore(baseRec)) continue;

                checkedCount++;
                string key = $"{baseRec.scene}::{baseRec.path}";
                if (!currentMap.TryGetValue(key, out var currRec))
                {
                    Debug.LogError($"[HD-TOA-DO] LỖI: Thiếu đối tượng trong cảnh: {baseRec.scene} -> {baseRec.path} (index {baseRec.siblingIndex})");
                    errorCount++;
                    continue;
                }

                // So vị trí (tối đa 1 mm = 0.001 m)
                Vector3 bp = new Vector3(baseRec.pos[0], baseRec.pos[1], baseRec.pos[2]);
                Vector3 cp = new Vector3(currRec.pos[0], currRec.pos[1], currRec.pos[2]);
                float posDiff = Vector3.Distance(bp, cp);
                if (posDiff > 0.001f)
                {
                    Debug.LogError($"[HD-TOA-DO] LỖI VỊ TRÍ: {baseRec.path} lệch {posDiff * 1000f:F2} mm (> 1.00 mm). Gốc: {bp}, Hiện: {cp}");
                    errorCount++;
                }

                // So góc xoay (tối đa 0.01°)
                Quaternion br = new Quaternion(baseRec.rot[0], baseRec.rot[1], baseRec.rot[2], baseRec.rot[3]);
                Quaternion cr = new Quaternion(currRec.rot[0], currRec.rot[1], currRec.rot[2], currRec.rot[3]);
                float rotDiff = Quaternion.Angle(br, cr);
                if (rotDiff > 0.01f)
                {
                    Debug.LogError($"[HD-TOA-DO] LỖI GÓC XOAY: {baseRec.path} lệch {rotDiff:F4}° (> 0.01°)");
                    errorCount++;
                }

                // So tỉ lệ lossyScale (tối đa 0.1% = 0.001)
                for (int axis = 0; axis < 3; axis++)
                {
                    float bs = baseRec.scale[axis];
                    float cs = currRec.scale[axis];
                    float diff = Mathf.Abs(cs - bs) / Mathf.Max(0.0001f, Mathf.Abs(bs));
                    if (diff > 0.001f)
                    {
                        Debug.LogError($"[HD-TOA-DO] LỖI TỈ LỆ: {baseRec.path} trục {axis} lệch {diff * 100f:F3}% (> 0.1%). Gốc: {bs}, Hiện: {cs}");
                        errorCount++;
                        break;
                    }
                }

                // So Collider
                bool baseHasCol = baseRec.collider != null && baseRec.collider.present;
                bool currHasCol = currRec.collider != null && currRec.collider.present;

                if (baseHasCol)
                {
                    if (!currHasCol)
                    {
                        Debug.LogError($"[HD-TOA-DO] LỖI COLLIDER: {baseRec.path} bị mất Collider (cần {baseRec.collider.type})");
                        errorCount++;
                    }
                    else if (currRec.collider.type != baseRec.collider.type)
                    {
                        Debug.LogError($"[HD-TOA-DO] LỖI COLLIDER: {baseRec.path} đổi kiểu Collider từ {baseRec.collider.type} sang {currRec.collider.type}");
                        errorCount++;
                    }
                    else
                    {
                        Vector3 bc = new Vector3(baseRec.collider.center[0], baseRec.collider.center[1], baseRec.collider.center[2]);
                        Vector3 cc = new Vector3(currRec.collider.center[0], currRec.collider.center[1], currRec.collider.center[2]);
                        if (Vector3.Distance(bc, cc) > 0.001f)
                        {
                            Debug.LogError($"[HD-TOA-DO] LỖI COLLIDER: {baseRec.path} tâm collider lệch {Vector3.Distance(bc, cc) * 1000f:F2} mm");
                            errorCount++;
                        }
                    }
                }
                else if (currHasCol)
                {
                    Debug.LogError($"[HD-TOA-DO] LỖI COLLIDER: {baseRec.path} thêm collider mới ({currRec.collider.type}) vào vật vốn không có collider");
                    errorCount++;
                }

                // So tracked components
                foreach (var comp in baseRec.trackedComponents)
                {
                    if (!currRec.trackedComponents.Contains(comp))
                    {
                        Debug.LogError($"[HD-TOA-DO] LỖI COMPONENT: {baseRec.path} bị mất thành phần logic: {comp}");
                        errorCount++;
                    }
                }
            }

            Debug.Log($"[HD-TOA-DO] Đã so {checkedCount} đối tượng. Tổng số lỗi: {errorCount}");
            return errorCount;
        }

        static bool ShouldIgnore(ObjectRecord rec)
        {
            if (IsHdDetail(rec.path)) return true;
            // Bỏ qua đèn, đầu dò, volume theo quy tắc
            if (rec.trackedComponents.Contains("Light") || rec.trackedComponents.Contains("ReflectionProbe") || rec.trackedComponents.Contains("Volume"))
                return true;
            string lower = rec.path.ToLowerInvariant();
            if (lower.Contains("probe") || lower.Contains("volume") || lower.Contains("đèn") || lower.Contains("mặt trời") || lower.Contains("sun"))
                return true;
            return false;
        }

        static bool IsHdDetail(string path)
        {
            var parts = path.Split('/');
            foreach (var p in parts)
            {
                if (p.StartsWith("HD_", StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        static void CollectObjects(Transform t, string scenePath, List<ObjectRecord> records)
        {
            var rec = new ObjectRecord
            {
                scene = scenePath,
                path = GetHierarchyPath(t),
                siblingIndex = t.GetSiblingIndex(),
                pos = new float[] { t.position.x, t.position.y, t.position.z },
                rot = new float[] { t.rotation.x, t.rotation.y, t.rotation.z, t.rotation.w },
                euler = new float[] { t.eulerAngles.x, t.eulerAngles.y, t.eulerAngles.z },
                scale = new float[] { t.lossyScale.x, t.lossyScale.y, t.lossyScale.z },
            };

            // Collider
            var col = t.GetComponent<Collider>();
            rec.collider = new ColliderInfo
            {
                present = col != null,
                type = col != null ? col.GetType().Name : "",
                isTrigger = col != null && col.isTrigger,
                center = col != null ? new float[] { col.bounds.center.x, col.bounds.center.y, col.bounds.center.z } : new float[3],
                size = col != null ? new float[] { col.bounds.size.x, col.bounds.size.y, col.bounds.size.z } : new float[3],
            };

            // Tracked logic components
            if (t.GetComponent<Grabbable>() != null) rec.trackedComponents.Add("Grabbable");
            if (t.GetComponent<LabContainer>() != null) rec.trackedComponents.Add("LabContainer");
            if (t.GetComponent<LabStation>() != null) rec.trackedComponents.Add("LabStation");
            if (t.GetComponent<EquipmentInfo>() != null) rec.trackedComponents.Add("EquipmentInfo");
            if (t.GetComponent<LabScreen>() != null) rec.trackedComponents.Add("LabScreen");
            if (t.GetComponent<AutoDoor>() != null) rec.trackedComponents.Add("AutoDoor");
            if (t.GetComponent<Light>() != null) rec.trackedComponents.Add("Light");
            if (t.GetComponent<ReflectionProbe>() != null) rec.trackedComponents.Add("ReflectionProbe");
            if (t.GetComponent<Volume>() != null) rec.trackedComponents.Add("Volume");

            records.Add(rec);

            for (int i = 0; i < t.childCount; i++)
            {
                CollectObjects(t.GetChild(i), scenePath, records);
            }
        }

        static string GetHierarchyPath(Transform t)
        {
            var parts = new List<string>();
            var curr = t;
            while (curr != null)
            {
                parts.Insert(0, $"{curr.name}[{curr.GetSiblingIndex()}]");
                curr = curr.parent;
            }
            return string.Join("/", parts);
        }

        static List<string> GetAllScenePaths()
        {
            var scenes = new List<string> { LabSceneBuilder.ScenePath };
            for (int z = 0; z < ZoneMap.Zones.Length; z++)
            {
                scenes.Add(ZoneMap.ScenePath(z));
            }
            return scenes;
        }

        static void OpenScenesAdditive(List<string> scenes)
        {
            if (scenes == null || scenes.Count == 0) return;
            EditorSceneManager.OpenScene(scenes[0], OpenSceneMode.Single);
            for (int i = 1; i < scenes.Count; i++)
            {
                EditorSceneManager.OpenScene(scenes[i], OpenSceneMode.Additive);
            }
        }

        [MenuItem("KHKT/HD/Đo độ sáng ảnh")]
        public static void MeasureBatch()
        {
            string landingDir = Path.Combine(Directory.GetCurrentDirectory(), "../docs/do-hoa-hd/anh_goc/landing");
            if (!Directory.Exists(landingDir))
                landingDir = Path.Combine(Directory.GetCurrentDirectory(), "docs/do-hoa-hd/anh_goc/landing");

            Debug.Log($"[HD-LUMINANCE] Bắt đầu đo độ sáng các ảnh trong: {landingDir}");
            var files = Directory.GetFiles(landingDir, "*.png");
            Array.Sort(files);

            Debug.Log($"{"Tên ảnh",-30} | {"Độ sáng TB",-11} | {"Vùng trần",-11} | {"Mặt bàn",-11} | {"Cháy (>0.98)",-12} | {"Đen (<0.03)",-11}");
            Debug.Log(new string('-', 96));

            foreach (var f in files)
            {
                byte[] data = File.ReadAllBytes(f);
                var tex = new Texture2D(2, 2);
                if (!tex.LoadImage(data)) continue;

                var pixels = tex.GetPixels();
                int w = tex.width;
                int h = tex.height;
                int n = pixels.Length;

                float sumAll = 0f;
                int burnt = 0;
                int black = 0;

                for (int i = 0; i < n; i++)
                {
                    var c = pixels[i];
                    float lum = 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
                    sumAll += lum;
                    if (lum > 0.98f) burnt++;
                    else if (lum < 0.03f) black++;
                }

                // Vùng trần (20% trên của ảnh)
                float sumCeil = 0f;
                int countCeil = 0;
                int yCeilMin = (int)(h * 0.75f); // Texture2D (0,0) ở góc dưới trái
                for (int y = yCeilMin; y < h; y++)
                {
                    for (int x = (int)(w * 0.2f); x < (int)(w * 0.8f); x++)
                    {
                        var c = pixels[y * w + x];
                        sumCeil += 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
                        countCeil++;
                    }
                }

                // Vùng mặt bàn (khoảng 30% đến 55% từ dưới lên)
                float sumTable = 0f;
                int countTable = 0;
                int yTabMin = (int)(h * 0.25f);
                int yTabMax = (int)(h * 0.55f);
                for (int y = yTabMin; y < yTabMax; y++)
                {
                    for (int x = (int)(w * 0.25f); x < (int)(w * 0.75f); x++)
                    {
                        var c = pixels[y * w + x];
                        sumTable += 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
                        countTable++;
                    }
                }

                float avgAll = sumAll / n;
                float avgCeil = countCeil > 0 ? sumCeil / countCeil : avgAll;
                float avgTable = countTable > 0 ? sumTable / countTable : avgAll;
                float burntPct = (burnt * 100f) / n;
                float blackPct = (black * 100f) / n;

                string name = Path.GetFileName(f);
                Debug.Log($"{name,-30} | {avgAll,11:F3} | {avgCeil,11:F3} | {avgTable,11:F3} | {burntPct,10:F2}% | {blackPct,9:F2}%");
                UnityEngine.Object.DestroyImmediate(tex);
            }
        }
    }
}
