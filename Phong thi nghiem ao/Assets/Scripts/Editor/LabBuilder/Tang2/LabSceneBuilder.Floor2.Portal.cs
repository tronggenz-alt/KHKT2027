using System.Collections.Generic;
using UnityEngine;
using Khkt.Demo.Bio.Portal;
using static Khkt.EditorTools.LabKit;

namespace Khkt.EditorTools
{
    /// <summary>
    /// Dựng Cổng vào Phòng Sinh học 3D (BioPortal) và Phòng ảo tĩnh (HoloBioRoom) (GĐ1 & GĐ2):
    ///   • BioPortal (khung cổng 3D) đặt tại (17.3, 3.8, 13.55) ở góc đông bắc khu giải phẫu tầng 2.
    ///   • HoloBioRoom đặt tại y = -600, sàn tròn 10m, bệ hologram, 4 vòng sáng, chùm sáng trụ hở 3.9m,
    ///     bụi sáng <= 150 hạt, mô hình BodyParts3D 1.7m (bộ xương, hệ cơ, da).
    ///   • Liên kết BioPortal -> HoloBioRoom.
    /// </summary>
    public static partial class LabSceneBuilder
    {
        public static GameObject BuildFloor2BioPortal(Transform anatomyArea)
        {
            // Khung cổng 3D rộng 2 m cần chỗ trống: đặt ở góc đông bắc, lệch khỏi đầu bàn giải phẫu, mặt trước quay về giữa phòng
            var portalGo = BuildBioPortal(anatomyArea, new Vector3(17.3f, 0f, 13.55f));
            portalGo.transform.localRotation = Quaternion.Euler(0f, 205f, 0f);
            var portal = portalGo.GetComponent<BioPortal>();

            var holo = Object.FindAnyObjectByType<HoloBioRoom>(FindObjectsInactive.Include);
            if (holo != null && portal != null)
            {
                portal.targetHoloRoom = holo;
            }

            var trans = Object.FindAnyObjectByType<BioPortalTransition>(FindObjectsInactive.Include);
            if (trans != null && portal != null)
            {
                trans.portal = portal;
            }

            return portalGo;
        }

        public static GameObject BuildBioPortal(Transform parent, Vector3 localPos)
        {
            var portalGo = new GameObject("Cổng vào Phòng Sinh học 3D");
            if (parent != null) portalGo.transform.SetParent(parent, false);
            portalGo.transform.localPosition = localPos;
            portalGo.transform.localRotation = Quaternion.identity;

            // Vòng sáng và khung cổng do BioPortal tự dựng lúc chạy (vật liệu tạo bằng mã không lưu được vào cảnh)
            portalGo.AddComponent<BioPortal>();

            return portalGo;
        }

        public static GameObject BuildHoloBioRoom(Transform root)
        {
            var existing = root != null ? root.Find("HoloBioRoom") : null;
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            var holoGo = new GameObject("HoloBioRoom");
            if (root != null) holoGo.transform.SetParent(root, false);
            holoGo.transform.localPosition = new Vector3(0f, -600f, 0f);
            holoGo.transform.localRotation = Quaternion.identity;

            var holoRoom = holoGo.AddComponent<HoloBioRoom>();
            holoRoom.EnsureRoomHierarchy();

            // Dựng mô hình BodyParts3D đứng trên bệ cao 1.7m (xuong, co, da)
            var meshes = Bp3dImporter.ImportBodyParts3D();
            if (meshes != null)
            {
                var shReveal = Shader.Find("Khkt/Tang2/HoloReveal") ?? Shader.Find("Universal Render Pipeline/Lit");
                var matBone = Mat("T2_XuongNguoi_Holo", new Color(0.9f, 0.86f, 0.77f), 0.35f);
                var matMuscle = Mat("T2_CoNguoi_Holo", new Color(0.72f, 0.24f, 0.22f), 0.45f);
                var matSkin = Mat("T2_DaNguoi_Holo", new Color(0.92f, 0.74f, 0.62f), 0.4f);

                if (shReveal != null)
                {
                    matBone.shader = shReveal;
                    matMuscle.shader = shReveal;
                    matSkin.shader = shReveal;
                }

                var bodyRoot = Group("BodyParts3D_1.7m", holoGo.transform, new Vector3(0f, 0.14f, 0f));
                var skel = Bp3dBody(bodyRoot, "Bộ xương", meshes, "xuong", matBone, Vector3.zero, 1.7f, false, 0f);
                var musc = Bp3dBody(bodyRoot, "Hệ cơ", meshes, "co", matMuscle, Vector3.zero, 1.7f, false, 0f);
                var skin = Bp3dBody(bodyRoot, "Da", meshes, "da", matSkin, Vector3.zero, 1.7f, false, 0f);

                holoRoom.layerSkeleton = skel;
                holoRoom.layerMuscles = musc;
                holoRoom.layerSkin = skin;

                if (musc != null) musc.SetActive(false);
                if (skel != null) skel.SetActive(false);
            }

            // Gắn bộ chuyển cảnh BioPortalTransition (GĐ3)
            var transition = holoGo.AddComponent<BioPortalTransition>();
            transition.holoRoom = holoRoom;

            // Gán liên kết sang BioPortal
            var portal = Object.FindAnyObjectByType<BioPortal>(FindObjectsInactive.Include);
            if (portal != null)
            {
                portal.targetHoloRoom = holoRoom;
                transition.portal = portal;
            }

            holoGo.SetActive(false);
            return holoGo;
        }
    }
}
