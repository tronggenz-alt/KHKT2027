using UnityEngine;

namespace Khkt.Demo
{
    /// <summary>
    /// Bàn tay ảo đeo găng nitrile xanh, dựng bằng khối cơ bản lúc chạy. Ngón tay co lại khi cầm đồ.
    /// Hệ tọa độ: +z là hướng các ngón tay, +y là mu bàn tay, lòng bàn tay quay xuống (-y).
    /// </summary>
    public class HandRig : MonoBehaviour
    {
        public Color gloveColor = new Color(0.27f, 0.56f, 0.93f);
        [Range(0, 1)] public float curl;

        /// <summary>Điểm nắm: dụng cụ đang cầm treo ở đây.</summary>
        public Transform Grip { get; private set; }

        Transform[] proximal, distal;
        Transform thumbA, thumbB;
        float shownCurl;

        void Awake()
        {
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            var glove = new Material(lit) { hideFlags = HideFlags.DontSave };
            glove.SetColor("_BaseColor", gloveColor);
            glove.SetFloat("_Smoothness", 0.55f);
            var cuff = new Material(lit) { hideFlags = HideFlags.DontSave };
            cuff.SetColor("_BaseColor", gloveColor * 0.8f);
            cuff.SetFloat("_Smoothness", 0.45f);

            Part(PrimitiveType.Sphere, transform, "Lòng bàn tay", new Vector3(0, 0, 0), new Vector3(0.082f, 0.03f, 0.095f), Vector3.zero, glove);
            Part(PrimitiveType.Sphere, transform, "Cổ tay", new Vector3(0, 0.002f, -0.055f), new Vector3(0.058f, 0.032f, 0.06f), Vector3.zero, glove);
            Part(PrimitiveType.Capsule, transform, "Cổ găng", new Vector3(0, 0.003f, -0.085f), new Vector3(0.05f, 0.022f, 0.036f), new Vector3(90, 0, 0), cuff);

            proximal = new Transform[4];
            distal = new Transform[4];
            float[] xs = { -0.027f, -0.009f, 0.009f, 0.026f };
            float[] lens = { 0.034f, 0.04f, 0.038f, 0.03f };
            for (int i = 0; i < 4; i++)
            {
                var j1 = Joint(transform, $"Ngón {i + 1}", new Vector3(xs[i], 0, 0.042f), new Vector3(0, (i - 1.5f) * 4f, 0));
                Part(PrimitiveType.Capsule, j1, "Đốt 1", new Vector3(0, 0, lens[i] / 2), new Vector3(0.017f, lens[i] / 2 + 0.006f, 0.017f), new Vector3(90, 0, 0), glove);
                var j2 = Joint(j1, "Khớp", new Vector3(0, 0, lens[i]), Vector3.zero);
                Part(PrimitiveType.Capsule, j2, "Đốt 2", new Vector3(0, 0, lens[i] * 0.4f), new Vector3(0.0155f, lens[i] * 0.4f + 0.006f, 0.0155f), new Vector3(90, 0, 0), glove);
                proximal[i] = j1;
                distal[i] = j2;
            }
            thumbA = Joint(transform, "Ngón cái", new Vector3(-0.04f, -0.006f, 0.0f), new Vector3(0, -40, 0));
            Part(PrimitiveType.Capsule, thumbA, "Đốt 1", new Vector3(0, 0, 0.018f), new Vector3(0.019f, 0.022f, 0.019f), new Vector3(90, 0, 0), glove);
            thumbB = Joint(thumbA, "Khớp", new Vector3(0, 0, 0.034f), new Vector3(0, 10, 0));
            Part(PrimitiveType.Capsule, thumbB, "Đốt 2", new Vector3(0, 0, 0.014f), new Vector3(0.017f, 0.018f, 0.017f), new Vector3(90, 0, 0), glove);

            Grip = new GameObject("Điểm nắm").transform;
            Grip.SetParent(transform, false);
            Grip.localPosition = new Vector3(0, -0.035f, 0.045f);
        }

        void Update()
        {
            shownCurl = Mathf.MoveTowards(shownCurl, curl, 6f * Time.unscaledDeltaTime);
            for (int i = 0; i < 4; i++)
            {
                var e1 = proximal[i].localEulerAngles;
                proximal[i].localEulerAngles = new Vector3(15f + shownCurl * 65f, e1.y, 0);
                distal[i].localEulerAngles = new Vector3(10f + shownCurl * 75f, 0, 0);
            }
            thumbA.localEulerAngles = new Vector3(10f + shownCurl * 25f, -40f + shownCurl * 25f, 0);
            thumbB.localEulerAngles = new Vector3(shownCurl * 40f, 10f, 0);
        }

        static Transform Joint(Transform parent, string name, Vector3 pos, Vector3 euler)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = pos;
            t.localEulerAngles = euler;
            return t;
        }

        static void Part(PrimitiveType type, Transform parent, string name, Vector3 pos, Vector3 scale, Vector3 euler, Material m)
        {
            var g = GameObject.CreatePrimitive(type);
            g.name = name;
            Destroy(g.GetComponent<Collider>());
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localEulerAngles = euler;
            g.transform.localScale = scale;
            var r = g.GetComponent<Renderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }
}
