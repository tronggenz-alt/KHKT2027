using System.Collections;
using Khkt.Environment;
using Khkt.Simulation.Bio;
using UnityEngine;

namespace Khkt.Demo.Bio
{
    /// <summary>Kính hiển vi bấm được: mỗi lần bấm xoay mâm sang vật kính kế tiếp (4× → 10× → 40× → 100×) có tiếng "tách".</summary>
    public class MicroscopeController : MonoBehaviour, IDemoUsable
    {
        public Transform nosepiece;
        public int currentObjectiveIndex;  // 0 = 4×, 1 = 10×, 2 = 40×, 3 = 100× dầu
        public float eyepieceMag = 10f;
        public float fieldNumberMm = 18f;

        static readonly ObjectiveType[] Objectives = { ObjectiveType.Scan4x, ObjectiveType.Low10x, ObjectiveType.High40x, ObjectiveType.Oil100x };
        bool turning;

        public string UseHint
        {
            get
            {
                var (mag, _, color, isOil) = Optics.GetObjectiveProps(Objectives[currentObjectiveIndex]);
                double total = Optics.TotalMagnification(eyepieceMag, mag);
                double fov = Optics.FieldOfViewMm(fieldNumberMm, mag);
                return $"Kính hiển vi: vật kính {mag}× (vòng {color}{(isOil ? ", dầu soi" : "")}), phóng đại {total}×, thị trường {fov:0.##} mm. Bấm để đổi vật kính.";
            }
        }

        public void Use() => CycleObjective();

        /// <summary>Độ phóng đại tổng (thị kính × vật kính) đang dùng.</summary>
        public double CurrentMagnification => Optics.TotalMagnification(eyepieceMag, Optics.GetObjectiveProps(Objectives[currentObjectiveIndex]).mag);

        /// <summary>Đường kính thị trường (mm) ở vật kính đang dùng.</summary>
        public double CurrentFovMm => Optics.FieldOfViewMm(fieldNumberMm, Optics.GetObjectiveProps(Objectives[currentObjectiveIndex]).mag);

        /// <summary>Độ lệch tiêu cự hiện tại (mm), dùng cho ảnh tiêu bản mờ / rõ.</summary>
        public float FocusOffsetMm { get; private set; }

        public void SetFocus(float offsetMm) => FocusOffsetMm = offsetMm;

        public void CycleObjective()
        {
            if (turning) return;
            currentObjectiveIndex = (currentObjectiveIndex + 1) % Objectives.Length;
            StartCoroutine(Turn());
        }

        IEnumerator Turn()
        {
            if (nosepiece == null) yield break;
            turning = true;
            var from = nosepiece.localRotation;
            var to = Quaternion.Euler(0, currentObjectiveIndex * 90f, 0);
            for (float t = 0; t < 0.25f; t += Time.deltaTime)
            {
                nosepiece.localRotation = Quaternion.Slerp(from, to, Easing.Standard(t / 0.25f));
                yield return null;
            }
            nosepiece.localRotation = to;
            turning = false;
        }
    }
}
