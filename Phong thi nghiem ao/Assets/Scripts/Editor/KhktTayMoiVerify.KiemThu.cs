using System;
using System.Collections;
using System.Collections.Generic;
using Khkt.Avatar;
using Khkt.Demo;
using Khkt.Environment;
using Khkt.Input;
using Khkt.Interaction;
using Khkt.Rendering;
using UnityEditor;
using UnityEngine;

namespace Khkt.EditorTools
{
    /// <summary>Các kiểm thử tay + cánh tay mới (gọi bởi KhktTayMoiVerify.Run).</summary>
    public static partial class KhktTayMoiVerify
    {
        static IEnumerator CoRun()
        {
            Application.logMessageReceived += OnUnityLog;
            yield return WaitSceneReady();

            var viewer = UnityEngine.Object.FindAnyObjectByType<DesktopViewer>();
            var hand = HandController.Active != null ? HandController.Active : UnityEngine.Object.FindAnyObjectByType<HandController>();
            var arm = UnityEngine.Object.FindAnyObjectByType<ArmRig>();
            Camera cam = hand != null && hand.viewCamera != null ? hand.viewCamera : Camera.main;
            Check(viewer != null && hand != null && hand.hand != null && arm != null && cam != null, "Cảnh có DesktopViewer, HandController, HandRig, ArmRig, Camera");
            if (viewer == null || hand == null || hand.hand == null || arm == null || cam == null) { FinishVerify(true); yield break; }

            HandRig rig = hand.hand;
            HandRig.WearGloves = true;
            HardwareProfile.Dat(MucDoHoa.Cao);
            float tm = Time.realtimeSinceStartup;
            while ((!rig.MeshReady || rig.MeshPending || arm.LeftHand == null || !arm.LeftHand.MeshReady) && Time.realtimeSinceStartup - tm < 40f) yield return null;
            Check(rig.MeshReady && arm.LeftHand != null && arm.LeftHand.MeshReady, "Lưới hai bàn tay dựng xong trên luồng nền (không chặn khung hình)",
                $"chờ thêm sau khi cảnh sẵn sàng {Time.realtimeSinceStartup - tm:0.0} s");
            yield return WaitFrames(4);
            var tempObjects = new List<GameObject>();
            Vector3 basePos = viewer.transform.position;
            float baseYaw = viewer.transform.eulerAngles.y;

            try
            {
                // ---------------- 1. Cấu trúc lưới ----------------
                var smr = rig.SkinRenderer;
                Mesh mesh = smr != null ? smr.sharedMesh : null;
                Check(mesh != null && smr.bones.Length == TayNguoiLuoi.HandBoneCount,
                    "Bàn tay là một lưới da liền khối có skinning 17 xương (5 ngón, ngón cái 3 khớp, cổ tay)",
                    $"đỉnh={rig.MeshVertexCount} tam giác={(mesh != null ? mesh.triangles.Length / 3 : 0)} dựng={rig.LastMeshBuildMs:0} ms");
                Check(rig.MeshVertexCount >= 6000, "Lưới mức Cao đủ mịn (≥ 6000 đỉnh)", $"đỉnh={rig.MeshVertexCount}");
                int nails = 0;
                foreach (var mf in rig.GetComponentsInChildren<MeshFilter>(true))
                    if (mf.name.StartsWith("Móng") && mf.sharedMesh != null && mf.sharedMesh.vertexCount > 20) nails++;
                Check(nails == 5 && rig.FingerNailsCount == 5, "5 móng tay cong bám mặt mu đốt xa", $"móng={nails}");
                Check(rig.ForearmBone != null && rig.WristAttach != null, "Có xương cổ tay ảo + miệng cổ tay áo");
                var s0 = arm.SleeveRenderer(0);
                var s1 = arm.SleeveRenderer(1);
                Check(s0 != null && s1 != null && s0.bones.Length == 5 && s0.sharedMesh != null && s0.sharedMesh.blendShapeCount >= 1,
                    "Hai ống tay áo blouse skinning 5 xương + blendshape gập khuỷu",
                    s0 != null && s0.sharedMesh != null ? $"đỉnh tay áo={s0.sharedMesh.vertexCount}" : "");
                Check(arm.LeftHand != null && arm.LeftHand.SkinRenderer != null, "Tay trái cũng là lưới skinning");
                Check(Shader.Find("Khkt/TayNguoi") != null && smr.sharedMaterial != null && smr.sharedMaterial.shader.name == "Khkt/TayNguoi",
                    "Vật liệu Khkt/TayNguoi (nạp qua Resources để có trong bản dựng)", smr.sharedMaterial != null ? smr.sharedMaterial.shader.name : "null");

                // ---------------- 2. Chiều mặt tam giác + trọng số ----------------
                if (mesh != null)
                {
                    var v = mesh.vertices; var n = mesh.normals; var t = mesh.triangles; var bw = mesh.boneWeights;
                    int good = 0, tc = t.Length / 3;
                    for (int i = 0; i < t.Length; i += 3)
                    {
                        Vector3 fn = Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]);
                        if (Vector3.Dot(fn, n[t[i]] + n[t[i + 1]] + n[t[i + 2]]) > 0f) good++;
                    }
                    Check(good >= tc * 0.995f, "Mặt tam giác hướng ra ngoài, khớp pháp tuyến SDF", $"{good}/{tc}");
                    float worst = 0f;
                    foreach (var w in bw) worst = Mathf.Max(worst, Mathf.Abs(w.weight0 + w.weight1 + w.weight2 + w.weight3 - 1f));
                    Check(worst < 1e-3f, "Trọng số skinning chuẩn hóa", $"lệch tối đa={worst:0.00000}");

                    // Ngón giữa gập hết cỡ không được kéo da ngón trỏ
                    var baked = new Mesh();
                    HandPoseData open = HandPoseTable.Get(HandPoseId.Xoe);
                    rig.ApplyPoseNow(open);
                    smr.BakeMesh(baked, true);
                    var va = baked.vertices;
                    HandPoseData bent = open;
                    bent.middle = new FingerPose(85f, 0f, 100f, 70f);
                    rig.ApplyPoseNow(bent);
                    smr.BakeMesh(baked, true);
                    var vb = baked.vertices;
                    float idxMove = 0f, midMove = 0f;
                    for (int i = 0; i < bw.Length; i++)
                    {
                        float d = (vb[i] - va[i]).magnitude;
                        int b0 = bw[i].boneIndex0;
                        if (bw[i].weight0 > 0.98f && b0 >= TayNguoiLuoi.BoneFinger(0, 0) && b0 <= TayNguoiLuoi.BoneFinger(0, 2)) idxMove = Mathf.Max(idxMove, d);
                        if (b0 == TayNguoiLuoi.BoneFinger(1, 2)) midMove = Mathf.Max(midMove, d);
                    }
                    UnityEngine.Object.Destroy(baked);
                    Check(idxMove < 0.0001f && midMove > 0.02f, "Gập ngón giữa không kéo da ngón trỏ (trọng số chỉ trộn cha–con quanh khớp)",
                        $"ngón trỏ dịch={idxMove * 1000f:0.000} mm, đầu ngón giữa dịch={midMove * 1000f:0} mm");
                    rig.SetTargetPose(HandPoseId.Xoe, immediate: true);
                }

