using Khkt.Demo;
using Khkt.Interaction;
using UnityEngine;

namespace Khkt.Avatar
{
    public enum HandPoseId
    {
        Xoe = 0,
        Nghi = 1,
        ChumCam = 2,
        BinhNon = 3,
        OngNghiem = 4,
        Pipet = 5,
        KhoaBuret = 6,
        Kep = 7,
        Que = 8,
        ChiTay = 9
    }

    /// <summary>Góc khớp của một ngón tay (đơn vị độ).</summary>
    public struct FingerPose
    {
        public float mcpFlex;
        public float mcpSpread;
        public float pipFlex;
        public float dipFlex;

        public FingerPose(float mcpFlex, float mcpSpread, float pipFlex, float dipFlex)
        {
            this.mcpFlex = mcpFlex;
            this.mcpSpread = mcpSpread;
            this.pipFlex = pipFlex;
            this.dipFlex = dipFlex;
        }

        public static FingerPose Lerp(FingerPose a, FingerPose b, float t) => new FingerPose(
            Mathf.Lerp(a.mcpFlex, b.mcpFlex, t),
            Mathf.Lerp(a.mcpSpread, b.mcpSpread, t),
            Mathf.Lerp(a.pipFlex, b.pipFlex, t),
            Mathf.Lerp(a.dipFlex, b.dipFlex, t));
    }

    /// <summary>Góc khớp ngón cái (đơn vị độ).</summary>
    public struct ThumbPose
    {
        public float cmcFlex;
        public float cmcOppose;
        public float cmcPronate;
        public float mcpFlex;
        public float ipFlex;

        public ThumbPose(float cmcFlex, float cmcOppose, float cmcPronate, float mcpFlex, float ipFlex)
        {
            this.cmcFlex = cmcFlex;
            this.cmcOppose = cmcOppose;
            this.cmcPronate = cmcPronate;
            this.mcpFlex = mcpFlex;
            this.ipFlex = ipFlex;
        }

        public static ThumbPose Lerp(ThumbPose a, ThumbPose b, float t) => new ThumbPose(
            Mathf.Lerp(a.cmcFlex, b.cmcFlex, t),
            Mathf.Lerp(a.cmcOppose, b.cmcOppose, t),
            Mathf.Lerp(a.cmcPronate, b.cmcPronate, t),
            Mathf.Lerp(a.mcpFlex, b.mcpFlex, t),
            Mathf.Lerp(a.ipFlex, b.ipFlex, t));
    }

    /// <summary>Tư thế đầy đủ của một bàn tay trong bảng tư thế.</summary>
    public struct HandPoseData
    {
        public ThumbPose thumb;
        public FingerPose index;
        public FingerPose middle;
        public FingerPose ring;
        public FingerPose pinky;
        public Vector3 wristEulerOffset;
        public Vector3 palmLocalOffset;

        public FingerPose GetFinger(int i) => i switch
        {
            0 => index,
            1 => middle,
            2 => ring,
            _ => pinky
        };

        public static HandPoseData Lerp(HandPoseData a, HandPoseData b, float t) => new HandPoseData
        {
            thumb = ThumbPose.Lerp(a.thumb, b.thumb, t),
            index = FingerPose.Lerp(a.index, b.index, t),
            middle = FingerPose.Lerp(a.middle, b.middle, t),
            ring = FingerPose.Lerp(a.ring, b.ring, t),
            pinky = FingerPose.Lerp(a.pinky, b.pinky, t),
            wristEulerOffset = Vector3.Lerp(a.wristEulerOffset, b.wristEulerOffset, t),
            palmLocalOffset = Vector3.Lerp(a.palmLocalOffset, b.palmLocalOffset, t)
        };
    }

    /// <summary>
    /// Bảng dữ liệu tư thế bàn tay theo loại dụng cụ thí nghiệm và giới hạn góc khớp sinh lý.
    /// Chuyển tư thế mượt trong 0,15 s (theo thời gian, không phụ thuộc FPS; ngón lệch pha nhẹ ở HandRig),
    /// không cho phép khớp gập ngược; ngón cái CMC có thể dạng ra ngoài (−35°) đến đối chiếu (68°).
    /// </summary>
    public static class HandPoseTable
    {
        public const float TransitionDurationS = 0.15f;

        // Giới hạn sinh lý (độ): góc dương luôn gập vào lòng bàn tay, cấm âm (gập ngược).
        public const float FingerMcpMinFlex = 0f, FingerMcpMaxFlex = 90f;
        public const float FingerMcpMinSpread = -16f, FingerMcpMaxSpread = 16f;
        public const float FingerPipMinFlex = 0f, FingerPipMaxFlex = 105f;
        public const float FingerDipMinFlex = 0f, FingerDipMaxFlex = 80f;

        public const float ThumbCmcMinFlex = -6f, ThumbCmcMaxFlex = 55f;
        public const float ThumbCmcMinOppose = -35f, ThumbCmcMaxOppose = 68f;   // âm = dạng ngón cái ra ngoài (xòe tay)
        public const float ThumbCmcMinPronate = 0f, ThumbCmcMaxPronate = 65f;
        public const float ThumbMcpMinFlex = 0f, ThumbMcpMaxFlex = 62f;
        public const float ThumbIpMinFlex = 0f, ThumbIpMaxFlex = 82f;

