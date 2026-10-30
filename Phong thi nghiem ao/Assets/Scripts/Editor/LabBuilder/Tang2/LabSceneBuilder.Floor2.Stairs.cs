using Khkt.Environment.Tang2;
using Khkt.Rendering;
using UnityEngine;
using static Khkt.EditorTools.LabKit;

namespace Khkt.EditorTools
{
    public static partial class LabSceneBuilder
    {
        static void BuildFloor2Stairs(Transform f2, Transform root)
        {
            // Lõi thang thuộc cả hai tầng (ZoneMap.Stairs, floor = -1).
            // Đặt các phần tĩnh của lõi thang vào nhóm "Lõi thang" ở root cảnh lõi để bao trùm từ y = 0 đến y = 7.4 m.
            var stairsGroup = Group("Lõi thang bộ và thang máy", root);

            const float x0 = 27.0f, x1 = 30.0f;
            const float z0 = -2.2f, z1 = 6.0f;
            const float hTotal = F2_Y + F2_H; // 7.4 m
            float cx = (x0 + x1) / 2; // 28.5 m
            float cz = (z0 + z1) / 2; // 1.9 m
            float lenZ = z1 - z0;     // 8.2 m
            float lenX = x1 - x0;     // 3.0 m

            // Sàn tầng 1 khu sảnh thang
            Box(stairsGroup, "Sàn tầng 1 sảnh thang", new Vector3(cx, -0.15f, cz), new Vector3(lenX, 0.3f, lenZ), floorHall);

            // Tường bao phía đông lõi thang (x = 30) suốt 2 tầng, có cửa sổ lớn chiếu sáng
            Box(stairsGroup, "Tường đông lõi thang (dưới)", new Vector3(x1, 0.45f, cz), new Vector3(T, 0.9f, lenZ), facade);
            Box(stairsGroup, "Tường đông lõi thang (trên)", new Vector3(x1, hTotal - 0.45f, cz), new Vector3(T, 0.9f, lenZ), facade);
            var glassStairs = Box(stairsGroup, "Cửa sổ kính lõi thang", new Vector3(x1, hTotal / 2, cz), new Vector3(0.02f, hTotal - 1.8f, lenZ - 0.4f), windowGlass, false);
            NoShadow(glassStairs);
            for (float z = z0 + 1.2f; z <= z1 - 1.2f; z += 1.6f)
                Box(stairsGroup, $"Đố kính thang {z:0.0}", new Vector3(x1, hTotal / 2, z), new Vector3(0.08f, hTotal - 1.8f, 0.06f), alu, false);

            // Tường nam lõi thang (z = -2.2, x 27..30)
            Box(stairsGroup, "Tường nam lõi thang", new Vector3(cx, hTotal / 2, z0), new Vector3(lenX, hTotal, T), facade);

            // Tường bắc lõi thang (z = 6.0, x 27..30)
            Box(stairsGroup, "Tường bắc lõi thang", new Vector3(cx, hTotal / 2, z1), new Vector3(lenX, hTotal, T), facade);

            // Mái che đỉnh lõi thang
            Box(stairsGroup, "Mái lõi thang", new Vector3(cx, hTotal + 0.15f, cz), new Vector3(lenX + 0.4f, 0.3f, lenZ + 0.4f), facade);

            // =====================================================================
            // 1. Thang bộ một vế thẳng (x 28.6..29.8, z 0.06..6.0, cao 3.8 m)
            // =====================================================================
            var stairs = Group("Thang bộ", stairsGroup);
            const int stepCount = 22;
            const float stepH = F2_Y / stepCount; // ~0.1727 m
            const float zStart = 0.06f;
            const float zEnd = 6.0f;
            float stepD = (zEnd - zStart) / stepCount; // ~0.27 m
            const float stairW = 1.2f;
            float stairCenterX = 29.2f;

            for (int i = 0; i < stepCount; i++)
            {
                float sy = (i + 0.5f) * stepH;
                float sz = zStart + (i + 0.5f) * stepD;
                // Bậc trực quan
                Box(stairs, $"Bậc {i + 1}", new Vector3(stairCenterX, sy - stepH * 0.25f, sz), new Vector3(stairW, stepH * 0.5f, stepD), oak);
                Box(stairs, $"Cổ bậc {i + 1}", new Vector3(stairCenterX, sy - stepH * 0.5f, sz - stepD * 0.45f), new Vector3(stairW, stepH, 0.03f), sill);
            }

            // Dốc va chạm nghiêng phẳng (CharacterController đi cực êm, không va vấp)
            float rampLenZ = zEnd - zStart;
            float rampAngle = Mathf.Atan2(F2_Y, rampLenZ) * Mathf.Rad2Deg;
            float rampLen = Mathf.Sqrt(rampLenZ * rampLenZ + F2_Y * F2_Y);
            var rampObj = new GameObject("Dốc va chạm thang bộ");
            rampObj.transform.SetParent(stairs, false);
            rampObj.transform.localPosition = new Vector3(stairCenterX, F2_Y / 2, (zStart + zEnd) / 2);
            rampObj.transform.localRotation = Quaternion.Euler(-rampAngle, 0, 0);
            var rampCol = rampObj.AddComponent<BoxCollider>();
            rampCol.size = new Vector3(stairW, 0.1f, rampLen);

            // Lan can kính dọc mép tây thang bộ (x = 28.55 m, z 0..6.0)
            var railing = Group("Lan can thang", stairs);
            for (int i = 0; i <= stepCount; i += 3)
            {
                float ry = i * stepH;
                float rz = zStart + i * stepD;
                Box(railing, $"Trụ lan can {i}", new Vector3(28.58f, ry + 0.55f, rz), new Vector3(0.04f, 1.1f, 0.04f), steel, false);
            }
            var glassRailing = Box(railing, "Kính lan can nghiêng", new Vector3(28.58f, F2_Y / 2 + 0.55f, (zStart + zEnd) / 2),
                new Vector3(0.012f, 0.9f, rampLen), windowGlass, false, new Vector3(-rampAngle, 0, 0));
            NoShadow(glassRailing);
            var handrail = Box(railing, "Tay vịn gỗ", new Vector3(28.58f, F2_Y / 2 + 1.05f, (zStart + zEnd) / 2),
                new Vector3(0.06f, 0.05f, rampLen), oak, false, new Vector3(-rampAngle, 0, 0));

            // Lan can bảo vệ tầng 2 dọc mép sảnh thang (x 28.5..29.8, z = 0)
            Box(railing, "Lan can sảnh T2", new Vector3(29.2f, F2_Y + 0.55f, 0f), new Vector3(stairW, 1.1f, 0.02f), windowGlass, true);
            Box(railing, "Tay vịn sảnh T2", new Vector3(29.2f, F2_Y + 1.05f, 0f), new Vector3(stairW, 0.05f, 0.06f), oak, false);

            // =====================================================================
            // 2. Thang máy dịch chuyển 2 tầng (x 28.4..29.8, z -2.2..-0.8)
            // =====================================================================
            BuildElevator(stairsGroup);
        }

