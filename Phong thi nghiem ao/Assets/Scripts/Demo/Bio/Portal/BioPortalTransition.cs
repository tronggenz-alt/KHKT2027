using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Khkt.Demo;
using Khkt.Environment;
using Khkt.Environment.Tang2;
using Khkt.Rendering;

namespace Khkt.Demo.Bio.Portal
{
    /// <summary>
    /// Bộ máy chuyển cảnh hoàn chỉnh (BioPortalTransition - GĐ3):
    ///   • FSM trạng thái: Nghi -> DangMo -> DangTai -> HienPhong -> TrongPhong -> DangDong -> Nghi.
    ///     Chống bấm nút 2 lần liên tiếp.
    ///   • Trình tự MỞ (8 bước, ~1.8s):
    ///       1. Khóa di chuyển/HUD (UiCapture++), lưu chính xác vị trí, góc nhìn, FOV.
    ///       2. Quét sáng mô hình (MaterialPropertyBlock), vòng sàn nở 1.5x, camera lab tiến 0.4m, FOV 60° -> 50°.
    ///       3. Lưới lục giác HexWipe phủ từ mô hình ra 4 góc, tăng nhẹ weight Volume hậu kỳ.
    ///       4. Chờ sẵn sàng: chuyển sang holoCamera, tắt vẽ lab, vòng tròn tiến trình nếu chờ > 0.3s.
    ///       5. Lưới lục giác tan từ tâm ra ngoài (0.50s).
    ///       6. Phòng thức dậy: sàn lan sáng -> 4 vòng sáng bật tuần tự cách nhau 0.08s -> chùm sáng dâng lên.
    ///       7. Cơ thể hiện hình từ chân lên đầu bằng HoloReveal (0.70s).
    ///       8. Giao diện uGUI trượt vào (Popup lệch 0.04s), mở khóa xoay.
    ///       • Âm thanh tổng hợp vút & ngân (AudioClip.Create, tôn trọng NutHieuUng.SoundEnabled).
    ///   • Trình tự ĐÓNG (6 bước, ~1.4s):
    ///       1. Khóa điều khiển, giao diện mờ dần.
    ///       2. Cơ thể tan đầu xuống chân (đảo chiều HoloReveal), chùm sáng hạ, 4 vòng sáng tắt ngược lại.
    ///       3. Lục giác phủ từ ngoài vào tâm.
    ///       4. Tắt phòng ảo, bật lab, phục hồi đúng vị trí / góc ngẩng / hướng nhìn, FOV 50°, chờ ZoneStreamer.
    ///       5. Lục giác tan từ vị trí mô hình ra ngoài, FOV 50° -> 60°, quét mô hình từ đầu xuống chân.
    ///       6. Mở khóa HUD / di chuyển (UiCapture--), thẻ lời mời cooldown 0.8s trước khi được hiện lại.
    ///   • Nhánh Giảm chuyển động (ReducedMotion.IsEnabled): Mờ chéo 0.25s, bảo toàn vị trí, FSM, ZoneStreamer.
    ///   • Ngân sách: 0 GC Alloc trong Update, Time.unscaledDeltaTime, an toàn khi OnApplicationPause.
    /// </summary>
    public class BioPortalTransition : MonoBehaviour
    {
        public enum TransitionState
        {
            Nghi,
            DangMo,
            DangTai,
            HienPhong,
            TrongPhong,
            DangDong
        }

        public static BioPortalTransition Instance { get; private set; }

        [Header("Trạng thái FSM")]
        [SerializeField] TransitionState currentState = TransitionState.Nghi;
        public TransitionState CurrentState => currentState;

        [Header("Tham chiếu liên kết")]
        public BioPortal portal;
        public HoloBioRoom holoRoom;

        // Âm thanh tổng hợp
        AudioSource audioSource;
        AudioClip clipWhoosh;
        AudioClip clipResonance;