        static readonly HandPoseData[] Table =
        {
            // 0. Xoe
            ClampAll(new HandPoseData
            {
                thumb = new ThumbPose(4f, -24f, 14f, 6f, 6f),
                index = new FingerPose(6f, -9f, 8f, 4f),
                middle = new FingerPose(5f, -2f, 7f, 4f),
                ring = new FingerPose(7f, 4f, 10f, 6f),
                pinky = new FingerPose(9f, 11f, 12f, 8f),
                wristEulerOffset = Vector3.zero
            }),
            // 1. Nghi
            ClampAll(new HandPoseData
            {
                thumb = new ThumbPose(20f, 0f, 30f, 18f, 20f),
                index = new FingerPose(20f, -5f, 26f, 16f),
                middle = new FingerPose(24f, -1f, 32f, 18f),
                ring = new FingerPose(28f, 3f, 36f, 22f),
                pinky = new FingerPose(32f, 8f, 40f, 25f),
                wristEulerOffset = new Vector3(4f, 0f, 0f)
            }),
            // 2. ChumCam (chụm cầm chính xác)
            ClampAll(new HandPoseData
            {
                thumb = new ThumbPose(26f, 54f, 48f, 32f, 34f),
                index = new FingerPose(40f, -3f, 54f, 34f),
                middle = new FingerPose(52f, 0f, 64f, 40f),
                ring = new FingerPose(62f, 3f, 74f, 48f),
                pinky = new FingerPose(68f, 6f, 80f, 52f),
                wristEulerOffset = new Vector3(-6f, 0f, 6f)
            }),
            // 3. BinhNon (xoay bàn tay nghiêng bên cổ bình nón, không che miệng bình)
            ClampAll(new HandPoseData
            {
                thumb = new ThumbPose(22f, 48f, 44f, 22f, 24f),
                index = new FingerPose(38f, -5f, 52f, 30f),
                middle = new FingerPose(44f, -1f, 56f, 34f),
                ring = new FingerPose(48f, 3f, 60f, 36f),
                pinky = new FingerPose(52f, 7f, 64f, 40f),
                wristEulerOffset = new Vector3(-8f, 0f, -74f),
                palmLocalOffset = new Vector3(0.040f, -0.022f, -0.028f)
            }),
            // 4. OngNghiem (xoay nghiêng ôm trụ hẹp ống nghiệm / lọ nhỏ)
            ClampAll(new HandPoseData
            {
                thumb = new ThumbPose(24f, 52f, 48f, 26f, 28f),
                index = new FingerPose(46f, -4f, 60f, 36f),
                middle = new FingerPose(50f, 0f, 64f, 38f),
                ring = new FingerPose(54f, 3f, 68f, 42f),
                pinky = new FingerPose(58f, 6f, 72f, 44f),
                wristEulerOffset = new Vector3(-5f, 0f, -67f),
                palmLocalOffset = new Vector3(0.025f, -0.018f, -0.006f)
            }),
            // 5. Pipet (xoay nghiêng cầm kẹp 3 ngón quanh thân/cổ pipet)
            ClampAll(new HandPoseData
            {
                thumb = new ThumbPose(26f, 54f, 48f, 26f, 30f),
                index = new FingerPose(44f, -4f, 58f, 34f),
                middle = new FingerPose(50f, -1f, 64f, 38f),
                ring = new FingerPose(66f, 3f, 82f, 52f),
                pinky = new FingerPose(72f, 6f, 86f, 56f),
                wristEulerOffset = new Vector3(-4f, 0f, -66f),
                palmLocalOffset = new Vector3(0.024f, -0.018f, -0.004f)
            }),
            // 6. KhoaBuret (cầm vặn khóa buret bằng ngón cái & trỏ)
            ClampAll(new HandPoseData
            {
                thumb = new ThumbPose(26f, 54f, 48f, 28f, 34f),
                index = new FingerPose(44f, -3f, 62f, 42f),
                middle = new FingerPose(54f, 0f, 68f, 44f),
                ring = new FingerPose(70f, 3f, 84f, 54f),
                pinky = new FingerPose(74f, 6f, 88f, 58f),
                wristEulerOffset = new Vector3(8f, 0f, -24f),
                palmLocalOffset = new Vector3(0.012f, -0.010f, -0.008f)
            }),
            // 7. Kep (cầm kẹp / nhíp)
            ClampAll(new HandPoseData
            {
                thumb = new ThumbPose(24f, 52f, 46f, 24f, 26f),
                index = new FingerPose(34f, -4f, 46f, 28f),
                middle = new FingerPose(42f, -1f, 54f, 32f),
                ring = new FingerPose(62f, 3f, 76f, 48f),
                pinky = new FingerPose(68f, 6f, 80f, 52f),
                wristEulerOffset = new Vector3(-4f, 0f, -8f)
            }),
            // 8. Que (cầm que cấy / đũa thủy tinh kiểu cầm bút)
            ClampAll(new HandPoseData
            {
                thumb = new ThumbPose(26f, 56f, 52f, 28f, 30f),
                index = new FingerPose(46f, -3f, 54f, 30f),
                middle = new FingerPose(52f, 0f, 60f, 34f),
                ring = new FingerPose(66f, 3f, 78f, 50f),
                pinky = new FingerPose(72f, 6f, 82f, 54f),
                wristEulerOffset = new Vector3(8f, 0f, -10f)
            }),
            // 9. ChiTay (trỏ ngón trỏ)
            ClampAll(new HandPoseData
            {
                thumb = new ThumbPose(24f, 40f, 36f, 30f, 36f),
                index = new FingerPose(6f, 0f, 8f, 4f),
                middle = new FingerPose(76f, 0f, 92f, 64f),
                ring = new FingerPose(80f, 2f, 95f, 66f),
                pinky = new FingerPose(84f, 5f, 96f, 68f),
                wristEulerOffset = new Vector3(-4f, 0f, 0f)
            })
        };

