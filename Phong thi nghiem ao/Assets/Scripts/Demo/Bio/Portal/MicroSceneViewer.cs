using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Khkt.Demo;

namespace Khkt.Demo.Bio.Portal
{
    /// <summary>
    /// MicroSceneViewer (GĐ4 - Cấp độ hiển vi 3D):
    ///   Dựng 6 cảnh vi mô tế bào / hóa sinh thủ tục (theo vi_mo_tham_khao.js):
    ///     1. Phổi: Phế nang & mao mạch phổi (pho_nang)
    ///     2. Thận: Đơn vị thận Nephron (nephron)
    ///     3. Não: Khe Synapse thần kinh (synapse)
    ///     4. Cơ/Tim: Sarcomere sợi actin & myosin (sarcomere)
    ///     5. Ruột non: Nhung mao & vi nhung mao niêm mạc (nhung_mao)
    ///     6. Gan: Tiểu thùy gan & xoang gan (tieu_thuy_gan)
    ///   • Mô hình 3D thủ tục bằng code, vật liệu URP chuẩn.
    ///   • Hoạt cảnh sinh lý động trong Update() với ngân sách 0 GC Alloc.
    ///   • Nhãn chú thích khoa học 3D thế giới hướng billboard vào camera.
    ///   • Thanh tiêu đề mô tả khoa học & nút "Quay lại toàn thân".
    /// </summary>
    public class MicroSceneViewer : MonoBehaviour
    {
        public enum MicroSceneType
        {
            None,
            PhoNang,       // Phế nang & mao mạch phổi
            Nephron,       // Đơn vị thận Nephron
            Synapse,       // Khe Synapse thần kinh
            Sarcomere,     // Sarcomere sợi actin & myosin
            NhungMao,      // Nhung mao ruột non
            TieuThuyGan    // Tiểu thùy gan
        }

        [Header("Tham chiếu camera & giao diện")]
        public Camera holoCamera;
        public Canvas holoCanvas;
        public Action OnReturnToFullBody;

        [Header("Gốc các cảnh hiển vi")]
        public Transform microRoot;

        // UI hiển vi
        private RectTransform microHeaderPanel;
        private CanvasGroup microHeaderCg;
        private Text microTitleText;
        private Text microDescText;
        private Button returnButton;

        private MicroSceneType currentSceneType = MicroSceneType.None;
        private GameObject currentSceneObject;
        private readonly List<Transform> active3DLabels = new List<Transform>();

        // Bộ nhớ đệm động hoạt cảnh (0 GC Alloc trong Update)
        private float animTime = 0f;
        private readonly List<Action<float>> animUpdaters = new List<Action<float>>();

        // Cấu trúc nhãn 3D
        private class LabelCallout
        {
            public string text;
            public Vector3 worldPos;
            public Transform transform;
        }
        private readonly List<LabelCallout> labelsList = new List<LabelCallout>();

        void Awake()
        {
            EnsureMicroHierarchy();
        }

        void Update()
        {
            if (currentSceneType == MicroSceneType.None) return;

            float dt = Time.deltaTime;
            animTime += dt;

            // Chạy các bộ cập nhật hoạt cảnh không cấp phát bộ nhớ
            for (int i = 0; i < animUpdaters.Count; i++)
            {
                animUpdaters[i](dt);
            }

            // Hướng toàn bộ nhãn 3D về phía holoCamera
            if (holoCamera != null)
            {
                for (int i = 0; i < active3DLabels.Count; i++)
                {
                    if (active3DLabels[i] != null)
                    {
                        active3DLabels[i].rotation = holoCamera.transform.rotation;
                    }
                }
            }
        }

        public void EnsureMicroHierarchy()
        {
            if (microRoot == null)
            {
                var rTrans = transform.Find("MicroSceneRoot");
                if (rTrans == null)
                {
                    var rGo = new GameObject("MicroSceneRoot");
                    rGo.transform.SetParent(transform, false);
                    rGo.transform.localPosition = new Vector3(0f, 10f, 0f); // Tách biệt trong không gian phòng ảo
                    microRoot = rGo.transform;
                }
                else
                {
                    microRoot = rTrans;
                }
            }

            EnsureMicroUI();
        }