        // UI HexWipe Overlay
        Canvas wipeCanvas;
        RawImage wipeImage;
        Material wipeMaterial;
        CanvasGroup waitOverlayGroup;
        Text waitText;
        Image waitSpinner;

        // Volume hậu kỳ
        Volume postVolume;
        float originalVolumeWeight = 1.0f;

        // Lưu thông tin người chơi
        DesktopViewer savedViewer;
        Camera savedViewCam;
        Vector3 savedPosition;
        Quaternion savedRotation;
        Quaternion savedCamLocalRotation;
        float savedFov = 60f;
        Vector3 savedPortalPos;

        // MaterialPropertyBlock cho HoloReveal
        MaterialPropertyBlock revealBlock;
        static readonly int doCaoCatPropId = Shader.PropertyToID("_DoCaoCat");
        static readonly int tienDoPropId = Shader.PropertyToID("_TienDo");
        static readonly int tamPropId = Shader.PropertyToID("_Tam");
        static readonly int chieuPropId = Shader.PropertyToID("_Chieu");
        static readonly int tiLeManHinhPropId = Shader.PropertyToID("_TiLeManHinh");

        Coroutine activeTransition;

        void Awake()
        {
            if (Instance == null) Instance = this;
            revealBlock = new MaterialPropertyBlock();
            EnsureAudio();
            EnsureWipeUI();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (wipeMaterial != null) Destroy(wipeMaterial);
        }

        void OnApplicationPause(bool pause)
        {
            if (pause && currentState != TransitionState.Nghi && currentState != TransitionState.TrongPhong)
            {
                // Khi ứng dụng tạm dừng giữa chuyển cảnh, ép hoàn tất an toàn
                if (activeTransition != null)
                {
                    StopCoroutine(activeTransition);
                    activeTransition = null;
                }
                ForceEmergencyReset();
            }
        }

        void EnsureAudio()
        {
            if (audioSource != null) return;
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 0f;
            audioSource.playOnAwake = false;
            audioSource.volume = 0.5f;

            // 1. Tiếng vút lên (WhooshSweep: 220Hz -> 1320Hz)
            clipWhoosh = SynthesizeClip("BioPortalWhoosh", 0.65f, (t, dur) =>
            {
                float p = t / dur;
                float freq = Mathf.Lerp(220f, 1320f, p * p);
                float env = Mathf.Sin(p * Mathf.PI);
                return Mathf.Sin(2f * Mathf.PI * freq * t) * env * 0.45f;
            });

            // 2. Tiếng ngân vũ trụ (ResonantChime: 432Hz + 864Hz hòa âm)
            clipResonance = SynthesizeClip("BioPortalResonance", 1.4f, (t, dur) =>
            {
                float env = Mathf.Exp(-t * 2.2f);
                float f1 = Mathf.Sin(2f * Mathf.PI * 432f * t);
                float f2 = Mathf.Sin(2f * Mathf.PI * 864f * t) * 0.4f;
                float tremolo = 1.0f + 0.15f * Mathf.Sin(2f * Mathf.PI * 8f * t);
                return (f1 + f2) * env * tremolo * 0.40f;
            });
        }

