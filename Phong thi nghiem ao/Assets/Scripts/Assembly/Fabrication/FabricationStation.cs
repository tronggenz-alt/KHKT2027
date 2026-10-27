using Khkt.Demo;
using UnityEngine;

namespace Khkt.Assembly
{
    /// <summary>Bàn chế tạo trong ngăn kính: bấm vào là mở giao diện lắp ráp 3D của khu chế tạo.</summary>
    public class FabricationStation : MonoBehaviour, IDemoUsable
    {
        public string UseHint => "Bấm chuột trái: mở giao diện lắp ráp dụng cụ";

        public void Use()
        {
            if (FabricationWorkspace.Instance != null) FabricationWorkspace.Instance.Open();
        }
    }
}