        void EnsureMicroUI()
        {
            if (microHeaderPanel != null) return;
            if (holoCanvas == null)
            {
                holoCanvas = GetComponentInParent<Canvas>();
            }
            if (holoCanvas == null) return;

            var panelGo = new GameObject("MicroHeaderPanel", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            panelGo.transform.SetParent(holoCanvas.transform, false);
            microHeaderPanel = (RectTransform)panelGo.transform;
            microHeaderCg = panelGo.GetComponent<CanvasGroup>();

            // Banner tiêu đề góc trên màn hình
            microHeaderPanel.anchorMin = new Vector2(0.5f, 1f);
            microHeaderPanel.anchorMax = new Vector2(0.5f, 1f);
            microHeaderPanel.pivot = new Vector2(0.5f, 1f);
            microHeaderPanel.sizeDelta = new Vector2(860f, 110f);
            microHeaderPanel.anchoredPosition = new Vector2(0f, -25f);

            var bg = panelGo.GetComponent<Image>();
            bg.sprite = GlassKit.Rounded;
            bg.type = Image.Type.Sliced;
            bg.color = new Color(10f / 255f, 18f / 255f, 34f / 255f, 0.92f);

            // Viền phát sáng ngọc lam
            var borderGo = new GameObject("VienPhatSang", typeof(RectTransform), typeof(Image));
            borderGo.transform.SetParent(microHeaderPanel, false);
            var rtB = (RectTransform)borderGo.transform;
            rtB.anchorMin = Vector2.zero;
            rtB.anchorMax = Vector2.one;
            rtB.sizeDelta = Vector2.zero;
            var imgB = borderGo.GetComponent<Image>();
            imgB.sprite = GlassKit.Rounded;
            imgB.type = Image.Type.Sliced;
            imgB.color = new Color(34f / 255f, 211f / 255f, 238f / 255f, 0.35f);
            imgB.raycastTarget = false;

            // Tiêu đề
            microTitleText = GlassKit.Label(microHeaderPanel, "TieuDeViMo", "🔬 Cảnh hiển vi", 20, GlassKit.Ink, true,
                TextAnchor.MiddleLeft, new Vector2(30f, -16f), new Vector2(580f, 32f), new Vector2(0f, 1f));

            // Mô tả khoa học
            microDescText = GlassKit.Label(microHeaderPanel, "MoTaKhoaHoc", "Mô tả cơ chế tế bào...", 13, GlassKit.Dim, false,
                TextAnchor.MiddleLeft, new Vector2(30f, -52f), new Vector2(580f, 48f), new Vector2(0f, 1f));

            // Nút "Quay lại toàn thân"
            returnButton = GlassKit.MakeButton(microHeaderPanel, "NutQuayLaiToanThan", "Quay lại toàn thân ↩",
                new Vector2(-25f, -32f), new Vector2(210f, 46f), ReturnToFullBody, primary: true, fontSize: 14, anchor: new Vector2(1f, 1f));

            microHeaderPanel.gameObject.SetActive(false);
        }

        public void OpenSceneForOrgan(string organId)
        {
            if (string.IsNullOrEmpty(organId)) return;
            switch (organId.ToLower())
            {
                case "phoi":
                    LoadScene(MicroSceneType.PhoNang);
                    break;
                case "than":
                    LoadScene(MicroSceneType.Nephron);
                    break;
                case "nao":
                case "tuy_song":
                case "tuy-song":
                    LoadScene(MicroSceneType.Synapse);
                    break;
                case "tim":
                case "co":
                    LoadScene(MicroSceneType.Sarcomere);
                    break;
                case "ruot_non":
                case "ruot-non":
                case "da_day":
                case "da-day":
                case "ruot_gia":
                case "ruot-gia":
                    LoadScene(MicroSceneType.NhungMao);
                    break;
                case "gan":
                case "tuy":
                case "lach":
                    LoadScene(MicroSceneType.TieuThuyGan);
                    break;
                default:
                    LoadScene(MicroSceneType.PhoNang);
                    break;
            }
        }

        public void LoadScene(MicroSceneType type)
        {
            EnsureMicroHierarchy();
            ClearCurrentScene();

            currentSceneType = type;
            var sceneGo = new GameObject($"Scene_{type}");
            sceneGo.transform.SetParent(microRoot, false);
            sceneGo.transform.localPosition = Vector3.zero;
            currentSceneObject = sceneGo;

            switch (type)
            {
                case MicroSceneType.PhoNang:
                    BuildPhoNangScene(sceneGo);
                    break;
                case MicroSceneType.Nephron:
                    BuildNephronScene(sceneGo);
                    break;
                case MicroSceneType.Synapse:
                    BuildSynapseScene(sceneGo);
                    break;
                case MicroSceneType.Sarcomere:
                    BuildSarcomereScene(sceneGo);
                    break;
                case MicroSceneType.NhungMao:
                    BuildNhungMaoScene(sceneGo);
                    break;
                case MicroSceneType.TieuThuyGan:
                    BuildTieuThuyGanScene(sceneGo);
                    break;
            }

            microRoot.gameObject.SetActive(true);
            if (microHeaderPanel != null)
            {
                microHeaderPanel.gameObject.SetActive(true);
                UiMotion.Popup(this, microHeaderPanel, microHeaderCg, 0.20f);
            }
        }

        public void ClearCurrentScene()
        {
            animUpdaters.Clear();
            active3DLabels.Clear();
            labelsList.Clear();

            if (currentSceneObject != null)
            {
                Destroy(currentSceneObject);
                currentSceneObject = null;
            }
            currentSceneType = MicroSceneType.None;
        }

        public void ReturnToFullBody()
        {
            if (microHeaderPanel != null && microHeaderPanel.gameObject.activeSelf)
            {
                UiMotion.Dismiss(this, microHeaderPanel, microHeaderCg, 0.14f, () =>
                {
                    if (microHeaderPanel != null) microHeaderPanel.gameObject.SetActive(false);
                });
            }

            ClearCurrentScene();
            if (microRoot != null) microRoot.gameObject.SetActive(false);

            OnReturnToFullBody?.Invoke();
        }

        // =====================================================================
        // 1. Phổi: Phế nang & mao mạch phổi (pho_nang)
        // =====================================================================

        void BuildPhoNangScene(GameObject root)
        {
            SetHeader("🔬 Phế nang & mao mạch phổi – Trao đổi khí",
                "Màng hô hấp mỏng ~0,5 µm: O₂ khuếch tán từ phế nang vào mao mạch, gắn Hb trong hồng cầu (đổi màu xanh → đỏ tươi); CO₂ khuếch tán ngược ra phế nang để thở ra ngoài.");

            var matAlveolus = CreateTransparentMaterial(new Color(249f / 255f, 168f / 255f, 212f / 255f, 0.40f), 0.25f);
            var matBronchiole = CreateLitMaterial(new Color(241f / 255f, 245f / 255f, 249f / 255f), 0.4f);
            var matCapillaryBlue = CreateLitMaterial(new Color(59f / 255f, 130f / 255f, 246f / 255f), 0.5f);
            var matCapillaryRed = CreateLitMaterial(new Color(239f / 255f, 68f / 255f, 68f / 255f), 0.5f);
            var matRbc = CreateLitMaterial(new Color(225f / 255f, 29f / 255f, 72f / 255f), 0.6f);

            // Cụm 7 phế nang hình cầu
            Vector3[] alveoliCenters = new Vector3[]
            {
                Vector3.zero,
                new Vector3(0.95f, 0.3f, 0.1f),
                new Vector3(-0.9f, 0.35f, -0.1f),
                new Vector3(0.3f, 0.95f, -0.3f),
                new Vector3(-0.25f, -0.85f, 0.2f),
                new Vector3(0.6f, -0.6f, -0.5f),
                new Vector3(-0.5f, 0.2f, 0.9f)
            };

            var alveoliTransforms = new Transform[alveoliCenters.Length];
            for (int i = 0; i < alveoliCenters.Length; i++)
            {
                var sph = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sph.transform.SetParent(root.transform, false);
                sph.transform.localPosition = alveoliCenters[i];
                sph.transform.localScale = Vector3.one * 1.25f;
                sph.GetComponent<MeshRenderer>().sharedMaterial = matAlveolus;
                alveoliTransforms[i] = sph.transform;
            }

            // Tiểu phế quản dẫn khí phía trên
            var bronch = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            bronch.transform.SetParent(root.transform, false);
            bronch.transform.localPosition = new Vector3(0f, 1.65f, 0.1f);
            bronch.transform.localScale = new Vector3(0.42f, 0.7f, 0.42f);
            bronch.GetComponent<MeshRenderer>().sharedMaterial = matBronchiole;

            // Mạng mao mạch uốn lượn ôm quanh phế nang
            var capRoot = new GameObject("Capillaries");
            capRoot.transform.SetParent(root.transform, false);
            int numCapLoops = 8;
            var capTubes = new Transform[numCapLoops];
            for (int i = 0; i < numCapLoops; i++)
            {
                var loop = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                loop.transform.SetParent(capRoot.transform, false);
                float angle = i * Mathf.PI / 4f;
                loop.transform.localPosition = new Vector3(Mathf.Cos(angle) * 1.15f, Mathf.Sin(angle * 2f) * 0.45f, Mathf.Sin(angle) * 1.15f);
                loop.transform.localRotation = Quaternion.Euler(30f * Mathf.Sin(angle), angle * Mathf.Rad2Deg, 45f);
                loop.transform.localScale = new Vector3(0.08f, 0.85f, 0.08f);
                loop.GetComponent<MeshRenderer>().sharedMaterial = (i < 4) ? matCapillaryBlue : matCapillaryRed;
                capTubes[i] = loop.transform;
            }

            // Hồng cầu (RBC) di chuyển trong mao mạch
            int rbcCount = 12;
            var rbcs = new Transform[rbcCount];
            var rbcProgress = new float[rbcCount];
            for (int i = 0; i < rbcCount; i++)
            {
                var rbcGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                rbcGo.transform.SetParent(root.transform, false);
                rbcGo.transform.localScale = new Vector3(0.16f, 0.06f, 0.16f); // Đĩa lõm 2 mặt
                rbcGo.GetComponent<MeshRenderer>().sharedMaterial = matRbc;
                rbcs[i] = rbcGo.transform;
                rbcProgress[i] = (float)i / rbcCount;
            }

            // Hoạt cảnh không cấp phát: phế nang thở phập phồng (chu kỳ 3,75s = 16 bpm), hồng cầu chạy tuần hoàn
            animUpdaters.Add((dt) =>
            {
                // Thở
                float breath = 1f + 0.045f * Mathf.Sin(animTime * (Mathf.PI * 2f / 3.75f));
                for (int i = 0; i < alveoliTransforms.Length; i++)
                {
                    alveoliTransforms[i].localScale = Vector3.one * (1.25f * breath);
                }

                // Hồng cầu di chuyển
                for (int i = 0; i < rbcCount; i++)
                {
                    rbcProgress[i] = (rbcProgress[i] + dt * 0.18f) % 1f;
                    float u = rbcProgress[i];
                    float a = u * Mathf.PI * 2f;
                    Vector3 pos = new Vector3(Mathf.Cos(a) * 1.18f, Mathf.Sin(a * 2f) * 0.45f, Mathf.Sin(a) * 1.18f);
                    rbcs[i].localPosition = pos;
                    rbcs[i].rotation = Quaternion.Euler(0f, a * Mathf.Rad2Deg + 90f, 0f);
                }
            });

            // Nhãn khoa học 3D
            Create3DLabel(root.transform, "Phế nang (Alveolus) – Ø ~0,2 mm", new Vector3(0.3f, 1.1f, 0.5f));
            Create3DLabel(root.transform, "Mao mạch phổi: máu xanh → đỏ", new Vector3(1.45f, -0.55f, 0.5f));
            Create3DLabel(root.transform, "Hồng cầu: Hb + 4O₂ ⇌ Hb(O₂)₄", new Vector3(-1.2f, 0.5f, 0.6f));
            Create3DLabel(root.transform, "O₂ khuếch tán vào máu (100 → 40 mmHg)", new Vector3(-0.4f, -1.2f, 0.8f));
            Create3DLabel(root.transform, "CO₂ ra phế nang (46 → 40 mmHg)", new Vector3(0.9f, 1.1f, -0.6f));
            Create3DLabel(root.transform, "Tiểu phế quản hô hấp", new Vector3(0f, 2.3f, 0.1f));
        }

        // =====================================================================
        // 2. Thận: Đơn vị thận Nephron (nephron)
        // =====================================================================

        void BuildNephronScene(GameObject root)
        {
            SetHeader("🔬 Đơn vị thận Nephron – Lọc máu và tạo nước tiểu",
                "Dịch lọc từ cầu thận (nang Bowman) đi qua ống lượn gần – quai Henle – ống lượn xa – ống góp. Glucose và Na⁺ được tái hấp thu triệt để vào mao mạch, nước rời nhánh xuống quai Henle dưới tác động áp suất thẩm thấu.");

            var matGlomerulus = CreateLitMaterial(new Color(220f / 255f, 38f / 255f, 38f / 255f), 0.6f);
            var matBowman = CreateTransparentMaterial(new Color(253f / 255f, 230f / 255f, 138f / 255f, 0.45f), 0.3f);
            var matTubule = CreateLitMaterial(new Color(254f / 255f, 243f / 255f, 199f / 255f), 0.4f);
            var matDuct = CreateLitMaterial(new Color(167f / 255f, 243f / 255f, 208f / 255f), 0.45f);
            var matCapillary = CreateLitMaterial(new Color(96f / 255f, 165f / 255f, 250f / 255f), 0.5f);
            var matFiltrate = CreateLitMaterial(new Color(250f / 255f, 204f / 255f, 21f / 255f), 0.8f);

            // Nang Bowman (bán cầu) + Cầu thận (búi mao mạch cuộn)
            var bowman = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bowman.transform.SetParent(root.transform, false);
            bowman.transform.localPosition = new Vector3(-1.6f, 1.0f, 0f);
            bowman.transform.localScale = Vector3.one * 1.1f;
            bowman.GetComponent<MeshRenderer>().sharedMaterial = matBowman;

            var glomerulus = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            glomerulus.transform.SetParent(bowman.transform, false);
            glomerulus.transform.localPosition = Vector3.zero;
            glomerulus.transform.localScale = Vector3.one * 0.75f;
            glomerulus.GetComponent<MeshRenderer>().sharedMaterial = matGlomerulus;

            // Ống lượn gần (đoạn cong gập)
            CreateCurvedSegment(root.transform, new Vector3(-1.0f, 1.0f, 0f), new Vector3(-0.3f, 1.2f, 0.2f), matTubule, 0.15f);
            CreateCurvedSegment(root.transform, new Vector3(-0.3f, 1.2f, 0.2f), new Vector3(0.2f, 0.7f, -0.1f), matTubule, 0.15f);

            // Quai Henle (chữ U sâu xuống tủy thận)
            var henleDown = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            henleDown.transform.SetParent(root.transform, false);
            henleDown.transform.localPosition = new Vector3(0.35f, -0.5f, 0f);
            henleDown.transform.localScale = new Vector3(0.12f, 1.1f, 0.12f);
            henleDown.GetComponent<MeshRenderer>().sharedMaterial = matTubule;

            var henleTurn = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            henleTurn.transform.SetParent(root.transform, false);
            henleTurn.transform.localPosition = new Vector3(0.55f, -1.6f, 0f);
            henleTurn.transform.localScale = Vector3.one * 0.28f;
            henleTurn.GetComponent<MeshRenderer>().sharedMaterial = matTubule;

            var henleUp = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            henleUp.transform.SetParent(root.transform, false);
            henleUp.transform.localPosition = new Vector3(0.75f, -0.5f, 0f);
            henleUp.transform.localScale = new Vector3(0.12f, 1.1f, 0.12f);
            henleUp.GetComponent<MeshRenderer>().sharedMaterial = matTubule;

            // Ống lượn xa
            CreateCurvedSegment(root.transform, new Vector3(0.75f, 0.6f, 0f), new Vector3(1.3f, 0.9f, 0.1f), matTubule, 0.15f);

            // Ống góp (trụ đứng lớn)
            var duct = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            duct.transform.SetParent(root.transform, false);
            duct.transform.localPosition = new Vector3(1.85f, -0.4f, 0f);
            duct.transform.localScale = new Vector3(0.25f, 1.7f, 0.25f);
            duct.GetComponent<MeshRenderer>().sharedMaterial = matDuct;

            // Mao mạch quanh ống
            var periCap = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            periCap.transform.SetParent(root.transform, false);
            periCap.transform.localPosition = new Vector3(0.55f, -0.3f, 0.35f);
            periCap.transform.localScale = new Vector3(0.09f, 1.2f, 0.09f);
            periCap.GetComponent<MeshRenderer>().sharedMaterial = matCapillary;

            // Hạt dịch lọc di chuyển
            int filtrateCount = 8;
            var filtrateParticles = new Transform[filtrateCount];
            var fProgress = new float[filtrateCount];
            for (int i = 0; i < filtrateCount; i++)
            {
                var p = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                p.transform.SetParent(root.transform, false);
                p.transform.localScale = Vector3.one * 0.12f;
                p.GetComponent<MeshRenderer>().sharedMaterial = matFiltrate;
                filtrateParticles[i] = p.transform;
                fProgress[i] = (float)i / filtrateCount;
            }

            animUpdaters.Add((dt) =>
            {
                for (int i = 0; i < filtrateCount; i++)
                {
                    fProgress[i] = (fProgress[i] + dt * 0.14f) % 1f;
                    float u = fProgress[i];
                    // Đường di chuyển từ cầu thận qua quai Henle đến ống góp
                    float x = Mathf.Lerp(-1.4f, 1.85f, u);
                    float y = (u < 0.3f) ? 1.0f : (u < 0.7f ? Mathf.Sin((u - 0.3f) / 0.4f * Mathf.PI) * -1.6f : 0.6f - (u - 0.7f) * 2f);
                    filtrateParticles[i].localPosition = new Vector3(x, y, 0f);
                }
            });

            // Nhãn khoa học 3D
            Create3DLabel(root.transform, "Cầu thận: lọc ~125 mL/phút", new Vector3(-1.6f, 1.65f, 0f));
            Create3DLabel(root.transform, "Nang Bowman", new Vector3(-2.15f, 0.55f, 0.2f));
            Create3DLabel(root.transform, "Ống lượn gần: tái hấp thu glucose, 65% Na⁺", new Vector3(-0.2f, 1.55f, 0.2f));
            Create3DLabel(root.transform, "Quai Henle: hấp thu nước, muối", new Vector3(0.55f, -1.85f, 0f));
            Create3DLabel(root.transform, "Ống lượn xa: aldosterone", new Vector3(1.3f, 1.25f, 0f));
            Create3DLabel(root.transform, "Ống góp: ADH tăng hấp thu nước", new Vector3(2.35f, -1.6f, 0f));
            Create3DLabel(root.transform, "Mao mạch quanh ống", new Vector3(0.9f, -0.9f, 0.7f));
        }

        // =====================================================================
        // 3. Não: Khe Synapse thần kinh (synapse)
        // =====================================================================

        void BuildSynapseScene(GameObject root)
        {
            SetHeader("🔬 Khe Synapse thần kinh – Dẫn truyền xung thần kinh",
                "Xung điện tới chùy synapse → kênh Ca²⁺ mở, Ca²⁺ tràn vào → bóng synapse hòa màng giải phóng Acetylcholine vào khe synapse (~20-40 nm) → gắn thụ thể mở kênh Na⁺ tạo xung ở màng sau; enzyme AChE phân giải ACh.");

            var matPreMembrane = CreateTransparentMaterial(new Color(253f / 255f, 230f / 255f, 138f / 255f, 0.5f), 0.3f);
            var matPostMembrane = CreateLitMaterial(new Color(147f / 255f, 197f / 255f, 253f / 255f), 0.4f);
            var matVesicle = CreateLitMaterial(new Color(167f / 255f, 139f / 255f, 250f / 255f), 0.6f);
            var matAch = CreateLitMaterial(new Color(240f / 255f, 171f / 255f, 252f / 255f), 0.7f);
            var matReceptor = CreateLitMaterial(new Color(96f / 255f, 165f / 255f, 250f / 255f), 0.5f);

            // Chùy synapse (bán cầu lồi màng trước)
            var preBulb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            preBulb.transform.SetParent(root.transform, false);
            preBulb.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            preBulb.transform.localScale = new Vector3(2.2f, 1.5f, 2.2f);
            preBulb.GetComponent<MeshRenderer>().sharedMaterial = matPreMembrane;

            // Sợi trục dẫn tới
            var axon = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            axon.transform.SetParent(root.transform, false);
            axon.transform.localPosition = new Vector3(0f, 1.85f, 0f);
            axon.transform.localScale = new Vector3(0.55f, 0.9f, 0.55f);
            axon.GetComponent<MeshRenderer>().sharedMaterial = matPreMembrane;

            // Màng sau synapse (tấm phẳng)
            var postMembrane = GameObject.CreatePrimitive(PrimitiveType.Cube);
            postMembrane.transform.SetParent(root.transform, false);
            postMembrane.transform.localPosition = new Vector3(0f, -0.95f, 0f);
            postMembrane.transform.localScale = new Vector3(3.2f, 0.15f, 2.2f);
            postMembrane.GetComponent<MeshRenderer>().sharedMaterial = matPostMembrane;

            // Thụ thể trên màng sau
            for (int i = 0; i < 5; i++)
            {
                var rec = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                rec.transform.SetParent(postMembrane.transform, false);
                rec.transform.localPosition = new Vector3(-0.35f + (i * 0.18f), 0.7f, (i % 2 == 0) ? -0.15f : 0.15f);
                rec.transform.localScale = new Vector3(0.06f, 0.45f, 0.06f);
                rec.GetComponent<MeshRenderer>().sharedMaterial = matReceptor;
            }

            // Bóng synapse chứa ACh
            int vesicleCount = 8;
            var vesicles = new Transform[vesicleCount];
            for (int i = 0; i < vesicleCount; i++)
            {
                var ves = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                ves.transform.SetParent(root.transform, false);
                float a = i * Mathf.PI / 4f;
                ves.transform.localPosition = new Vector3(Mathf.Cos(a) * 0.6f, 0.4f + Mathf.Sin(a) * 0.2f, Mathf.Sin(a) * 0.6f);
                ves.transform.localScale = Vector3.one * 0.22f;
                ves.GetComponent<MeshRenderer>().sharedMaterial = matVesicle;
                vesicles[i] = ves.transform;
            }

            // Hạt acetylcholine (ACh) qua khe
            int achCount = 10;
            var achParticles = new Transform[achCount];
            var achProgress = new float[achCount];
            for (int i = 0; i < achCount; i++)
            {
                var p = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                p.transform.SetParent(root.transform, false);
                p.transform.localScale = Vector3.one * 0.08f;
                p.GetComponent<MeshRenderer>().sharedMaterial = matAch;
                achParticles[i] = p.transform;
                achProgress[i] = (float)i / achCount;
            }

            animUpdaters.Add((dt) =>
            {
                for (int i = 0; i < achCount; i++)
                {
                    achProgress[i] = (achProgress[i] + dt * 0.35f) % 1f;
                    float u = achProgress[i];
                    // Chạy từ màng trước y=0 xuống màng sau y=-0.85
                    float y = Mathf.Lerp(0f, -0.85f, u);
                    float spread = Mathf.Sin(u * Mathf.PI) * 0.6f;
                    achParticles[i].localPosition = new Vector3(Mathf.Cos(i * 1.3f) * spread, y, Mathf.Sin(i * 1.3f) * spread);
                }
            });

            // Nhãn khoa học 3D
            Create3DLabel(root.transform, "Chuỳ synapse (màng trước)", new Vector3(-0.3f, 0.95f, 0.9f));
            Create3DLabel(root.transform, "Bóng chứa acetylcholine", new Vector3(-0.9f, 0.55f, 0.6f));
            Create3DLabel(root.transform, "Ca²⁺ vào → xuất bào", new Vector3(1.2f, -0.45f, 0.6f));
            Create3DLabel(root.transform, "Khe synapse ~20–40 nm", new Vector3(1.05f, -0.85f, 0.4f));
            Create3DLabel(root.transform, "Thụ thể màng sau: kênh Na⁺ mở", new Vector3(-1.3f, -1.35f, 0.6f));
            Create3DLabel(root.transform, "AChE phân giải ACh → choline + acetic acid", new Vector3(0.4f, -1.55f, 0.9f));
        }

        // =====================================================================
        // 4. Cơ: Sarcomere sợi actin/myosin (sarcomere)
        // =====================================================================

        void BuildSarcomereScene(GameObject root)
        {
            SetHeader("🔬 Sarcomere cơ tim & cơ vân – Cơ chế co cơ trượt sợi",
                "Khi co cơ: Ca²⁺ tăng cao làm bộc lộ vị trí gắn trên actin, đầu myosin tiêu thụ ATP thực hiện động tác kéo sợi actin trượt về phía vạch M, hai đĩa Z tiến lại gần nhau làm sarcomere ngắn lại ~15%.");

            var matZDisc = CreateLitMaterial(new Color(252f / 255f, 165f / 255f, 165f / 255f), 0.4f);
            var matActin = CreateLitMaterial(new Color(248f / 255f, 113f / 255f, 113f / 255f), 0.5f);
            var matMyosin = CreateLitMaterial(new Color(124f / 255f, 58f / 255f, 237f / 255f), 0.6f);
            var matMyosinHead = CreateLitMaterial(new Color(196f / 255f, 181f / 255f, 253f / 255f), 0.7f);

            // Hai đĩa Z 2 bên
            var zDiscLeft = GameObject.CreatePrimitive(PrimitiveType.Cube);
            zDiscLeft.transform.SetParent(root.transform, false);
            zDiscLeft.transform.localScale = new Vector3(0.08f, 2.2f, 1.6f);
            zDiscLeft.GetComponent<MeshRenderer>().sharedMaterial = matZDisc;

            var zDiscRight = GameObject.CreatePrimitive(PrimitiveType.Cube);
            zDiscRight.transform.SetParent(root.transform, false);
            zDiscRight.transform.localScale = new Vector3(0.08f, 2.2f, 1.6f);
            zDiscRight.GetComponent<MeshRenderer>().sharedMaterial = matZDisc;

            // Các sợi mảnh actin nối từ đĩa Z vào trong
            var leftActins = new List<Transform>();
            var rightActins = new List<Transform>();
            for (int i = 0; i < 4; i++)
            {
                float y = -0.6f + (i * 0.4f);

                var al = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                al.transform.SetParent(root.transform, false);
                al.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                al.transform.localScale = new Vector3(0.04f, 0.7f, 0.04f);
                al.GetComponent<MeshRenderer>().sharedMaterial = matActin;
                leftActins.Add(al.transform);

                var ar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                ar.transform.SetParent(root.transform, false);
                ar.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                ar.transform.localScale = new Vector3(0.04f, 0.7f, 0.04f);
                ar.GetComponent<MeshRenderer>().sharedMaterial = matActin;
                rightActins.Add(ar.transform);
            }

            // Sợi dày myosin nằm ở giữa với các đầu chèo
            var myosinRoot = new GameObject("MyosinFilaments");
            myosinRoot.transform.SetParent(root.transform, false);
            for (int i = 0; i < 3; i++)
            {
                float y = -0.4f + (i * 0.4f);
                var thick = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                thick.transform.SetParent(myosinRoot.transform, false);
                thick.transform.localPosition = new Vector3(0f, y, 0f);
                thick.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                thick.transform.localScale = new Vector3(0.09f, 0.95f, 0.09f);
                thick.GetComponent<MeshRenderer>().sharedMaterial = matMyosin;

                // Các đầu myosin nhô ra hai phía
                for (int k = -3; k <= 3; k++)
                {
                    if (k == 0) continue;
                    var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    head.transform.SetParent(thick.transform, false);
                    head.transform.localPosition = new Vector3(0.6f, k * 0.26f, 0f);
                    head.transform.localScale = new Vector3(0.12f, 0.08f, 0.08f);
                    head.GetComponent<MeshRenderer>().sharedMaterial = matMyosinHead;
                }
            }

            // Hoạt cảnh co cơ trượt sợi: 72 bpm (chu kỳ 0.833s)
            animUpdaters.Add((dt) =>
            {
                float cycle = (animTime / 0.833f) % 1f;
                // Pha co 0.3s, pha giãn 0.5s
                float contract = (cycle < 0.36f) ? Mathf.Sin(cycle / 0.36f * Mathf.PI) : 0f;
                float halfDist = 1.45f - (0.22f * contract);

                zDiscLeft.transform.localPosition = new Vector3(-halfDist, 0f, 0f);
                zDiscRight.transform.localPosition = new Vector3(halfDist, 0f, 0f);

                for (int i = 0; i < leftActins.Count; i++)
                {
                    float y = -0.6f + (i * 0.4f);
                    leftActins[i].localPosition = new Vector3(-halfDist + 0.7f, y, 0f);
                    rightActins[i].localPosition = new Vector3(halfDist - 0.7f, y, 0f);
                }
            });

            // Nhãn khoa học 3D
            Create3DLabel(root.transform, "Đĩa Z", new Vector3(-1.6f, 1.25f, 0f));
            Create3DLabel(root.transform, "Actin (sợi mảnh)", new Vector3(-0.9f, -1.2f, 0.5f));
            Create3DLabel(root.transform, "Myosin (sợi dày) – đầu chèo", new Vector3(0.2f, 0.75f, 0.6f));
            Create3DLabel(root.transform, "Ca²⁺ mở vị trí bám → co", new Vector3(1.3f, 1.1f, 0.5f));
            Create3DLabel(root.transform, "ATP → ADP + Pᵢ (năng lượng)", new Vector3(1.2f, -1.15f, 0.4f));
        }

        // =====================================================================
        // 5. Ruột non: Nhung mao & vi nhung mao niêm mạc (nhung_mao)
        // =====================================================================

        void BuildNhungMaoScene(GameObject root)
        {
            SetHeader("🔬 Nhung mao & vi nhung mao ruột non – Hấp thụ chất dinh dưỡng",
                "Các nếp gấp và lông ruột (nhung mao) cùng hàng tỉ vi nhung mao làm tăng diện tích tiếp xúc lên gấp hàng trăm lần (~250-300 m²). Glucose và amino acid được hấp thu vào mao mạch máu, lipid đi vào mạch bạch huyết.");

            var matMucosa = CreateLitMaterial(new Color(254f / 255f, 202f / 255f, 202f / 255f), 0.35f);
            var matVillus = CreateLitMaterial(new Color(253f / 255f, 164f / 255f, 175f / 255f), 0.4f);
            var matLacteal = CreateLitMaterial(new Color(254f / 255f, 240f / 255f, 138f / 255f), 0.5f);
            var matCapillary = CreateLitMaterial(new Color(239f / 255f, 68f / 255f, 68f / 255f), 0.5f);
            var matNutrient = CreateLitMaterial(new Color(52f / 255f, 211f / 255f, 153f / 255f), 0.8f);

            // Tấm đáy thành niêm mạc
            var baseWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            baseWall.transform.SetParent(root.transform, false);
            baseWall.transform.localPosition = new Vector3(0f, -1.2f, 0f);
            baseWall.transform.localScale = new Vector3(4.2f, 0.25f, 2.5f);
            baseWall.GetComponent<MeshRenderer>().sharedMaterial = matMucosa;

            // 5 lông ruột (nhung mao) hình ngón tay vươn lên
            var villiTransforms = new List<Transform>();
            for (int i = 0; i < 5; i++)
            {
                float x = -1.6f + (i * 0.8f);
                var villusGo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                villusGo.transform.SetParent(root.transform, false);
                villusGo.transform.localPosition = new Vector3(x, 0f, (i % 2 == 0) ? -0.15f : 0.15f);
                villusGo.transform.localScale = new Vector3(0.38f, 1.1f, 0.38f);
                villusGo.GetComponent<MeshRenderer>().sharedMaterial = matVillus;
                villiTransforms.Add(villusGo.transform);

                // Mạch bạch huyết ở giữa (lacteal)
                var lacteal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                lacteal.transform.SetParent(villusGo.transform, false);
                lacteal.transform.localPosition = Vector3.zero;
                lacteal.transform.localScale = new Vector3(0.28f, 0.85f, 0.28f);
                lacteal.GetComponent<MeshRenderer>().sharedMaterial = matLacteal;

                // Mạng mao mạch bao quanh
                var cap = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                cap.transform.SetParent(villusGo.transform, false);
                cap.transform.localPosition = new Vector3(0.35f, 0f, 0f);
                cap.transform.localScale = new Vector3(0.12f, 0.88f, 0.12f);
                cap.GetComponent<MeshRenderer>().sharedMaterial = matCapillary;
            }

            // Hạt chất dinh dưỡng hấp thu vào
            int nutCount = 12;
            var nutParticles = new Transform[nutCount];
            var nutProgress = new float[nutCount];
            for (int i = 0; i < nutCount; i++)
            {
                var p = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                p.transform.SetParent(root.transform, false);
                p.transform.localScale = Vector3.one * 0.1f;
                p.GetComponent<MeshRenderer>().sharedMaterial = matNutrient;
                nutParticles[i] = p.transform;
                nutProgress[i] = (float)i / nutCount;
            }

            animUpdaters.Add((dt) =>
            {
                // Nhung mao đung đưa nhẹ theo nhu động ruột
                for (int i = 0; i < villiTransforms.Count; i++)
                {
                    float angle = Mathf.Sin(animTime * 1.8f + i) * 4f;
                    villiTransforms[i].localRotation = Quaternion.Euler(0f, 0f, angle);
                }

                // Hạt dinh dưỡng từ lòng ruột ngấm vào
                for (int i = 0; i < nutCount; i++)
                {
                    nutProgress[i] = (nutProgress[i] + dt * 0.22f) % 1f;
                    float u = nutProgress[i];
                    float targetX = -1.6f + ((i % 5) * 0.8f);
                    float y = Mathf.Lerp(1.5f, -0.2f, u);
                    nutParticles[i].localPosition = new Vector3(targetX + Mathf.Sin(u * 5f) * 0.1f, y, 0f);
                }
            });

            // Nhãn khoa học 3D
            Create3DLabel(root.transform, "Nhung mao ruột (Villi) – tăng diện tích tiếp xúc", new Vector3(-0.3f, 1.4f, 0.4f));
            Create3DLabel(root.transform, "Tế bào biểu mô niêm mạc có vi nhung mao", new Vector3(-1.6f, 0.4f, 0.5f));
            Create3DLabel(root.transform, "Mao mạch máu: hấp thu glucose, amino acid", new Vector3(1.2f, 0.3f, 0.5f));
            Create3DLabel(root.transform, "Mạch dưỡng chấp (bạch huyết): hấp thu lipid", new Vector3(0.5f, -0.4f, 0.5f));
            Create3DLabel(root.transform, "Lòng ruột non: dịch mật nhũ tương hoá lipid", new Vector3(-1.2f, 1.7f, 0.2f));
        }

        // =====================================================================
        // 6. Gan: Tiểu thùy gan & xoang gan (tieu_thuy_gan)
        // =====================================================================

        void BuildTieuThuyGanScene(GameObject root)
        {
            SetHeader("🔬 Tiểu thùy gan & xoang gan – Nhà máy chuyển hóa và giải độc",
                "Tiểu thùy gan hình lăng trụ lục giác. Máu từ bộ ba cửa (TM cửa, ĐM gan) chảy qua xoang gan về tĩnh mạch trung tâm. Tế bào gan khử độc, biến NH₃ thành urea, dự trữ glycogen và tiết mật chảy ngược ra ống mật.");

            var matLobule = CreateTransparentMaterial(new Color(154f / 255f, 52f / 255f, 18f / 255f, 0.35f), 0.3f);
            var matCentralVein = CreateLitMaterial(new Color(59f / 255f, 130f / 255f, 246f / 255f), 0.5f);
            var matPortalVein = CreateLitMaterial(new Color(124f / 255f, 58f / 255f, 237f / 255f), 0.5f);
            var matHepaticArtery = CreateLitMaterial(new Color(239f / 255f, 68f / 255f, 68f / 255f), 0.5f);
            var matBileDuct = CreateLitMaterial(new Color(34f / 255f, 197f / 255f, 94f / 255f), 0.5f);
            var matPlates = CreateLitMaterial(new Color(194f / 255f, 65f / 255f, 12f / 255f), 0.6f);
            var matBloodParticle = CreateLitMaterial(new Color(225f / 255f, 29f / 255f, 72f / 255f), 0.8f);

            // Khung lăng trụ lục giác (dựng từ 6 cột ở 6 góc)
            Vector3[] hexCorners = new Vector3[6];
            float hexRadius = 1.8f;
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI / 3f;
                hexCorners[i] = new Vector3(Mathf.Cos(a) * hexRadius, 0f, Mathf.Sin(a) * hexRadius);

                // Bộ ba cửa ở mỗi góc: TM cửa (tím), ĐM gan (đỏ), Ống mật (xanh lá)
                var pv = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pv.transform.SetParent(root.transform, false);
                pv.transform.localPosition = hexCorners[i];
                pv.transform.localScale = new Vector3(0.08f, 0.8f, 0.08f);
                pv.GetComponent<MeshRenderer>().sharedMaterial = matPortalVein;

                var art = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                art.transform.SetParent(root.transform, false);
                art.transform.localPosition = hexCorners[i] + new Vector3(0.12f, 0f, 0.12f);
                art.transform.localScale = new Vector3(0.06f, 0.8f, 0.06f);
                art.GetComponent<MeshRenderer>().sharedMaterial = matHepaticArtery;

                var bile = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                bile.transform.SetParent(root.transform, false);
                bile.transform.localPosition = hexCorners[i] + new Vector3(-0.12f, 0f, 0.08f);
                bile.transform.localScale = new Vector3(0.06f, 0.8f, 0.06f);
                bile.GetComponent<MeshRenderer>().sharedMaterial = matBileDuct;
            }

            // Tĩnh mạch trung tâm ở giữa (màu xanh dương)
            var centralVein = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            centralVein.transform.SetParent(root.transform, false);
            centralVein.transform.localPosition = Vector3.zero;
            centralVein.transform.localScale = new Vector3(0.35f, 0.9f, 0.35f);
            centralVein.GetComponent<MeshRenderer>().sharedMaterial = matCentralVein;

            // 12 nan hoa dây tế bào gan (hepatic cords) nối từ trung tâm ra ngoài
            for (int i = 0; i < 12; i++)
            {
                float a = i * Mathf.PI / 6f;
                var cord = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cord.transform.SetParent(root.transform, false);
                cord.transform.localPosition = new Vector3(Mathf.Cos(a) * 0.95f, 0f, Mathf.Sin(a) * 0.95f);
                cord.transform.localRotation = Quaternion.Euler(0f, -a * Mathf.Rad2Deg, 0f);
                cord.transform.localScale = new Vector3(1.25f, 0.35f, 0.08f);
                cord.GetComponent<MeshRenderer>().sharedMaterial = matPlates;
            }

            // Dòng máu di chuyển trong xoang gan từ góc về trung tâm
            int bloodCount = 12;
            var bloodParticles = new Transform[bloodCount];
            var bloodProgress = new float[bloodCount];
            for (int i = 0; i < bloodCount; i++)
            {
                var bp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                bp.transform.SetParent(root.transform, false);
                bp.transform.localScale = Vector3.one * 0.1f;
                bp.GetComponent<MeshRenderer>().sharedMaterial = matBloodParticle;
                bloodParticles[i] = bp.transform;
                bloodProgress[i] = (float)i / bloodCount;
            }

            animUpdaters.Add((dt) =>
            {
                for (int i = 0; i < bloodCount; i++)
                {
                    bloodProgress[i] = (bloodProgress[i] + dt * 0.25f) % 1f;
                    float u = bloodProgress[i];
                    int cornerIdx = i % 6;
                    Vector3 startPos = hexCorners[cornerIdx];
                    // Chảy từ góc vào trung tâm
                    bloodParticles[i].localPosition = Vector3.Lerp(startPos, Vector3.zero, u);
                }
            });

            // Nhãn khoa học 3D
            Create3DLabel(root.transform, "Tĩnh mạch trung tâm", new Vector3(0f, 0.65f, 0f));
            Create3DLabel(root.transform, "Bộ ba cửa: TM cửa · ĐM gan · ống mật", new Vector3(1.8f, 0.55f, 0f));
            Create3DLabel(root.transform, "Xoang gan: máu chảy vào trung tâm", new Vector3(-0.9f, 0.45f, 0.8f));
            Create3DLabel(root.transform, "Mật chảy ngược ra ống mật", new Vector3(-1.1f, 0.35f, -1.0f));
            Create3DLabel(root.transform, "Tế bào gan: 2NH₃ + CO₂ → urea", new Vector3(0.9f, -0.45f, -0.6f));
        }