        static AudioClip SynthesizeClip(string name, float duration, Func<float, float, float> gen)
        {
            const int sampleRate = 22050;
            int total = Mathf.CeilToInt(sampleRate * duration);
            var samples = new float[total];
            for (int i = 0; i < total; i++)
            {
                float t = (float)i / sampleRate;
                samples[i] = gen(t, duration);
            }
            var clip = AudioClip.Create(name, total, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        void EnsureWipeUI()
        {
            if (wipeCanvas != null) return;

            var cGo = new GameObject("HexWipeCanvas", typeof(Canvas), typeof(CanvasScaler));
            cGo.transform.SetParent(transform, false);
            wipeCanvas = cGo.GetComponent<Canvas>();
            wipeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            wipeCanvas.sortingOrder = 9990;

            var scaler = cGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            // RawImage cho shader HexWipe
            var imgGo = new GameObject("HexWipeImage", typeof(RectTransform), typeof(RawImage));
            imgGo.transform.SetParent(wipeCanvas.transform, false);
            var rt = (RectTransform)imgGo.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;

            wipeImage = imgGo.GetComponent<RawImage>();
            wipeImage.raycastTarget = false;

            var shader = Shader.Find("Khkt/Tang2/HexWipe") ?? Shader.Find("UI/Default");
            wipeMaterial = new Material(shader) { name = "HexWipeMatInstance", hideFlags = HideFlags.DontSave };
            wipeImage.material = wipeMaterial;
            wipeImage.gameObject.SetActive(false);

            // Bảng chờ nếu tải > 0.3s (Vòng tiến trình + chữ)
            var waitGo = new GameObject("WaitOverlay", typeof(RectTransform), typeof(CanvasGroup));
            waitGo.transform.SetParent(wipeCanvas.transform, false);
            var waitRt = (RectTransform)waitGo.transform;
            waitRt.anchorMin = new Vector2(0.5f, 0.5f);
            waitRt.anchorMax = new Vector2(0.5f, 0.5f);
            waitRt.sizeDelta = new Vector2(300, 120);
            waitRt.anchoredPosition = Vector2.zero;

            waitOverlayGroup = waitGo.GetComponent<CanvasGroup>();
            waitOverlayGroup.alpha = 0f;

            var spGo = new GameObject("Spinner", typeof(RectTransform), typeof(Image));
            spGo.transform.SetParent(waitGo.transform, false);
            var spRt = (RectTransform)spGo.transform;
            spRt.anchorMin = new Vector2(0.5f, 0.7f);
            spRt.anchorMax = new Vector2(0.5f, 0.7f);
            spRt.sizeDelta = new Vector2(40, 40);
            waitSpinner = spGo.GetComponent<Image>();
            waitSpinner.sprite = GlassKit.Circle;
            waitSpinner.color = new Color(0.133f, 0.827f, 0.933f, 0.9f);

            var tGo = new GameObject("WaitText", typeof(RectTransform), typeof(Text));
            tGo.transform.SetParent(waitGo.transform, false);
            var tRt = (RectTransform)tGo.transform;
            tRt.anchorMin = new Vector2(0.5f, 0.2f);
            tRt.anchorMax = new Vector2(0.5f, 0.2f);
            tRt.sizeDelta = new Vector2(280, 30);
            waitText = tGo.GetComponent<Text>();
            waitText.font = GlassKit.Regular;
            waitText.fontSize = 15;
            waitText.alignment = TextAnchor.MiddleCenter;
            waitText.color = GlassKit.Ink;
            waitText.text = "Đang kết nối Phòng Sinh học 3D...";

            waitGo.SetActive(false);
        }

        // =====================================================================
        // TRÌNH TỰ MỞ (OPEN TRANSITION - 8 BƯỚC, ~1.8S)
        // =====================================================================

        public void StartOpenTransition(BioPortal p, HoloBioRoom r)
        {
            if (currentState != TransitionState.Nghi) return;
            portal = p;
            holoRoom = r;

            if (activeTransition != null) StopCoroutine(activeTransition);
            activeTransition = StartCoroutine(CoOpenSequence());
        }

        IEnumerator CoOpenSequence()
        {
            currentState = TransitionState.DangMo;

            // Nhánh Giảm chuyển động (Reduced Motion)
            if (ReducedMotion.IsEnabled)
            {
                yield return CoOpenReducedMotion();
                yield break;
            }

            // BƯỚC 1: Khóa di chuyển/HUD, lưu chính xác vị trí, góc nhìn, FOV
            savedViewer = FindAnyObjectByType<DesktopViewer>();
            savedViewCam = savedViewer != null ? savedViewer.viewCamera : Camera.main;

            if (savedViewer != null)
            {
                savedPosition = savedViewer.transform.position;
                savedRotation = savedViewer.transform.rotation;
                savedCamLocalRotation = savedViewer.cameraTransform != null
                    ? savedViewer.cameraTransform.localRotation
                    : Quaternion.identity;
            }
            else if (savedViewCam != null)
            {
                savedPosition = savedViewCam.transform.position;
                savedRotation = savedViewCam.transform.rotation;
                savedCamLocalRotation = savedViewCam.transform.localRotation;
            }

            if (savedViewCam != null) savedFov = savedViewCam.fieldOfView;
            savedPortalPos = portal != null ? portal.transform.position : transform.position;

            ZoneStreamer.Instance?.Keep(this, 9999f, savedPortalPos);
            DesktopViewer.UiCapture++;

            // BƯỚC 2: Quét sáng mô hình, vòng sàn nở 1.5x, camera lab tiến 0.4m, FOV 60° -> 50°
            PlaySound(clipWhoosh);

            Vector3 startCamPos = savedPosition;
            Vector3 forward = savedViewer != null ? savedViewer.transform.forward : (savedViewCam != null ? savedViewCam.transform.forward : Vector3.forward);
            Vector3 targetCamPos = startCamPos + forward * 0.40f;

            Transform portalRing = portal != null ? portal.transform.Find("FloorGlowingRing") : null;
            Vector3 ringStartScale = portalRing != null ? portalRing.localScale : Vector3.one;

            float step2Duration = 0.38f;
            float elapsed = 0f;
            while (elapsed < step2Duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / step2Duration);
                float ease = 1f - Mathf.Pow(1f - t, 3f);

                if (savedViewer != null)
                {
                    var cc = savedViewer.GetComponent<CharacterController>();
                    if (cc != null) cc.enabled = false;
                    savedViewer.transform.position = Vector3.Lerp(startCamPos, targetCamPos, ease);
                    if (cc != null) cc.enabled = true;
                }
                if (savedViewCam != null)
                {
                    savedViewCam.fieldOfView = Mathf.Lerp(savedFov, 50f, ease);
                }
                if (portalRing != null)
                {
                    portalRing.localScale = Vector3.Lerp(ringStartScale, Vector3.one * 1.5f, ease);
                }
                yield return null;
            }

            // BƯỚC 3: Lưới lục giác phủ từ vị trí mô hình ra 4 góc, tăng nhẹ weight Volume
            currentState = TransitionState.DangTai;
            postVolume = FindAnyObjectByType<Volume>();
            if (postVolume != null) originalVolumeWeight = postVolume.weight;

            Vector2 screenCenterNorm = new Vector2(0.5f, 0.5f);
            if (savedViewCam != null && portal != null)
            {
                Vector3 screenP = savedViewCam.WorldToViewportPoint(portal.transform.position);
                screenCenterNorm = new Vector2(Mathf.Clamp01(screenP.x), Mathf.Clamp01(screenP.y));
            }

            SetWipeParams(0f, screenCenterNorm, 1.0f);
            wipeImage.gameObject.SetActive(true);

            float step3Duration = 0.45f;
            elapsed = 0f;
            while (elapsed < step3Duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / step3Duration);
                SetWipeProgress(t);
                if (postVolume != null) postVolume.weight = Mathf.Lerp(originalVolumeWeight, 1.25f, t);
                yield return null;
            }
            SetWipeProgress(1.0f);

            // BƯỚC 4: Chờ sẵn sàng: chuyển sang holoCamera, tắt vẽ lab
            if (savedViewCam != null) savedViewCam.enabled = false;
            if (holoRoom != null)
            {
                holoRoom.gameObject.SetActive(true);
                if (holoRoom.holoCamera != null) holoRoom.holoCamera.enabled = true;
                holoRoom.ResetOrbitView();
            }

            // Nếu khu tải lâu (> 0.3s) thì hiện vòng tròn tiến trình
            float waitTimer = 0f;
            while (waitTimer < 0.35f)
            {
                waitTimer += Time.unscaledDeltaTime;
                if (waitTimer > 0.30f && waitOverlayGroup != null)
                {
                    waitOverlayGroup.gameObject.SetActive(true);
                    waitOverlayGroup.alpha = 1f;
                    if (waitSpinner != null) waitSpinner.transform.Rotate(0, 0, -360f * Time.unscaledDeltaTime * 2f);
                }
                yield return null;
            }
            if (waitOverlayGroup != null) waitOverlayGroup.gameObject.SetActive(false);

            // BƯỚC 5: Lưới lục giác tan từ tâm ra ngoài (0.50s)
            currentState = TransitionState.HienPhong;
            PlaySound(clipResonance);

            SetWipeParams(1.0f, new Vector2(0.5f, 0.5f), 1.0f);
            float step5Duration = 0.50f;
            elapsed = 0f;
            while (elapsed < step5Duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / step5Duration);
                // Tan dần: tiến độ giảm từ 1 về 0
                SetWipeProgress(1.0f - t);
                yield return null;
            }
            wipeImage.gameObject.SetActive(false);
            if (postVolume != null) postVolume.weight = originalVolumeWeight;