                // ---------------- 3. Găng / da ----------------
                HandRig.WearGloves = false;
                float modeSkin = smr.sharedMaterial.GetFloat("_Mode");
                HandRig.WearGloves = true;
                float modeGlove = smr.sharedMaterial.GetFloat("_Mode");
                Check(Mathf.Approximately(modeSkin, 0f) && Mathf.Approximately(modeGlove, 1f), "Công tắc găng nitrile ⇄ da hoạt động");

                // ---------------- 4. Mức chi tiết theo HardwareProfile ----------------
                var lodVerts = new int[4];
                var lodMs = new float[4];
                MucDoHoa[] levels = { MucDoHoa.SieuNet, MucDoHoa.Cao, MucDoHoa.Vua, MucDoHoa.Thap };
                for (int i = 0; i < 4; i++)
                {
                    HardwareProfile.Dat(levels[i]);
                    float tl = Time.realtimeSinceStartup;
                    while ((rig.MeshPending || rig.MeshLod != 3 - (int)levels[i]) && Time.realtimeSinceStartup - tl < 150f) yield return null;
                    yield return WaitFrames(2);
                    lodVerts[i] = rig.MeshVertexCount;
                    lodMs[i] = rig.LastMeshBuildMs;
                }
                HardwareProfile.Dat(MucDoHoa.Cao);
                yield return WaitFrames(2);
                rig.WaitMesh();
                Check(lodVerts[0] > lodVerts[1] && lodVerts[1] > lodVerts[2] && lodVerts[2] > lodVerts[3] && rig.MeshLod == 1,
                    "Mức chi tiết lưới theo HardwareProfile (Siêu nét > Cao > Vừa > Thấp)",
                    $"đỉnh={lodVerts[0]}/{lodVerts[1]}/{lodVerts[2]}/{lodVerts[3]} dựng ms={lodMs[0]:0}/{lodMs[1]:0}/{lodMs[2]:0}/{lodMs[3]:0}");

                // ---------------- 4b. Bộ nhớ đệm lưới trên đĩa ----------------
                {
                    TayNguoiLuoi.ClearCache();   // chỉ bộ nhớ RAM; tệp trên đĩa còn
                    var rd = NewTestRig("TAY_TestDisk", tempObjects);
                    float td = Time.realtimeSinceStartup;
                    while (!rd.MeshReady && Time.realtimeSinceStartup - td < 40f) yield return null;
                    Check(rd.MeshReady && rd.MeshFromDisk && rd.LastMeshBuildMs < 1000f,
                        "Lưới bàn tay đọc lại từ bộ đệm trên đĩa (lần mở app sau hiện tay ngay)",
                        $"từ đĩa={rd.MeshFromDisk} thời gian={rd.LastMeshBuildMs:0} ms, đỉnh={rd.MeshVertexCount} (dựng mới: {rig.LastMeshBuildMs:0} ms)");
                    rd.gameObject.SetActive(false);
                }

                // ---------------- 5. MediaPipe 21 điểm: khứ hồi chính xác ----------------
                {
                    var go = new GameObject("TAY_TestMediaPipe");
                    go.SetActive(false);
                    go.transform.position = new Vector3(0f, -60f, 0f);
                    var tr = go.AddComponent<HandRig>();
                    tr.side = 0;
                    tr.ownSleeve = false;
                    go.SetActive(true);
                    tr.SkipIdleUpdate = true;
                    tempObjects.Add(go);
                    var rnd = new System.Random(20112026);
                    var world = new Vector3[21];
                    var lmCam = new Vector3[21];
                    var expect = new Quaternion[15];
                    float maxF = 0f, maxT = 0f, maxRoot = 0f;
                    for (int it = 0; it < 60; it++)
                    {
                        var d = new HandPoseData
                        {
                            index = RandFinger(rnd), middle = RandFinger(rnd), ring = RandFinger(rnd), pinky = RandFinger(rnd),
                            thumb = new ThumbPose(R(rnd, -4f, 50f), R(rnd, -30f, 62f), R(rnd, 4f, 60f), R(rnd, 12f, 58f), R(rnd, 8f, 78f))
                        };
                        go.transform.rotation = UnityEngine.Random.rotationUniform;
                        tr.ApplyPoseNow(d);
                        Quaternion rootExp = go.transform.rotation;
                        JointRots(tr, expect);
                        tr.ReadLandmarks(world);
                        Quaternion camRot = UnityEngine.Random.rotationUniform;
                        Quaternion ic = Quaternion.Inverse(camRot);
                        for (int i = 0; i < 21; i++) lmCam[i] = ic * (world[i] - world[0]);
                        tr.ApplyMediaPipeLandmarks(lmCam, camRot);
                        maxRoot = Mathf.Max(maxRoot, Quaternion.Angle(rootExp, go.transform.rotation));
                        var got = new Quaternion[15];
                        JointRots(tr, got);
                        for (int j = 0; j < 12; j++) maxF = Mathf.Max(maxF, Quaternion.Angle(expect[j], got[j]));
                        for (int j = 12; j < 15; j++) maxT = Mathf.Max(maxT, Quaternion.Angle(expect[j], got[j]));
                    }
                    Check(maxRoot < 0.5f && maxF < 1.0f && maxT < 1.5f,
                        "Ánh xạ đủ 21 điểm MediaPipe: khứ hồi 60 tư thế ngẫu nhiên (cổ tay, 12 khớp ngón, CMC/MCP/IP ngón cái)",
                        $"sai số tối đa: gan tay={maxRoot:0.00}° ngón={maxF:0.00}° ngón cái={maxT:0.00}°");

                    // Không cấp phát khi ánh xạ
                    long b0 = GC.GetAllocatedBytesForCurrentThread();
                    for (int it = 0; it < 200; it++) tr.ApplyMediaPipeLandmarks(lmCam, Quaternion.identity);
                    long used = GC.GetAllocatedBytesForCurrentThread() - b0;
                    Check(used == 0, "Ánh xạ MediaPipe không cấp phát bộ nhớ", $"{used} byte / 200 lần");
                }

