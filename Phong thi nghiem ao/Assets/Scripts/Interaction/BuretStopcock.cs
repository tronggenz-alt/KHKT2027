using System;
using Khkt.Input;
using Khkt.Simulation;
using UnityEngine;

namespace Khkt.Interaction
{
    /// <summary>
    /// Buret và khóa buret. Dung dịch nằm trong LabContainer của buret; số chỉ trên vạch suy ra từ
    /// thể tích: mặt khum ở vạch 0 khi buret chứa zeroVolumeMl (tính cả đoạn dưới vạch 50 và đầu buret).
    ///
    /// VR: chỉ tia vào khóa và giữ cò (BeginTurn/EndTurn), vặn cổ tay để mở. Máy tính: AdjustOpening().
    /// Khóa chỉ mở được khi buret đã lắp trên giá. Thả tay thì khóa GIỮ NGUYÊN độ mở như khóa thật.
    /// Giọt rơi thẳng xuống từ đầu buret vào vật chứa bên dưới; không có gì bên dưới thì rơi ra bàn.
    /// </summary>
    public class BuretStopcock : MonoBehaviour
    {
        public LabContainer container;
        [Tooltip("Có thể để trống khi chạy chế độ máy tính")]
        public ControllerSource source;
        public HapticFeedback haptics;

        [Header("Buret")]
        [Tooltip("Thể tích trong buret khi mặt khum ở đúng vạch 0 (ml)")]
        public float zeroVolumeMl = 60.9f;
        [Tooltip("Vạch cuối cùng trên thang chia (ml)")]
        public float lastMarkMl = 50f;
        [Tooltip("Đầu buret; giọt rơi thẳng xuống từ đây")]
        public Transform tip;
        public LayerMask receiverMask = ~0;
        [Tooltip("Buret đang được lắp trên giá (do ổ đặt cập nhật)")]
        public bool mounted;

        [Header("Khóa")]
        public float fullOpenTwistDeg = 90f;
        public bool invertTwist;
        public float maxFlowMlPerS = 0.6f;
        public float fineJoystickRate = 0.15f;

        [Header("Giọt")]
        public float dropMl = Solution.DropMl;
        public float dropJitterMl = 0.004f;
        public int randomSeed = 1;

        public float Opening { get; private set; }
        /// <summary>Số chỉ tại đáy mặt khum (ml), 0 ở trên. Âm nghĩa là mặt khum đang ở trên vạch 0.</summary>
        public float ReadingMl => zeroVolumeMl - (container != null ? container.VolumeMl : 0f);
        public bool HasLiquid => container != null && container.VolumeMl > 0.3f;
        /// <summary>Tổng thể tích đã chảy ra khỏi buret kể từ lần ResetCounters gần nhất.</summary>
        public float DeliveredMl { get; private set; }
        public int DropCount { get; private set; }
        public float FlowMlPerS => mounted && HasLiquid ? maxFlowMlPerS * Opening * Opening : 0f;
        public bool IsStream => FlowMlPerS >= 4 * dropMl;
        public bool IsTurning { get; private set; }

        /// <summary>Mỗi giọt: (lượng dung dịch, vật nhận; null = rơi ra ngoài).</summary>
        public event Action<Solution, LabContainer> DropFell;

        Quaternion grabRotation;
        float grabOpening, pendingMl, nextDropMl;
        System.Random rng;

        void Awake()
        {
            rng = new System.Random(randomSeed);
            nextDropMl = NextDrop();
        }

        public void ResetCounters()
        {
            DeliveredMl = 0;
            DropCount = 0;
            pendingMl = 0;
        }

        public void BeginTurn()
        {
            IsTurning = true;
            grabOpening = Opening;
            if (source != null) grabRotation = source.Latest.UnityRotation;
        }

        public void EndTurn() => IsTurning = false;

        public void AdjustOpening(float delta)
        {
            if (!mounted) delta = -1f;  // buret chưa lắp lên giá: không cho mở
            Opening = Mathf.Clamp01(Opening + delta);
            grabOpening = Opening;
            if (source != null) grabRotation = source.Latest.UnityRotation;
        }

        public void Close() => AdjustOpening(-1f);

        void Update()
        {
            if (!mounted) Opening = 0;
            if (IsTurning && source != null && mounted)
            {
                float twist = TwistAboutForward(Quaternion.Inverse(grabRotation) * source.Latest.UnityRotation);
                if (invertTwist) twist = -twist;
                grabOpening += source.Latest.Joy1.y * fineJoystickRate * Time.deltaTime;
                Opening = Mathf.Clamp01(grabOpening + twist / fullOpenTwistDeg);
            }

            pendingMl += FlowMlPerS * Time.deltaTime;
            bool dropwise = !IsStream;
            while (pendingMl >= nextDropMl && HasLiquid)
            {
                pendingMl -= nextDropMl;
                var drop = container.Take(nextDropMl);
                DeliveredMl += drop.volumeMl;
                DropCount++;
                var receiver = Receiver();
                if (receiver != null) receiver.Add(drop);
                DropFell?.Invoke(drop, receiver);
                if (dropwise && haptics != null) haptics.Drop();
                nextDropMl = NextDrop();
            }
            if (!HasLiquid) pendingMl = 0;
        }

        /// <summary>Vật chứa ngay dưới đầu buret (null nếu không có).</summary>
        public LabContainer Receiver()
        {
            Transform from = tip != null ? tip : transform;
            if (Physics.Raycast(from.position, Vector3.down, out RaycastHit hit, 0.5f, receiverMask, QueryTriggerInteraction.Ignore))
            {
                var c = hit.collider.GetComponentInParent<LabContainer>();
                if (c != null && c != container) return c;
            }
            return null;
        }

        float NextDrop()
        {
            if (rng == null) rng = new System.Random(randomSeed);  // có thể mất sau khi Unity nạp lại script lúc đang chạy
            double u1 = 1.0 - rng.NextDouble(), u2 = rng.NextDouble();
            double z = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2 * Math.PI * u2);
            return Mathf.Max(0.02f, dropMl + (float)z * dropJitterMl);
        }

        static float TwistAboutForward(Quaternion q)
        {
            float angle = 2f * Mathf.Atan2(q.z, q.w) * Mathf.Rad2Deg;
            return Mathf.DeltaAngle(0, angle);
        }
    }
}
