using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Khkt.Interaction.Bio
{
    /// <summary>
    /// Hệ thống phản hồi âm thanh tổng hợp bằng toán học (không cần file .wav ngoài),
    /// hiển thị thông báo nổi (Toast) và ghi nhận lỗi thao tác khoa học cho Tầng 2 – Khu Sinh học.
    /// </summary>
    public class BioFeedback : MonoBehaviour
    {
        public static readonly CultureInfo ViCulture = new CultureInfo("vi-VN");

        public static BioFeedback Instance { get; private set; }

        /// <summary>
        /// Sự kiện phát ra khi người học mắc lỗi thao tác: (equipmentId, mistakeCode, description, scientificReason).
        /// BioLabSync lắng nghe sự kiện này để gửi lên máy chủ web và gọi Trợ giảng Sinh học.
        /// </summary>
        public static event Action<string, string, string, string> OnMistakeRecorded;

        /// <summary>
        /// Sự kiện phát ra khi người học thực hiện thao tác đúng: (equipmentId, actionCode, detail).
        /// </summary>
        public static event Action<string, string, string> OnActionLogged;

        public struct MistakeRecord
        {
            public float time;
            public string equipmentId;
            public string mistakeCode;
            public string description;
            public string scientificReason;
        }

        public static readonly List<MistakeRecord> MistakeHistory = new List<MistakeRecord>();

        AudioSource audioSource;
        AudioClip clipClick, clipButton, clipSuccess, clipWarning, clipCrack, clipKorotkoff, clipPop;

        string toastTitle = "";
        string toastBody = "";
        string toastSub = "";
        bool toastIsError;
        float toastTimer;

        GUIStyle boxStyle, titleStyle, bodyStyle, subStyle;
        Texture2D bgNormalTex, bgErrorTex;

        public static BioFeedback Ensure()
        {
            if (Instance != null) return Instance;
            var existing = FindAnyObjectByType<BioFeedback>();
            if (existing != null)
            {
                Instance = existing;
                return existing;
            }
            var go = new GameObject("BioFeedback");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<BioFeedback>();
            return Instance;
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 0f;
            audioSource.playOnAwake = false;
            audioSource.volume = 0.45f;
            BuildProceduralClips();
        }

        void BuildProceduralClips()
        {
            const int sr = 22050;
            // 1. Tiếng tách nhẹ khi vặn núm (click)
            clipClick = CreateClip("BioClick", sr, 0.025f, (t, dur) =>
            {
                float env = Mathf.Exp(-t * 180f);
                return Mathf.Sin(2f * Mathf.PI * 1450f * t) * env * 0.45f;
            });

            // 2. Tiếng bấm nút cơ học
            clipButton = CreateClip("BioButton", sr, 0.055f, (t, dur) =>
            {
                float f = t < 0.02f ? 920f : 1240f;
                float env = Mathf.Exp(-t * 75f);
                return Mathf.Sin(2f * Mathf.PI * f * t) * env * 0.55f;
            });

            // 3. Tiếng hoàn thành thao tác đúng (2 nốt tăng dần)
            clipSuccess = CreateClip("BioSuccess", sr, 0.22f, (t, dur) =>
            {
                float f = t < 0.10f ? 660f : 880f;
                float localT = t < 0.10f ? t : t - 0.10f;
                float env = Mathf.Exp(-localT * 18f);
                return Mathf.Sin(2f * Mathf.PI * f * t) * env * 0.5f;
            });

            // 4. Tiếng cảnh báo thao tác sai (2 nhịp trầm-gắt)
            clipWarning = CreateClip("BioWarning", sr, 0.32f, (t, dur) =>
            {
                if (t > 0.13f && t < 0.17f) return 0f;
                float f = 310f;
                float harm = Mathf.Sin(2f * Mathf.PI * f * t) + 0.45f * Mathf.Sin(2f * Mathf.PI * (f * 2f) * t);
                return Mathf.Clamp(harm * 0.45f, -0.8f, 0.8f);
            });

            // 5. Tiếng nứt vỡ kính lamen khi vặn ốc sơ cấp ở vật kính 40x / 100x
            var rng = new System.Random(2027);
            clipCrack = CreateClip("BioGlassCrack", sr, 0.18f, (t, dur) =>
            {
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                float env = Mathf.Exp(-t * 32f) + (t > 0.04f && t < 0.07f ? 0.65f : 0f);
                float ring = Mathf.Sin(2f * Mathf.PI * 2950f * t) * Mathf.Exp(-t * 25f);
                return Mathf.Clamp((noise * 0.6f + ring * 0.5f) * env, -0.95f, 0.95f);
            });

            // 6. Tiếng đập mạch Korotkoff khi nghe huyết áp
            clipKorotkoff = CreateClip("BioKorotkoff", sr, 0.12f, (t, dur) =>
            {
                float env = Mathf.Exp(-t * 38f);
                float thump = Mathf.Sin(2f * Mathf.PI * 82f * t) + 0.5f * Mathf.Sin(2f * Mathf.PI * 145f * t);
                return thump * env * 0.65f;
            });

            // 7. Tiếng bọt khí hoặc tàn đóm bùng cháy nhẹ
            clipPop = CreateClip("BioPop", sr, 0.06f, (t, dur) =>
            {
                float f = Mathf.Lerp(520f, 1180f, t / dur);
                float env = Mathf.Exp(-t * 55f);
                return Mathf.Sin(2f * Mathf.PI * f * t) * env * 0.4f;
            });
        }

        static AudioClip CreateClip(string name, int sampleRate, float duration, Func<float, float, float> gen)
        {
            int count = Mathf.Max(64, Mathf.RoundToInt(sampleRate * duration));
            float[] data = new float[count];
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / sampleRate;
                data[i] = Mathf.Clamp(gen(t, duration), -1f, 1f);
            }
            var clip = AudioClip.Create(name, count, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        public static void PlayClick()
        {
            var inst = Ensure();
            if (inst.audioSource != null && inst.clipClick != null)
                inst.audioSource.PlayOneShot(inst.clipClick, 0.45f);
        }

        public static void PlayButton()
        {
            var inst = Ensure();
            if (inst.audioSource != null && inst.clipButton != null)
                inst.audioSource.PlayOneShot(inst.clipButton, 0.55f);
        }

        public static void PlaySuccess()
        {
            var inst = Ensure();
            if (inst.audioSource != null && inst.clipSuccess != null)
                inst.audioSource.PlayOneShot(inst.clipSuccess, 0.6f);
        }

        public static void PlayWarning()
        {
            var inst = Ensure();
            if (inst.audioSource != null && inst.clipWarning != null)
                inst.audioSource.PlayOneShot(inst.clipWarning, 0.7f);
        }

        public static void PlayCrack()
        {
            var inst = Ensure();
            if (inst.audioSource != null && inst.clipCrack != null)
                inst.audioSource.PlayOneShot(inst.clipCrack, 0.85f);
        }

        public static void PlayKorotkoff()
        {
            var inst = Ensure();
            if (inst.audioSource != null && inst.clipKorotkoff != null)
                inst.audioSource.PlayOneShot(inst.clipKorotkoff, 0.75f);
        }

        public static void PlayBubblePop()
        {
            var inst = Ensure();
            if (inst.audioSource != null && inst.clipPop != null)
                inst.audioSource.PlayOneShot(inst.clipPop, 0.4f);
        }

        public static void ShowToast(string message, bool isError = false, float seconds = 4.2f, string subtitle = "")
        {
            var inst = Ensure();
            inst.toastTitle = isError ? "CẢNH BÁO THAO TÁC SINH HỌC" : "THÔNG BÁO THỰC HÀNH SINH HỌC";
            inst.toastBody = message ?? "";
            inst.toastSub = subtitle ?? "";
            inst.toastIsError = isError;
            inst.toastTimer = Mathf.Max(1.5f, seconds);
        }

        public static void LogAction(string equipmentId, string actionCode, string detail)
        {
            ShowToast(detail, false, 3.2f, $"Thiết bị: {equipmentId}");
            OnActionLogged?.Invoke(equipmentId ?? "", actionCode ?? "", detail ?? "");
        }

        public static void RecordMistake(string equipmentId, string mistakeCode, string description, string scientificReason)
        {
            PlayWarning();
            MistakeHistory.Add(new MistakeRecord
            {
                time = Time.time,
                equipmentId = equipmentId ?? "",
                mistakeCode = mistakeCode ?? "",
                description = description ?? "",
                scientificReason = scientificReason ?? ""
            });
            ShowToast(description, true, 5.5f, scientificReason);
            Debug.Log($"[BioFeedback-Mistake] [{equipmentId}:{mistakeCode}] {description} -> {scientificReason}");
            OnMistakeRecorded?.Invoke(equipmentId ?? "", mistakeCode ?? "", description ?? "", scientificReason ?? "");
        }

        public static string FormatVi(float value, string format = "0.0")
        {
            return value.ToString(format, ViCulture);
        }

        void Update()
        {
            if (toastTimer > 0f)
                toastTimer -= Time.unscaledDeltaTime;
        }

        void EnsureStyles()
        {
            if (boxStyle != null) return;
            bgNormalTex = MakeTex(new Color(0.06f, 0.22f, 0.16f, 0.92f));
            bgErrorTex = MakeTex(new Color(0.42f, 0.09f, 0.08f, 0.94f));

            boxStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(14, 14, 10, 10),
                alignment = TextAnchor.UpperLeft
            };
            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.95f, 0.98f, 0.96f) }
            };
            bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                wordWrap = true,
                normal = { textColor = Color.white }
            };
            subStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                wordWrap = true,
                fontStyle = FontStyle.Italic,
                normal = { textColor = new Color(0.86f, 0.92f, 0.88f) }
            };
        }

        static Texture2D MakeTex(Color c)
        {
            var t = new Texture2D(2, 2);
            t.SetPixels(new[] { c, c, c, c });
            t.Apply();
            return t;
        }

        void OnGUI()
        {
            if (toastTimer <= 0f || string.IsNullOrEmpty(toastBody)) return;
            EnsureStyles();

            float w = Mathf.Min(540f, Screen.width - 40f);
            float h = string.IsNullOrEmpty(toastSub) ? 74f : 102f;
            float x = (Screen.width - w) * 0.5f;
            float y = Screen.height - h - 26f;

            boxStyle.normal.background = toastIsError ? bgErrorTex : bgNormalTex;
            titleStyle.normal.textColor = toastIsError ? new Color(1f, 0.78f, 0.35f) : new Color(0.52f, 0.96f, 0.72f);

            GUILayout.BeginArea(new Rect(x, y, w, h), boxStyle);
            GUILayout.Label(toastTitle, titleStyle);
            GUILayout.Space(2f);
            GUILayout.Label(toastBody, bodyStyle);
            if (!string.IsNullOrEmpty(toastSub))
            {
                GUILayout.Space(2f);
                GUILayout.Label("Giải thích khoa học: " + toastSub, subStyle);
            }
            GUILayout.EndArea();
        }
    }
}

