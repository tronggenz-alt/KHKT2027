using Khkt.Demo;
using UnityEngine;

namespace Khkt.Environment.Tang2
{
    /// <summary>Nút thang máy (gọi thang ở sảnh, số tầng / mở / đóng cửa trong cabin). Bấm thì nút sáng viền cam 1 giây.</summary>
    public class ElevatorButton : MonoBehaviour, IDemoUsable
    {
        public enum Kind { Floor1, Floor2, Open, Close, Call1, Call2 }

        public ElevatorController controller;
        public Kind kind;
        public Renderer lamp;
        public Material litMaterial;

        Material normal;
        float litUntil;

        public string UseHint
        {
            get
            {
                switch (kind)
                {
                    case Kind.Floor1: return "Đi xuống tầng 1";
                    case Kind.Floor2: return "Đi lên tầng 2 (khu sinh học)";
                    case Kind.Open: return "Mở cửa thang máy";
                    case Kind.Close: return "Đóng cửa thang máy";
                    default: return "Gọi thang máy";
                }
            }
        }

        public void Use()
        {
            if (controller == null) return;
            switch (kind)
            {
                case Kind.Floor1: controller.Go(0); break;
                case Kind.Floor2: controller.Go(1); break;
                case Kind.Open: controller.OpenDoors(); break;
                case Kind.Close: controller.CloseDoors(); break;
                case Kind.Call1: controller.Call(0); break;
                case Kind.Call2: controller.Call(1); break;
            }
            if (lamp != null && litMaterial != null)
            {
                if (normal == null) normal = lamp.sharedMaterial;
                lamp.sharedMaterial = litMaterial;
                litUntil = Time.time + 1f;
                enabled = true;
            }
        }

        void Awake() => enabled = false;

        void Update()
        {
            if (Time.time < litUntil) return;
            if (lamp != null && normal != null) lamp.sharedMaterial = normal;
            enabled = false;
        }
    }
}
