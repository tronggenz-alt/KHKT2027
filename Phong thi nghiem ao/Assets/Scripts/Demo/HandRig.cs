using UnityEngine;

namespace Khkt.Demo
{
    /// <summary>
    /// Bàn tay ảo đeo găng nitrile xanh (tay phải), mesh liền dựng lúc chạy bằng cùng bộ dựng với cánh tay MediaPipe.
    /// Ngón tay ôm quanh vật khi cầm đồ.
    /// Hệ tọa độ: +z là hướng các ngón tay, +y là mu bàn tay, lòng bàn tay quay xuống (-y). Gốc ở giữa lòng bàn tay.
    /// </summary>
    public class HandRig : MonoBehaviour
    {
        public Color gloveColor = new Color(0.27f, 0.56f, 0.93f);
        [Range(0, 1)] public float curl;

        /// <summary>Điểm nắm: dụng cụ đang cầm treo ở đây.</summary>
        public Transform Grip { get; private set; }

        /// <summary>Cổ tay trong hệ trục của găng: cánh tay ảo với tới đây khi thay găng cầm vật.</summary>
        public static readonly Vector3 WristLocal = new Vector3(0, 0.004f, -0.066f);
        public Vector3 WristWorld => transform.TransformPoint(local[0]);

        readonly Vector3[] local = new Vector3[21];
        TubeMesh mesh;
        HandSkin skin;
        float shownCurl, builtCurl = -1f;

        void Awake()
        {
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            var glove = new Material(lit) { hideFlags = HideFlags.DontSave };
            glove.SetColor("_BaseColor", gloveColor);
            glove.SetFloat("_Smoothness", 0.55f);

            mesh = new TubeMesh(1, "Găng tay");
            skin = new HandSkin(mesh, 0);
            mesh.Finish();
            ArmRig.MakeRenderer(transform, "Găng tay", mesh.Mesh, new[] { glove });

            Grip = new GameObject("Điểm nắm").transform;
            Grip.SetParent(transform, false);
            Grip.localPosition = new Vector3(0, -0.038f, 0.04f);
            Rebuild();
        }

        void Update()
        {
            shownCurl = Mathf.MoveTowards(shownCurl, curl, 6f * Time.unscaledDeltaTime);
            if (Mathf.Abs(shownCurl - builtCurl) > 1e-4f) Rebuild();
        }

        /// <summary>
        /// Dáng găng: curl 0 thả lỏng hơi khum, 0,3 sắp cầm, 1 ôm quanh vật cỡ cốc thí nghiệm, ngón cái khép đối diện.
        /// </summary>
        void Rebuild()
        {
            builtCurl = shownCurl;
            HandAnatomy.Pose(local, 0.18f + 0.82f * shownCurl, 0.3f - 0.2f * shownCurl, 0.25f + 0.65f * shownCurl, 1f);
            for (int i = 0; i < 21; i++) local[i] += WristLocal;
            skin.Write(local, Vector3.forward);
            mesh.Apply();
        }

        /// <summary>21 khớp hiện tại theo toạ độ thế giới (cùng chỉ số MediaPipe).</summary>
        public void WorldJoints(Vector3[] world)
        {
            for (int i = 0; i < 21; i++) world[i] = transform.TransformPoint(local[i]);
        }
    }
}
