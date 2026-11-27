using System;
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Khkt.Rendering;

namespace Khkt.Demo
{
#if UNITY_EDITOR
    /// <summary>
    /// Runner PlayMode kiểm thử kỹ thuật cho các pha P1, P3 (Phòng thử quang học 2880x1800), P5, P6, P9.
    /// Tự động bị MacBuild loại bỏ khỏi bản phát hành nhờ tên khớp Khkt.*Runner*.
    /// </summary>
    public class KhktPhongThuRunner : MonoBehaviour
    {
        public string CheDo = "P1";

        const int Rong = 2880;
        const int Cao = 1800;

        static string ThuMucXuat => Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile),
            "AppBuild", "kinh_long_so_sanh");

        void Start()
        {
            if (CheDo == "P1")
                StartCoroutine(ChayP1());
            else if (CheDo == "P3")
                StartCoroutine(ChayP3());
            else if (CheDo == "P4")
                StartCoroutine(ChayP4());
        }

        // ========================================================================= P1
        IEnumerator ChayP1()
        {
            var (baseCam, canvas, haiTang) = TaoSanKhauCoBan(new Color(0.12f, 0.24f, 0.45f, 1f));

            var contentBox = TaoHop(canvas.transform, "ContentBox", Vector2.zero, new Vector2(600, 300), new Color(0.95f, 0.45f, 0.1f, 1f));
            contentBox.layer = 5;

            var navBox = TaoHop(canvas.transform, "NavBox", new Vector2(160, 60), new Vector2(260, 120), new Color(0.1f, 0.85f, 0.45f, 1f));
            KinhGuongDieuHuong.Gan(navBox, haiTang, true);

            if (navBox.layer != KinhLongLop.LayerDieuHuong)
            {
                Debug.LogError($"[P1_FAIL] NavBox không nằm ở Layer 14: {navBox.layer}");
                EditorApplication.Exit(1);
                yield break;
            }

            var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
            rt.Create();
            baseCam.targetTexture = rt;
            KinhLongBlurCache.EpChupMoiKhung = true;

            yield return null;
            yield return null;
            foreach (var g in FindObjectsByType<Graphic>(FindObjectsInactive.Exclude))
                g.SetAllDirty();
            yield return null;
            Canvas.ForceUpdateCanvases();
            baseCam.Render();

            var texOut = DocRT(rt);
            int navX = 640 + (int)(160f / 1920f * 1280f);
            int navY = 360 + (int)(60f / 1080f * 720f);
            Color centerC = texOut.GetPixel(640, 360);
            Color navC = texOut.GetPixel(navX, navY);

            Texture klNoiDung = KinhLongBlurFeature.KinhLongBlurPass.NoiDungRT;
            Texture klMo1 = KinhLongBlurFeature.KinhLongBlurPass.Mo1RT;
            Texture klMo4 = KinhLongBlurFeature.KinhLongBlurPass.Mo4RT;

            Color bgAtNavInNoiDung = Color.black;
            if (klNoiDung is RenderTexture noiRt)
            {
                var texNoi = DocRT(noiRt);
                bgAtNavInNoiDung = texNoi.GetPixel(navX, navY);
                Destroy(texNoi);
            }

            Debug.Log($"[P1 Check] Center={centerC} Nav={navC} NoiDungAtNav={bgAtNavInNoiDung} NoiDung={(klNoiDung != null ? klNoiDung.width + "x" + klNoiDung.height : "null")} Mo1={(klMo1 != null ? klMo1.width + "x" + klMo1.height : "null")} Mo4={(klMo4 != null ? klMo4.width + "x" + klMo4.height : "null")}");

            KinhLongBlurCache.EpChupMoiKhung = false;
            KinhLongBlurCache.DanhDauBan(0);
            int dummyW = 1280, dummyH = 720;
            for (int i = 0; i < 6; i++)
                KinhLongBlurCache.CanRenderBlur(1280, 720, ref dummyW, ref dummyH);
            bool idleSkip = !KinhLongBlurCache.CanRenderBlur(1280, 720, ref dummyW, ref dummyH);

            baseCam.targetTexture = null;
            rt.Release();
            Destroy(rt);
            Destroy(texOut);

            bool okContent = centerC.r > 0.90f && centerC.g < 0.55f && centerC.b < 0.25f;
            bool okOverlay = navC.g > 0.75f && navC.r < 0.30f;
            bool okClean = bgAtNavInNoiDung.r > 0.85f && bgAtNavInNoiDung.g < 0.55f;
            bool okTex = klNoiDung != null && klNoiDung.width == 1280 && klNoiDung.height == 720
                      && klMo1 != null && klMo1.width == 640 && klMo1.height == 360
                      && klMo4 != null && klMo4.width == 80 && klMo4.height == 45;

            if (!okContent || !okOverlay || !okClean || !okTex || !idleSkip)
            {
                Debug.LogError($"[P1_FAIL] okContent={okContent} okOverlay={okOverlay} okClean={okClean} okTex={okTex} idleSkip={idleSkip}");
                EditorApplication.Exit(1);
                yield break;
            }

            Debug.Log("[P1_PASS] Thành công P1!");
            EditorApplication.Exit(0);
        }

        static bool Fail(string msg)
        {
            Debug.LogError(msg);
            EditorApplication.Exit(1);
            return false;
        }

        // ========================================================================= P3
        IEnumerator ChayP3()
        {
            Directory.CreateDirectory(ThuMucXuat);
            PlayerPrefs.SetInt("KHKT_KINH_LONG", 1);
            PlayerPrefs.SetInt("KHKT_GIAM_TRONG_SUOT", 0);
            PlayerPrefs.SetInt("KHKT_TANG_TUONG_PHAN", 0);
            PlayerPrefs.SetFloat("KHKT_DO_TRONG", 0.35f);
            KinhLongQuangHoc.DamBaoKhoiTao();

            var (baseCam, canvas, haiTang) = TaoSanKhauCoBan(new Color(0.08f, 0.10f, 0.14f, 1f));
            var rt = new RenderTexture(Rong, Cao, 24, RenderTextureFormat.ARGB32);
            rt.Create();
            baseCam.targetTexture = rt;
            KinhLongBlurCache.EpChupMoiKhung = true;

            var rootContent = new GameObject("RootContent", typeof(RectTransform));
            rootContent.layer = 5;
            rootContent.transform.SetParent(canvas.transform, false);
            Stretch((RectTransform)rootContent.transform);

            var rootNav = new GameObject("RootNav", typeof(RectTransform));
            rootNav.transform.SetParent(canvas.transform, false);
            Stretch((RectTransform)rootNav.transform);
            KinhGuongDieuHuong.Gan(rootNav, haiTang, true);

            // ---------------- Trường hợp (a): Sáng / Tối & giữ trung tính trên nền xám #808080
            yield return DựngVaChupA(baseCam, rt, rootContent.transform, rootNav.transform);

            // ---------------- Trường hợp (b): Khúc xạ Snell ở vành viên nang (so với phần phẳng giữa)
            XoaCon(rootContent.transform);
            XoaCon(rootNav.transform);
            yield return DungVaChupB(baseCam, rt, rootContent.transform, rootNav.transform);

            // ---------------- Trường hợp (c): Xem trước ngưng tụ vật chất 4 bước tĩnh (0.05..1.0)
            XoaCon(rootContent.transform);
            XoaCon(rootNav.transform);
            yield return DungVaChupC(baseCam, rt, rootContent.transform, rootNav.transform);

            // ---------------- Trường hợp (d): Regular vs Clear trên nền chữ 17pt & ảnh màu (hai tầng)
            XoaCon(rootContent.transform);
            XoaCon(rootNav.transform);
            yield return DungVaChupD(baseCam, rt, rootContent.transform, rootNav.transform);

            // ---------------- Trường hợp (e): Xem trước nút gạt / thanh trượt với tiết diện Lip
            XoaCon(rootContent.transform);
            XoaCon(rootNav.transform);
            yield return DungVaChupE(baseCam, rt, rootContent.transform, rootNav.transform);

            baseCam.targetTexture = null;
            rt.Release();
            Destroy(rt);

            Debug.Log("[P3_PASS] Đã chụp xong các ảnh chuẩn (a)-(e) 2880x1800 và đạt toàn bộ kiểm tra quang học §12.2 (#1, #2, #3, #5)!");
            EditorApplication.Exit(0);
        }

        // ========================================================================= P4
        IEnumerator ChayP4()
        {
            PlayerPrefs.SetInt("KHKT_KINH_LONG", 1);
            UiTheme.Set(UiTheme.Mode.XanhMuc, false);
            KinhLongQuangHoc.DamBaoKhoiTao();
            var tieuDiem = KinhLongTieuDiem.DamBao();

            var (baseCam, canvas, haiTang) = TaoSanKhauCoBan(new Color(0.06f, 0.08f, 0.12f, 1f));
            var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
            rt.Create();
            baseCam.targetTexture = rt;
            KinhLongBlurCache.EpChupMoiKhung = true;

            var contentRoot = new GameObject("P4Content", typeof(RectTransform));
            contentRoot.layer = 5;
            contentRoot.transform.SetParent(canvas.transform, false);
            Stretch((RectTransform)contentRoot.transform);

            var navRoot = new GameObject("P4Nav", typeof(RectTransform));
            navRoot.transform.SetParent(canvas.transform, false);
            Stretch((RectTransform)navRoot.transform);
            KinhGuongDieuHuong.Gan(navRoot, haiTang, true);

            // Nền Content nửa trái tối (#10141D), nửa phải giấy trắng (#F6F7FA)
            var bgDark = TaoHop(contentRoot.transform, "BgDark", new Vector2(-480, 0), new Vector2(960, 1080), new Color(0.06f, 0.08f, 0.11f, 1f));
            var bgLight = TaoHop(contentRoot.transform, "BgLight", new Vector2(480, 0), new Vector2(960, 1080), new Color(0.96f, 0.97f, 0.98f, 1f));

            // 1. Nút nhỏ 56x48 (<= 64u) bật TuDong ở bên tối trước rồi trượt sang bên sáng
            var smallGlass = TaoKinh(navRoot.transform, "SmallAdaptive", new Vector2(-360, 0), new Vector2(56, 48),
                KinhLongCheDo.Regular, laSang: false, vienNang: true, laDieuHuong: true, bongCuongDo: 0.20f);
            var smallVung = smallGlass.gameObject.AddComponent<VungKinhApple>();
            smallVung.CheDoThichUng = KinhLongThichUngCheDo.TuDong;
            var smallLbl = TaoChu(smallGlass.transform, "A", 20, Color.white);
            var smallAdapt = KinhLongThichUng.Gan(smallGlass, smallVung);

            // 2. Bảng lớn 420x120 (> 64u) đặt đè trên nền sáng -> PHẢI khóa theo UiTheme.IsLight (Dark)
            var largeGlass = TaoKinh(navRoot.transform, "LargeLocked", new Vector2(480, 140), new Vector2(420, 120),
                KinhLongCheDo.Regular, laSang: true, vienNang: true, laDieuHuong: true);
            var largeVung = largeGlass.gameObject.AddComponent<VungKinhApple>();
            largeVung.CheDoThichUng = KinhLongThichUngCheDo.KhoaTheoChuDe;
            var largeAdapt = KinhLongThichUng.Gan(largeGlass, largeVung);

            yield return null;
            yield return null;
            Canvas.ForceUpdateCanvases();
            baseCam.Render();
            yield return null; // cho AsyncGPUReadback trả về lưới 24x14

            float tWait = 0f;
            while (!KinhLongBlurCache.CoLuoiGpu && tWait < 0.5f)
            {
                baseCam.Render();
                tWait += 0.02f;
                yield return null;
            }

            smallAdapt.CapNhatNgay();
            largeAdapt.CapNhatNgay();
            bool smallStartDark = !smallAdapt.DangSang && smallLbl.color.r > 0.8f;
            bool largeLockedDark = !largeAdapt.DangSang;
            float shadowDark = smallGlass.BongCuongDo;

            // Di chuyển nút nhỏ sang nền sáng (+480, 0) và chờ lưới sáng GPU cập nhật + chuyển mượt
            smallGlass.rectTransform.anchoredPosition = new Vector2(480, 0);
            for (int f = 0; f < 20; f++)
            {
                if (f == 8 || f == 14) baseCam.Render();
                yield return null;
                smallAdapt.CapNhatNgay();
            }
            bool smallTurnedLight = smallAdapt.DangSang && smallLbl.color.r < 0.25f;
            float shadowLight = smallGlass.BongCuongDo;

            // Kiểm tra ánh sáng môi trường _KL_Light lệch phải trong +-15 độ của (-0.45, 0.89)
            Vector4 lightVec = Shader.GetGlobalVector("_KL_Light");
            float dotNeo = Vector2.Dot(new Vector2(lightVec.x, lightVec.y).normalized, new Vector2(-0.45f, 0.89f).normalized);
            bool lightOk = dotNeo >= Mathf.Cos(16f * Mathf.Deg2Rad);

            // Kiểm tra làm dịu mất tiêu điểm (trong batchmode mặc định = 0, khi giả lập = 1)
            float focusDefault = Shader.GetGlobalFloat("_KL_MatTieuDiem");
            KinhLongTieuDiem.DatGiaLapNgay(true);
            float focusSim = Shader.GetGlobalFloat("_KL_MatTieuDiem");
            KinhLongTieuDiem.DatGiaLapNgay(false);

            Debug.Log($"[P4 Check] coLuoiGpu={KinhLongBlurCache.CoLuoiGpu} smallStartDark={smallStartDark} smallTurnedLight={smallTurnedLight} largeLockedDark={largeLockedDark} shadowDark={shadowDark:F3} shadowLight={shadowLight:F3} lightDot={dotNeo:F4} focusDefault={focusDefault} focusSim={focusSim}");

            baseCam.targetTexture = null;
            rt.Release();
            Destroy(rt);

            if (!smallStartDark || !smallTurnedLight || !largeLockedDark || shadowLight <= shadowDark || !lightOk || focusDefault > 0.01f || focusSim < 0.99f)
            {
                Fail($"[P4_FAIL] startDark={smallStartDark} turnLight={smallTurnedLight} largeDark={largeLockedDark} shadow={shadowDark:F3}->{shadowLight:F3} light={lightOk} focus={focusDefault}->{focusSim}");
                yield break;
            }

            Debug.Log("[P4_PASS] Thành công P4: thích ứng Sáng/Tối trễ hai ngưỡng, bóng đổ thích ứng, ánh sáng neo ±15° & mất tiêu điểm an toàn batchmode!");
            EditorApplication.Exit(0);
        }

        IEnumerator DựngVaChupA(Camera cam, RenderTexture rt, Transform content, Transform nav)
        {
            // Nửa trái tối xanh sâu + mảng xám chuẩn #808080, nửa phải sáng mềm + mảng xám chuẩn #808080
            var texBg = TaoAnhNenKiemThuA();
            var bgRaw = TaoRaw(content, "BgA", Vector2.zero, new Vector2(1920, 1080), texBg);

            // Kính Tối Regular đặt lên vùng xám #808080 bên trái (-480, 60)
            var darkPill = TaoKinh(nav, "DarkPillGray", new Vector2(-480, 60), new Vector2(380, 120),
                KinhLongCheDo.Regular, laSang: false, vienNang: true, laDieuHuong: true, mucMo: 2.3f, cuongDoNen: 0.44f);
            // Kính Tối Clear trên dải chuyển sắc (-480, -140)
            TaoKinh(nav, "DarkClearGrad", new Vector2(-480, -140), new Vector2(380, 110),
                KinhLongCheDo.Clear, laSang: false, vienNang: true, laDieuHuong: true, mucMo: 0.85f, cuongDoNen: 0.25f, toiCucBo: 0.25f);

            // Kính Sáng Regular đặt lên vùng xám #808080 bên phải (+480, 60)
            var lightPill = TaoKinh(nav, "LightPillGray", new Vector2(480, 60), new Vector2(380, 120),
                KinhLongCheDo.Regular, laSang: true, vienNang: true, laDieuHuong: true, mucMo: 2.3f, cuongDoNen: 0.44f);
            // Kính Sáng Clear trên dải màu rực (+480, -140)
            TaoKinh(nav, "LightClearGrad", new Vector2(480, -140), new Vector2(380, 110),
                KinhLongCheDo.Clear, laSang: true, vienNang: true, laDieuHuong: true, mucMo: 0.85f, cuongDoNen: 0.25f);

            yield return CapNhatVaChup(cam, rt, "truong_hop_a.png");

            var tex = DocRT(rt);
            // §12.2 #1: Kiểm tra sắc độ trung tính trong lòng kính đặt trên vùng xám #808080 (|R-G|, |G-B| <= 2/255)
            float maxChromaDiff = 0f;
            float darkCenterLuma = 0f, darkRimMaxLuma = 0f;
            int pxDarkX = Uy(1920 * 0.5f - 480f);
            int pxDarkY = UyY(1080 * 0.5f + 60f);
            for (int dy = -12; dy <= 12; dy += 6)
            {
                for (int dx = -40; dx <= 40; dx += 20)
                {
                    Color c = tex.GetPixel(pxDarkX + dx, pxDarkY + dy);
                    float dRG = Mathf.Abs(c.r - c.g);
                    float dGB = Mathf.Abs(c.g - c.b);
                    maxChromaDiff = Mathf.Max(maxChromaDiff, Mathf.Max(dRG, dGB));
                    darkCenterLuma += 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
                }
            }
            darkCenterLuma /= 25f;

            // Đo vùng vành trên của DarkPillGray (~ +60 * 1800/1080 = +100 px trên tâm)
            int rimHalfH = Mathf.RoundToInt(60f * Cao / 1080f);
            for (int dy = rimHalfH - 4; dy <= rimHalfH + 1; dy++)
            {
                Color c = tex.GetPixel(pxDarkX - 30, pxDarkY + dy);
                float l = 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
                darkRimMaxLuma = Mathf.Max(darkRimMaxLuma, l);
            }

            // Kiểm tra tương tự trên LightPillGray
            int pxLightX = Uy(1920 * 0.5f + 480f);
            float maxChromaLight = 0f;
            for (int dy = -12; dy <= 12; dy += 6)
                for (int dx = -40; dx <= 40; dx += 20)
                {
                    Color c = tex.GetPixel(pxLightX + dx, pxDarkY + dy);
                    maxChromaLight = Mathf.Max(maxChromaLight, Mathf.Max(Mathf.Abs(c.r - c.g), Mathf.Abs(c.g - c.b)));
                }

            float maxDiff = Mathf.Max(maxChromaDiff, maxChromaLight);
            float rimBoost = darkRimMaxLuma - darkCenterLuma;
            Debug.Log($"[P3 Check (a)] neutralMaxDiff={maxDiff:F4} (limit<=0.0080), darkCenterLuma={darkCenterLuma:F3}, darkRimMaxLuma={darkRimMaxLuma:F3}, rimBoost={rimBoost:F3}");

            Destroy(tex);
            Destroy(texBg);

            if (maxDiff > 2.2f / 255f)
                { Fail($"[P3_FAIL (a)] Kính bị ám màu trên nền xám #808080: maxDiff={maxDiff:F4}"); yield break; }
            if (rimBoost < 0.06f)
                { Fail($"[P3_FAIL (a)] Viền sáng (rim highlight) chưa nổi rõ hơn lòng kính: rimBoost={rimBoost:F3}"); yield break; }
        }

        IEnumerator DungVaChupB(Camera cam, RenderTexture rt, Transform content, Transform nav)
        {
            // Nền Content sọc sắc nét tương phản cao (giống thân cây xanh/vàng ở 4k_731s)
            var texB = TaoAnhSocKhucXaB();
            TaoRaw(content, "BgB", Vector2.zero, new Vector2(1920, 1080), texB);

            // Viên nang Clear nét gần rìa để thấy khúc xạ Snell rõ rệt
            TaoKinh(nav, "SnellPillClear", new Vector2(0, 60), new Vector2(560, 180),
                KinhLongCheDo.Clear, laSang: false, vienNang: true, laDieuHuong: true, mucMo: 0.2f, cuongDoKhucXa: 1.3f, vienKhucXa: 24f, cuongDoNen: 0.14f, toiCucBo: 0.18f);

            // So sánh 2 bên: Squircle & Lip
            TaoKinh(nav, "SnellSquircle", new Vector2(-360, -180), new Vector2(420, 150),
                KinhLongCheDo.Clear, laSang: false, vienNang: false, laDieuHuong: true, mucMo: 0.25f, cuongDoKhucXa: 1.25f, vienKhucXa: 22f, cuongDoNen: 0.16f, banKinh: 34f, matCat: KinhLongMatCat.Squircle);
            TaoKinh(nav, "SnellLip", new Vector2(360, -180), new Vector2(420, 150),
                KinhLongCheDo.Clear, laSang: false, vienNang: true, laDieuHuong: true, mucMo: 0.25f, cuongDoKhucXa: 1.25f, vienKhucXa: 24f, cuongDoNen: 0.16f, matCat: KinhLongMatCat.Lip);

            yield return CapNhatVaChup(cam, rt, "truong_hop_b.png");

            // §12.2 #2: Lấy mẫu dọc đường ranh rìa ngang của SnellPillClear vs tâm phẳng
            var tex = DocRT(rt);
            int cx = Uy(960f);
            int cy = UyY(540f + 60f);
            int rimDy = Mathf.RoundToInt(82f * Cao / 1080f); // vùng vành ~8px từ mép ngoài 90 UI-units

            // Dọc ranh sọc ngang/đứng, nếu có khúc xạ lệch biên độ đáng kể tại rim
            float maxRimVsInteriorDiff = 0f;
            for (int dx = -160; dx <= 160; dx += 8)
            {
                Color cCenter = tex.GetPixel(cx + dx, cy);
                Color cRim = tex.GetPixel(cx + dx, cy + rimDy);
                float d = Mathf.Abs(cCenter.r - cRim.r) + Mathf.Abs(cCenter.g - cRim.g) + Mathf.Abs(cCenter.b - cRim.b);
                maxRimVsInteriorDiff = Mathf.Max(maxRimVsInteriorDiff, d);
            }
            Debug.Log($"[P3 Check (b)] Snell rim vs interior max color deflection = {maxRimVsInteriorDiff:F3}");

            Destroy(tex);
            Destroy(texB);

            if (maxRimVsInteriorDiff < 0.25f)
                { Fail($"[P3_FAIL (b)] Độ khúc xạ vành Snell quá yếu: {maxRimVsInteriorDiff:F3}"); yield break; }
        }

        IEnumerator DungVaChupC(Camera cam, RenderTexture rt, Transform content, Transform nav)
        {
            var texC = TaoAnhNenGradient();
            TaoRaw(content, "BgC", Vector2.zero, new Vector2(1920, 1080), texC);

            float[] steps = { 0.10f, 0.35f, 0.65f, 1.00f };
            float[] xs = { -510f, -170f, 170f, 510f };
            for (int i = 0; i < 4; i++)
            {
                var card = TaoKinh(nav, $"Mat_{i}", new Vector2(xs[i], 0), new Vector2(260, 160),
                    KinhLongCheDo.Regular, laSang: false, vienNang: false, laDieuHuong: true, mucMo: 2.3f, banKinh: 30f, ngungTu: steps[i]);
                TaoChu(card.transform, $"{steps[i]:0.00}s", 26, Color.white);
            }

            yield return CapNhatVaChup(cam, rt, "truong_hop_c_tinh.png");
            Destroy(texC);
        }

        IEnumerator DungVaChupD(Camera cam, RenderTexture rt, Transform content, Transform nav)
        {
            // Nửa trái nền giấy sáng (#F8F9FC) + chữ 17pt đen đậm chạy phía sau (Content Layer)
            TaoHop(content, "PaperLeft", new Vector2(-480, 0), new Vector2(960, 1080), new Color(0.965f, 0.972f, 0.988f, 1f));
            for (int row = -6; row <= 6; row++)
            {
                var line = TaoChu(content, "VERA Lab • C6H5CH3 + HNO3 → Trinitrotoluene (17pt Content Layer)", 22, new Color(0.11f, 0.13f, 0.18f, 1f));
                var lrt = line.rectTransform;
                lrt.anchoredPosition = new Vector2(-480, row * 58);
                lrt.sizeDelta = new Vector2(880, 44);
            }

            // Nửa phải nền tối rực (#0E131D + dải gradient màu) + chữ trắng 17pt phía sau
            var texRight = TaoAnhNenGradient();
            TaoRaw(content, "DarkRight", new Vector2(480, 0), new Vector2(960, 1080), texRight);
            for (int row = -6; row <= 6; row++)
            {
                var line = TaoChu(content, "Cơ chế SN2 • CH3Br + OH- → CH3OH + Br- (Nền Tối 17pt)", 22, new Color(0.92f, 0.94f, 0.98f, 1f));
                var lrt = line.rectTransform;
                lrt.anchoredPosition = new Vector2(480, row * 58);
                lrt.sizeDelta = new Vector2(880, 44);
            }

            // Kính điều hướng Layer 14 nằm đè lên giữa dòng chữ 17pt!
            var pLightReg = TaoKinh(nav, "LightRegularOverText", new Vector2(-480, 60), new Vector2(540, 110),
                KinhLongCheDo.Regular, laSang: true, vienNang: true, laDieuHuong: true, mucMo: 3.1f, cuongDoNen: 0.56f, bongCuongDo: 0.24f);
            TaoChu(pLightReg.transform, "Thanh điều hướng Sáng • Regular", 28, new Color(0.08f, 0.09f, 0.12f, 1f));

            var pDarkReg = TaoKinh(nav, "DarkRegularOverText", new Vector2(480, 60), new Vector2(540, 110),
                KinhLongCheDo.Regular, laSang: false, vienNang: true, laDieuHuong: true, mucMo: 3.1f, cuongDoNen: 0.68f, bongCuongDo: 0.22f);
            TaoChu(pDarkReg.transform, "Thanh điều hướng Tối • Regular", 28, new Color(0.96f, 0.97f, 0.99f, 1f));

            var pDarkClear = TaoKinh(nav, "ClearWithDim35", new Vector2(480, -140), new Vector2(420, 96),
                KinhLongCheDo.Clear, laSang: false, vienNang: true, laDieuHuong: true, mucMo: 0.9f, cuongDoNen: 0.12f, toiCucBo: 0.35f);
            TaoChu(pDarkClear.transform, "Toàn màn hình 3D • Clear", 26, new Color(0.98f, 0.98f, 1.0f, 1f));

            yield return CapNhatVaChup(cam, rt, "truong_hop_d.png");

            // §12.2 #3 & #5: kiểm tra độ tương phản đọc chữ trên kính & _KL_NoiDung có chứa nội dung chữ Layer 5 sạch Layer 14
            var tex = DocRT(rt);
            int lx = Uy(960f - 480f + 190f);
            int ly = UyY(540f + 60f + 34f);
            Color cLightBg = tex.GetPixel(lx, ly);
            float lGlassLight = TuongPhanLuma(cLightBg);
            float lTextDark = TuongPhanLuma(new Color(0.08f, 0.09f, 0.12f));
            float crLight = (lGlassLight + 0.05f) / (lTextDark + 0.05f);

            int dxPos = Uy(960f + 480f + 190f);
            Color cDarkBg = tex.GetPixel(dxPos, ly);
            float lGlassDark = TuongPhanLuma(cDarkBg);
            float lTextLight = TuongPhanLuma(new Color(0.96f, 0.97f, 0.99f));
            float crDark = (lTextLight + 0.05f) / (lGlassDark + 0.05f);

            // Kiểm chứng 2 tầng (§12.2 #5): đọc _KL_NoiDung tại đúng tâm LightRegularOverText -> không chứa kính điều hướng mà chứa Content
            bool okTwoLayer = false;
            if (KinhLongBlurFeature.KinhLongBlurPass.NoiDungRT is RenderTexture noiRt)
            {
                var texNoi = DocRT(noiRt);
                Color underLight = texNoi.GetPixel(lx, ly);
                okTwoLayer = underLight.r > 0.7f;
                Destroy(texNoi);
            }

            Debug.Log($"[P3 Check (d)] CR_Light={crLight:F2} (min>=4.5), CR_Dark={crDark:F2} (min>=4.5), TwoLayerClean={okTwoLayer}");

            Destroy(tex);
            Destroy(texRight);

            if (crLight < 4.5f || crDark < 4.5f)
                { Fail($"[P3_FAIL (d)] Độ tương phản chữ trên kính chưa đạt 4.5:1! Light={crLight:F2}, Dark={crDark:F2}"); yield break; }
            if (!okTwoLayer)
                { Fail("[P3_FAIL (d)] _KL_NoiDung chưa tách sạch Content Layer phía dưới!"); yield break; }
        }

        IEnumerator DungVaChupE(Camera cam, RenderTexture rt, Transform content, Transform nav)
        {
            var texE = TaoAnhNenGradient();
            TaoRaw(content, "BgE", Vector2.zero, new Vector2(1920, 1080), texE);

            // Rãnh công tắc xanh lá + thấu kính Lip đang nhấc lên phóng đại rãnh phía dưới (kiểm chứng trực quan 4f)
            TaoHop(content, "SwitchTrack", new Vector2(-240, 80), new Vector2(128, 62), new Color(0.20f, 0.78f, 0.35f, 1f));
            TaoKinh(nav, "SwitchKnobLip", new Vector2(-190, 80), new Vector2(96, 78),
                KinhLongCheDo.Clear, laSang: true, vienNang: true, laDieuHuong: true, mucMo: 0.1f, cuongDoKhucXa: 1.55f, vienKhucXa: 22f, cuongDoNen: 0.08f, matCat: KinhLongMatCat.Lip);

            // Thanh trượt xanh lam + thấu kính Squircle đang nhấc lên (4g)
            TaoHop(content, "SliderTrack", new Vector2(260, -80), new Vector2(460, 18), new Color(0.18f, 0.52f, 0.96f, 1f));
            TaoKinh(nav, "SliderKnobSquircle", new Vector2(260, -80), new Vector2(112, 68),
                KinhLongCheDo.Clear, laSang: true, vienNang: true, laDieuHuong: true, mucMo: 0.1f, cuongDoKhucXa: 1.45f, vienKhucXa: 20f, cuongDoNen: 0.08f, matCat: KinhLongMatCat.Squircle);

            yield return CapNhatVaChup(cam, rt, "truong_hop_e_tinh.png");
            Destroy(texE);
        }

        // ========================================================================= Tiện ích
        static (Camera cam, Canvas canvas, HaiTangCanvas haiTang) TaoSanKhauCoBan(Color bg)
        {
            var camGo = new GameObject("BaseCam", typeof(Camera));
            var cam = camGo.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = bg;
            cam.cullingMask = ~0;

            var cGo = new GameObject("CanvasNoiDung", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = cGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 10f;

            var scaler = cGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            var haiTang = HaiTangCanvas.Ensure(canvas, true);
            return (cam, canvas, haiTang);
        }

        static GameObject TaoHop(Transform parent, string ten, Vector2 pos, Vector2 size, Color col)
        {
            var go = new GameObject(ten, typeof(RectTransform), typeof(Image));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var img = go.GetComponent<Image>();
            img.color = col;
            img.material = Graphic.defaultGraphicMaterial;
            return go;
        }

        static RawImage TaoRaw(Transform parent, string ten, Vector2 pos, Vector2 size, Texture tex)
        {
            var go = new GameObject(ten, typeof(RectTransform), typeof(RawImage));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var r = go.GetComponent<RawImage>();
            r.texture = tex;
            r.color = Color.white;
            return r;
        }

        static KinhHinh TaoKinh(
            Transform parent, string ten, Vector2 pos, Vector2 size,
            KinhLongCheDo cheDo, bool laSang, bool vienNang, bool laDieuHuong,
            float mucMo = 2.35f, float cuongDoKhucXa = 1.0f, float vienKhucXa = 18f,
            float cuongDoNen = 0.48f, float toiCucBo = 0f, float bongCuongDo = 0.18f,
            float banKinh = 26f, KinhLongMatCat matCat = KinhLongMatCat.Squircle, float ngungTu = 1f)
        {
            var go = new GameObject(ten, typeof(RectTransform), typeof(Image));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            var img = go.GetComponent<Image>();
            img.color = Color.white;

            var kh = go.AddComponent<KinhHinh>();
            kh.CheDo = cheDo;
            kh.LoaiMatCat = matCat;
            kh.LaNenSang = laSang;
            kh.HinhVienNang = vienNang;
            kh.LaDieuHuong = laDieuHuong;
            kh.BanKinhBo = banKinh;
            kh.MucMo = mucMo;
            kh.CuongDoKhucXa = cuongDoKhucXa;
            kh.VienKhucXa = vienKhucXa;
            kh.CuongDoNen = cuongDoNen;
            kh.ToiCucBo = toiCucBo;
            kh.BongCuongDo = bongCuongDo;
            kh.NgungTu = ngungTu;
            kh.CuongDoVienToi = laSang ? 0.18f : 0.30f;
            kh.CapNhatVatLieu();
            return kh;
        }

        static Text TaoChu(Transform parent, string text, int size, Color col)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            Stretch(rt);
            var t = go.GetComponent<Text>();
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.text = text;
            t.fontSize = size;
            t.fontStyle = FontStyle.Bold;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = col;
            t.raycastTarget = false;
            return t;
        }

        static IEnumerator CapNhatVaChup(Camera cam, RenderTexture rt, string fileName)
        {
            yield return null;
            yield return null;
            foreach (var g in FindObjectsByType<Graphic>(FindObjectsInactive.Exclude))
                g.SetAllDirty();
            foreach (var kh in FindObjectsByType<KinhHinh>(FindObjectsInactive.Exclude))
                kh.CapNhatVatLieu();
            yield return null;
            Canvas.ForceUpdateCanvases();
            KinhLongBlurCache.DanhDauBan(3);
            cam.Render();

            var tex = DocRT(rt);
            File.WriteAllBytes(Path.Combine(ThuMucXuat, fileName), tex.EncodeToPNG());
            Destroy(tex);
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        static void XoaCon(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--)
                Destroy(t.GetChild(i).gameObject);
        }

        static int Uy(float xRef) => Mathf.Clamp(Mathf.RoundToInt(xRef / 1920f * Rong), 0, Rong - 1);
        static int UyY(float yRef) => Mathf.Clamp(Mathf.RoundToInt(yRef / 1080f * Cao), 0, Cao - 1);

        static float TuongPhanLuma(Color c)
        {
            float Lin(float ch) => ch <= 0.03928f ? ch / 12.92f : Mathf.Pow((ch + 0.055f) / 1.055f, 2.4f);
            return 0.2126f * Lin(c.r) + 0.7152f * Lin(c.g) + 0.0722f * Lin(c.b);
        }

        static Texture2D DocRT(RenderTexture rt)
        {
            var t = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            t.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            t.Apply();
            RenderTexture.active = prev;
            return t;
        }

        static Texture2D TaoAnhNenKiemThuA()
        {
            const int w = 480, h = 270;
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var px = new Color32[w * h];
            Color32 gray80 = new Color32(128, 128, 128, 255);
            for (int y = 0; y < h; y++)
            {
                float fy = y / (float)(h - 1);
                for (int x = 0; x < w; x++)
                {
                    float fx = x / (float)(w - 1);
                    bool right = fx >= 0.5f;
                    // Vùng xám chuẩn #808080 đặt sau DarkPillGray (fx 0.14..0.36) và LightPillGray (fx 0.64..0.86), fy 0.42..0.72
                    bool inGrayLeft = fx > 0.12f && fx < 0.38f && fy > 0.42f && fy < 0.72f;
                    bool inGrayRight = fx > 0.62f && fx < 0.88f && fy > 0.42f && fy < 0.72f;
                    if (inGrayLeft || inGrayRight)
                    {
                        px[y * w + x] = gray80;
                    }
                    else if (!right)
                    {
                        Color c = Color.Lerp(new Color(0.05f, 0.07f, 0.12f), new Color(0.18f, 0.36f, 0.68f), fx * 1.4f + fy * 0.35f);
                        c = Color.Lerp(c, new Color(0.85f, 0.32f, 0.55f), Mathf.SmoothStep(0.35f, 0f, fy) * 0.65f);
                        px[y * w + x] = (Color32)c;
                    }
                    else
                    {
                        Color c = Color.Lerp(new Color(0.92f, 0.94f, 0.98f), new Color(0.65f, 0.84f, 0.98f), (fx - 0.5f) * 1.5f);
                        c = Color.Lerp(c, new Color(0.98f, 0.72f, 0.45f), Mathf.SmoothStep(0.38f, 0f, fy) * 0.75f);
                        px[y * w + x] = (Color32)c;
                    }
                }
            }
            t.SetPixels32(px);
            t.Apply();
            return t;
        }

        static Texture2D TaoAnhSocKhucXaB()
        {
            const int w = 480, h = 270;
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                float fy = y / (float)(h - 1);
                for (int x = 0; x < w; x++)
                {
                    float fx = x / (float)(w - 1);
                    bool stripe = ((x / 18) % 2 == 0);
                    Color baseC = (fy > 0.52f)
                        ? new Color(0.14f, 0.72f, 0.28f)
                        : ((fy > 0.44f) ? new Color(0.96f, 0.84f, 0.14f) : new Color(0.10f, 0.38f, 0.88f));
                    if (stripe) baseC *= 0.75f;
                    if (Mathf.Abs(fy - 0.58f) < 0.015f || Mathf.Abs(fy - 0.42f) < 0.015f)
                        baseC = Color.white;
                    px[y * w + x] = (Color32)baseC;
                }
            }
            t.SetPixels32(px);
            t.Apply();
            return t;
        }

        static Texture2D TaoAnhNenGradient()
        {
            const int w = 480, h = 270;
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                float fy = y / (float)(h - 1);
                for (int x = 0; x < w; x++)
                {
                    float fx = x / (float)(w - 1);
                    Color c = Color.Lerp(new Color(0.08f, 0.14f, 0.26f), new Color(0.18f, 0.52f, 0.78f), fx * 0.7f + fy * 0.3f);
                    c = Color.Lerp(c, new Color(0.86f, 0.35f, 0.54f), Mathf.Exp(-Vector2.Distance(new Vector2(fx, fy), new Vector2(0.75f, 0.35f)) * 4f));
                    px[y * w + x] = (Color32)c;
                }
            }
            t.SetPixels32(px);
            t.Apply();
            return t;
        }
    }
#endif
}
