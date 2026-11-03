using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Khkt.EditorTools
{
    /// <summary>
    /// Hệ thống vật liệu PBR siêu thực cho VERA Lab HD (Giai đoạn 2):
    ///   • Nguồn quản lý duy nhất toàn viện, loại bỏ hoàn toàn các hàm sin giải tích gây vệt sọc/ô lưới.
    ///   • Tường, trần, thân tủ: Bề mặt sơn trơn phẳng, không normal map, màu đồng nhất chuẩn PBR.
    ///   • Sàn gạch men: Normal map và Mask map khớp 100% với lưới gạch 2x2 viên của GachLatSan60, ron trùng khít.
    ///   • Gỗ, kim loại, composite: Sử dụng texture PBR sạch (FFT Ratio < 30.0) với thớ dọc tự nhiên và hairline scratches.
    /// </summary>
    public static partial class LabKit
    {
        public const string HdRoot = "Assets/Lab/HD";
        public const string HdTextureRoot = "Assets/Lab/HD/Textures";

        static readonly Dictionary<string, (Texture2D albedo, Texture2D normal, Texture2D mask, float bumpScale, float occStrength, float smoothMult, float metalMult)> hdMaterialMap
            = new Dictionary<string, (Texture2D, Texture2D, Texture2D, float, float, float, float)>();

        static bool hdInitialized = false;

        public static void EnsureHdDirectories()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Lab/HD"))
                AssetDatabase.CreateFolder("Assets/Lab", "HD");
            if (!AssetDatabase.IsValidFolder("Assets/Lab/HD/Textures"))
                AssetDatabase.CreateFolder("Assets/Lab/HD", "Textures");
        }

        static void InitHdMaterialCatalog()
        {
            if (hdInitialized) return;
            EnsureHdDirectories();

            // Nạp bộ texture PBR sạch (đã kiểm định FFT Peak-to-Median < 30.0)
            var normTiles = AssetDatabase.LoadAssetAtPath<Texture2D>($"{HdTextureRoot}/Normal_TilesLab.png");
            var maskTiles = AssetDatabase.LoadAssetAtPath<Texture2D>($"{HdTextureRoot}/Mask_TilesLab.png");
            var normWood = AssetDatabase.LoadAssetAtPath<Texture2D>($"{HdTextureRoot}/Normal_WoodGrain.png");
            var normInox = AssetDatabase.LoadAssetAtPath<Texture2D>($"{HdTextureRoot}/Normal_InoxSatin.png");
            var normPhenolic = AssetDatabase.LoadAssetAtPath<Texture2D>($"{HdTextureRoot}/Normal_LabPhenolic.png");
            var normEpoxy = AssetDatabase.LoadAssetAtPath<Texture2D>($"{HdTextureRoot}/Normal_LabEpoxy.png");
            var normAlu = AssetDatabase.LoadAssetAtPath<Texture2D>($"{HdTextureRoot}/Normal_AnodizedAlu.png");
            var normSoil = AssetDatabase.LoadAssetAtPath<Texture2D>($"{HdTextureRoot}/Normal_Soil.png");
            var normTerra = AssetDatabase.LoadAssetAtPath<Texture2D>($"{HdTextureRoot}/Normal_Terracotta.png");

            hdMaterialMap.Clear();

            // 1. Tường & Trần sơn trơn: BỎ NORMAL MAP, BỎ MASK, MÀU TRƠN ĐỒNG NHẤT (Mục 4B, GĐ3)
            RegisterMap("TuongSon", null, null, null, 0f, 1.0f, 0.40f, 0.0f);
            RegisterMap("TuongNgoai", null, null, null, 0f, 1.0f, 0.35f, 0.0f);
            RegisterMap("Tran", null, null, null, 0f, 1.0f, 0.12f, 0.0f);
            RegisterMap("ThanTu", null, null, null, 0f, 1.0f, 0.50f, 0.0f);
            RegisterMap("T2_TuongKem", null, null, null, 0f, 1.0f, 0.38f, 0.0f);

            // 2. Sàn phòng lab & hành lang: Lưới 2x2 viên trùng khít 100% GachLatSan60 (Mục 4C, GĐ3)
            // Đặt Smoothness = 1.0f để kênh Alpha của maskTiles (mặt gạch 0.70, ron 0.25) điều khiển trực tiếp
            RegisterMap("SanPhongLab", null, normTiles, maskTiles, 0.75f, 0.9f, 1.0f, 0.0f);
            RegisterMap("SanPhongHoaChat", null, normTiles, maskTiles, 0.65f, 0.9f, 1.0f, 0.0f);
            RegisterMap("SanHanhLang", null, normTiles, maskTiles, 0.70f, 0.9f, 1.0f, 0.0f);
            RegisterMap("SanBanCong", null, normTiles, maskTiles, 0.80f, 0.8f, 1.0f, 0.0f);
            RegisterMap("T2_SanHanhLangT2", null, normTiles, maskTiles, 0.70f, 0.9f, 1.0f, 0.0f);
            RegisterMap("T2_SanHanhLangDong", null, normTiles, maskTiles, 0.70f, 0.9f, 1.0f, 0.0f);
            RegisterMap("T2_SanKho", null, normTiles, maskTiles, 0.65f, 0.9f, 1.0f, 0.0f);
            RegisterMap("T2_SanPhanTu", null, normTiles, maskTiles, 0.70f, 0.9f, 1.0f, 0.0f);

            // 3. Mặt bàn thí nghiệm (Phenolic Compact Laminate & Epoxy Resin đen đúc khối)
            RegisterMap("MatBanPhenolic", null, normPhenolic, null, 0.5f, 1.0f, 0.60f, 0.0f);
            RegisterMap("MatBanEpoxy", null, normEpoxy, null, 0.3f, 1.0f, 0.80f, 0.0f);
            RegisterMap("T2_BanSinhHoc", null, normPhenolic, null, 0.5f, 1.0f, 0.60f, 0.0f);

            // 4. Gỗ tự nhiên (Cánh tủ, sàn gỗ, lam gỗ)
            RegisterMap("CanhTu", null, normWood, null, 0.5f, 0.95f, 0.50f, 0.0f);
            RegisterMap("Go", null, normWood, null, 0.5f, 0.9f, 0.45f, 0.0f);
            RegisterMap("GoSoi", null, normWood, null, 0.5f, 0.9f, 0.48f, 0.0f);
            RegisterMap("T2_SanGo", null, normWood, null, 0.5f, 0.9f, 0.50f, 0.0f);

            // 5. Kim loại: Thép không gỉ 304 satin & Nhôm Anodized mờ
            RegisterMap("Inox", null, normInox, null, 0.45f, 1.0f, 0.68f, 0.95f);
            RegisterMap("KimLoaiInox", null, normInox, null, 0.45f, 1.0f, 0.68f, 0.95f);
            RegisterMap("GiaInox", null, normInox, null, 0.45f, 1.0f, 0.68f, 0.95f);
            RegisterMap("ChanGhe", null, normInox, null, 0.3f, 1.0f, 0.72f, 0.96f);
            RegisterMap("KinhHienVi_Inox", null, normInox, null, 0.4f, 1.0f, 0.68f, 0.95f);
            RegisterMap("T2_TuAnToanSinhHoc", null, normInox, null, 0.45f, 1.0f, 0.68f, 0.95f);
            RegisterMap("T2_TuAm", null, normInox, null, 0.45f, 1.0f, 0.68f, 0.95f);
            RegisterMap("T2_BanSoiUV", null, normInox, null, 0.45f, 1.0f, 0.68f, 0.95f);
            RegisterMap("KhungCuaTuDong", null, normAlu, null, 0.35f, 1.0f, 0.55f, 0.88f);
            RegisterMap("NhomKhung", null, normAlu, null, 0.35f, 1.0f, 0.55f, 0.85f);
            RegisterMap("T2_NapRanh", null, normAlu, null, 0.5f, 0.8f, 0.35f, 0.65f);

            // 6. Đất hữu cơ & Gốm nung Terracotta nhà kính
            RegisterMap("T2_DatAm", null, normSoil, null, 0.8f, 0.85f, 0.15f, 0.0f);
            RegisterMap("T2_GomTerracotta", null, normTerra, null, 0.5f, 1.0f, 0.25f, 0.0f);

            hdInitialized = true;
        }

        static void RegisterMap(string matName, Texture2D albedo, Texture2D normal, Texture2D mask,
                                float bumpScale, float occStrength, float smooth, float metal)
        {
            hdMaterialMap[matName] = (albedo, normal, mask, bumpScale, occStrength, smooth, metal);
        }

        /// <summary>
        /// Hook được gọi từ Mat() để nâng cấp vật liệu sang chuẩn PBR HD nếu nằm trong danh mục.
        /// </summary>
        public static void ApplyHdMaterial(string name, Material m, Color color, float smoothness, float metallic)
        {
            InitHdMaterialCatalog();

            if (hdMaterialMap.TryGetValue(name, out var data))
            {
                // 1. Normal Map
                if (data.normal != null)
                {
                    m.SetTexture("_BumpMap", data.normal);
                    m.SetFloat("_BumpScale", data.bumpScale);
                    m.EnableKeyword("_NORMALMAP");
                }
                else
                {
                    m.SetTexture("_BumpMap", null);
                    m.SetFloat("_BumpScale", 0f);
                    m.DisableKeyword("_NORMALMAP");
                }

                // 2. Mask Map (Metallic/Occlusion/Detail/Smoothness)
                if (data.mask != null)
                {
                    m.SetTexture("_MetallicGlossMap", data.mask);
                    m.SetTexture("_OcclusionMap", data.mask);
                    m.SetFloat("_OcclusionStrength", data.occStrength);
                    m.EnableKeyword("_METALLICSPECGLOSSMAP");
                    m.EnableKeyword("_OCCLUSIONMAP");
                }
                else
                {
                    m.SetTexture("_MetallicGlossMap", null);
                    m.SetTexture("_OcclusionMap", null);
                    m.DisableKeyword("_METALLICSPECGLOSSMAP");
                    m.DisableKeyword("_OCCLUSIONMAP");
                }

                // 3. Albedo Texture
                if (data.albedo != null)
                {
                    m.SetTexture("_BaseMap", data.albedo);
                    m.SetTexture("_MainTex", data.albedo);
                }
                else if (name == "TuongSon" || name == "Tran" || name == "TuongNgoai" || name == "ThanTu" || name == "T2_TuongKem")
                {
                    // Tường và trần: bỏ hẳn albedo texture để màu sơn trơn mịn đồng nhất, không bị kéo giãn UV
                    m.SetTexture("_BaseMap", null);
                    m.SetTexture("_MainTex", null);
                }

                m.SetColor("_BaseColor", color);
                m.SetFloat("_Smoothness", data.smoothMult > 0 ? data.smoothMult : smoothness);
                m.SetFloat("_Metallic", data.metalMult > 0 ? data.metalMult : metallic);
            }
        }

        /// <summary>
        /// Cập nhật lại toàn bộ file .mat trong Assets/Lab/Materials sang chuẩn PBR HD.
        /// </summary>
        [MenuItem("KHKT/HD/Cập nhật vật liệu PBR")]
        public static void UpdateAllMaterialsToHd()
        {
            InitHdMaterialCatalog();
            string[] guids = AssetDatabase.FindAssets("t:Material", new[] { $"{Root}/Materials" });
            int count = 0;
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null) continue;

                string matName = Path.GetFileNameWithoutExtension(path);
                Color col = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : Color.white;
                float smooth = m.HasProperty("_Smoothness") ? m.GetFloat("_Smoothness") : 0.2f;
                float metal = m.HasProperty("_Metallic") ? m.GetFloat("_Metallic") : 0f;

                ApplyHdMaterial(matName, m, col, smooth, metal);
                EditorUtility.SetDirty(m);
                count++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[HD-MATERIALS] Đã nâng cấp {count} vật liệu sang chuẩn PBR HD sạch.");
        }
    }
}
