using System.Collections;
using Khkt.Demo;
using Khkt.Interaction;
using UnityEngine;

namespace Khkt.Environment.Tang2
{
    /// <summary>
    /// Điều khiển thang máy di chuyển giữa tầng 1 (y = 0) và tầng 2 (y = 3,8 m).
    /// Hoạt cảnh: đóng cửa 0,8 s -> mờ màn hình 0,35 s -> đổi tầng -> ting -> đổi số LED -> mở cửa 0,8 s.
    /// </summary>
    public class ElevatorController : MonoBehaviour, IDemoUsable
    {
        [Header("Cửa thang")]
        public Transform[] leaves;          // Cánh cửa trượt
        public Vector3[] openOffsets;       // Độ lệch khi mở
        public float doorDuration = 0.8f;
        public float fadeDuration = 0.35f;

        [Header("Hiển thị & Âm thanh")]
        public TextMesh[] floorIndicators;  // Bảng LED số tầng (trên cửa mỗi tầng)
        public AudioSource audioSource;

        [Header("Trạng thái")]
        public int currentFloor = 1;        // 1 hoặc 2
        public bool isMoving;
        public bool isOpen = true;

        const float Floor1Y = 0f;
        const float Floor2Y = 3.8f;

        void Start()
        {
            UpdateIndicators();
        }

        public string UseHint => isMoving ? "Thang máy đang chuyển tầng..." : (currentFloor == 1 ? "Đi lên Tầng 2" : "Đi xuống Tầng 1");

        public void Use()
        {
            if (isMoving) return;
            int targetFloor = currentFloor == 1 ? 2 : 1;
            StartCoroutine(MoveToFloor(targetFloor));
        }

        public void CallToFloor(int floor)
        {
            if (isMoving || currentFloor == floor) return;
            StartCoroutine(MoveToFloor(floor));
        }

        IEnumerator MoveToFloor(int targetFloor)
        {
            isMoving = true;

            // 1. Đóng cửa
            if (isOpen)
            {
                yield return AnimateDoors(false);
                isOpen = false;
            }

            // 2. Chờ & Mờ màn hình chuyển tầng
            yield return new WaitForSeconds(0.15f);
            var viewer = FindAnyObjectByType<DesktopViewer>();
            CharacterController cc = viewer != null ? viewer.GetComponent<CharacterController>() : null;

            // Nếu người chơi đang đứng trong cabin thang máy (khoảng x 28.4..29.8, z -2.2..-0.8)
            bool playerInside = false;
            if (viewer != null)
            {
                Vector3 vp = viewer.transform.position;
                if (vp.x >= 28.0f && vp.x <= 30.0f && vp.z >= -2.4f && vp.z <= -0.6f)
                {
                    playerInside = true;
                }
            }

            yield return new WaitForSeconds(fadeDuration);

            // Dịch chuyển người chơi nếu đang trong cabin
            currentFloor = targetFloor;
            UpdateIndicators();
            PlayChime();

            if (playerInside && cc != null)
            {
                cc.enabled = false;
                Vector3 p = viewer.transform.position;
                p.y = targetFloor == 2 ? Floor2Y : Floor1Y;
                viewer.transform.position = p;
                cc.enabled = true;
            }

            yield return new WaitForSeconds(fadeDuration);

            // 3. Mở cửa tầng đích
            yield return AnimateDoors(true);
            isOpen = true;
            isMoving = false;
        }

        IEnumerator AnimateDoors(bool open)
        {
            if (leaves == null || openOffsets == null) yield break;
            float elapsed = 0f;
            Vector3[] startPos = new Vector3[leaves.Length];
            Vector3[] endPos = new Vector3[leaves.Length];

            for (int i = 0; i < leaves.Length; i++)
            {
                if (leaves[i] == null) continue;
                startPos[i] = leaves[i].localPosition;
                // Base position là lúc đóng (Vector3.zero theo local)
                endPos[i] = open ? openOffsets[i] : Vector3.zero;
            }

            while (elapsed < doorDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / doorDuration);
                float ease = Easing.Standard(t);
                for (int i = 0; i < leaves.Length; i++)
                {
                    if (leaves[i] == null) continue;
                    leaves[i].localPosition = Vector3.Lerp(startPos[i], endPos[i], ease);
                }
                yield return null;
            }

            for (int i = 0; i < leaves.Length; i++)
            {
                if (leaves[i] != null) leaves[i].localPosition = endPos[i];
            }
        }

        void UpdateIndicators()
        {
            if (floorIndicators == null) return;
            string txt = currentFloor == 2 ? "2F" : "1F";
            foreach (var ind in floorIndicators)
            {
                if (ind != null) ind.text = txt;
            }
        }

        void PlayChime()
        {
            if (audioSource != null && audioSource.clip != null)
            {
                audioSource.Play();
            }
        }
    }
}