        static void BuildElevator(Transform parent)
        {
            var elGroup = Group("Thang máy", parent);
            const float elX0 = 28.4f, elX1 = 29.8f;
            const float elZ0 = -2.2f, elZ1 = -0.8f;
            float elCx = (elX0 + elX1) / 2; // 29.1 m
            float elCz = (elZ0 + elZ1) / 2; // -1.5 m
            float elW = elX1 - elX0;        // 1.4 m
            float elD = elZ1 - elZ0;        // 1.4 m
            const float doorW = 0.9f;

            // Giếng thang bao quanh (vách thép/kính)
            Box(elGroup, "Vách sau thang máy (+x)", new Vector3(elX1, 3.7f, elCz), new Vector3(T, 7.4f, elD), darkMetal);
            Box(elGroup, "Vách nam thang máy (-z)", new Vector3(elCx, 3.7f, elZ0), new Vector3(elW, 7.4f, T), darkMetal);
            Box(elGroup, "Vách bắc thang máy (+z)", new Vector3(elCx, 3.7f, elZ1), new Vector3(elW, 7.4f, T), darkMetal);

            // Khung cửa trước thang máy mặt x = 28.4 (mỗi tầng một khung cửa)
            var indList = new System.Collections.Generic.List<TextMesh>();
            var leafList = new System.Collections.Generic.List<Transform>();
            var offsetList = new System.Collections.Generic.List<Vector3>();

            for (int f = 1; f <= 2; f++)
            {
                float fy = f == 1 ? 0f : F2_Y;
                var floorDoor = Group($"Cửa tầng {f}", elGroup, new Vector3(elX0, fy, elCz), 90f);

                // Khung cửa inox
                Box(floorDoor, "Khung trên", new Vector3(0, 2.2f, 0), new Vector3(doorW + 0.15f, 0.15f, 0.12f), steel);
                Box(floorDoor, "Khung trái", new Vector3(-doorW / 2 - 0.05f, 1.1f, 0), new Vector3(0.1f, 2.2f, 0.12f), steel);
                Box(floorDoor, "Khung phải", new Vector3(doorW / 2 + 0.05f, 1.1f, 0), new Vector3(0.1f, 2.2f, 0.12f), steel);

                // Cánh cửa trượt 2 cánh
                var lDoor = Box(floorDoor, "Cánh trái", new Vector3(-doorW / 4, 1.05f, 0), new Vector3(doorW / 2 - 0.01f, 2.1f, 0.04f), steel);
                var rDoor = Box(floorDoor, "Cánh phải", new Vector3(doorW / 4, 1.05f, 0), new Vector3(doorW / 2 - 0.01f, 2.1f, 0.04f), steel);
                leafList.Add(lDoor.transform);
                leafList.Add(rDoor.transform);
                offsetList.Add(new Vector3(-doorW / 2, 0, 0));
                offsetList.Add(new Vector3(doorW / 2, 0, 0));

                // Bảng LED số tầng trên cửa
                var ledBox = Box(floorDoor, "Bảng LED", new Vector3(0, 2.4f, 0.06f), new Vector3(0.32f, 0.16f, 0.02f), blackPlastic, false);
                var lbl = Label(floorDoor, $"{f}F", new Vector3(0, 2.4f, 0.075f), Vector3.forward, 0.08f, Color.green);
                indList.Add(lbl.GetComponent<TextMesh>());

                // Nút gọi thang máy cạnh cửa
                var btn = Box(floorDoor, "Nút gọi thang", new Vector3(doorW / 2 + 0.15f, 1.1f, 0.06f), new Vector3(0.08f, 0.16f, 0.02f), alu);
                NoShadow(btn);
            }

            // Gắn component ElevatorController
            var ctrl = elGroup.gameObject.AddComponent<ElevatorController>();
            ctrl.leaves = leafList.ToArray();
            ctrl.openOffsets = offsetList.ToArray();
            ctrl.floorIndicators = indList.ToArray();

            // Nút bấm tương tác bên trong cabin thang máy
            var cabinBtn = Box(elGroup, "Bảng điều khiển cabin", new Vector3(elX0 + 0.05f, 1.2f, elCz + 0.4f), new Vector3(0.03f, 0.35f, 0.15f), steel);
            var usableProxy = cabinBtn.AddComponent<ElevatorButtonProxy>();
            usableProxy.controller = ctrl;
        }
    }

    /// <summary>
    /// Proxy cho phép người chơi bấm vào bảng điều khiển thang máy để đổi tầng.
    /// </summary>
    public class ElevatorButtonProxy : MonoBehaviour, Khkt.Demo.IDemoUsable
    {
        public ElevatorController controller;
        public string UseHint => controller != null ? controller.UseHint : "Thang máy";
        public void Use()
        {
            if (controller != null) controller.Use();
        }
    }
}
