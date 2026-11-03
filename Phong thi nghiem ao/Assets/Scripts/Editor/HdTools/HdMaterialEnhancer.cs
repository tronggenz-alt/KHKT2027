using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Khkt.EditorTools
{
    /// <summary>
    /// Hệ thống nâng cấp vật liệu PBR siêu thực cho VERA Lab HD:
    ///   • Áp dụng Normal map, AO map và tinh chỉnh Metallic/Smoothness chuẩn vật lý cho vật liệu toàn viện.
    ///   • Thép không gỉ 304 satin #4, Nhôm anodized, Trespa TopLab kháng hóa chất, Epoxy nguyên khối.
    ///   • Cây xanh siêu thực (gân lá nổi khối, SSS xuyên sáng, đung đưa gió nhẹ, chậu terracotta, đất hữu cơ).
    ///   • Dụng cụ & máy móc (thủy tinh Borosilicate 3.3, vạch chia sắc nét, inox xước, đèn LED, màn hình hiển thị).
    /// </summary>
    public static class HdMaterialEnhancer
    {
        const string HdTexDir = "Assets/Lab/HD/Textures";
        const string MatDir = "Assets/Lab/Materials";

        public static void EnsureLeafNormalMap()
        {
            if (!Directory.Exists(HdTexDir))
                Directory.CreateDirectory(HdTexDir);

            string path = $"{HdTexDir}/Normal_LeafVein.png";
            if (File.Exists(path)) return;

            int w = 1024, h = 1024;
            var heights = new float[w, h];
            for (int y = 0; y < h; y++)
            {
                float v = (float)y / h;
                for (int x = 0; x < w; x++)
                {
                    float u = (float)x / w;
                    float centerU = Mathf.Abs(u - 0.5f);
                    // Gân chính (midrib) chạy dọc tâm, vuốt thon về ngọn
                    float midrib = Mathf.Clamp01(1.0f - centerU * 28.0f) * Mathf.Lerp(1.0f, 0.25f, v);
                    // Gân nhánh xương cá lệch góc ~48 độ hướng lên
                    float branchCoord = (v - centerU * 0.7f) * 26.0f;
                    float branchWave = Mathf.Sin(branchCoord * Mathf.PI);
                    float branchMask = Mathf.SmoothStep(0.55f, 1.0f, branchWave) * Mathf.Clamp01(1.0f - centerU * 2.2f);
                    // Lưới gân con vi mô
                    float fine1 = Mathf.Sin(u * 120.0f + v * 80.0f) * Mathf.Cos(u * 90.0f - v * 110.0f);
                    float fine2 = Mathf.Sin(u * 200.0f - v * 160.0f) * 0.5f;
                    float fineMesh = (fine1 + fine2) * 0.08f;

                    heights[x, y] = midrib * 0.65f + branchMask * 0.35f + fineMesh;
                }
            }

            var normalTex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            float strength = 4.5f;
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
                    Color nCol = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1.0f);
                    normalTex.SetPixel(x, y, nCol);
                }
            }
            normalTex.Apply();
            File.WriteAllBytes(path, normalTex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(normalTex);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp != null)
            {
                imp.textureType = TextureImporterType.NormalMap;
                imp.wrapMode = TextureWrapMode.Repeat;
                imp.maxTextureSize = 1024;
                imp.SaveAndReimport();
            }
        }

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
                bool isNormal = filename.StartsWith("Normal_") || filename.EndsWith("_Normal");

                bool needUpdate = false;
                if (isNormal && importer.textureType != TextureImporterType.NormalMap)
                {
                    importer.textureType = TextureImporterType.NormalMap;
                    needUpdate = true;
                }
                else if (!isNormal && importer.textureType != TextureImporterType.Default)
                {
                    importer.textureType = TextureImporterType.Default;
                    importer.sRGBTexture = !filename.Contains("Mask") && !filename.Contains("AO_");
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
            EnsureLeafNormalMap();
            ConfigureTextureImporters();

            // Nạp các textures PBR
            var normTilesLab = AssetDatabase.LoadAssetAtPath<Texture2D>($"{HdTexDir}/Normal_TilesLab.png");
            var aoTilesLab = AssetDatabase.LoadAssetAtPath<Texture2D>($"{HdTexDir}/AO_TilesLab.png");
            var normTilesChem = AssetDatabase.LoadAssetAtPath<Texture2D>($"{HdTexDir}/Normal_TilesChem.png");
            var normPlaster = AssetDatabase.LoadAssetAtPath<Texture2D>($"{HdTexDir}/Normal_Plaster.png");
            var normFacade = AssetDatabase.LoadAssetAtPath<Texture2D>($"{HdTexDir}/Normal_Facade.png");
            var normInox = AssetDatabase.LoadAssetAtPath<Texture2D>($"{HdTexDir}/Normal_InoxSatin.png") ??
                           AssetDatabase.LoadAssetAtPath<Texture2D>($"{HdTexDir}/InoxSatin304_Normal.png");
            var normAlu = AssetDatabase.LoadAssetAtPath<Texture2D>($"{HdTexDir}/Normal_AnodizedAlu.png") ??
                          AssetDatabase.LoadAssetAtPath<Texture2D>($"{HdTexDir}/AlumAnodized029_Normal.png");
            var normPhenolic = AssetDatabase.LoadAssetAtPath<Texture2D>($"{HdTexDir}/Normal_LabPhenolic.png");
            var normEpoxy = AssetDatabase.LoadAssetAtPath<Texture2D>($"{HdTexDir}/Normal_LabEpoxy.png");
            var normWood = AssetDatabase.LoadAssetAtPath<Texture2D>($"{HdTexDir}/Normal_WoodGrain.png");
            var normConc = AssetDatabase.LoadAssetAtPath<Texture2D>($"{HdTexDir}/Normal_Concrete.png");
            var normGlass = AssetDatabase.LoadAssetAtPath<Texture2D>($"{HdTexDir}/Normal_GlassOptic.png");
            var normRubber = AssetDatabase.LoadAssetAtPath<Texture2D>($"{HdTexDir}/Normal_Rubber.png");
            var normLeaf = AssetDatabase.LoadAssetAtPath<Texture2D>($"{HdTexDir}/Normal_LeafVein.png");

            var soilAlbedo = AssetDatabase.LoadAssetAtPath<Texture2D>($"{HdTexDir}/SoilGarden037_Albedo.png");
            var soilNormal = AssetDatabase.LoadAssetAtPath<Texture2D>($"{HdTexDir}/SoilGarden037_Normal.png");
            var soilMask = AssetDatabase.LoadAssetAtPath<Texture2D>($"{HdTexDir}/SoilGarden037_Mask.png");

            var terraAlbedo = AssetDatabase.LoadAssetAtPath<Texture2D>($"{HdTexDir}/TerracottaClay002_Albedo.png");
            var terraNormal = AssetDatabase.LoadAssetAtPath<Texture2D>($"{HdTexDir}/TerracottaClay002_Normal.png");
            var terraMask = AssetDatabase.LoadAssetAtPath<Texture2D>($"{HdTexDir}/TerracottaClay002_Mask.png");

            var shaderLaCay = Shader.Find("Khkt/Bio/LaCay");

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
            }, ref count);

            Enhance("T2_TuongKem", m => {
                SetNormal(m, normPlaster, 0.5f);
                m.SetFloat("_Smoothness", 0.16f);
            }, ref count);

            // 2. Sàn các khu
            Enhance("SanPhongLab", m => {
                SetNormal(m, normTilesLab, 0.8f);
                SetAO(m, aoTilesLab, 0.9f);
                m.SetFloat("_Smoothness", 0.55f);
            }, ref count);

            Enhance("SanPhongHoaChat", m => {
                SetNormal(m, normTilesChem, 0.75f);
                m.SetFloat("_Smoothness", 0.48f);
            }, ref count);

            Enhance("SanHanhLang", m => {
                SetNormal(m, normTilesLab, 0.7f);
                SetAO(m, aoTilesLab, 0.85f);
                m.SetFloat("_Smoothness", 0.52f);
            }, ref count);

            Enhance("SanBanCong", m => {
                SetNormal(m, normTilesLab, 0.9f);
                m.SetFloat("_Smoothness", 0.35f);
            }, ref count);

            Enhance("T2_SanGo", m => {
                SetNormal(m, normWood, 0.8f);
                m.SetFloat("_Smoothness", 0.45f);
            }, ref count);

            Enhance("T2_SanHanhLangT2", m => {
                SetNormal(m, normTilesLab, 0.7f);
                m.SetFloat("_Smoothness", 0.50f);
            }, ref count);

            Enhance("T2_SanNhaKinh", m => {
                SetNormal(m, normTilesChem, 0.8f);
                m.SetFloat("_Smoothness", 0.40f);
            }, ref count);

            // 3. Mặt bàn thí nghiệm
            Enhance("MatBanPhenolic", m => {
                SetNormal(m, normPhenolic, 0.6f);
                m.SetFloat("_Smoothness", 0.45f);
                m.SetFloat("_Metallic", 0f);
            }, ref count);

            Enhance("MatBanEpoxy", m => {
                SetNormal(m, normEpoxy, 0.35f);
                m.SetFloat("_Smoothness", 0.65f);
                m.SetFloat("_Metallic", 0f);
            }, ref count);

            Enhance("T2_BanSinhHoc", m => {
                SetNormal(m, normPhenolic, 0.5f);
                m.SetFloat("_Smoothness", 0.40f);
            }, ref count);

            // 4. Kim loại (Inox 304 satin #4, Nhôm anodized)
            Enhance("Inox", m => {
                SetNormal(m, normInox, 0.75f);
                m.SetTextureScale("_BumpMap", new Vector2(8, 8));
                m.SetTextureScale("_BaseMap", new Vector2(8, 8));
                m.SetFloat("_Smoothness", 0.72f);
                m.SetFloat("_Metallic", 0.95f);
            }, ref count);

            Enhance("KimLoaiInox", m => {
                SetNormal(m, normInox, 0.65f);
                m.SetFloat("_Smoothness", 0.72f);
                m.SetFloat("_Metallic", 0.95f);
            }, ref count);

            Enhance("GiaInox", m => {
                SetNormal(m, normInox, 0.60f);
                m.SetFloat("_Smoothness", 0.68f);
                m.SetFloat("_Metallic", 0.92f);
            }, ref count);

            Enhance("ChanGhe", m => {
                SetNormal(m, normInox, 0.40f);
                m.SetFloat("_Smoothness", 0.78f);
                m.SetFloat("_Metallic", 0.98f);
            }, ref count);

            Enhance("NhomMo", m => {
                SetNormal(m, normAlu, 0.5f);
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

            Enhance("KhungNhomNFT", m => {
                SetNormal(m, normAlu, 0.6f);
                m.SetFloat("_Smoothness", 0.55f);
                m.SetFloat("_Metallic", 0.85f);
            }, ref count);

            Enhance("T2_NapRanh", m => {
                SetNormal(m, normAlu, 0.7f);
                m.SetFloat("_Smoothness", 0.35f);
                m.SetFloat("_Metallic", 0.70f);
            }, ref count);

            Enhance("T2_TuAnToanSinhHoc", m => {
                SetNormal(m, normInox, 0.55f);
                m.SetFloat("_Smoothness", 0.68f);
                m.SetFloat("_Metallic", 0.95f);
            }, ref count);

            Enhance("T2_TuAm", m => {
                SetNormal(m, normInox, 0.55f);
                m.SetFloat("_Smoothness", 0.68f);
                m.SetFloat("_Metallic", 0.95f);
            }, ref count);

            Enhance("KinhHienVi_Inox", m => {
                SetNormal(m, normInox, 0.50f);
                m.SetFloat("_Smoothness", 0.75f);
                m.SetFloat("_Metallic", 0.95f);
            }, ref count);

            Enhance("T2_BanSoiUV", m => {
                SetNormal(m, normInox, 0.60f);
                m.SetFloat("_Smoothness", 0.62f);
                m.SetFloat("_Metallic", 0.88f);
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

            Enhance("CaoSuDo", m => {
                SetNormal(m, normRubber, 0.8f);
                m.SetFloat("_Smoothness", 0.35f);
                m.SetFloat("_Metallic", 0f);
            }, ref count);

            Enhance("BeTong", m => {
                SetNormal(m, normConc, 1.0f);
                m.SetFloat("_Smoothness", 0.25f);
            }, ref count);

            Enhance("NhuaMo", m => {
                SetNormal(m, normPhenolic, 0.4f);
                m.SetFloat("_Smoothness", 0.42f);
            }, ref count);

            Enhance("T2_PipetXanhLa", m => { m.SetFloat("_Smoothness", 0.65f); m.SetFloat("_Metallic", 0f); }, ref count);
            Enhance("T2_HopXanhLa", m => { m.SetFloat("_Smoothness", 0.65f); m.SetFloat("_Metallic", 0f); }, ref count);
            Enhance("ThungChuaXanh", m => { m.SetFloat("_Smoothness", 0.58f); m.SetFloat("_Metallic", 0.05f); }, ref count);
            Enhance("Khay8C59BF", m => { m.SetFloat("_Smoothness", 0.62f); m.SetFloat("_Metallic", 0.05f); }, ref count);

            // 8. Thủy tinh Borosilicate 3.3, Mặt khum & Kính quang học (Giai đoạn 7)
            Enhance("ThuyTinh", m => {
                SetNormal(m, normGlass, 0.15f);
                m.SetFloat("_Smoothness", 0.98f);
                m.SetFloat("_Metallic", 0f);
                m.SetColor("_BaseColor", new Color(0.92f, 0.98f, 1.0f, 0.18f));
            }, ref count);

            Enhance("CocThuyTinh", m => {
                SetNormal(m, normGlass, 0.15f);
                m.SetFloat("_Smoothness", 0.98f);
                m.SetFloat("_Metallic", 0f);
                m.SetColor("_BaseColor", new Color(0.92f, 0.98f, 1.0f, 0.20f));
            }, ref count);

            Enhance("MatKhum", m => {
                m.SetFloat("_Smoothness", 0.98f);
                m.SetFloat("_Metallic", 0f);
            }, ref count);

            Enhance("NuocCoc", m => {
                m.SetFloat("_Smoothness", 0.96f);
                m.SetFloat("_Metallic", 0f);
            }, ref count);

            Enhance("DungDichPhanUng", m => {
                m.SetFloat("_Smoothness", 0.95f);
                m.SetFloat("_Metallic", 0f);
            }, ref count);

            Enhance("HopLamKinh", m => {
                SetNormal(m, normGlass, 0.15f);
                m.SetFloat("_Smoothness", 0.98f);
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

            Enhance("T2_DiaPetri", m => {
                m.SetFloat("_Smoothness", 0.96f);
            }, ref count);

            Enhance("T2_NapDienDi", m => {
                m.SetFloat("_Smoothness", 0.94f);
            }, ref count);

            Enhance("T2_KinhLocCam", m => {
                m.SetFloat("_Smoothness", 0.95f);
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

            // 9. CÂY XANH, CHẬU & ĐẤT (GIAI ĐOẠN 6)
            Action<Material> configureLeaf = m => {
                if (shaderLaCay != null) m.shader = shaderLaCay;
                if (normLeaf != null)
                {
                    m.SetTexture("_BumpMap", normLeaf);
                    m.SetFloat("_BumpScale", 0.85f);
                }
                m.SetFloat("_Translucency", 0.45f);
                m.SetFloat("_WindSpeed", 2.4f);
                m.SetFloat("_WindStrength", 0.035f);
                m.SetFloat("_Cutoff", 0.5f);
            };

            Enhance("La", configureLeaf, ref count);
            Enhance("LaDam", configureLeaf, ref count);
            Enhance("LaCayVanPhong", configureLeaf, ref count);
            Enhance("T2_LaCay", configureLeaf, ref count);
            Enhance("T2_LaCayDam", configureLeaf, ref count);
            Enhance("T2_LaCayNon", configureLeaf, ref count);

            Enhance("ThanCay", m => {
                SetNormal(m, normWood, 0.85f);
                m.SetFloat("_Smoothness", 0.28f);
            }, ref count);

            Enhance("ChauDat", m => {
                if (terraAlbedo != null) m.SetTexture("_BaseMap", terraAlbedo);
                if (terraNormal != null) SetNormal(m, terraNormal, 0.75f);
                if (terraMask != null) m.SetTexture("_MetallicGlossMap", terraMask);
                m.SetFloat("_Smoothness", 0.22f);
            }, ref count);

            Enhance("T2_ChauDatNung", m => {
                if (terraAlbedo != null) m.SetTexture("_BaseMap", terraAlbedo);
                if (terraNormal != null) SetNormal(m, terraNormal, 0.75f);
                if (terraMask != null) m.SetTexture("_MetallicGlossMap", terraMask);
                m.SetFloat("_Smoothness", 0.25f);
            }, ref count);

            Enhance("T2_ChauXam", m => {
                SetNormal(m, normConc, 0.7f);
                m.SetFloat("_Smoothness", 0.32f);
            }, ref count);

            Enhance("ChauSuTrang", m => {
                SetNormal(m, normGlass, 0.05f);
                m.SetFloat("_Smoothness", 0.94f);
                m.SetFloat("_Metallic", 0f);
            }, ref count);

            Enhance("T2_DatAm", m => {
                if (soilAlbedo != null) m.SetTexture("_BaseMap", soilAlbedo);
                if (soilNormal != null) SetNormal(m, soilNormal, 0.9f);
                if (soilMask != null) m.SetTexture("_MetallicGlossMap", soilMask);
                m.SetFloat("_Smoothness", 0.15f);
            }, ref count);

            // 10. MÁY MÓC & HIỂN THỊ ĐIỆN TỬ (GIAI ĐOẠN 7)
            Enhance("KinhHienVi_Than", m => {
                SetNormal(m, normPlaster, 0.4f);
                m.SetFloat("_Smoothness", 0.42f);
                m.SetFloat("_Metallic", 0.12f);
            }, ref count);

            Enhance("KinhHienVi_Den", m => {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", new Color(1.8f, 1.9f, 2.0f) * 1.5f);
            }, ref count);

            Enhance("KinhHienVi_LED", m => {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", new Color(1.8f, 1.9f, 2.0f) * 1.5f);
            }, ref count);

            Enhance("T2_ManLedXanhLa", m => {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", new Color(0.1f, 1.8f, 0.4f) * 2.2f);
            }, ref count);

            Enhance("LedHoPhach", m => {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", new Color(2.0f, 1.2f, 0.1f) * 1.8f);
            }, ref count);

            Enhance("T2_BongUvTat", m => {
                m.SetFloat("_Smoothness", 0.85f);
                m.SetFloat("_Metallic", 0.2f);
            }, ref count);

            Enhance("T2_DenTrongCay", m => {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", new Color(0.95f, 0.45f, 0.85f) * 2.5f);
            }, ref count);

            AssetDatabase.SaveAssets();
            Debug.Log($"[HD-PBR] Đã nâng cấp thành công {count} vật liệu chuẩn PBR siêu thực (Cây xanh, Dụng cụ & Máy móc).");
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
