using System.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Khkt.Rendering;

namespace Khkt.Demo
{
#if UNITY_EDITOR
    /// <summary>
    /// Runner tự động chạy trong PlayMode để kiểm thử từng pha Kính Lỏng Apple (bắt đầu từ P1).
    /// Khớp mẫu Khkt.*Runner.*\.cs của MacBuild nên không đi kèm vào bản phát hành.
    /// </summary>
    public class KhktPhongThuRunner : MonoBehaviour
    {
        public string CheDo = "P1";

        void Start()
        {
            if (CheDo == "P1")
                StartCoroutine(ChayP1());
        }

        IEnumerator ChayP1()
        {
            var camGo = new GameObject("BaseCam", typeof(Camera));
            var baseCam = camGo.GetComponent<Camera>();
            baseCam.clearFlags = CameraClearFlags.SolidColor;
            baseCam.backgroundColor = new Color(0.12f, 0.24f, 0.45f, 1f);
            baseCam.cullingMask = ~0;

            var canvasGo = new GameObject("CanvasNoiDung", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = baseCam;
            canvas.planeDistance = 10f;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            // Hình cam trên Content Layer (Layer 5 UI)
            var contentBox = new GameObject("ContentBox", typeof(RectTransform), typeof(Image));
            contentBox.layer = 5;
            contentBox.transform.SetParent(canvasGo.transform, false);
            var cbRt = (RectTransform)contentBox.transform;
            cbRt.anchorMin = cbRt.anchorMax = new Vector2(0.5f, 0.5f);
            cbRt.pivot = new Vector2(0.5f, 0.5f);
            cbRt.anchoredPosition = Vector2.zero;
            cbRt.sizeDelta = new Vector2(600, 300);
            var cbImg = contentBox.GetComponent<Image>();
            cbImg.color = new Color(0.95f, 0.45f, 0.1f, 1f);
            cbImg.material = Graphic.defaultGraphicMaterial;

            // Hình xanh lá trên Navigation Layer (Layer 14 KinhDieuHuong) chồng lên góc trên phải của ContentBox
            var navBox = new GameObject("NavBox", typeof(RectTransform), typeof(Image));
            navBox.layer = 5;
            navBox.transform.SetParent(canvasGo.transform, false);
            var nbRt = (RectTransform)navBox.transform;
            nbRt.anchorMin = nbRt.anchorMax = new Vector2(0.5f, 0.5f);
            nbRt.pivot = new Vector2(0.5f, 0.5f);
            nbRt.anchoredPosition = new Vector2(160, 60);
            nbRt.sizeDelta = new Vector2(260, 120);
            var nbImg = navBox.GetComponent<Image>();
            nbImg.color = new Color(0.1f, 0.85f, 0.45f, 1f);
            nbImg.material = Graphic.defaultGraphicMaterial;

            PlayerPrefs.SetInt("KHKT_KINH_LONG", 1);
            var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
            rt.Create();
            baseCam.targetTexture = rt;

            var haiTang = HaiTangCanvas.Ensure(canvas, true);
            KinhGuongDieuHuong.Gan(navBox, haiTang, true);

            if (navBox.layer != 14)
            {
                Debug.LogError($"[P1_FAIL] NavBox không nằm ở Layer 14: {navBox.layer}");
                EditorApplication.Exit(1);
                yield break;
            }

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

            // Đọc lại _KL_NoiDung tại đúng điểm (navX, navY) -> phải CHỈ có Content cam, KHÔNG có xanh lá của Navigation Layer!
            Color bgAtNavInNoiDung = Color.black;
            if (klNoiDung is RenderTexture noiRt)
            {
                var texNoi = DocRT(noiRt);
                bgAtNavInNoiDung = texNoi.GetPixel(navX, navY);
                Destroy(texNoi);
            }

            Debug.Log($"[P1 Check] pixelRect=({canvas.pixelRect}) navMode={haiTang.CanvasDieuHuong.renderMode} Center={centerC} Nav={navC} NoiDungAtNav={bgAtNavInNoiDung} NoiDung={(klNoiDung != null ? klNoiDung.width + "x" + klNoiDung.height : "null")} Mo1={(klMo1 != null ? klMo1.width + "x" + klMo1.height : "null")} Mo4={(klMo4 != null ? klMo4.width + "x" + klMo4.height : "null")}");

            // Kiểm tra cơ chế dirty-flag bỏ qua blur khi đứng yên
            KinhLongBlurCache.EpChupMoiKhung = false;
            KinhLongBlurCache.DanhDauBan(0);
            int dummyW = 1280, dummyH = 720;
            for (int i = 0; i < 6; i++)
                KinhLongBlurCache.CanRenderBlur(1280, 720, ref dummyW, ref dummyH);
            bool idleSkip = !KinhLongBlurCache.CanRenderBlur(1280, 720, ref dummyW, ref dummyH);
            Debug.Log($"[P1 Check] IdleSkip={idleSkip} (skipped={KinhLongBlurCache.SoKhungBoQuaLienTiep})");

            baseCam.targetTexture = null;
            rt.Release();
            Destroy(rt);
            Destroy(texOut);

            bool okContent = centerC.r > 0.90f && centerC.g < 0.55f && centerC.b < 0.25f;
            bool okOverlay = navC.g > 0.75f && navC.r < 0.30f;
            bool okCleanContentCapture = bgAtNavInNoiDung.r > 0.85f && bgAtNavInNoiDung.g < 0.55f;
            bool okTex = klNoiDung != null && klNoiDung.width == 1280 && klNoiDung.height == 720
                      && klMo1 != null && klMo1.width == 640 && klMo1.height == 360
                      && klMo4 != null && klMo4.width == 80 && klMo4.height == 45;

            if (!okContent || !okOverlay || !okCleanContentCapture || !okTex || !idleSkip)
            {
                Debug.LogError($"[P1_FAIL] okContent={okContent} okOverlay={okOverlay} okCleanContent={okCleanContentCapture} okTex={okTex} idleSkip={idleSkip}");
                EditorApplication.Exit(1);
                yield break;
            }

            Debug.Log("[P1_PASS] Thành công toàn bộ kiểm tra P1 (2 tầng trên 1 RenderGraph, Layer 14 tách sạch khỏi _KL_NoiDung, Dual Kawase, Idle Cache 0ms)!");
            EditorApplication.Exit(0);
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
    }
#endif
}
