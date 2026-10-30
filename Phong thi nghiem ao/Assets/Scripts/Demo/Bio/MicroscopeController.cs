using System.Collections;
using Khkt.Demo;
using Khkt.Simulation.Bio;
using UnityEngine;

namespace Khkt.Demo.Bio
{
    public class MicroscopeController : MonoBehaviour, IDemoUsable
    {
        [Header("Mâm vật kính xoay")]
        public Transform nosepiece;
        public int currentObjectiveIndex = 1; // 0=4x, 1=10x, 2=40x, 3=100x

        [Header("Thị kính & Tiêu cự")]
        public float eyepieceMag = 10f;
        public float fieldNumberMm = 20f;
        public float focusOffsetUm = 0f;

        [Header("Tiêu bản hiển thị")]
        public Renderer slideRenderer;

        static readonly ObjectiveType[] Objectives =
        {
            ObjectiveType.Scan4x,
            ObjectiveType.Low10x,
            ObjectiveType.High40x,
            ObjectiveType.Oil100x
        };

        public string UseHint
        {
            get
            {
                var (mag, na, color, isOil) = Optics.GetObjectiveProps(Objectives[currentObjectiveIndex]);
                double total = Optics.TotalMagnification(eyepieceMag, mag);
                double fov = Optics.FieldOfViewMm(fieldNumberMm, mag);
                return $"Kính hiển vi: Vật kính {mag}× (vòng {color}) | Phóng đại {total}× | Thị trường {fov:F1} mm. Bấm để đổi vật kính.";
            }
        }

        public void Use()
        {
            // Đổi vật kính xoay tuần hoàn (4x -> 10x -> 40x -> 100x)
            currentObjectiveIndex = (currentObjectiveIndex + 1) % Objectives.Length;
            StartCoroutine(RotateNosepieceRoutine());
        }

        IEnumerator RotateNosepieceRoutine()
        {
            if (nosepiece == null) yield break;

            Quaternion startRot = nosepiece.localRotation;
            Quaternion targetRot = Quaternion.Euler(0, currentObjectiveIndex * 90f, 0);

            float duration = 0.25f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                // Đường cong cubic-bezier standard
                nosepiece.localRotation = Quaternion.Slerp(startRot, targetRot, t);
                yield return null;
            }

            nosepiece.localRotation = targetRot;
            UpdateSlideFocus();
        }

        public void AdjustFocus(float deltaUm)
        {
            focusOffsetUm += deltaUm;
            UpdateSlideFocus();
        }

        void UpdateSlideFocus()
        {
            if (slideRenderer == null || slideRenderer.sharedMaterial == null) return;

            var (mag, na, _, isOil) = Optics.GetObjectiveProps(Objectives[currentObjectiveIndex]);
            double dof = Optics.DepthOfFieldUm(eyepieceMag * mag, na, isOil);

            slideRenderer.material.SetFloat("_FocusOffset", focusOffsetUm);
            slideRenderer.material.SetFloat("_DOF", (float)dof);
        }
    }
}