            // BƯỚC 6: Phòng thức dậy: 4 vòng sáng bật cách nhau 0.08s -> chùm sáng dâng lên
            yield return CoWakeRoomRings();

            // BƯỚC 7: Cơ thể hiện hình từ chân lên đầu bằng HoloReveal (0.70s)
            yield return CoHoloRevealModel(true, 0.70f);

            // BƯỚC 8: Giao diện uGUI trượt vào, mở khóa xoay
            if (holoRoom != null && holoRoom.exitButtonRect != null)
            {
                var cg = holoRoom.exitButtonRect.GetComponent<CanvasGroup>();
                if (cg != null) cg.alpha = 0f;
                UiMotion.Popup(this, holoRoom.exitButtonRect, cg, 0.20f);
            }

            currentState = TransitionState.TrongPhong;
            activeTransition = null;
        }

        // =====================================================================
        // TRÌNH TỰ ĐÓNG (CLOSE TRANSITION - 6 BƯỚC, ~1.4S)
        // =====================================================================

        public void StartCloseTransition()
        {
            if (currentState != TransitionState.TrongPhong) return;

            if (activeTransition != null) StopCoroutine(activeTransition);
            activeTransition = StartCoroutine(CoCloseSequence());
        }

        IEnumerator CoCloseSequence()
        {
            currentState = TransitionState.DangDong;

            // Nhánh Giảm chuyển động
            if (ReducedMotion.IsEnabled)
            {
                yield return CoCloseReducedMotion();
                yield break;
            }

            // BƯỚC 1: Khóa điều khiển, giao diện mờ dần
            if (holoRoom != null && holoRoom.exitButtonRect != null)
            {
                var cg = holoRoom.exitButtonRect.GetComponent<CanvasGroup>();
                if (cg != null) UiMotion.FadeOut(this, cg, 0.12f);
            }

            // BƯỚC 2: Cơ thể tan đầu xuống chân (đảo chiều HoloReveal), chùm sáng hạ, 4 vòng sáng tắt ngược lại
            PlaySound(clipWhoosh);
            yield return CoHoloRevealModel(false, 0.40f);
            yield return CoSleepRoomRings();

            // BƯỚC 3: Lục giác phủ từ ngoài vào tâm
            SetWipeParams(0f, new Vector2(0.5f, 0.5f), -1.0f);
            wipeImage.gameObject.SetActive(true);

            float step3Duration = 0.35f;
            float elapsed = 0f;
            while (elapsed < step3Duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / step3Duration);
                SetWipeProgress(t);
                yield return null;
            }
            SetWipeProgress(1.0f);

