// Cấu hình đồ hoạ cho app Phòng Lab (chạy lại bao nhiêu lần cũng được – chỉ tạo cái còn thiếu, đặt lại các giá trị mong muốn):
//  • Resources/PTN/PBR.mat       – vật liệu gốc có bật _NORMALMAP + _METALLICSPECGLOSSMAP (VatLieuThuc.VatPBR) → biến thể shader CHẮC CHẮN vào bản build
//  • Resources/PTN/NgonLua.mat   – vật liệu shader PhongLab/NgonLua (ngọn lửa) → shader vào bản build
//  • URP_Asset: MSAA 4×, đổ bóng mềm (chất lượng vừa), bản đồ bóng 2048, khoảng bóng 25 m
//  • URP_Renderer: thêm Screen Space Ambient Occlusion (bóng tối ở góc tường, chân bàn, khe tủ)
// Menu: Phòng Lab ▸ Cấu hình đồ hoạ.   Dòng lệnh: Unity -batchmode -quit -projectPath … -executeMethod PhongThiNghiem.Editor.CauHinhDoHoa.Chay
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PhongThiNghiem.Editor
{
    public static class CauHinhDoHoa
    {
        const string THU_MUC = "Assets/Resources/PTN";

        [MenuItem("Phòng Lab/Cấu hình đồ hoạ (SSAO, MSAA, vật liệu)", false, 120)]
        public static void Chay()
        {
            TaoVatLieuPBR();
            DamBaoTuKhoa("PhatSangDac", "_EMISSION");
            TaoVatLieuNgonLua();
            CauHinhURP();
            ThemSSAO();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[CauHinhDoHoa] Xong.");
        }

        static void TaoVatLieuPBR()
        {
            string duong = THU_MUC + "/PBR.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(duong) == null)
            {
                var goc = AssetDatabase.LoadAssetAtPath<Material>(THU_MUC + "/Dac.mat");
                var m = new Material(goc) { name = "PBR" };
                m.SetColor("_BaseColor", Color.white);
                m.SetFloat("_Smoothness", 1f);
                AssetDatabase.CreateAsset(m, duong);
            }
            DamBaoTuKhoa("PBR", "_NORMALMAP", "_METALLICSPECGLOSSMAP");
        }

        /// <summary>Bật từ khoá shader và LƯU vào vật liệu (m_ValidKeywords). Phải dùng LocalKeyword: Material.EnableKeyword(string) lên vật liệu
        /// vừa nhân bản KHÔNG được lưu → bản build cắt mất biến thể shader (vật liệu hiện không có pháp tuyến / phát sáng).</summary>
        static void DamBaoTuKhoa(string ten, params string[] tuKhoa)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(THU_MUC + "/" + ten + ".mat");
            if (m == null) { Debug.LogWarning("[CauHinhDoHoa] không thấy " + ten + ".mat"); return; }
            foreach (var k in tuKhoa) m.EnableKeyword(new LocalKeyword(m.shader, k));
            EditorUtility.SetDirty(m);
        }

        static void TaoVatLieuNgonLua()
        {
            string duong = THU_MUC + "/NgonLua.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(duong) != null) return;
            var sh = Shader.Find("PhongLab/NgonLua");
            if (sh == null) { Debug.LogError("[CauHinhDoHoa] Không thấy shader PhongLab/NgonLua"); return; }
            AssetDatabase.CreateAsset(new Material(sh) { name = "NgonLua" }, duong);
        }

        static void CauHinhURP()
        {
            var urp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/URP_Asset.asset");
            if (urp == null) { Debug.LogWarning("[CauHinhDoHoa] Không thấy Assets/Settings/URP_Asset.asset"); return; }
            var so = new SerializedObject(urp);
            void Dat(string ten, System.Action<SerializedProperty> f) { var p = so.FindProperty(ten); if (p != null) f(p); else Debug.LogWarning("[CauHinhDoHoa] thiếu thuộc tính " + ten); }
            Dat("m_MSAA", p => p.intValue = 4);
            Dat("m_SoftShadowsSupported", p => p.boolValue = true);
            Dat("m_SoftShadowQuality", p => p.intValue = 2);
            Dat("m_MainLightShadowmapResolution", p => p.intValue = 2048);
            Dat("m_ShadowDistance", p => p.floatValue = 25f);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(urp);
        }

        static void ThemSSAO()
        {
            var rd = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/URP_Renderer.asset");
            if (rd == null) { Debug.LogWarning("[CauHinhDoHoa] Không thấy Assets/Settings/URP_Renderer.asset"); return; }
            foreach (var f in rd.rendererFeatures) if (f is ScreenSpaceAmbientOcclusion) return;   // đã có
            var ssao = ScriptableObject.CreateInstance<ScreenSpaceAmbientOcclusion>();
            ssao.name = "SSAO";
            AssetDatabase.AddObjectToAsset(ssao, rd);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(ssao, out _, out long id);
            var so = new SerializedObject(rd);
            var ds = so.FindProperty("m_RendererFeatures");
            ds.arraySize++;
            ds.GetArrayElementAtIndex(ds.arraySize - 1).objectReferenceValue = ssao;
            var ban = so.FindProperty("m_RendererFeatureMap");
            ban.arraySize++;
            ban.GetArrayElementAtIndex(ban.arraySize - 1).longValue = id;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(rd);
        }
    }
}
