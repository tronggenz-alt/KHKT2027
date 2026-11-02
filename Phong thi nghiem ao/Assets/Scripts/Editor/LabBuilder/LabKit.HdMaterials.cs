using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Khkt.EditorTools
{
    /// <summary>
    /// Mở rộng hệ thống vật liệu PBR siêu thực cho VERA Lab HD:
    ///   • Tự động ánh xạ ~40 vật liệu diện tích lớn sang bộ map PBR (Albedo, Normal, Mask, Occlusion).
    ///   • Sinh procedural texture 1024x1024 chất lượng cao (vân gạch men, vữa sơn mịn, inox chải xước satin,
    ///     mặt bàn Compact Laminate / Epoxy, gỗ sồi tự nhiên, đất nhà kính, gốm terracotta).
    ///   • Chuẩn hóa TextureImporter (NormalMap, Linear Mask Map) tối ưu cho URP trên Apple Silicon M3 Metal.
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

            // 1. Sàn gạch men lab Tiles086
            var (labTileAlbedo, labTileNormal, labTileMask) = CreateOrLoadPbrSet("TilesLab086", 1024, 1024,
                (u, v) => GenTilePattern(u, v, new Color(0.92f, 0.93f, 0.93f), new Color(0.68f, 0.70f, 0.71f), 0.015f),
                (u, v) => GenTileHeight(u, v, 0.015f),
                (u, v) => (metallic: 0.0f, occlusion: GenTileHeight(u, v, 0.015f) > 0.8f ? 1.0f : 0.45f, smoothness: 0.58f));

            // 2. Vữa thạch cao sơn trắng mịn Plaster001
            var (plasterAlbedo, plasterNormal, plasterMask) = CreateOrLoadPbrSet("PlasterWall001", 1024, 1024,
                (u, v) => GenNoisePattern(u, v, new Color(0.96f, 0.96f, 0.95f), 0.035f),
                (u, v) => GenMicroNoise(u, v, 32f),
                (u, v) => (metallic: 0.0f, occlusion: 1.0f, smoothness: 0.12f));

            // 3. Thép không gỉ AISI 304 satin chải xước Metal009
            var (inoxAlbedo, inoxNormal, inoxMask) = CreateOrLoadPbrSet("InoxSatin304", 1024, 1024,
                (u, v) => GenBrushedMetal(u, v, new Color(0.88f, 0.89f, 0.90f)),
                (u, v) => GenBrushedNormalHeight(u, v),
                (u, v) => (metallic: 0.96f, occlusion: 1.0f, smoothness: 0.68f));

            // 4. Mặt bàn Compact Laminate Trespa TopLab
            var (trespaAlbedo, trespaNormal, trespaMask) = CreateOrLoadPbrSet("TrespaTopLab", 1024, 1024,
                (u, v) => GenNoisePattern(u, v, new Color(0.38f, 0.40f, 0.42f), 0.05f),
                (u, v) => GenMicroNoise(u, v, 64f),
                (u, v) => (metallic: 0.0f, occlusion: 1.0f, smoothness: 0.40f));

            // 5. Mặt bàn Epoxy Resin đen bóng đúc khối
            var (epoxyAlbedo, epoxyNormal, epoxyMask) = CreateOrLoadPbrSet("EpoxyResinDark", 1024, 1024,
                (u, v) => new Color(0.12f, 0.13f, 0.14f),
                (u, v) => GenMicroNoise(u, v, 128f) * 0.3f,
                (u, v) => (metallic: 0.0f, occlusion: 1.0f, smoothness: 0.65f));

            // 6. Gỗ sồi tự nhiên Wood051
            var (oakAlbedo, oakNormal, oakMask) = CreateOrLoadPbrSet("OakWood051", 1024, 1024,
                (u, v) => GenWoodPattern(u, v, new Color(0.78f, 0.62f, 0.46f), new Color(0.55f, 0.40f, 0.28f)),
                (u, v) => GenWoodHeight(u, v),
                (u, v) => (metallic: 0.0f, occlusion: 0.95f, smoothness: 0.38f));

            // 7. Nhôm định hình mờ Anodized Metal029
            var (aluAlbedo, aluNormal, aluMask) = CreateOrLoadPbrSet("AlumAnodized029", 1024, 1024,
                (u, v) => new Color(0.25f, 0.27f, 0.29f),
                (u, v) => GenMicroNoise(u, v, 80f),
                (u, v) => (metallic: 0.88f, occlusion: 1.0f, smoothness: 0.52f));

            // 8. Đất vườn hữu cơ nhà kính Ground037
            var (soilAlbedo, soilNormal, soilMask) = CreateOrLoadPbrSet("SoilGarden037", 1024, 1024,
                (u, v) => GenNoisePattern(u, v, new Color(0.24f, 0.18f, 0.13f), 0.12f),
                (u, v) => GenMicroNoise(u, v, 16f),
                (u, v) => (metallic: 0.0f, occlusion: 0.85f, smoothness: 0.15f));

            // 9. Gạch gốm nung Terracotta Clay002
            var (terraAlbedo, terraNormal, terraMask) = CreateOrLoadPbrSet("TerracottaClay002", 1024, 1024,
                (u, v) => GenNoisePattern(u, v, new Color(0.72f, 0.40f, 0.28f), 0.08f),
                (u, v) => GenMicroNoise(u, v, 40f),
                (u, v) => (metallic: 0.0f, occlusion: 1.0f, smoothness: 0.22f));

            // Đăng ký bảng ánh xạ vật liệu
            RegisterMap("TuongSon", plasterAlbedo, plasterNormal, plasterMask, 0.4f, 1.0f, 0.18f, 0.0f);
            RegisterMap("TuongNgoai", plasterAlbedo, plasterNormal, plasterMask, 0.6f, 1.0f, 0.15f, 0.0f);
            RegisterMap("Tran", plasterAlbedo, plasterNormal, plasterMask, 0.25f, 1.0f, 0.08f, 0.0f);
            RegisterMap("SanPhongLab", labTileAlbedo, labTileNormal, labTileMask, 0.8f, 0.9f, 0.55f, 0.0f);
            RegisterMap("SanPhongHoaChat", labTileAlbedo, labTileNormal, labTileMask, 0.6f, 0.9f, 0.48f, 0.0f);
            RegisterMap("SanHanhLang", labTileAlbedo, labTileNormal, labTileMask, 0.7f, 0.9f, 0.52f, 0.0f);
            RegisterMap("SanBanCong", labTileAlbedo, labTileNormal, labTileMask, 0.9f, 0.8f, 0.35f, 0.0f);

            RegisterMap("MatBanPhenolic", trespaAlbedo, trespaNormal, trespaMask, 0.5f, 1.0f, 0.42f, 0.0f);
            RegisterMap("MatBanEpoxy", epoxyAlbedo, epoxyNormal, epoxyMask, 0.3f, 1.0f, 0.62f, 0.0f);
            RegisterMap("ThanTu", plasterAlbedo, plasterNormal, plasterMask, 0.2f, 1.0f, 0.38f, 0.0f);
            RegisterMap("CanhTu", oakAlbedo, oakNormal, oakMask, 0.6f, 0.95f, 0.36f, 0.0f);
            RegisterMap("KhungCuaTuDong", aluAlbedo, aluNormal, aluMask, 0.4f, 1.0f, 0.50f, 0.85f);

            RegisterMap("Inox", inoxAlbedo, inoxNormal, inoxMask, 0.5f, 1.0f, 0.68f, 0.95f);
            RegisterMap("KimLoaiInox", inoxAlbedo, inoxNormal, inoxMask, 0.5f, 1.0f, 0.68f, 0.95f);
            RegisterMap("GiaInox", inoxAlbedo, inoxNormal, inoxMask, 0.5f, 1.0f, 0.68f, 0.95f);
            RegisterMap("ChanGhe", inoxAlbedo, inoxNormal, inoxMask, 0.3f, 1.0f, 0.75f, 0.98f);

            RegisterMap("T2_SanGo", oakAlbedo, oakNormal, oakMask, 0.6f, 0.9f, 0.45f, 0.0f);
            RegisterMap("T2_TuongKem", plasterAlbedo, plasterNormal, plasterMask, 0.35f, 1.0f, 0.16f, 0.0f);
            RegisterMap("T2_TuAnToanSinhHoc", inoxAlbedo, inoxNormal, inoxMask, 0.45f, 1.0f, 0.65f, 0.95f);
            RegisterMap("T2_TuAm", inoxAlbedo, inoxNormal, inoxMask, 0.45f, 1.0f, 0.65f, 0.95f);
            RegisterMap("T2_BanSinhHoc", trespaAlbedo, trespaNormal, trespaMask, 0.5f, 1.0f, 0.40f, 0.0f);
            RegisterMap("T2_NapRanh", aluAlbedo, aluNormal, aluMask, 0.7f, 0.8f, 0.30f, 0.65f);
            RegisterMap("T2_DatAm", soilAlbedo, soilNormal, soilMask, 1.0f, 0.85f, 0.15f, 0.0f);
            RegisterMap("T2_GomTerracotta", terraAlbedo, terraNormal, terraMask, 0.7f, 1.0f, 0.25f, 0.0f);

            hdInitialized = true;
        }

        static void RegisterMap(string matName, Texture2D albedo, Texture2D normal, Texture2D mask,
                                float bumpScale, float occStrength, float smooth, float metal)
        {
            hdMaterialMap[matName] = (albedo, normal, mask, bumpScale, occStrength, smooth, metal);
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
            Debug.Log($"[HD-MATERIALS] Đã nâng cấp {count} vật liệu sang chuẩn PBR HD.");
        }

        /// <summary>
        /// Hook được gọi từ Mat() để nâng cấp vật liệu sang chuẩn PBR HD nếu nằm trong danh mục.
        /// </summary>
        public static void ApplyHdMaterial(string name, Material m, Color color, float smoothness, float metallic)
        {
            InitHdMaterialCatalog();

            if (hdMaterialMap.TryGetValue(name, out var data))
            {
                if (data.normal != null)
                {
                    m.SetTexture("_BumpMap", data.normal);
                    m.SetFloat("_BumpScale", data.bumpScale);
                    m.EnableKeyword("_NORMALMAP");
                }
                if (data.mask != null)
                {
                    m.SetTexture("_MetallicGlossMap", data.mask);
                    m.SetTexture("_OcclusionMap", data.mask);
                    m.SetFloat("_OcclusionStrength", data.occStrength);
                    m.EnableKeyword("_METALLICSPECGLOSSMAP");
                    m.EnableKeyword("_OCCLUSIONMAP");
                }
                if (data.albedo != null && m.GetTexture("_BaseMap") == null)
                {
                    m.SetTexture("_BaseMap", data.albedo);
                }

                m.SetFloat("_Smoothness", data.smoothMult > 0 ? data.smoothMult : smoothness);
                m.SetFloat("_Metallic", data.metalMult > 0 ? data.metalMult : metallic);
            }
        }

        // ---------- Procedural PBR Generators (100% CC0) ----------

        static (Texture2D albedo, Texture2D normal, Texture2D mask) CreateOrLoadPbrSet(
            string name, int w, int h,
            Func<float, float, Color> albedoFn,
            Func<float, float, float> heightFn,
            Func<float, float, (float metallic, float occlusion, float smoothness)> maskFn)
        {
            string albedoPath = $"{HdTextureRoot}/{name}_Albedo.png";
            string normalPath = $"{HdTextureRoot}/{name}_Normal.png";
            string maskPath = $"{HdTextureRoot}/{name}_Mask.png";

            var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(albedoPath);
            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
            var mask = AssetDatabase.LoadAssetAtPath<Texture2D>(maskPath);

            if (albedo != null && normal != null && mask != null)
                return (albedo, normal, mask);

            // Sinh Albedo & Height map
            var albedoTex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var normalTex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var maskTex = new Texture2D(w, h, TextureFormat.RGBA32, false);

            var heights = new float[w, h];
            for (int y = 0; y < h; y++)
            {
                float v = (float)y / h;
                for (int x = 0; x < w; x++)
                {
                    float u = (float)x / w;
                    albedoTex.SetPixel(x, y, albedoFn(u, v));
                    heights[x, y] = heightFn(u, v);
                    var m = maskFn(u, v);
                    maskTex.SetPixel(x, y, new Color(m.metallic, m.occlusion, 0f, m.smoothness));
                }
            }

            // Tính Normal map từ Height (Sobel operator)
            float strength = 4.0f;
            for (int y = 0; y < h; y++)
            {
                int y0 = (y - 1 + h) % h;
                int y1 = (y + 1) % h;
                for (int x = 0; x < w; x++)
                {
                    int x0 = (x - 1 + w) % w;
                    int x1 = (x + 1) % w;

                    float dx = (heights[x1, y] - heights[x0, y]) * strength;
                    float dy = (heights[x, y1] - heights[x, y0]) * strength;

                    Vector3 n = new Vector3(-dx, -dy, 1.0f).normalized;
                    // Chuyển sang không gian [0..1]
                    Color nCol = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1.0f);
                    normalTex.SetPixel(x, y, nCol);
                }
            }

            albedoTex.Apply();
            normalTex.Apply();
            maskTex.Apply();

            File.WriteAllBytes(albedoPath, albedoTex.EncodeToPNG());
            File.WriteAllBytes(normalPath, normalTex.EncodeToPNG());
            File.WriteAllBytes(maskPath, maskTex.EncodeToPNG());

            UnityEngine.Object.DestroyImmediate(albedoTex);
            UnityEngine.Object.DestroyImmediate(normalTex);
            UnityEngine.Object.DestroyImmediate(maskTex);

            AssetDatabase.ImportAsset(albedoPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(normalPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(maskPath, ImportAssetOptions.ForceUpdate);

            // Cấu hình TextureImporter
            var nImp = (TextureImporter)AssetImporter.GetAtPath(normalPath);
            if (nImp != null)
            {
                nImp.textureType = TextureImporterType.NormalMap;
                nImp.wrapMode = TextureWrapMode.Repeat;
                nImp.SaveAndReimport();
            }

            var mImp = (TextureImporter)AssetImporter.GetAtPath(maskPath);
            if (mImp != null)
            {
                mImp.sRGBTexture = false; // Mask map (linear data)
                mImp.wrapMode = TextureWrapMode.Repeat;
                mImp.SaveAndReimport();
            }

            var aImp = (TextureImporter)AssetImporter.GetAtPath(albedoPath);
            if (aImp != null)
            {
                aImp.wrapMode = TextureWrapMode.Repeat;
                aImp.anisoLevel = 4;
                aImp.SaveAndReimport();
            }

            return (AssetDatabase.LoadAssetAtPath<Texture2D>(albedoPath),
                    AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath),
                    AssetDatabase.LoadAssetAtPath<Texture2D>(maskPath));
        }

        // ---------- Các hàm sinh vân giải tích ----------

        static Color GenTilePattern(float u, float v, Color tileCol, Color seamCol, float seamWidth)
        {
            float fu = Mathf.Repeat(u * 8f, 1f);
            float fv = Mathf.Repeat(v * 8f, 1f);
            bool isSeam = fu < seamWidth || fu > (1f - seamWidth) || fv < seamWidth || fv > (1f - seamWidth);
            float noise = (Mathf.Sin(u * 120f) * Mathf.Cos(v * 120f)) * 0.02f;
            return isSeam ? seamCol : new Color(tileCol.r + noise, tileCol.g + noise, tileCol.b + noise, 1f);
        }

        static float GenTileHeight(float u, float v, float seamWidth)
        {
            float fu = Mathf.Repeat(u * 8f, 1f);
            float fv = Mathf.Repeat(v * 8f, 1f);
            bool isSeam = fu < seamWidth || fu > (1f - seamWidth) || fv < seamWidth || fv > (1f - seamWidth);
            return isSeam ? 0.2f : 0.95f;
        }

        static Color GenNoisePattern(float u, float v, Color baseCol, float amp)
        {
            float n = (Mathf.Sin(u * 50f) * Mathf.Cos(v * 50f) + Mathf.Sin(u * 110f + v * 90f) * 0.5f) * amp;
            return new Color(Mathf.Clamp01(baseCol.r + n), Mathf.Clamp01(baseCol.g + n), Mathf.Clamp01(baseCol.b + n), 1f);
        }

        static float GenMicroNoise(float u, float v, float freq)
        {
            return (Mathf.Sin(u * freq * Mathf.PI) * Mathf.Cos(v * freq * Mathf.PI)) * 0.5f + 0.5f;
        }

        static Color GenBrushedMetal(float u, float v, Color metalCol)
        {
            float scratch = Mathf.Sin(v * 800f) * 0.04f + Mathf.Sin(v * 1600f) * 0.02f;
            return new Color(Mathf.Clamp01(metalCol.r + scratch), Mathf.Clamp01(metalCol.g + scratch), Mathf.Clamp01(metalCol.b + scratch), 1f);
        }

        static float GenBrushedNormalHeight(float u, float v)
        {
            return Mathf.Sin(v * 800f) * 0.3f + 0.5f;
        }

        static Color GenWoodPattern(float u, float v, Color lightCol, Color darkCol)
        {
            float grain = Mathf.Sin(u * 20f + Mathf.Sin(v * 40f) * 2f) * 0.5f + 0.5f;
            return Color.Lerp(lightCol, darkCol, grain);
        }

        static float GenWoodHeight(float u, float v)
        {
            return Mathf.Sin(u * 20f + Mathf.Sin(v * 40f) * 2f) * 0.5f + 0.5f;
        }
    }
}