        public static HandPoseData Get(HandPoseId id)
        {
            int idx = Mathf.Clamp((int)id, 0, Table.Length - 1);
            return Table[idx];
        }

        public static FingerPose ClampFinger(FingerPose f) => new FingerPose(
            Mathf.Clamp(f.mcpFlex, FingerMcpMinFlex, FingerMcpMaxFlex),
            Mathf.Clamp(f.mcpSpread, FingerMcpMinSpread, FingerMcpMaxSpread),
            Mathf.Clamp(f.pipFlex, FingerPipMinFlex, FingerPipMaxFlex),
            Mathf.Clamp(f.dipFlex, FingerDipMinFlex, FingerDipMaxFlex));

        public static ThumbPose ClampThumb(ThumbPose t) => new ThumbPose(
            Mathf.Clamp(t.cmcFlex, ThumbCmcMinFlex, ThumbCmcMaxFlex),
            Mathf.Clamp(t.cmcOppose, ThumbCmcMinOppose, ThumbCmcMaxOppose),
            Mathf.Clamp(t.cmcPronate, ThumbCmcMinPronate, ThumbCmcMaxPronate),
            Mathf.Clamp(t.mcpFlex, ThumbMcpMinFlex, ThumbMcpMaxFlex),
            Mathf.Clamp(t.ipFlex, ThumbIpMinFlex, ThumbIpMaxFlex));

        public static HandPoseData ClampAll(HandPoseData d) => new HandPoseData
        {
            thumb = ClampThumb(d.thumb),
            index = ClampFinger(d.index),
            middle = ClampFinger(d.middle),
            ring = ClampFinger(d.ring),
            pinky = ClampFinger(d.pinky),
            wristEulerOffset = d.wristEulerOffset,
            palmLocalOffset = d.palmLocalOffset
        };

        /// <summary>Chọn tư thế bàn tay phù hợp theo dụng cụ đang cầm hoặc thao tác hiện tại.</summary>
        public static HandPoseId ClassifyToolPose(Grabbable held, HandController.Act act, bool hoverInteractive)
        {
            if (act == HandController.Act.Stopcock) return HandPoseId.KhoaBuret;
            if (act == HandController.Act.Use || act == HandController.Act.BioButton) return HandPoseId.ChiTay;

            if (held != null)
            {
                if (held.GetComponent<Pipette>() != null || held.GetComponent<Dropper>() != null)
                    return HandPoseId.Pipet;
                if (held.GetComponent<BuretStopcock>() != null)
                    return HandPoseId.OngNghiem;
                if (held.GetComponent<MeterProbe>() != null || held.GetComponent<TestPaper>() != null)
                    return HandPoseId.Que;

                string kind = (held.kind ?? "").ToLowerInvariant();
                string name = (held.name ?? "").ToLowerInvariant();

                if (kind.Contains("pipet") || name.Contains("pipet") || name.Contains("nhỏ giọt"))
                    return HandPoseId.Pipet;
                if (kind.Contains("kep") || name.Contains("kẹp") || name.Contains("nhíp"))
                    return HandPoseId.Kep;
                if (kind.Contains("que") || name.Contains("que") || name.Contains("đũa") || name.Contains("thìa"))
                    return HandPoseId.Que;
                if (kind.Contains("ong") || name.Contains("ống") || name.Contains("cuvet") || name.Contains("lam"))
                    return HandPoseId.OngNghiem;
                if (kind.Contains("binh") || kind.Contains("coc") || kind.Contains("chai") || kind.Contains("lo") ||
                    name.Contains("bình") || name.Contains("cốc") || name.Contains("chai") || name.Contains("lọ"))
                    return HandPoseId.BinhNon;
                return HandPoseId.ChumCam;
            }

            if (hoverInteractive) return HandPoseId.ChumCam;
            if (act == HandController.Act.Pick || act == HandController.Act.Swirl || act == HandController.Act.BioTarget)
                return HandPoseId.ChiTay;
            return HandPoseId.Xoe;
        }
    }
}
