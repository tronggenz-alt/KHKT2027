using UnityEngine;
using Khkt.Demo.Bio.Portal;
using static Khkt.EditorTools.LabKit;

namespace Khkt.EditorTools
{
    /// <summary>
    /// Dựng Cổng vào Phòng Sinh học 3D (BioPortal) tại khu giải phẫu tầng 2:
    ///   • Vị trí thế giới: (18.3, 3.8, 12.6) trong khu giải phẫu.
    ///   • Gắn component BioPortal quản lý cự ly 10 Hz, vòng sáng sàn 1 draw call,
    ///     và thẻ mời kính mờ responsive.
    /// </summary>
    public static partial class LabSceneBuilder
    {
        public static GameObject BuildFloor2BioPortal(Transform anatomyArea)
        {
            return BuildBioPortal(anatomyArea, new Vector3(18.3f, 0f, 12.6f));
        }

        public static GameObject BuildBioPortal(Transform parent, Vector3 localPos)
        {
            var portalGo = new GameObject("Cổng vào Phòng Sinh học 3D");
            if (parent != null) portalGo.transform.SetParent(parent, false);
            portalGo.transform.localPosition = localPos;
            portalGo.transform.localRotation = Quaternion.identity;

            var portal = portalGo.AddComponent<BioPortal>();
            portal.EnsureFloorRing();

            return portalGo;
        }
    }
}