        // =====================================================================
        // Bộ dựng Nhãn khoa học 3D & Trợ giúp hình học
        // =====================================================================

        void Create3DLabel(Transform parent, string text, Vector3 localPos)
        {
            var labelGo = new GameObject("Label3D_" + text);
            labelGo.transform.SetParent(parent, false);
            labelGo.transform.localPosition = localPos;

            var tm = labelGo.AddComponent<TextMesh>();
            tm.text = text;
            tm.fontSize = 24;
            tm.characterSize = 0.035f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = new Color(248f / 255f, 250f / 255f, 252f / 255f);

            // Nền tối mờ phía sau text
            var bgGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
            bgGo.name = "Background";
            bgGo.transform.SetParent(labelGo.transform, false);
            bgGo.transform.localPosition = new Vector3(0f, 0f, 0.02f);
            float approxWidth = text.Length * 0.026f;
            bgGo.transform.localScale = new Vector3(approxWidth, 0.16f, 1f);

            var matBg = CreateTransparentMaterial(new Color(8f / 255f, 15f / 255f, 30f / 255f, 0.85f), 0f);
            bgGo.GetComponent<MeshRenderer>().sharedMaterial = matBg;

            active3DLabels.Add(labelGo.transform);
        }

        void CreateCurvedSegment(Transform parent, Vector3 from, Vector3 to, Material mat, float thickness)
        {
            var seg = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            seg.transform.SetParent(parent, false);
            seg.transform.localPosition = (from + to) * 0.5f;
            Vector3 dir = to - from;
            seg.transform.up = dir.normalized;
            seg.transform.localScale = new Vector3(thickness, dir.magnitude * 0.5f, thickness);
            seg.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }

        void SetHeader(string title, string desc)
        {
            if (microTitleText != null) microTitleText.text = title;
            if (microDescText != null) microDescText.text = desc;
        }

        Material CreateLitMaterial(Color col, float smoothness)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var m = new Material(shader) { name = "MicroLitMat", hideFlags = HideFlags.DontSave };
            m.color = col;
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            return m;
        }

        Material CreateTransparentMaterial(Color col, float smoothness)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var m = new Material(shader) { name = "MicroTransMat", hideFlags = HideFlags.DontSave };
            m.color = col;
            m.SetFloat("_Surface", 1f); // Transparent
            m.SetFloat("_Blend", 0f); // Alpha blend
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.renderQueue = (int)RenderQueue.Transparent;
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            return m;
        }
    }
}