                // ---------------- 6. Nội suy tư thế độc lập FPS ----------------
                {
                    HandRig A = NewTestRig("TAY_TestA", tempObjects), B = NewTestRig("TAY_TestB", tempObjects);
                    A.SetTargetPose(HandPoseId.Xoe, true); B.SetTargetPose(HandPoseId.Xoe, true);
                    A.SetTargetPose(HandPoseId.ChumCam); B.SetTargetPose(HandPoseId.ChumCam);
                    var qa = new Quaternion[15]; var qb = new Quaternion[15]; var prev = new Quaternion[15];
                    JointRots(B, prev);
                    float maxStep = 0f;
                    for (int i = 0; i < 3; i++) A.StepPoseAndContact(1f / 30f);
                    for (int i = 0; i < 15; i++)
                    {
                        B.StepPoseAndContact(1f / 150f);
                        JointRots(B, qb);
                        for (int j = 0; j < 15; j++) maxStep = Mathf.Max(maxStep, Quaternion.Angle(prev[j], qb[j]));
                        Array.Copy(qb, prev, 15);
                    }
                    JointRots(A, qa); JointRots(B, qb);
                    float mid = MaxAngle(qa, qb);
                    float dur = A.CurrentTransitionS;
                    for (int i = 0; i < 8; i++) A.StepPoseAndContact(1f / 30f);
                    for (int i = 0; i < 40; i++) B.StepPoseAndContact(1f / 150f);
                    JointRots(A, qa); JointRots(B, qb);
                    float end = MaxAngle(qa, qb);
                    Check(mid < 0.3f && end < 0.05f && Mathf.Approximately(HandPoseTable.TransitionDurationS, 0.15f) && dur > 0.2f && dur <= 0.32f,
                        "Ngón tay nội suy mượt, thời lượng cố định theo độ lớn chuyển (0,15–0,32 s), 30 FPS và 150 FPS cho cùng tư thế",
                        $"Xòe→Chụm: {dur:0.000} s; lệch giữa chừng={mid:0.000}° cuối={end:0.000}° bước lớn nhất@150FPS={maxStep:0.0}°");
                    Check(maxStep < 12f, "Không giật: mỗi khung 150 FPS đổi < 12° ở khớp nào", $"{maxStep:0.0}°");

                    // Lò xo lắc tay: nghiệm đúng → cùng kết quả ở 30 và 150 FPS
                    Vector3 x1 = Vector3.zero, v1 = Vector3.zero, x2 = Vector3.zero, v2 = Vector3.zero;
                    float worst = 0f;
                    for (int i = 0; i < 30; i++)
                    {
                        Vector3 target = i < 10 ? Vector3.one * 0.02f : Vector3.zero;
                        ArmRig.Spring(ref x1, ref v1, target, 11f, 1f / 30f);
                        for (int k = 0; k < 5; k++) ArmRig.Spring(ref x2, ref v2, target, 11f, 1f / 150f);
                        worst = Mathf.Max(worst, (x1 - x2).magnitude);
                    }
                    Check(worst < 1e-5f, "Lò xo lắc/trễ cánh tay không phụ thuộc FPS", $"lệch tối đa={worst * 1000f:0.0000} mm");
                }

                // ---------------- 7. IK hai khớp: giới hạn sinh lý ----------------
                {
                    var rnd = new System.Random(7);
                    float worstLen = 0f, maxFlex = 0f, minFlex = 999f;
                    for (int i = 0; i < 2000; i++)
                    {
                        Vector3 dir = UnityEngine.Random.onUnitSphere;
                        Vector3 target = dir * R(rnd, 0.0f, 0.9f);
                        Vector3 pole = UnityEngine.Random.onUnitSphere;
                        ArmRig.SolveIk(Vector3.zero, target, pole, out Vector3 e, out Vector3 w);
                        worstLen = Mathf.Max(worstLen, Mathf.Abs(e.magnitude - ArmRig.UpperLen), Mathf.Abs((w - e).magnitude - ArmRig.ForeLen));
                        float flex = Vector3.Angle(e, w - e);
                        maxFlex = Mathf.Max(maxFlex, flex); minFlex = Mathf.Min(minFlex, flex);
                    }
                    Check(worstLen < 1e-4f && maxFlex <= ArmRig.ElbowMaxFlexDeg + 0.5f && minFlex >= 0f,
                        "IK vai–khuỷu–cổ tay: giữ chiều dài xương, khuỷu gập 0–150°, không duỗi quá (2000 điểm ngẫu nhiên)",
                        $"lệch dài={worstLen * 1000f:0.000} mm, gập {minFlex:0.0}–{maxFlex:0.0}°");
                }

                // ---------------- 8. Ảnh "sau" + IK dụng cụ + đo cánh tay ----------------
                ArmNotes.Clear();
                armMaxGapMm = armMaxFlex = armMaxDev = 0f; armMinFlex = 999f; armMaxPron = -999f; armMinPron = 999f;
                yield return CoCapture("sau", exitWhenDone: false);
                foreach (var kv in IkResult)
                    Check(kv.Value.pen == 0 && kv.Value.errMm <= 3.0f, $"IK bề mặt {kv.Key}: không xuyên, sai lệch ≤ 3 mm",
                        $"pen={kv.Value.pen} err={kv.Value.errMm:0.00} mm");
                Check(IkResult.Count == 4, "Đủ 4 tư thế cầm dụng cụ (bình nón, ống nghiệm, pipet, khóa buret)", $"{IkResult.Count}");
                Check(armMaxGapMm <= 1.0f, "Ống tay áo khít cổ tay ở mọi tư thế", $"hở tối đa={armMaxGapMm:0.00} mm");
                Check(armMinFlex >= 0f && armMaxFlex <= ArmRig.ElbowMaxFlexDeg && armMinPron >= ArmRig.PronationMinDeg && armMaxPron <= ArmRig.PronationMaxDeg,
                    "Khuỷu và xoay sấp/ngửa trong giới hạn sinh lý ở mọi tư thế",
                    $"khuỷu {armMinFlex:0}–{armMaxFlex:0}°, sấp {armMinPron:0}–{armMaxPron:0}°, cổ tay ≤ {armMaxDev:0}°");

