using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Khkt.EditorTools
{
    /// <summary>
    /// Hệ thống nâng cấp vật liệu PBR siêu thực cho VERA Lab HD:
    ///   • Áp dụng Normal map, AO map và tinh chỉnh Metallic/Smoothness chuẩn vật lý cho hơn 40 vật liệu diện tích lớn.
    ///   • Thép không gỉ 304 satin #4, Nhôm anodized, Trespa TopLab kháng hóa chất, Epoxy nguyên khối.
    ///   • Thạch sinh học tầng 2, kính quang học và vi biến thiên vi mô.
    /// </summary>
    public static class HdMaterialEnhancer
    {
        const string HdTexDir = "Assets/Lab/HD/Textures";
        const string MatDir = "Assets/Lab/Materials";

        public static void ConfigureTextureImporters()
        {
            if (!Directory.Exists(HdTexDir)) return;

            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { HdTexDir });
            bool changed = false;

            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;

                string filename = Path.GetFileNameWithoutExtension(path);
                bool isNormal = filename.StartsWith("Normal_");

                bool needUpdate = false;
                if (isNormal && importer.textureType != TextureImporterType.NormalMap)
                {
                    importer.textureType = TextureImporterType.NormalMap;
                    needUpdate = true;
                }
                else if (!isNormal && importer.textureType != TextureImporterType.Default)
                {
                    importer.textureType = TextureImporterType.Default;
                    importer.sRGBTexture = false;
                    needUpdate = true;
                }

                if (importer.maxTextureSize > 1024)
                {
                    importer.maxTextureSize = 1024;
                    needUpdate = true;
                }

                if (importer.wrapMode != TextureWrapMode.Repeat)
                {
                    importer.wrapMode = TextureWrapMode.Repeat;
                    needUpdate = true;
                }

                if (needUpdate)
                {
                    importer.SaveAndReimport();
                    changed = true;
                }
            }

            if (changed) AssetDatabase.Refresh();
        }

        [MenuItem("KHKT/HD/Áp vật liệu PBR", priority = 210)]
        public static void ApplyAllMaterials()
        {
            ConfigureTextureImporters();

            // Nạp các textures PBR
            var normTilesLab = AssetDatabase.LoadAssetAtPath<Texture2D>($"{HdTexDir}/Normal_TilesLab.png");
            var aoTilesLab = AssetDatabase.LoadAssetAtPath<Texture2D>($"{HdTexDir}/AO_TilesLab.png");
            var normTilesChem = AssetDatabase.LoadAssetAtPath<Texture2D>($"{HdTexDir}/Normal_TilesChem.png");
            var normPlaster = AssetDatabase.LoadAssetAtPath<Texture2D>($"{HdTexDir}/Normal_Plaster.png");
            var normFacade = AssetDatabase.LoadAssetAtPath<Texture2D>($"{HdTexDir}/Normal_Facade.png");
            var normInox = AssetDatabase.LoadAssetAtPath<Texture2D>($"{HdTexDir}/Normal_InoxSatin.png");
            var normAlu = AssetDatabase.LoadAssetAtPath<Texture2D>($"{HdTexDir}/Normal_AnodizedAlu.png");
            var normPhenolic = AssetDatabase.LoadAssetAtPath<Texture2D>($"{HdTexDir}/Normal_LabPhenolic.png");
            var normEpoxy = AssetDatabase.LoadAssetAtPath<Texture2D>($"{HdTexDir}/Normal_LabEpoxy.png");
            var normWood = AssetDatabase.LoadAssetAtPath<Texture2D>($"{HdTexDir}/Normal_WoodGrain.png");
            var normConc = AssetDatabase.LoadAssetAtPath<Texture2D>($"{HdTexDir}/Normal_Concrete.png");
            var normGlass = AssetDatabase.LoadAssetAtPath<Texture2D>($"{HdTexDir}/Normal_GlassOptic.png");
            var normRubber = AssetDatabase.LoadAssetAtPath<Texture2D>($"{HdTexDir}/Normal_Rubber.png");

            int count = 0;

            // 1. Tường & Trần
            Enhance("TuongSon", m => {
                SetNormal(m, normPlaster, 0.6f);
                m.SetFloat("_Smoothness", 0.18f);
                m.SetFloat("_Metallic", 0f);
            }, ref count);

            Enhance("TuongNgoai", m => {
                SetNormal(m, normFacade, 0.9f);
                m.SetFloat("_Smoothness", 0.12f);
                m.SetFloat("_Metallic", 0f);
            }, ref count);

            Enhance("Tran", m => {
                SetNormal(m, normPlaster, 0.4f);
                m.SetFloat("_Smoothness", 0.08f);
                m.SetFloat("_Metallic", 0f);
                m.SetColor("_BaseColor", new Color(0.98f, 0.98f, 0.98f));
                m.SetColor("_EmissionColor", new Color(0.45f, 0.45f, 0.45f));
            }, ref count);

            Enhance("VachTrang", m => {
                SetNormal(m, normPlaster, 0.3f);
                m.SetFloat("_Smoothness", 0.28f);
            }, ref count);

            // 2. Sàn phòng lab & khu vực
            Enhance("SanPhongLab", m => {
                SetNormal(m, normTilesLab, 0.85f);
                SetAO(m, aoTilesLab, 0.7f);
                m.SetFloat("_Smoothness", 0.42f);
                m.SetFloat("_Metallic", 0f);
            }, ref count);

            Enhance("SanPhongHoaChat", m => {
                SetNormal(m, normTilesChem, 1.0f);
                m.SetFloat("_Smoothness", 0.35f);
                m.SetFloat("_Metallic", 0f);
            }, ref count);

            Enhance("SanHanhLang", m => {
                SetNormal(m, normTilesLab, 0.75f);
                SetAO(m, aoTilesLab, 0.6f);
                m.SetFloat("_Smoothness", 0.38f);
            }, ref count);

            Enhance("SanBanCong", m => {
                SetNormal(m, normConc, 0.8f);
                m.SetFloat("_Smoothness", 0.25f);
            }, ref count);

            Enhance("GachLatSan", m => {
                SetNormal(m, normTilesLab, 0.8f);
                SetAO(m, aoTilesLab, 0.6f);
                m.SetFloat("_Smoothness", 0.40f);
            }, ref count);

            Enhance("GachLatSan60", m => {
                SetNormal(m, normTilesLab, 0.8f);
                SetAO(m, aoTilesLab, 0.6f);
                m.SetFloat("_Smoothness", 0.40f);
            }, ref count);

            // 3. Mặt bàn thí nghiệm
            Enhance("MatBanPhenolic", m => {
                SetNormal(m, normPhenolic, 0.7f);
                m.SetFloat("_Smoothness", 0.42f);
                m.SetFloat("_Metallic", 0.04f);
            }, ref count);

            Enhance("MatBanEpoxy", m => {
                SetNormal(m, normEpoxy, 0.85f);
                m.SetFloat("_Smoothness", 0.65f);
                m.SetFloat("_Metallic", 0.08f);
            }, ref count);

            Enhance("BauCua", m => {
                SetNormal(m, normConc, 0.5f);
                m.SetFloat("_Smoothness", 0.45f);
                m.SetFloat("_Metallic", 0.15f);
            }, ref count);

            // 4. Kim loại: Inox, Nhôm, Thép tối
            Enhance("Inox", m => {
                SetNormal(m, normInox, 1.1f);
                m.SetFloat("_Smoothness", 0.82f);
                m.SetFloat("_Metallic", 0.95f);
            }, ref count);

            Enhance("SangSac", m => {
                SetNormal(m, normInox, 0.4f);
                m.SetFloat("_Smoothness", 0.94f);
                m.SetFloat("_Metallic", 0.98f);
            }, ref count);

            Enhance("KimLoaiToi", m => {
                SetNormal(m, normAlu, 0.6f);
                m.SetFloat("_Smoothness", 0.40f);
                m.SetFloat("_Metallic", 0.70f);
            }, ref count);

            Enhance("NhomKhung", m => {
                SetNormal(m, normAlu, 0.75f);
                m.SetFloat("_Smoothness", 0.60f);
                m.SetFloat("_Metallic", 0.80f);
            }, ref count);

            Enhance("KhungCuaTuDong", m => {
                SetNormal(m, normAlu, 0.6f);
                m.SetFloat("_Smoothness", 0.55f);
                m.SetFloat("_Metallic", 0.75f);
            }, ref count);

            // 5. Gỗ, Laminate & Cánh tủ
            Enhance("CanhTu", m => {
                SetNormal(m, normWood, 0.8f);
                m.SetFloat("_Smoothness", 0.36f);
            }, ref count);

            Enhance("ThanTu", m => {
                SetNormal(m, normPhenolic, 0.5f);
                m.SetFloat("_Smoothness", 0.38f);
            }, ref count);

            Enhance("Go", m => {
                SetNormal(m, normWood, 0.9f);
                m.SetFloat("_Smoothness", 0.38f);
            }, ref count);

            Enhance("GoSoi", m => {
                SetNormal(m, normWood, 0.95f);
                m.SetFloat("_Smoothness", 0.38f);
            }, ref count);

            Enhance("LamGoSoi", m => {
                SetNormal(m, normWood, 0.85f);
                m.SetFloat("_Smoothness", 0.38f);
            }, ref count);

            // 6. Cửa các khu & Ghế
            Enhance("CanhCuaLab", m => { SetNormal(m, normPhenolic, 0.5f); m.SetFloat("_Smoothness", 0.46f); }, ref count);
            Enhance("CanhCuaKho", m => { SetNormal(m, normPhenolic, 0.5f); m.SetFloat("_Smoothness", 0.46f); }, ref count);
            Enhance("CanhCuaPhongMay", m => { SetNormal(m, normPhenolic, 0.5f); m.SetFloat("_Smoothness", 0.46f); }, ref count);
            Enhance("CanhCuaPhongRobot", m => { SetNormal(m, normPhenolic, 0.5f); m.SetFloat("_Smoothness", 0.46f); }, ref count);

            Enhance("MatGheXanhNgoc", m => { SetNormal(m, normRubber, 0.6f); m.SetFloat("_Smoothness", 0.45f); }, ref count);
            Enhance("MatGheCam", m => { SetNormal(m, normRubber, 0.6f); m.SetFloat("_Smoothness", 0.45f); }, ref count);
            Enhance("MatGheXanhCom", m => { SetNormal(m, normRubber, 0.6f); m.SetFloat("_Smoothness", 0.45f); }, ref count);
            Enhance("MatGheVang", m => { SetNormal(m, normRubber, 0.6f); m.SetFloat("_Smoothness", 0.45f); }, ref count);
            Enhance("MatGheXanhTroi", m => { SetNormal(m, normRubber, 0.6f); m.SetFloat("_Smoothness", 0.45f); }, ref count);

            // 7. Cao su, Bê tông, Nhựa
            Enhance("CaoSu", m => {
                SetNormal(m, normRubber, 0.9f);
                m.SetFloat("_Smoothness", 0.22f);
            }, ref count);

            Enhance("BeTong", m => {
                SetNormal(m, normConc, 1.0f);
                m.SetFloat("_Smoothness", 0.25f);
            }, ref count);

            Enhance("NhuaMo", m => {
                SetNormal(m, normPhenolic, 0.4f);
                m.SetFloat("_Smoothness", 0.42f);
            }, ref count);

            // 8. Thủy tinh quang học & Kính
            Enhance("ThuyTinh", m => {
                SetNormal(m, normGlass, 0.5f);
                m.SetFloat("_Smoothness", 0.98f);
                m.SetFloat("_Metallic", 0f);
            }, ref count);

            Enhance("KinhCuaSo", m => {
                m.SetFloat("_Smoothness", 0.96f);
            }, ref count);

            Enhance("KinhTu", m => {
                m.SetFloat("_Smoothness", 0.94f);
            }, ref count);

            Enhance("KhopNham", m => {
                SetNormal(m, normConc, 1.2f);
                m.SetFloat("_Smoothness", 0.15f);
            }, ref count);

            // 9. Vật liệu Tầng 2
            Enhance("T2_BanSoiUV", m => {
                SetNormal(m, normInox, 0.6f);
                m.SetFloat("_Smoothness", 0.60f);
                m.SetFloat("_Metallic", 0.85f);
            }, ref count);

            Enhance("T2_GelAgarose", m => {
                m.SetFloat("_Smoothness", 0.88f);
            }, ref count);

            Enhance("T2_ThachAgar", m => {
                m.SetFloat("_Smoothness", 0.86f);
            }, ref count);

            Enhance("T2_ThachXgal", m => {
                m.SetFloat("_Smoothness", 0.86f);
            }, ref count);

            Enhance("T2_DiaPetri", m => {
                m.SetFloat("_Smoothness", 0.94f);
            }, ref count);

            Enhance("T2_NapDienDi", m => {
                m.SetFloat("_Smoothness", 0.93f);
            }, ref count);

            Enhance("T2_KinhLocCam", m => {
                m.SetFloat("_Smoothness", 0.95f);
            }, ref count);

            Enhance("T2_DenTrongCay", m => {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", new Color(0.9f, 0.4f, 0.7f) * 1.5f);
            }, ref count);

            AssetDatabase.SaveAssets();
            Debug.Log($"[HD-PBR] Đã nâng cấp thành công {count} vật liệu chuẩn PBR siêu thực.");
        }

        public static void ApplyBatch()
        {
            try
            {
                ApplyAllMaterials();
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[HD-PBR] Lỗi: {ex}");
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }

        static void SetNormal(Material m, Texture2D normalMap, float scale = 1.0f)
        {
            if (normalMap == null || m == null) return;
            m.EnableKeyword("_NORMALMAP");
            m.SetTexture("_BumpMap", normalMap);
            m.SetFloat("_BumpScale", scale);
        }

        static void SetAO(Material m, Texture2D aoMap, float strength = 1.0f)
        {
            if (aoMap == null || m == null) return;
            m.EnableKeyword("_OCCLUSIONMAP");
            m.SetTexture("_OcclusionMap", aoMap);
            m.SetFloat("_OcclusionStrength", strength);
        }

        static void Enhance(string matName, Action<Material> configure, ref int count)
        {
            string path = $"{MatDir}/{matName}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) return;

            configure(m);
            EditorUtility.SetDirty(m);
            count++;
        }
    }
}
