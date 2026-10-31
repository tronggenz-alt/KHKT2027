using UnityEngine;

namespace Khkt.Environment.Tang2
{
    /// <summary>
    /// Thang máy hai điểm dừng chạy như thật: cabin là vật thật đi lên / xuống trong giếng, người chơi đứng trong cabin đi theo
    /// (không dịch chuyển, không mờ màn hình). Cửa tầng và cửa cabin mở / đóng cùng nhau, có cảm biến chặn cửa khi có người
    /// đứng ở ngưỡng. Màn hình trong cabin báo "ĐANG LÊN TẦNG 2", bảng số tầng ngoài sảnh báo vị trí cabin.
    ///
    /// Chạy trước DesktopViewer (thứ tự −50) để người chơi được nâng cùng sàn cabin trước khi tự tính trọng lực.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class ElevatorController : MonoBehaviour
    {
        [Header("Cabin")]
        public Transform cabin;
        public float[] floorY = { 0f, 3.8f };
        public Vector2 cabinHalfSize = new Vector2(0.57f, 0.57f);  // nửa lòng cabin theo x, z (để biết người chơi có trong cabin)
        public float travelTime = 5.2f;                             // giây cho một tầng (tăng tốc 1 s, chạy đều, giảm tốc 1 s)

        [Header("Cửa")]
        public Transform[] cabinLeaves;     // 2 cánh, trượt theo z cục bộ
        public Transform[] landingLeaves;   // 2 cánh mỗi tầng: [tầng 1 nam, tầng 1 bắc, tầng 2 nam, tầng 2 bắc]
        public float leafTravel = 0.41f;
        public float doorTime = 1.1f;
        public float holdOpen = 8f;          // tự đóng cửa sau khi đứng yên
        public Vector2 doorwayX = new Vector2(28.25f, 28.7f), doorwayZ = new Vector2(-1.85f, -1.05f);

        [Header("Hiển thị, âm thanh")]
        public TextMesh cabinFloorDisplay, cabinStatusDisplay, cabinPanelDisplay;
        public TextMesh[] landingDisplays;
        public AudioSource audioSource;

        public int CurrentFloor { get; private set; }
        public bool Moving => state == State.Moving;
        public string StatusText { get; private set; } = "";

        enum State { Idle, Closing, Moving, Opening }
        State state = State.Idle;
        float door;          // 0 đóng, 1 mở
        float idleTimer, moveT;
        int target = -1;
        float fromY, toY;
        float[] leafBase;
        CharacterController player;
        AudioClip chime, hum;

        void Awake()
        {
            leafBase = new float[(cabinLeaves?.Length ?? 0) + (landingLeaves?.Length ?? 0)];
            int k = 0;
            if (cabinLeaves != null) foreach (var l in cabinLeaves) leafBase[k++] = l != null ? l.localPosition.z : 0;
            if (landingLeaves != null) foreach (var l in landingLeaves) leafBase[k++] = l != null ? l.localPosition.z : 0;
            chime = MakeChime();
            hum = MakeHum();
            CurrentFloor = 0;
            door = 1f;
            ApplyDoors();
            UpdateDisplays(floorY[0]);
        }

        // ----- Yêu cầu từ nút bấm -----

        /// <summary>Bấm nút gọi ở sảnh tầng f (0 = tầng 1, 1 = tầng 2).</summary>
        public void Call(int f) => Request(f);

        /// <summary>Bấm số tầng trong cabin.</summary>
        public void Go(int f) => Request(f);

        public void OpenDoors()
        {
            if (state == State.Moving) return;
            idleTimer = 0;
            if (state == State.Closing || state == State.Idle)
            {
                state = State.Opening;
                target = -1;
            }
        }

        public void CloseDoors()
        {
            if (state == State.Idle && door > 0) { state = State.Closing; }
        }

        void Request(int f)
        {
            if (f < 0 || f >= floorY.Length) return;
            if (state == State.Moving) return;
            if (f == CurrentFloor)
            {
                target = -1;
                OpenDoors();
                return;
            }
            target = f;
            state = door > 0 ? State.Closing : State.Moving;
            if (state == State.Moving) BeginMove();
        }

        // ----- Vòng lặp -----

        void Update()
        {
            float dt = Time.deltaTime;
            switch (state)
            {
                case State.Idle:
                    if (door > 0 && (idleTimer += dt) > holdOpen && !SomeoneInDoorway()) state = State.Closing;
                    break;
                case State.Opening:
                    door = Mathf.MoveTowards(door, 1f, dt / doorTime);
                    if (door >= 1f) { state = State.Idle; idleTimer = 0; }
                    ApplyDoors();
                    break;
                case State.Closing:
                    if (SomeoneInDoorway()) { state = State.Opening; target = -1; break; }  // cảm biến cửa: có người thì mở lại
                    door = Mathf.MoveTowards(door, 0f, dt / doorTime);
                    ApplyDoors();
                    if (door <= 0f)
                    {
                        if (target >= 0) BeginMove();
                        else state = State.Idle;
                    }
                    break;
                case State.Moving:
                    MoveCabin(dt);
                    break;
            }
        }

        void BeginMove()
        {
            state = State.Moving;
            moveT = 0;
            fromY = cabin.localPosition.y;
            toY = floorY[target];
            if (audioSource != null && hum != null)
            {
                audioSource.clip = hum;
                audioSource.loop = true;
                audioSource.volume = 0.12f;
                audioSource.Play();
            }
        }

        void MoveCabin(float dt)
        {
            moveT += dt;
            float s = Profile(Mathf.Clamp01(moveT / travelTime), 1f / travelTime);
            float y = Mathf.Lerp(fromY, toY, s);
            float dy = y - cabin.localPosition.y;
            bool carry = PlayerInCabin();
            var p = cabin.localPosition;
            p.y = y;
            cabin.localPosition = p;
            if (carry && Mathf.Abs(dy) > 0f) CarryPlayer(dy);
            UpdateDisplays(y);
            if (moveT >= travelTime)
            {
                CurrentFloor = target;
                target = -1;
                state = State.Opening;
                idleTimer = 0;
                if (audioSource != null)
                {
                    audioSource.Stop();
                    audioSource.loop = false;
                    audioSource.volume = 0.5f;
                    if (chime != null) audioSource.PlayOneShot(chime);
                }
                UpdateDisplays(y);
            }
        }

        /// <summary>Vận tốc hình thang: tăng tốc trong khoảng a (tỉ lệ thời gian), chạy đều, giảm tốc a. Trả về quãng đường 0..1.</summary>
        static float Profile(float t, float a)
        {
            a = Mathf.Clamp(a, 0.05f, 0.45f);
            float v = 1f / (1f - a);  // vận tốc đều (đơn vị quãng / đơn vị thời gian)
            if (t < a) return 0.5f * v / a * t * t;
            if (t > 1f - a) { float u = 1f - t; return 1f - 0.5f * v / a * u * u; }
            return 0.5f * v * a + v * (t - a);
        }

        void ApplyDoors()
        {
            float e = door * door * (3f - 2f * door);  // êm đầu, êm cuối
            int k = 0;
            void Slide(Transform[] leaves, int floorIndex)
            {
                if (leaves == null) return;
                for (int i = 0; i < leaves.Length; i++, k++)
                {
                    var l = leaves[i];
                    if (l == null) continue;
                    // cửa tầng chỉ mở ở tầng có cabin
                    bool active = floorIndex < 0 || i / 2 == CurrentFloor;
                    float side = Mathf.Sign(leafBase[k]);
                    var lp = l.localPosition;
                    lp.z = leafBase[k] + side * leafTravel * (active ? e : 0f);
                    l.localPosition = lp;
                }
            }
            Slide(cabinLeaves, -1);
            Slide(landingLeaves, 0);
        }

        void UpdateDisplays(float y)
        {
            int shown = Mathf.Abs(y - floorY[0]) < Mathf.Abs(y - floorY[floorY.Length - 1]) ? 0 : 1;
            string arrow = state == State.Moving ? (toY > fromY ? "▲" : "▼") : "";
            string num = (shown + 1).ToString();
            if (state == State.Moving)
                StatusText = toY > fromY ? $"ĐANG LÊN TẦNG {target + 1}" : $"ĐANG XUỐNG TẦNG {target + 1}";
            else
                StatusText = CurrentFloor == 1 ? "TẦNG 2 · KHU SINH HỌC" : "TẦNG 1 · KHU HÓA HỌC";
            if (cabinFloorDisplay != null) cabinFloorDisplay.text = arrow + num;
            if (cabinStatusDisplay != null) cabinStatusDisplay.text = StatusText;
            if (cabinPanelDisplay != null) cabinPanelDisplay.text = num;
            if (landingDisplays != null)
                foreach (var d in landingDisplays)
                    if (d != null) d.text = arrow + num;
        }

        // ----- Người chơi -----

        CharacterController Player()
        {
            if (player == null)
            {
                var v = FindAnyObjectByType<DesktopViewer>();
                if (v != null) player = v.GetComponent<CharacterController>();
            }
            return player;
        }

        bool PlayerInCabin()
        {
            var cc = Player();
            if (cc == null) return false;
            Vector3 local = cabin.InverseTransformPoint(cc.transform.position);
            return Mathf.Abs(local.x) <= cabinHalfSize.x && Mathf.Abs(local.z) <= cabinHalfSize.y && local.y > -0.3f && local.y < 1.2f;
        }

        bool SomeoneInDoorway()
        {
            var cc = Player();
            if (cc == null) return false;
            Vector3 p = cc.transform.position;
            float fy = floorY[CurrentFloor];
            return p.x > doorwayX.x && p.x < doorwayX.y && p.z > doorwayZ.x - 0.15f && p.z < doorwayZ.y + 0.15f && Mathf.Abs(p.y - fy) < 1f;
        }

        void CarryPlayer(float dy)
        {
            var cc = Player();
            if (cc == null) return;
            // dời thẳng vị trí (tắt bộ điều khiển một nhịp để Unity không ghi đè), người chơi luôn đứng trên sàn cabin
            cc.enabled = false;
            cc.transform.position += Vector3.up * dy;
            cc.enabled = true;
        }

        // ----- Âm thanh sinh bằng code (không cần tệp âm thanh) -----

        static AudioClip MakeChime()
        {
            const int rate = 22050;
            int n = (int)(rate * 1.2f);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / rate;
                float a = t < 0.45f ? Mathf.Sin(2 * Mathf.PI * 1046.5f * t) * Mathf.Exp(-t * 5f)
                                    : Mathf.Sin(2 * Mathf.PI * 784f * (t - 0.45f)) * Mathf.Exp(-(t - 0.45f) * 4f);
                data[i] = 0.35f * a;
            }
            var clip = AudioClip.Create("Chuông thang máy", n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static AudioClip MakeHum()
        {
            const int rate = 22050;
            int n = rate * 2;  // 2 s lặp liền (tần số chia hết cho 0,5 Hz)
            var data = new float[n];
            var rnd = new System.Random(3);
            float lp = 0;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / rate;
                lp += ((float)rnd.NextDouble() * 2f - 1f - lp) * 0.02f;  // tiếng gió lọc thấp
                data[i] = 0.25f * Mathf.Sin(2 * Mathf.PI * 110f * t) * (0.8f + 0.2f * Mathf.Sin(2 * Mathf.PI * 0.5f * t)) + 0.6f * lp;
            }
            var clip = AudioClip.Create("Tiếng chạy thang máy", n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