                // ---------------- 9. Lắc khi xoay camera / khi đi ----------------
                {
                    // DesktopViewer đang ở chế độ ngồi (giữ chỗ) → tắt tạm, tự xoay/dời gốc người chơi theo thời gian thật
                    viewer.SetPose(basePos, baseYaw, 18f);
                    hand.OverrideAimPoint = null;
                    yield return WaitFrames(10);
                    viewer.enabled = false;
                    Transform vt = viewer.transform;
                    yield return WaitSeconds(1.2f);
                    float rest = arm.SwayOffset.magnitude;
                    float turnMax = 0f, t0 = Time.realtimeSinceStartup;
                    while (Time.realtimeSinceStartup - t0 < 1.0f)
                    {
                        vt.rotation = Quaternion.Euler(0f, baseYaw + 120f * (Time.realtimeSinceStartup - t0), 0f);
                        yield return null;
                        turnMax = Mathf.Max(turnMax, arm.SwayOffset.magnitude);
                    }
                    yield return WaitSeconds(1.5f);
                    float settle = arm.SwayOffset.magnitude;
                    Check(turnMax > 0.004f && turnMax < 0.06f && settle < 0.003f, "Cánh tay trễ/nghiêng khi xoay camera (120°/s) rồi về lại",
                        $"đứng yên={rest * 1000f:0.0} mm, khi xoay={turnMax * 1000f:0.0} mm, sau 1,5 s={settle * 1000f:0.0} mm");

                    vt.rotation = Quaternion.Euler(0f, baseYaw, 0f);
                    yield return WaitSeconds(0.5f);
                    Vector3 fwd = Quaternion.Euler(0f, baseYaw, 0f) * Vector3.forward;
                    float yMin = 1f, yMax = -1f;
                    Vector3 p0 = vt.position;
                    t0 = Time.realtimeSinceStartup;
                    while (Time.realtimeSinceStartup - t0 < 2.0f)
                    {
                        vt.position = p0 + fwd * (1.3f * (Time.realtimeSinceStartup - t0));
                        yield return null;
                        if (Time.realtimeSinceStartup - t0 > 0.8f)
                        {
                            yMin = Mathf.Min(yMin, arm.SwayOffset.y);
                            yMax = Mathf.Max(yMax, arm.SwayOffset.y);
                        }
                    }
                    viewer.enabled = true;
                    viewer.SetPose(basePos, baseYaw, 18f);
                    Check(yMax - yMin > 0.003f && yMax - yMin < 0.04f, "Cánh tay nhún nhẹ theo bước đi (1,3 m/s)", $"biên độ dọc={(yMax - yMin) * 1000f:0.0} mm");
                    yield return WaitFrames(10);
                }

                // ---------------- 11. Xoay vật trong tay như tay người ----------------
                yield return CoTurnTests(viewer, hand, arm, cam, basePos, baseYaw);

                // ---------------- 12. Chuẩn bị nắm + quán tính cổ tay ----------------
                {
                    HandPoseData from = HandPoseTable.Get(HandPoseId.Xoe), to = HandPoseTable.Get(HandPoseId.ChumCam);
                    float minIdx = 999f, minOpp = 999f;
                    float T = HandRig.TransitionFor(from, to);
                    for (float e = 0f; e <= T + 1e-4f; e += 0.004f)
                    {
                        var d = HandRig.BlendStaggered(from, to, e, T);
                        minIdx = Mathf.Min(minIdx, d.index.mcpFlex);
                        minOpp = Mathf.Min(minOpp, d.thumb.cmcOppose);
                    }
                    var end = HandRig.BlendStaggered(from, to, T, T);
                    Check(minIdx < from.index.mcpFlex - 5f && minOpp < from.thumb.cmcOppose - 5f && Mathf.Abs(end.index.mcpFlex - to.index.mcpFlex) < 0.01f,
                        "Chuẩn bị nắm: ngón duỗi/xòe và ngón cái dạng ra trước rồi mới khép (thời lượng cố định)",
                        $"MCP trỏ thấp nhất {minIdx:0.0}° (đầu {from.index.mcpFlex:0}°, cuối {to.index.mcpFlex:0}°), CMC cái thấp nhất {minOpp:0.0}°");

                    HandRig L = NewTestRig("TAY_TestLag", tempObjects);
                    L.SetTargetPose(HandPoseId.Nghi, true);
                    float lagMax = 0f;
                    for (int i = 0; i < 15; i++) { L.transform.position += Vector3.up * (1.0f / 60f); L.StepPoseAndContact(1f / 60f); lagMax = Mathf.Max(lagMax, L.WristLagDeg.magnitude); }
                    for (int i = 0; i < 60; i++) L.StepPoseAndContact(1f / 60f);
                    float lagRest = L.WristLagDeg.magnitude;
                    Check(lagMax > 5f && lagRest < 0.5f, "Cổ tay trễ theo quán tính khi tay di chuyển nhanh rồi bật về",
                        $"tay đi lên 1 m/s: cổ tay lệch tối đa {lagMax:0.0}°, sau 1 s còn {lagRest:0.00}°");
                }

                // ---------------- 12b. Cầm lần lượt mọi ống nghiệm / pipet trong cảnh ----------------
                foreach (var kind in new[] { "ong_nghiem", "pipet" })
                {
                    int n = 0, bad = 0; float worst = 0f;
                    foreach (var g in UnityEngine.Object.FindObjectsByType<Grabbable>(FindObjectsInactive.Exclude))
                    {
                        if (g == null || !string.Equals(g.kind, kind, StringComparison.OrdinalIgnoreCase) || n >= 8) continue;
                        viewer.SetPose(basePos, baseYaw, 18f);
                        if (hand.Held != null) hand.ReturnHome();
                        yield return WaitFrames(2);
                        hand.Pick(g);
                        if (hand.Held != g) continue;
                        hand.OverrideAimPoint = Aim(cam, 0.44f, -0.08f, 0.05f);
                        yield return WaitSeconds(0.6f);
                        n++;
                        float e = rig.LastMaxSurfaceErrorMm;
                        worst = Mathf.Max(worst, e);
                        if (rig.LastPenetrationCount > 0 || e > 3f) bad++;
                        hand.ReturnHome();
                        hand.OverrideAimPoint = null;
                    }
                    Check(n > 0 && bad == 0, $"Cầm mọi {kind} trong cảnh: không xuyên, sai lệch ≤ 3 mm", $"{n} vật, sai lệch lớn nhất {worst:0.00} mm, lỗi {bad}");
                }

                // ---------------- 13. Hành vi theo hoàn cảnh + vải ----------------
                yield return CoBehaviourTests(viewer, hand, arm, cam, basePos, baseYaw);