            // BƯỚC 4: Tắt phòng ảo, bật lab, phục hồi ĐÚNG vị trí/góc nhìn (sai số <= 1cm, <= 0.5°)
            if (holoRoom != null)
            {
                if (holoRoom.holoCamera != null) holoRoom.holoCamera.enabled = false;
                holoRoom.gameObject.SetActive(false);
            }
            if (savedViewCam != null) savedViewCam.enabled = true;

            if (savedViewer != null)
            {
                var cc = savedViewer.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;

                savedViewer.transform.position = savedPosition;
                savedViewer.transform.rotation = savedRotation;
                if (savedViewer.cameraTransform != null)
                {
                    savedViewer.cameraTransform.localRotation = savedCamLocalRotation;
                }
                if (savedViewCam != null) savedViewCam.fieldOfView = 50f; // Bắt đầu ở 50° rồi zoom ra 60°
                if (cc != null) cc.enabled = true;
            }

            ZoneStreamer.Instance?.Release(this);
            yield return ZoneStreamer.WaitReadyAt(savedPortalPos, 5f);

            // BƯỚC 5: Lục giác tan từ vị trí mô hình ra ngoài, FOV 50° -> 60°, vòng sàn co về 1.0x
            Transform portalRing = portal != null ? portal.transform.Find("FloorGlowingRing") : null;

