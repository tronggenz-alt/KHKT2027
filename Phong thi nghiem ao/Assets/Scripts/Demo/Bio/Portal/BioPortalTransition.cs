using System;
using System.Collections;
using UnityEngine;
using Khkt.Demo;
using Khkt.Environment;
using Khkt.Environment.Tang2;
using Khkt.Rendering;

namespace Khkt.Demo.Bio.Portal
{
    /// <summary>
    /// Bộ máy chuyển cảnh (BioPortalTransition):
    ///   • FSM trạng thái: Nghi -> DangMo -> TrongPhong -> DangDong -> Nghi.
    ///     Chống kích hoạt 2 lần liên tiếp.
    ///   • Trình tự MỞ:
    ///       1. Khóa di chuyển/HUD (UiCapture++), lưu chính xác vị trí, góc nhìn, FOV.
    ///       2. Tạm dừng / đóng băng tương tác phòng lab chính (và âm thanh khi mở trang web nhúng).
    ///       3. Mở phòng Sinh học 3D: Mac, iPad dùng trang BIO-LAB nhúng (BioWebViewBridge); Windows và nền khác
    ///          mở khung xem giải phẫu 3D trong game (GiaiPhauMoDunImpl), không bật trình duyệt ngoài.
    ///   • Trình tự TRỞ VỀ (OnReturnFromBioLab):
    ///       1. Phục hồi âm thanh lab (AudioListener.pause = false).
    ///       2. Phục hồi ĐÚNG vị trí, góc ngẩng, hướng nhìn, FOV của camera và người chơi.
    ///       3. Mở khóa HUD / di chuyển (DesktopViewer.UiCapture--).
    ///       4. Đặt thời gian hồi chiêu 1.0s trên cổng để không bị kích hoạt lại ngay lập tức.
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

        // Lưu thông tin người chơi
        DesktopViewer savedViewer;
        Camera savedViewCam;
        Vector3 savedPosition;
        Quaternion savedRotation;
        Quaternion savedCamLocalRotation;
        float savedFov = 60f;
        Vector3 savedPortalPos;

        Coroutine activeTransition;

        void Awake()
        {
            if (Instance == null) Instance = this;
            EnsureAudio();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void OnApplicationPause(bool pause)
        {
            if (pause && currentState != TransitionState.Nghi && currentState != TransitionState.TrongPhong)
            {
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

        // =====================================================================
        // TRÌNH TỰ MỞ (OPEN TRANSITION VÀO PHÒNG SINH HỌC 3D QUA BIOWEBVIEWBRIDGE)
        // =====================================================================

        public void StartOpenTransition(BioPortal p, HoloBioRoom r = null)
        {
            if (currentState != TransitionState.Nghi) return;
            portal = p;
            if (r != null) holoRoom = r;

            currentState = TransitionState.DangMo;

            // 1. Lưu chính xác vị trí, góc nhìn, FOV của người chơi
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

            // 2. Pause hoặc freeze tương tác lab & âm thanh, và set DesktopViewer.UiCapture++
            DesktopViewer.UiCapture++;
            PlaySound(clipWhoosh);

            currentState = TransitionState.TrongPhong;

            // 3. Mở Phòng Sinh học 3D.
            //    Mac, iPad: trang BIO-LAB nhúng trong app (WKWebView), vẫn ở trong app.
            //    Windows và các nền khác: Unity không có WebView nên trước đây phải bật trình duyệt ngoài;
            //    nay mở khung xem giải phẫu 3D ngay trong game (từ đó sang được bàn mổ ếch), đóng lại thì về đúng chỗ trong lab.
            if (DungWebNhung)
            {
                AudioListener.pause = true;
                BioWebViewBridge.Open(() => OnReturnFromBioLab());
            }
            else
            {
                // Màn hình "Đang tải" che khoảng khựng của lần dựng + vẽ đầu tiên (27/11/2026), thay vì lab đứng hình
                StartCoroutine(Khkt.Demo.Bio.GiaiPhau.GiaiPhauDangTai.ChayDuoiManHinhTai(
                    () => Khkt.Demo.Bio.GiaiPhau.GiaiPhauMoDunImpl.Mo(() => OnReturnFromBioLab())));
            }
        }

        // =====================================================================
        // TRÌNH TỰ TRỞ VỀ (RESTORE LAB TỪ PHÒNG SINH HỌC 3D)
        // =====================================================================

        /// <summary>Chỉ Mac và iPad nhúng được trang web trong app; nền khác mở phòng giải phẫu dựng bằng Unity.</summary>
        public static bool DungWebNhung =>
            Application.platform == RuntimePlatform.OSXPlayer || Application.platform == RuntimePlatform.OSXEditor ||
            Application.platform == RuntimePlatform.IPhonePlayer;

        public void OnReturnFromBioLab()
        {
            if (currentState != TransitionState.TrongPhong && currentState != TransitionState.DangMo) return;
            currentState = TransitionState.DangDong;

            // 1. Phục hồi âm thanh lab
            AudioListener.pause = false;

            // 2. Phục hồi ĐÚNG vị trí, góc nhìn, FOV camera và trạng thái người chơi
            if (savedViewCam != null)
            {
                savedViewCam.enabled = true;
                savedViewCam.fieldOfView = savedFov;
            }

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
                if (cc != null) cc.enabled = true;
            }

            ZoneStreamer.Instance?.Release(this);

            // 3. Mở khóa HUD / di chuyển (DesktopViewer.UiCapture--)
            DesktopViewer.UiCapture = Mathf.Max(0, DesktopViewer.UiCapture - 1);

            // 4. Đặt cooldown 1.0s cho cổng để không kích hoạt lại ngay lập tức
            if (portal != null)
            {
                portal.SetCooldown(1.0f);
            }

            currentState = TransitionState.Nghi;
            activeTransition = null;
        }

        public void StartCloseTransition()
        {
            OnReturnFromBioLab();
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
            AudioListener.pause = false;
            if (savedViewCam != null)
            {
                savedViewCam.enabled = true;
                savedViewCam.fieldOfView = savedFov;
            }
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
                if (cc != null) cc.enabled = true;
            }
            if (holoRoom != null)
            {
                if (holoRoom.holoCamera != null) holoRoom.holoCamera.enabled = false;
                holoRoom.gameObject.SetActive(false);
            }
            DesktopViewer.UiCapture = Mathf.Max(0, DesktopViewer.UiCapture - 1);
            ZoneStreamer.Instance?.Release(this);
            if (portal != null) portal.SetCooldown(1.0f);
            currentState = TransitionState.Nghi;
        }
    }
}