                // ---------------- 10. Không cấp phát + chi phí CPU mỗi khung ----------------
                {
                    var flask = FindGrabbable("binh_non");
                    if (flask != null)
                    {
                        if (hand.Held != null) hand.ReturnHome();
                        hand.Pick(flask);
                        hand.OverrideAimPoint = Aim(cam, 0.44f, -0.08f, 0.05f);
                        yield return WaitSeconds(0.4f);
                        for (int i = 0; i < 5; i++) arm.Step(1f / 60f);
                        long b0 = GC.GetAllocatedBytesForCurrentThread();
                        var sw = System.Diagnostics.Stopwatch.StartNew();
                        for (int i = 0; i < 200; i++) arm.Step(1f / 60f);
                        sw.Stop();
                        long used = GC.GetAllocatedBytesForCurrentThread() - b0;
                        float msPer = (float)sw.Elapsed.TotalMilliseconds / 200f;
                        Check(used == 0, "Cập nhật hai cánh tay + bàn tay + IK dụng cụ không cấp phát bộ nhớ", $"{used} byte / 200 khung");
                        Check(msPer < 1.5f, "Chi phí CPU tay + cánh tay mỗi khung (đang cầm bình nón, IK bật)", $"{msPer:0.000} ms/khung (Editor)");
                        hand.ReturnHome();
                        hand.OverrideAimPoint = null;
                    }
                    else Check(false, "Tìm thấy bình nón để đo cấp phát");
                }
            }
            finally
            {
                foreach (var go in tempObjects) if (go != null) UnityEngine.Object.Destroy(go);
                viewer.enabled = true;
                HandRig.WearGloves = true;
                HardwareProfile.DatTuDong();
                hand.OverrideAimPoint = null;
                viewer.SetPose(basePos, baseYaw, 10f);
            }