            Vector2 screenCenterNorm = new Vector2(0.5f, 0.5f);
            if (savedViewCam != null && portal != null)
            {
                Vector3 sp = savedViewCam.WorldToViewportPoint(portal.transform.position);
                screenCenterNorm = new Vector2(Mathf.Clamp01(sp.x), Mathf.Clamp01(sp.y));
            }
            SetWipeParams(1.0f, screenCenterNorm, 1.0f);

            float step5Duration = 0.42f;
            elapsed = 0f;
            while (elapsed < step5Duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / step5Duration);
                float ease = 1f - Mathf.Pow(1f - t, 3f);

                SetWipeProgress(1.0f - t);
                if (savedViewCam != null)
                {
                    savedViewCam.fieldOfView = Mathf.Lerp(50f, savedFov, ease);
                }
                if (portalRing != null)
                {
                    portalRing.localScale = Vector3.Lerp(Vector3.one * 1.5f, Vector3.one, ease);
                }
                yield return null;
            }
            wipeImage.gameObject.SetActive(false);

            // BƯỚC 6: Mở khóa HUD / di chuyển (UiCapture--), thẻ lời mời cooldown 0.8s
            DesktopViewer.UiCapture = Mathf.Max(0, DesktopViewer.UiCapture - 1);
            if (portal != null)
            {
                portal.StartCoroutine(CoPortalCooldown(portal, 0.8f));
            }

            currentState = TransitionState.Nghi;
            activeTransition = null;
        }

        static IEnumerator CoPortalCooldown(BioPortal p, float sec)
        {
            yield return new WaitForSecondsRealtime(sec);
        }

        // =====================================================================
        // NHÁNH GIẢM CHUYỂN ĐỘNG (REDUCED MOTION - MỜ CHÉO 0.25S)
        // =====================================================================

        IEnumerator CoOpenReducedMotion()
        {
            savedViewer = FindAnyObjectByType<DesktopViewer>();
            savedViewCam = savedViewer != null ? savedViewer.viewCamera : Camera.main;

            if (savedViewer != null)
            {
                savedPosition = savedViewer.transform.position;
                savedRotation = savedViewer.transform.rotation;
                savedCamLocalRotation = savedViewer.cameraTransform != null ? savedViewer.cameraTransform.localRotation : Quaternion.identity;
            }
            if (savedViewCam != null) savedFov = savedViewCam.fieldOfView;
            savedPortalPos = portal != null ? portal.transform.position : transform.position;

            ZoneStreamer.Instance?.Keep(this, 9999f, savedPortalPos);
            DesktopViewer.UiCapture++;

            // Mờ chéo 0.12s
            yield return CoSimpleFade(0f, 1f, 0.12f);

            if (savedViewCam != null) savedViewCam.enabled = false;
            if (holoRoom != null)
            {
                holoRoom.gameObject.SetActive(true);
                if (holoRoom.holoCamera != null) holoRoom.holoCamera.enabled = true;
                holoRoom.ResetOrbitView();
            }

            yield return CoSimpleFade(1f, 0f, 0.13f);

            currentState = TransitionState.TrongPhong;
            activeTransition = null;
        }

        IEnumerator CoCloseReducedMotion()
        {
            yield return CoSimpleFade(0f, 1f, 0.12f);

            if (holoRoom != null)
            {
                if (holoRoom.holoCamera != null) holoRoom.holoCamera.enabled = false;
                holoRoom.gameObject.SetActive(false);
            }
            if (savedViewCam != null) savedViewCam.enabled = true;

            if (savedViewer != null)
            {
                var cc = savedViewer.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                savedViewer.transform.position = savedPosition;
                savedViewer.transform.rotation = savedRotation;
                if (savedViewer.cameraTransform != null) savedViewer.cameraTransform.localRotation = savedCamLocalRotation;
                if (savedViewCam != null) savedViewCam.fieldOfView = savedFov;
                if (cc != null) cc.enabled = true;
            }

            ZoneStreamer.Instance?.Release(this);
            yield return ZoneStreamer.WaitReadyAt(savedPortalPos, 5f);
            DesktopViewer.UiCapture = Mathf.Max(0, DesktopViewer.UiCapture - 1);

            yield return CoSimpleFade(1f, 0f, 0.13f);

            currentState = TransitionState.Nghi;
            activeTransition = null;
        }

        IEnumerator CoSimpleFade(float from, float to, float dur)
        {
            if (holoRoom != null && holoRoom.crossfadeGroup != null)
            {
                var cg = holoRoom.crossfadeGroup;
                cg.gameObject.SetActive(true);
                float el = 0f;
                while (el < dur)
                {
                    el += Time.unscaledDeltaTime;
                    cg.alpha = Mathf.Lerp(from, to, el / dur);
                    yield return null;
                }
                cg.alpha = to;
                if (to <= 0.01f) cg.gameObject.SetActive(false);
            }
        }

        // =====================================================================
        // TRỢ LÝ HOẠT CẢNH PHÒNG ẢO (VÒNG SÁNG & HOLO REVEAL)
        // =====================================================================

        IEnumerator CoWakeRoomRings()
        {
            if (holoRoom == null) yield break;

            Transform r1 = holoRoom.transform.Find("Vong1_SatBe");
            Transform r2 = holoRoom.transform.Find("Vong2_LucGiac");
            Transform r3 = holoRoom.ring3Torus != null ? holoRoom.ring3Torus : holoRoom.transform.Find("Vong3_Torus_0.6m");
            Transform r4 = holoRoom.ring4Torus != null ? holoRoom.ring4Torus : holoRoom.transform.Find("Vong4_Torus_2.4m");
            Transform beam = holoRoom.transform.Find("HoloLightBeam");

            if (r1 != null) r1.gameObject.SetActive(true);
            yield return new WaitForSecondsRealtime(0.08f);
            if (r2 != null) r2.gameObject.SetActive(true);
            yield return new WaitForSecondsRealtime(0.08f);
            if (r3 != null) r3.gameObject.SetActive(true);
            yield return new WaitForSecondsRealtime(0.08f);
            if (r4 != null) r4.gameObject.SetActive(true);

            // Chùm sáng dâng lên
            if (beam != null)
            {
                beam.gameObject.SetActive(true);
                float dur = 0.35f;
                float el = 0f;
                while (el < dur)
                {
                    el += Time.unscaledDeltaTime;
                    float s = Mathf.Clamp01(el / dur);
                    beam.localScale = new Vector3(1f, s, 1f);
                    yield return null;
                }
                beam.localScale = Vector3.one;
            }
        }

        IEnumerator CoSleepRoomRings()
        {
            if (holoRoom == null) yield break;

            Transform r1 = holoRoom.transform.Find("Vong1_SatBe");
            Transform r2 = holoRoom.transform.Find("Vong2_LucGiac");
            Transform r3 = holoRoom.ring3Torus;
            Transform r4 = holoRoom.ring4Torus;
            Transform beam = holoRoom.transform.Find("HoloLightBeam");

            if (beam != null)
            {
                float dur = 0.25f;
                float el = 0f;
                while (el < dur)
                {
                    el += Time.unscaledDeltaTime;
                    float s = 1.0f - Mathf.Clamp01(el / dur);
                    beam.localScale = new Vector3(1f, s, 1f);
                    yield return null;
                }
                beam.gameObject.SetActive(false);
            }

            if (r4 != null) r4.gameObject.SetActive(false);
            yield return new WaitForSecondsRealtime(0.05f);
            if (r3 != null) r3.gameObject.SetActive(false);
            yield return new WaitForSecondsRealtime(0.05f);
            if (r2 != null) r2.gameObject.SetActive(false);
            yield return new WaitForSecondsRealtime(0.05f);
            if (r1 != null) r1.gameObject.SetActive(false);
        }

        IEnumerator CoHoloRevealModel(bool revealUp, float duration)
        {
            if (holoRoom == null) yield break;

            // Vị trí thế giới: gốc phòng tại y = -600, bệ cao 0.14m -> chân tại y = -599.86m, đầu tại y = -598.16m
            float feetY = holoRoom.transform.position.y + 0.14f;
            float headY = feetY + 1.70f;

            float fromY = revealUp ? (feetY - 0.05f) : (headY + 0.10f);
            float toY = revealUp ? (headY + 0.10f) : (feetY - 0.05f);

            var renderers = holoRoom.GetComponentsInChildren<Renderer>(true);

            float el = 0f;
            while (el < duration)
            {
                el += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(el / duration);
                float curY = Mathf.Lerp(fromY, toY, t);

                revealBlock.SetFloat(doCaoCatPropId, curY);
                for (int i = 0; i < renderers.Length; i++)
                {
                    var r = renderers[i];
                    if (r != null && (r.name.Contains("xuong") || r.name.Contains("co") || r.name.Contains("da") || r.name.Contains("Bộ xương") || r.name.Contains("Hệ cơ") || r.name.Contains("Da")))
                    {
                        r.SetPropertyBlock(revealBlock);
                    }
                }
                yield return null;
            }

            revealBlock.SetFloat(doCaoCatPropId, toY);
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r != null) r.SetPropertyBlock(revealBlock);
            }
        }

        // =====================================================================
        // TRỢ LÝ HEX WIPE UI SHADER
        // =====================================================================

        void SetWipeParams(float progress, Vector2 centerNorm, float direction)
        {
            if (wipeMaterial == null) return;
            float aspect = Screen.height > 0 ? (float)Screen.width / Screen.height : 1.77778f;
            wipeMaterial.SetFloat(tienDoPropId, progress);
            wipeMaterial.SetVector(tamPropId, new Vector4(centerNorm.x, centerNorm.y, 0, 0));
            wipeMaterial.SetFloat(chieuPropId, direction);
            wipeMaterial.SetFloat(tiLeManHinhPropId, aspect);
        }

        void SetWipeProgress(float p)
        {
            if (wipeMaterial != null) wipeMaterial.SetFloat(tienDoPropId, p);
        }

        void PlaySound(AudioClip clip)
        {
            if (clip != null && audioSource != null && NutHieuUng.SoundEnabled)
            {
                audioSource.PlayOneShot(clip);
            }
        }

        void ForceEmergencyReset()
        {
            if (savedViewCam != null) savedViewCam.enabled = true;
            if (holoRoom != null)
            {
                if (holoRoom.holoCamera != null) holoRoom.holoCamera.enabled = false;
                holoRoom.gameObject.SetActive(false);
            }
            if (wipeImage != null) wipeImage.gameObject.SetActive(false);
            DesktopViewer.UiCapture = Mathf.Max(0, DesktopViewer.UiCapture - 1);
            ZoneStreamer.Instance?.Release(this);
            currentState = TransitionState.Nghi;
        }
    }
}