            Application.logMessageReceived -= OnUnityLog;
            FinishVerify(true);
        }

        static IEnumerator CoTurnTests(DesktopViewer viewer, HandController hand, ArmRig arm, Camera cam, Vector3 basePos, float baseYaw)
        {
            var flask = FindGrabbable("binh_non");
            if (flask == null) { Check(false, "Tìm thấy bình nón để thử xoay"); yield break; }
            viewer.SetPose(basePos, baseYaw, 18f);
            if (hand.Held != null) hand.ReturnHome();
            hand.OverrideAimPoint = Aim(cam, 0.44f, -0.08f, 0.05f);   // ngắm trước khi cầm: bỏ cảnh vươn tay
            hand.Pick(flask);
            hand.OverrideHeldTurn = Vector3.zero;
            yield return WaitSeconds(0.7f);
            HandRig rig = hand.hand;
            Quaternion obj0 = flask.transform.rotation, hand0 = rig.transform.rotation;
            float neutralElbowLat = Vector3.Dot(arm.RightElbow - arm.RightShoulder, cam.transform.right);
            Vector3[] turns =
            {
                new Vector3(0f, 0f, -80f), new Vector3(0f, 0f, 80f), new Vector3(0f, 0f, -150f), new Vector3(0f, 0f, 130f),
                new Vector3(55f, 0f, 0f), new Vector3(-45f, 0f, 0f), new Vector3(0f, 50f, 0f), new Vector3(30f, -30f, -60f)
            };
            string[] names = { "lat_trai_80", "lat_phai_80", "lat_trai_150", "lat_phai_130", "gap_co_tay_55", "duoi_co_tay_45", "lech_50", "ket_hop" };
            string outDir = ShotDir();
            var sideGo = new GameObject("TAY_SideCam2");
            var sideCam = sideGo.AddComponent<Camera>();
            sideCam.enabled = false; sideCam.fieldOfView = 40f; sideCam.nearClipPlane = 0.01f; sideCam.cullingMask = cam.cullingMask;
            var rt = new RenderTexture(ShotW, ShotH, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            var tex = new Texture2D(ShotW, ShotH, TextureFormat.RGB24, false);
            float worstObj = 0f, worstHand = 0f, worstGap = 0f, maxElbowShift = 0f, rawMin = 999f, rawMax = -999f;
            int pen = 0;
            float errMax = 0f;
            try
            {
                for (int i = 0; i < turns.Length; i++)
                {
                    Vector3 t = turns[i];
                    hand.OverrideHeldTurn = t;
                    yield return WaitSeconds(0.7f);
                    yield return WaitFrames(3);
                    Vector3 fwd = new Vector3(cam.transform.forward.x, 0f, cam.transform.forward.z).normalized;
                    Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;
                    Quaternion exp = Quaternion.AngleAxis(t.y, Vector3.up) * Quaternion.AngleAxis(t.x, right) * Quaternion.AngleAxis(-t.z, fwd);
                    float eo = Quaternion.Angle(flask.transform.rotation * Quaternion.Inverse(obj0), exp);
                    float eh = Quaternion.Angle(rig.transform.rotation * Quaternion.Inverse(hand0), exp);
                    float gap = CuffGapMm(arm, rig);
                    float lat = Vector3.Dot(arm.RightElbow - arm.RightShoulder, cam.transform.right);
                    worstObj = Mathf.Max(worstObj, eo); worstHand = Mathf.Max(worstHand, eh); worstGap = Mathf.Max(worstGap, gap);
                    maxElbowShift = Mathf.Max(maxElbowShift, Mathf.Abs(lat - neutralElbowLat));
                    float raw = arm.PronationRawDeg(0);
                    rawMin = Mathf.Min(rawMin, raw); rawMax = Mathf.Max(rawMax, raw);
                    pen += rig.LastPenetrationCount; errMax = Mathf.Max(errMax, rig.LastMaxSurfaceErrorMm);
                    Debug.Log($"[TAY_XOAY] {names[i]}: vật lệch {eo:0.0}°, tay lệch {eh:0.0}°, hở {gap:0.00} mm, khuỷu {arm.ElbowFlexDeg(0):0}°, cổ tay gập {arm.WristDeviationDeg(0):0}°, " +
                              $"sấp thật {raw:0}°, khuỷu dang {(lat - neutralElbowLat) * 100f:0.0} cm, IK pen={rig.LastPenetrationCount} err={rig.LastMaxSurfaceErrorMm:0.0} mm");
                    if (i == 0 || i == 1 || i == 3 || i == 4 || i == 7)
                    {
                        RenderToFile(cam, rt, tex, System.IO.Path.Combine(outDir, $"sau_xoay_{names[i]}_goc_nguoi_choi.png"));
                        Vector3 pivot = rig.transform.position;
                        sideCam.transform.position = pivot + cam.transform.right * 0.26f + cam.transform.forward * 0.08f + Vector3.up * 0.10f;
                        sideCam.transform.LookAt(pivot - cam.transform.forward * 0.04f, Vector3.up);
                        RenderToFile(sideCam, rt, tex, System.IO.Path.Combine(outDir, $"sau_xoay_{names[i]}_goc_nghieng.png"));
                        Vector3 c = cam.transform.position;
                        sideCam.fieldOfView = 55f;
                        sideCam.transform.position = c + cam.transform.right * 0.60f + Vector3.up * 0.22f + cam.transform.forward * 0.30f;
                        sideCam.transform.LookAt(c + cam.transform.forward * 0.30f - Vector3.up * 0.25f, Vector3.up);
                        RenderToFile(sideCam, rt, tex, System.IO.Path.Combine(outDir, $"sau_xoay_{names[i]}_goc_ngoai.png"));
                        sideCam.fieldOfView = 40f;
                    }
                }
            }
            finally
            {
                hand.OverrideHeldTurn = null;
                hand.OverrideAimPoint = null;
                if (hand.Held != null) hand.ReturnHome();
                rt.Release();
                UnityEngine.Object.Destroy(rt); UnityEngine.Object.Destroy(tex); UnityEngine.Object.Destroy(sideGo);
            }
            Check(worstObj < 2.5f && worstHand < 3.5f, "Xoay vật trong tay: lật ±80…150°, gập/duỗi cổ tay, lệch, kết hợp – vật và bàn tay xoay đúng góc yêu cầu",
                $"sai lệch vật ≤ {worstObj:0.0}°, bàn tay ≤ {worstHand:0.0}°");
            Check(worstGap <= 1.0f, "Ống tay áo vẫn khít cổ tay khi xoay vật", $"hở tối đa {worstGap:0.00} mm");
            Check(maxElbowShift > 0.03f, "Lật vật quá tầm cẳng tay thì khuỷu tự dang ra/khép vào (xoay vai) như người thật",
                $"khuỷu dịch ngang tối đa {maxElbowShift * 100f:0.0} cm; sấp/ngửa thật {rawMin:0}…{rawMax:0}°");
            Check(pen == 0, "Xoay vật không làm ngón xuyên bình", $"pen={pen}, sai lệch IK lớn nhất {errMax:0.0} mm");
        }

        /// <summary>Miệng tay áo có nằm đúng sau khớp cổ tay không (mm): |khoảng miệng áo–tâm cổ tay − khoảng WristAttach–tâm cổ tay|.</summary>
        static float CuffGapMm(ArmRig arm, HandRig rig)
        {
            Vector3 wc = rig.ForearmBone.position;
            return Mathf.Abs(Vector3.Distance(arm.RightWrist, wc) - Vector3.Distance(rig.WristAttach.position, wc)) * 1000f;
        }

        static IEnumerator CoBehaviourTests(DesktopViewer viewer, HandController hand, ArmRig arm, Camera cam, Vector3 basePos, float baseYaw)
        {
            Check(HandPoseTable.ClassifyToolPose(null, HandController.Act.None, false) == HandPoseId.Nghi &&
                  HandPoseTable.ClassifyToolPose(null, HandController.Act.Pick, true) == HandPoseId.MoCam,
                  "Tư thế theo hoàn cảnh: rảnh thì thả lỏng (Nghỉ), đưa tới vật thì mở đón hình chữ C");

            string outDir = ShotDir();
            var go = new GameObject("TAY_SideCam3");
            var sc = go.AddComponent<Camera>();
            sc.enabled = false; sc.nearClipPlane = 0.01f; sc.cullingMask = cam.cullingMask;
            var rt = new RenderTexture(ShotW, ShotH, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            var tex = new Texture2D(ShotW, ShotH, TextureFormat.RGB24, false);
            HandRig rig = hand.hand;
            try
            {
                // (a) Tay rảnh: giữ tư thế nghỉ cong hai bên, di chuột / đổi chỗ ngắm không kéo tay theo
                if (hand.Held != null) hand.ReturnHome();
                hand.OverrideAimPoint = null;
                viewer.SetPose(basePos, baseYaw, -8f);
                yield return WaitSeconds(1.5f);
                Vector3 r0 = arm.HandLocalOut(0);
                float restMove = 0f, engMax = 0f;
                float tA = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - tA < 2.0f)
                {
                    yield return null;
                    restMove = Mathf.Max(restMove, (arm.HandLocalOut(0) - r0).magnitude);
                    engMax = Mathf.Max(engMax, arm.EngageWeight);
                }
                Vector3 rl = arm.HandLocalOut(0);
                Check(restMove < 0.015f && engMax < 0.05f && rl.x > 0.08f && rl.y < -0.10f && rl.z > 0.25f,
                    "Tay rảnh giữ tư thế nghỉ bên phải phía dưới tầm nhìn, chỉ thở nhẹ – không bám theo chuột",
                    $"dao động {restMove * 1000f:0.0} mm trong 2 s, mức vươn tối đa {engMax:0.00}, vị trí (hệ mắt) {rl.x:0.00}/{rl.y:0.00}/{rl.z:0.00} m");
                ShotOutside(cam, sc, rt, tex, System.IO.Path.Combine(outDir, "sau_hv_1_tu_the_nghi_goc_ngoai.png"));
                RenderToFile(cam, rt, tex, System.IO.Path.Combine(outDir, "sau_hv_1_tu_the_nghi_goc_nguoi_choi.png"));

                // (a2) Bấm lấy vật: tay vươn tới theo cung (vật đứng yên chờ), ngón ôm lấy rồi mới mang về
                {
                    viewer.SetPose(basePos, baseYaw, 30f);
                    yield return WaitSeconds(0.6f);
                    Grabbable fg = NearestGrabbable("coc", cam.transform.position) ?? NearestGrabbable("binh", cam.transform.position);
                    if (fg != null && (fg.transform.position - cam.transform.position).magnitude < 1.2f)
                    {
                        Vector3 objStart = fg.transform.position;
                        Vector3 gripPt = objStart + Vector3.up * fg.gripHeightM;
                        float startDist = rig.Grip != null ? Vector3.Distance(rig.Grip.position, gripPt) : 9f;
                        hand.ForceFetchAnimation = true;
                        hand.Pick(fg);
                        float objMoveBefore = 0f, minHandDist = 9f, liftMax = 0f, tGrab = -1f;
                        bool pathOk = true;
                        Vector3 prevGrip = rig.Grip != null ? rig.Grip.position : Vector3.zero;
                        float maxStep = 0f;
                        float tF = Time.realtimeSinceStartup;
                        bool shotReach = false, skipStep = true;
                        while (Time.realtimeSinceStartup - tF < 2.0f)
                        {
                            yield return null;
                            if (hand.Held != fg) { pathOk = false; break; }
                            Vector3 gp = rig.Grip != null ? rig.Grip.position : Vector3.zero;
                            // bước tay mỗi khung quy về 60 FPS (Editor có khung chậm khi chụp ảnh)
                            float fdt = Mathf.Max(Time.unscaledDeltaTime, 1f / 240f);
                            if (!skipStep) maxStep = Mathf.Max(maxStep, (gp - prevGrip).magnitude * (1f / 60f) / fdt);
                            skipStep = false;
                            prevGrip = gp;
                            if (!hand.FetchGrabbed && hand.Fetching)
                            {
                                objMoveBefore = Mathf.Max(objMoveBefore, (fg.transform.position - objStart).magnitude);
                                minHandDist = Mathf.Min(minHandDist, Vector3.Distance(gp, gripPt));
                                if (!shotReach && arm.EngageWeight > 0.5f)
                                {
                                    shotReach = true;
                                    RenderToFile(cam, rt, tex, System.IO.Path.Combine(outDir, "sau_hv_0_vuon_tay_lay_goc_nguoi_choi.png"));
                                    ShotOutside(cam, sc, rt, tex, System.IO.Path.Combine(outDir, "sau_hv_0_vuon_tay_lay_goc_ngoai.png"));
                                    skipStep = true;
                                }
                            }
                            else if (tGrab < 0f) tGrab = Time.realtimeSinceStartup - tF;
                            if (hand.Fetching) liftMax = Mathf.Max(liftMax, fg.transform.position.y - objStart.y);
                            if (!hand.Fetching && tGrab > 0f && Time.realtimeSinceStartup - tF > tGrab + 0.6f) break;
                        }
                        hand.ForceFetchAnimation = false;
                        float carryGap = rig.Grip != null && hand.Held != null ? Vector3.Distance(rig.Grip.position, hand.Held.transform.position + hand.Held.transform.up * hand.Held.gripHeightM) : 9f;
                        Check(pathOk && objMoveBefore < 0.001f && minHandDist < 0.03f && tGrab > 0.2f && tGrab < 1.15f && !hand.Fetching && carryGap < 0.02f && maxStep < 0.06f,
                            "Bấm lấy vật: tay vươn tới vật (vật đứng yên chờ), nắm rồi mang về – không hút vật vào tay",
                            $"cách vật lúc đầu {startDist * 100f:0} cm, tay tới gần nhất {minHandDist * 1000f:0} mm, nắm sau {tGrab:0.00} s, vật xê dịch trước khi nắm {objMoveBefore * 1000f:0.0} mm, " +
                            $"nhấc cao {liftMax * 1000f:0} mm, bước tay lớn nhất/khung {maxStep * 1000f:0} mm, tay–vật khi cầm {carryGap * 1000f:0} mm");
                        hand.ReturnHome();
                        yield return WaitSeconds(1.2f);
                        Check(arm.EngageWeight < 0.1f, "Đặt/trả vật xong tay thu về tư thế nghỉ", $"mức vươn {arm.EngageWeight:0.00}");
                    }
                    else Check(false, "Tìm thấy cốc/bình gần để thử vươn tay lấy");
                }

                // (a3) Tay áo gập tròn ở khuỷu: vải không bị bóp dẹt vào trục khuỷu
                {
                    var sm = arm.SleeveRenderer(0);
                    arm.JointPositions(0, out Vector3 jS, out Vector3 jE, out Vector3 jW);
                    float flexNow = arm.ElbowFlexDeg(0);
                    if (sm != null && sm.sharedMesh != null)
                    {
                        var baked = new Mesh();
                        sm.BakeMesh(baked, true);
                        var bv = baked.vertices;
                        var bind = new List<Vector3>();
                        sm.sharedMesh.GetUVs(1, bind);
                        float minR = 9f;
                        float l1 = ArmRig.UpperLen;
                        Matrix4x4 l2w = sm.transform.localToWorldMatrix;
                        for (int i = 0; i < bv.Length && i < bind.Count; i++)
                        {
                            if (Mathf.Abs(bind[i].z - l1) > 0.03f) continue;
                            if (new Vector2(bind[i].x, bind[i].y).magnitude < 0.03f) continue;   // mặt trong miệng tay áo
                            // mặt trong khuỷu (y > 0) vải dồn, chồng vào nhau là đúng; đo nửa ngoài + hai bên (chỗ ống hay bị bóp dẹt)
                            if (bind[i].y > 0.012f) continue;
                            Vector3 wp = l2w.MultiplyPoint3x4(bv[i]);
                            float dd = Mathf.Min(SegDistT(wp, jS, jE), SegDistT(wp, jE, jW));
                            minR = Mathf.Min(minR, dd);
                        }
                        UnityEngine.Object.DestroyImmediate(baked);
                        Check(minR > 0.040f, "Khuỷu tay áo gập tròn, vải không bị bóp dẹt vào khớp (nửa ngoài + hai bên)",
                            $"gập khuỷu {flexNow:0}°, khoảng cách nhỏ nhất vải–xương quanh khuỷu {minR * 1000f:0} mm");
                    }
                }

                // (b) Đi bộ: hai tay buông và vung ngược pha theo bước, vải trễ theo quán tính
                viewer.enabled = false;
                Transform vt = viewer.transform;
                Vector3 p0 = vt.position;
                Vector3 fwd = Quaternion.Euler(0f, baseYaw, 0f) * Vector3.forward;
                float zMin = 1f, zMax = -1f, lzMin = 1f, lzMax = -1f, lagMax = 0f;
                bool shot = false;
                float t0 = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - t0 < 3.0f)
                {
                    float tt = Time.realtimeSinceStartup - t0;
                    vt.position = p0 + fwd * (1.3f * tt);
                    yield return null;
                    if (tt > 1.8f)
                    {
                        Vector3 lr = arm.HandLocalOut(0);
                        zMin = Mathf.Min(zMin, lr.z); zMax = Mathf.Max(zMax, lr.z);
                        if (arm.LeftHand != null)
                        {
                            Vector3 ll = arm.HandLocalOut(1);
                            lzMin = Mathf.Min(lzMin, ll.z); lzMax = Mathf.Max(lzMax, ll.z);
                        }
                        lagMax = Mathf.Max(lagMax, arm.ClothLag(0).magnitude);
                        if (!shot && tt > 2.4f)
                        {
                            shot = true;
                            ShotOutside(cam, sc, rt, tex, System.IO.Path.Combine(outDir, "sau_hv_2_di_bo_goc_ngoai.png"));
                        }
                    }
                }
                float flexL = arm.ElbowFlexDeg(1), flexR = arm.ElbowFlexDeg(0);
                Check(flexL < 55f && flexR < 55f, "Đi bộ: khuỷu buông thoải mái (không gập nâng khuỷu)", $"gập khuỷu phải {flexR:0}°, trái {flexL:0}°; sấp/ngửa phải {arm.PronationRawDeg(0):0}°, trái {arm.PronationRawDeg(1):0}°");
                Check(arm.RelaxWeight > 0.8f && zMax - zMin > 0.06f && lzMax - lzMin > 0.06f,
                    "Đi bộ: hai tay buông xuống và vung theo bước (ngược pha)", $"vung tay phải {(zMax - zMin) * 100f:0.0} cm, tay trái {(lzMax - lzMin) * 100f:0.0} cm");
                Check(lagMax > 0.001f && lagMax < 0.0119f, "Vải tay áo văng nhẹ theo cử động tay (không dính trần khi đi đều)", $"lệch tối đa {lagMax * 1000f:0.0} mm");
                vt.position = p0;
                viewer.enabled = true;
                viewer.SetPose(basePos, baseYaw, 18f);
                yield return WaitSeconds(1.0f);

                // (c) Tay trái đỡ cốc khi tay phải làm việc với cốc đó
                Grabbable beaker = NearestGrabbable("coc", cam.transform.position);
                if (beaker != null)
                {
                    // đứng trước cốc ~0,45 m (như khi rót vào cốc), cốc lệch trái một chút
                    Vector3 bp = beaker.transform.position;
                    Vector3 dir = new Vector3(bp.x - basePos.x, 0f, bp.z - basePos.z);
                    if (dir.sqrMagnitude < 1e-4f) dir = Vector3.forward;
                    dir.Normalize();
                    Vector3 rightF = Vector3.Cross(Vector3.up, dir);
                    Vector3 stand = new Vector3(bp.x, basePos.y, bp.z) - dir * 0.42f + rightF * 0.10f;
                    viewer.SetPose(stand, Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg, 28f);
                    viewer.enabled = false;   // chế độ ngồi sẽ kéo người về ghế – tạm tắt để đứng trước cốc
                    yield return WaitFrames(3);
                    hand.OverrideAimPoint = bp + Vector3.up * 0.14f + cam.transform.right * 0.05f;
                    arm.AssistOverride = beaker.transform;
                    yield return WaitSeconds(1.6f);
                    HandRig L = arm.LeftHand;
                    Debug.Log($"[TAY_DO] mắt={cam.transform.position} cốc={beaker.transform.position} (cách mắt {Vector3.Distance(cam.transform.position, beaker.transform.position):0.00} m) " +
                              $"đích đỡ={arm.AssistTarget} tay trái={(L != null ? L.transform.position.ToString() : "null")} viewer={viewer.transform.position}");
                    float dist = L != null && L.Grip != null ? Vector3.Distance(L.Grip.position, beaker.transform.position) : 9f;
                    Check(arm.AssistWeight > 0.9f && L != null && L.LastPenetrationCount == 0 && L.LastMaxSurfaceErrorMm <= 3f && dist < 0.15f,
                        "Tay trái đưa sang đỡ cốc, ngón ôm thành cốc không xuyên (IK ≤ 3 mm)",
                        $"mức đỡ={arm.AssistWeight:0.00}, vai trái–điểm đỡ {arm.AssistReachM * 100f:0} cm, cách cốc {dist * 100f:0.0} cm, pen={(L != null ? L.LastPenetrationCount : -1)}, err={(L != null ? L.LastMaxSurfaceErrorMm : -1):0.0} mm");
                    RenderToFile(cam, rt, tex, System.IO.Path.Combine(outDir, "sau_hv_3_tay_trai_do_coc_goc_nguoi_choi.png"));
                    ShotOutside(cam, sc, rt, tex, System.IO.Path.Combine(outDir, "sau_hv_3_tay_trai_do_coc_goc_ngoai.png"));
                    arm.AssistOverride = null;
                    hand.OverrideAimPoint = null;
                    yield return WaitSeconds(1.0f);
                    viewer.enabled = true;
                    viewer.SetPose(basePos, baseYaw, 18f);
                    Check(arm.AssistWeight < 0.1f, "Xong việc thì tay trái trở về chỗ nghỉ", $"mức đỡ={arm.AssistWeight:0.00}");
                }
                else Check(false, "Tìm thấy cốc để thử tay trái đỡ");

                var s0 = arm.SleeveRenderer(0);
                bool btn = s0 != null && s0.transform.parent != null && s0.transform.parent.GetComponentInChildren<MeshFilter>() != null;
                Check(s0 != null && s0.sharedMesh.blendShapeCount >= 2 && btn, "Tay áo có nếp xoắn khi lật cẳng tay (blendshape 2) và cúc cổ tay áo",
                    s0 != null ? $"blendshape={s0.sharedMesh.blendShapeCount}" : "");
            }
            finally
            {
                viewer.enabled = true;
                arm.AssistOverride = null;
                rt.Release();
                UnityEngine.Object.Destroy(rt); UnityEngine.Object.Destroy(tex); UnityEngine.Object.Destroy(go);
            }
        }

        static void ShotOutside(Camera cam, Camera sc, RenderTexture rt, Texture2D tex, string path)
        {
            Vector3 c = cam.transform.position;
            sc.fieldOfView = 55f;
            sc.transform.position = c + cam.transform.right * 0.65f + Vector3.up * 0.15f + cam.transform.forward * 0.45f;
            sc.transform.LookAt(c + cam.transform.forward * 0.15f - Vector3.up * 0.35f, Vector3.up);
            RenderToFile(sc, rt, tex, path);
        }

        static float SegDistT(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float t = ab.sqrMagnitude > 1e-10f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
            return Vector3.Distance(p, a + ab * t);
        }

        static Grabbable NearestGrabbable(string kind, Vector3 from)
        {
            Grabbable best = null;
            float bd = float.MaxValue;
            foreach (var g in UnityEngine.Object.FindObjectsByType<Grabbable>(FindObjectsInactive.Exclude))
            {
                if (g == null || !string.Equals(g.kind, kind, StringComparison.OrdinalIgnoreCase)) continue;
                float d = (g.transform.position - from).sqrMagnitude;
                if (d < bd) { bd = d; best = g; }
            }
            return best;
        }

        static float R(System.Random r, float a, float b) => a + (float)r.NextDouble() * (b - a);

        static FingerPose RandFinger(System.Random r) =>
            new FingerPose(R(r, 0f, 85f), R(r, -14f, 14f), R(r, 0f, 100f), R(r, 0f, 75f));

        static HandRig NewTestRig(string name, List<GameObject> temp)
        {
            var go = new GameObject(name);
            go.SetActive(false);
            go.transform.position = new Vector3(0f, -70f, 0f);
            var r = go.AddComponent<HandRig>();
            r.side = 0;
            r.ownSleeve = false;
            go.SetActive(true);
            r.SkipIdleUpdate = true;
            temp.Add(go);
            return r;
        }

        static void JointRots(HandRig r, Quaternion[] q)
        {
            for (int f = 0; f < 4; f++)
            {
                r.GetFingerJoints(f, out var m, out var p, out var d, out _);
                q[f * 3] = m.localRotation; q[f * 3 + 1] = p.localRotation; q[f * 3 + 2] = d.localRotation;
            }
            r.GetThumbJoints(out var c, out var tm, out var ti, out _);
            q[12] = c.localRotation; q[13] = tm.localRotation; q[14] = ti.localRotation;
        }

        static float MaxAngle(Quaternion[] a, Quaternion[] b)
        {
            float m = 0f;
            for (int i = 0; i < a.Length; i++) m = Mathf.Max(m, Quaternion.Angle(a[i], b[i]));
            return m;
        }
    }
}
