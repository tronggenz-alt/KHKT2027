using System;
using System.Collections.Generic;
using Khkt.Simulation.Bio.ThucVat;
using UnityEngine;

namespace Khkt.Demo.Bio.ThucVat
{
    public enum TrangThaiChayQuaTrinh
    {
        Nghi = 0,
        DangChay = 1,
        TamDung = 2,
        HoanThanh = 3
    }

    /// <summary>
    /// Bộ dựng & hoạt ảnh Mũi tên 3D giải phẫu tinh gọn cho 6 quá trình sinh lý thực vật.
    /// Lấy cảm hứng chuẩn từ ArrowRenderer & StepPlayer bên Phòng Giải phẫu:
    ///   • Mũi tên 3D kích thước nhỏ gọn, thanh mảnh vừa đủ nhìn, có viền tối nhẹ nổi bật trên mọi nền.
    ///   • Điểm đầu (chấm neo) và đỉnh nhọn hình nón đầu mũi tên chạm CHÍNH XÁC tọa độ giải phẫu (Transform + offset)
    ///     nên bám khít ngay cả khi thân uốn hướng sáng hoặc khi tách lớp 3D.
    ///   • Vẽ mọc dần từ điểm đầu → điểm cuối, kèm chấm sáng lướt liên tục chỉ rõ chiều dòng chảy;
    ///     các bước đã qua trên cùng khung nhìn được giữ lại mờ dần để học sinh nhìn trọn chuỗi đường đi.
    /// </summary>
    public class ThucVatMuiTen3D : MonoBehaviour
    {
        public QuaTrinhSinhLyThucVat QuaTrinhHienTai { get; private set; }
        public int BuocHienTai { get; private set; } = -1;
        public TrangThaiChayQuaTrinh TrangThai { get; private set; } = TrangThaiChayQuaTrinh.Nghi;
        public bool TuDongChay { get; private set; } = true;
        public float TocDo { get; private set; } = 1.0f;
        public bool DangMo => QuaTrinhHienTai != null && TrangThai != TrangThaiChayQuaTrinh.Nghi;
        public float TienDoDoc => pha == PhaBuoc.Doc ? Mathf.Clamp01(stepTimer / Mathf.Max(0.1f, GetReadDuration())) : 0f;
        public bool DangChoDoc => TuDongChay && pha == PhaBuoc.Doc && TrangThai == TrangThaiChayQuaTrinh.DangChay;

        public event Action<int, BuocQuaTrinhThucVat> OnStepStarted;
        public event Action<QuaTrinhSinhLyThucVat> OnFinished;
        public event Action OnStateChanged;

        enum PhaBuoc { Ve, Doc, Xong }

        CayHaiLaMam3D cay;
        Transform stageCamTr;
        Transform arrowRoot;
        PhaBuoc pha = PhaBuoc.Ve;
        float stepTimer;
        float globalClock;

        class ArrowVisual
        {
            public string keyFrom, keyTo;
            public int branchIdx, branchCount;
            public Color color;
            public bool isSliceScale;

            public GameObject root;
            public MeshFilter shaftMf, outlineMf;
            public MeshRenderer shaftMr, outlineMr;
            public Mesh shaftMesh, outlineMesh;
            public Transform headTr, headOutlineTr;
            public Renderer headMr, headOutlineMr;
            public Transform startDotTr, startDotOutlineTr;
            public Renderer startDotMr, startDotOutlineMr;
            public Transform beadTr;
            public Renderer beadMr;
            public Vector3 lastMidWorld;
        }

        class PulseVisual
        {
            public string keyAnchor;
            public Color color;
            public bool isSliceScale;
            public Transform ringTr, outlineTr;
            public Renderer ringMr, outlineMr;
            public Vector3 lastWorldPos;
        }

        class StepVisual
        {
            public int index;
            public BuocQuaTrinhThucVat step;
            public readonly List<ArrowVisual> arrows = new List<ArrowVisual>(4);
            public readonly List<PulseVisual> pulses = new List<PulseVisual>(2);
            public bool isPastApplied;
        }

        readonly Dictionary<int, StepVisual> stepVisuals = new Dictionary<int, StepVisual>();
        readonly Dictionary<string, Material> matCache = new Dictionary<string, Material>();
        Mesh sharedConeMesh, sharedConeOutlineMesh, sharedDotMesh, sharedRingMesh, sharedRingOutlineMesh;
        Shader unlitShader;

        // Bộ đệm dựng lưới ống thân mũi tên (không cấp phát mỗi khung hình)
        const int TubeSegs = 22;
        const int RadialSegs = 8;
        readonly Vector3[] curvePts = new Vector3[TubeSegs + 1];

        public void Initialize(CayHaiLaMam3D plant, Transform cameraTransform)
        {
            cay = plant;
            stageCamTr = cameraTransform;
            unlitShader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");

            var go = new GameObject("HeThongMuiTen3D_ThucVat") { layer = CayHaiLaMam3D.StageLayer };
            go.transform.SetParent(transform, false);
            arrowRoot = go.transform;

            BuildSharedPrimitives();
        }

        public void StartProcess(QuaTrinhSinhLyThucVat qt, int startStep = 0)
        {
            if (qt == null || qt.CacBuoc == null || qt.CacBuoc.Length == 0) return;
            ClearVisuals();
            QuaTrinhHienTai = qt;
            BuocHienTai = -1;
            GoToStep(startStep);
        }

        public void CloseProcess()
        {
            ClearVisuals();
            QuaTrinhHienTai = null;
            BuocHienTai = -1;
            TrangThai = TrangThaiChayQuaTrinh.Nghi;
            pha = PhaBuoc.Ve;
            stepTimer = 0f;
            OnStateChanged?.Invoke();
        }

        public void TogglePlayPause()
        {
            if (QuaTrinhHienTai == null) return;
            if (TrangThai == TrangThaiChayQuaTrinh.DangChay)
            {
                TrangThai = TrangThaiChayQuaTrinh.TamDung;
                OnStateChanged?.Invoke();
            }
            else if (TrangThai == TrangThaiChayQuaTrinh.HoanThanh || BuocHienTai < 0)
            {
                GoToStep(0);
            }
            else if (TrangThai == TrangThaiChayQuaTrinh.TamDung && pha == PhaBuoc.Xong)
            {
                NextStep();
            }
            else
            {
                TrangThai = TrangThaiChayQuaTrinh.DangChay;
                OnStateChanged?.Invoke();
            }
        }

        public void NextStep()
        {
            if (QuaTrinhHienTai == null) return;
            if (BuocHienTai < QuaTrinhHienTai.CacBuoc.Length - 1)
                GoToStep(BuocHienTai + 1);
            else
                FinishProcess();
        }

        public void PrevStep()
        {
            if (QuaTrinhHienTai == null || BuocHienTai <= 0) return;
            GoToStep(BuocHienTai - 1);
        }

        public void GoToStep(int index)
        {
            if (QuaTrinhHienTai == null || QuaTrinhHienTai.CacBuoc.Length == 0) return;
            int clamped = Mathf.Clamp(index, 0, QuaTrinhHienTai.CacBuoc.Length - 1);
            BuocHienTai = clamped;
            pha = PhaBuoc.Ve;
            stepTimer = 0f;
            TrangThai = TrangThaiChayQuaTrinh.DangChay;

            // Xoá các bước > clamped khi người dùng lùi bước
            var toRemove = new List<int>();
            foreach (var kv in stepVisuals)
                if (kv.Key > clamped) toRemove.Add(kv.Key);
            for (int i = 0; i < toRemove.Count; i++) DestroyStepVisual(toRemove[i]);

            var step = QuaTrinhHienTai.CacBuoc[clamped];
            OnStepStarted?.Invoke(clamped, step);
            OnStateChanged?.Invoke();
            SyncVisuals(0f);
        }

        public void SetSpeed(float spd)
        {
            TocDo = Mathf.Clamp(spd, 0.25f, 3.0f);
            OnStateChanged?.Invoke();
        }

        public void SetAutoPlay(bool on)
        {
            TuDongChay = on;
            if (on && TrangThai == TrangThaiChayQuaTrinh.TamDung && pha == PhaBuoc.Xong)
            {
                pha = PhaBuoc.Doc;
                stepTimer = 0f;
                TrangThai = TrangThaiChayQuaTrinh.DangChay;
            }
            OnStateChanged?.Invoke();
        }

        void FinishProcess()
        {
            pha = PhaBuoc.Xong;
            stepTimer = 0f;
            TrangThai = TrangThaiChayQuaTrinh.HoanThanh;
            OnFinished?.Invoke(QuaTrinhHienTai);
            OnStateChanged?.Invoke();
        }

        float GetDrawDuration()
        {
            if (QuaTrinhHienTai == null || BuocHienTai < 0) return 1.8f;
            return Mathf.Max(0.4f, QuaTrinhHienTai.CacBuoc[BuocHienTai].ThoiGianVe);
        }

        float GetReadDuration()
        {
            if (QuaTrinhHienTai == null || BuocHienTai < 0) return 4.0f;
            var s = QuaTrinhHienTai.CacBuoc[BuocHienTai];
            int chars = (s.DieuGiXayRa?.Length ?? 0) + (s.ViSao?.Length ?? 0);
            return Mathf.Clamp(2.8f + chars * 0.015f, 3.5f, 7.2f);
        }

        public bool TryGetActiveStepBadge(out Vector3 worldPos, out int stepNum, out string title, out Color color)
        {
            worldPos = Vector3.zero;
            stepNum = 0;
            title = null;
            color = Color.white;
            if (!DangMo || BuocHienTai < 0 || QuaTrinhHienTai == null) return false;
            if (!stepVisuals.TryGetValue(BuocHienTai, out var sv)) return false;

            var st = sv.step;
            stepNum = BuocHienTai + 1;
            title = st.TieuDe;
            color = ParseColor(st.MauHex);

            if (sv.arrows.Count > 0)
            {
                Vector3 sum = Vector3.zero;
                for (int i = 0; i < sv.arrows.Count; i++) sum += sv.arrows[i].lastMidWorld;
                worldPos = sum / sv.arrows.Count + new Vector3(0f, st.KhungNhin == "tong_the" ? 0.042f : 0.028f, 0f);
                return true;
            }
            if (sv.pulses.Count > 0)
            {
                Vector3 sum = Vector3.zero;
                for (int i = 0; i < sv.pulses.Count; i++) sum += sv.pulses[i].lastWorldPos;
                worldPos = sum / sv.pulses.Count + new Vector3(0f, st.KhungNhin == "tong_the" ? 0.045f : 0.032f, 0f);
                return true;
            }
            return false;
        }

        void Update()
        {
            if (!DangMo || QuaTrinhHienTai == null || BuocHienTai < 0) return;

            float dt = Time.unscaledDeltaTime;
            globalClock += dt;

            if (TrangThai == TrangThaiChayQuaTrinh.DangChay)
            {
                stepTimer += dt * TocDo;
                if (pha == PhaBuoc.Ve && stepTimer >= GetDrawDuration())
                {
                    if (BuocHienTai >= QuaTrinhHienTai.CacBuoc.Length - 1)
                    {
                        FinishProcess();
                    }
                    else if (TuDongChay)
                    {
                        pha = PhaBuoc.Doc;
                        stepTimer = 0f;
                    }
                    else
                    {
                        pha = PhaBuoc.Xong;
                        TrangThai = TrangThaiChayQuaTrinh.TamDung;
                        OnStateChanged?.Invoke();
                    }
                }
                else if (pha == PhaBuoc.Doc && stepTimer >= GetReadDuration())
                {
                    NextStep();
                    return;
                }
            }

            float drawProgress = pha == PhaBuoc.Ve ? Mathf.Clamp01(stepTimer / GetDrawDuration()) : 1f;
            SyncVisuals(drawProgress);
        }

        void SyncVisuals(float activeDrawT)
        {
            if (QuaTrinhHienTai == null || cay == null) return;
            string curViewId = QuaTrinhHienTai.CacBuoc[BuocHienTai].KhungNhin;

            for (int k = 0; k <= BuocHienTai; k++)
            {
                var step = QuaTrinhHienTai.CacBuoc[k];
                bool sameView = string.Equals(step.KhungNhin, curViewId, StringComparison.OrdinalIgnoreCase);

                if (!sameView)
                {
                    if (stepVisuals.ContainsKey(k)) DestroyStepVisual(k);
                    continue;
                }

                if (!stepVisuals.TryGetValue(k, out var sv))
                    sv = BuildStepVisual(k, step);

                bool isPast = k < BuocHienTai;
                float t = isPast ? 1f : EaseOutCubic(activeDrawT);
                UpdateStepVisual(sv, t, isPast);
            }
        }

        StepVisual BuildStepVisual(int idx, BuocQuaTrinhThucVat step)
        {
            var sv = new StepVisual { index = idx, step = step };
            Color col = ParseColor(step.MauHex);
            bool isSlice = !string.Equals(step.KhungNhin, "tong_the", StringComparison.OrdinalIgnoreCase);

            if (step.TaiCho)
            {
                int cnt = step.NeoTu != null ? step.NeoTu.Length : 0;
                for (int i = 0; i < cnt; i++)
                {
                    var pv = new PulseVisual { keyAnchor = step.NeoTu[i], color = col, isSliceScale = isSlice };
                    var goO = MakeMeshObj($"XungVien_{idx}_{i}", sharedRingOutlineMesh, GetMat(new Color(0.03f, 0.05f, 0.08f, 0.78f), "outline", 3994));
                    var goR = MakeMeshObj($"Xung_{idx}_{i}", sharedRingMesh, GetMat(col, "pulse_active", 3998));
                    pv.outlineTr = goO.transform; pv.outlineMr = goO.GetComponent<Renderer>();
                    pv.ringTr = goR.transform; pv.ringMr = goR.GetComponent<Renderer>();
                    sv.pulses.Add(pv);
                }
            }
            else
            {
                var pairs = new List<(string a, string b)>(4);
                int nf = step.NeoTu?.Length ?? 0;
                int nt = step.NeoDen?.Length ?? 0;
                if (nf == nt)
                {
                    for (int i = 0; i < nf; i++) pairs.Add((step.NeoTu[i], step.NeoDen[i]));
                }
                else if (nf == 1)
                {
                    for (int i = 0; i < nt; i++) pairs.Add((step.NeoTu[0], step.NeoDen[i]));
                }
                else if (nt == 1)
                {
                    for (int i = 0; i < nf; i++) pairs.Add((step.NeoTu[i], step.NeoDen[0]));
                }

                for (int p = 0; p < pairs.Count; p++)
                {
                    var av = new ArrowVisual
                    {
                        keyFrom = pairs[p].a,
                        keyTo = pairs[p].b,
                        branchIdx = p,
                        branchCount = pairs.Count,
                        color = col,
                        isSliceScale = isSlice
                    };
                    av.root = new GameObject($"MuiTen_{idx}_{p}") { layer = CayHaiLaMam3D.StageLayer };
                    av.root.transform.SetParent(arrowRoot, false);

                    av.outlineMesh = new Mesh { name = $"VienOng_{idx}_{p}" };
                    av.shaftMesh = new Mesh { name = $"ThanOng_{idx}_{p}" };

                    var outlineGo = MakeMeshObj("VienThan", av.outlineMesh, GetMat(new Color(0.03f, 0.05f, 0.08f, 0.80f), "outline", 3994), av.root.transform);
                    var shaftGo = MakeMeshObj("ThanMuiTen", av.shaftMesh, GetMat(col, "active", 3997), av.root.transform);
                    av.outlineMf = outlineGo.GetComponent<MeshFilter>(); av.outlineMr = outlineGo.GetComponent<MeshRenderer>();
                    av.shaftMf = shaftGo.GetComponent<MeshFilter>(); av.shaftMr = shaftGo.GetComponent<MeshRenderer>();

                    var headOutGo = MakeMeshObj("VienDau", sharedConeOutlineMesh, GetMat(new Color(0.03f, 0.05f, 0.08f, 0.82f), "outline", 3998), av.root.transform);
                    var headGo = MakeMeshObj("DauMuiTen", sharedConeMesh, GetMat(col, "active", 3999), av.root.transform);
                    av.headOutlineTr = headOutGo.transform; av.headOutlineMr = headOutGo.GetComponent<Renderer>();
                    av.headTr = headGo.transform; av.headMr = headGo.GetComponent<Renderer>();

                    var dotOutGo = MakeMeshObj("VienDiemDau", sharedDotMesh, GetMat(new Color(0.03f, 0.05f, 0.08f, 0.82f), "outline", 3995), av.root.transform);
                    var dotGo = MakeMeshObj("DiemDau", sharedDotMesh, GetMat(col, "active", 3996), av.root.transform);
                    av.startDotOutlineTr = dotOutGo.transform; av.startDotOutlineMr = dotOutGo.GetComponent<Renderer>();
                    av.startDotTr = dotGo.transform; av.startDotMr = dotGo.GetComponent<Renderer>();

                    var beadGo = MakeMeshObj("HatLuot", sharedDotMesh, GetMat(new Color(1f, 1f, 1f, 0.96f), "bead", 4000), av.root.transform);
                    av.beadTr = beadGo.transform; av.beadMr = beadGo.GetComponent<Renderer>();

                    sv.arrows.Add(av);
                }
            }

            stepVisuals[idx] = sv;
            return sv;
        }

        void UpdateStepVisual(StepVisual sv, float t, bool isPast)
        {
            if (sv.isPastApplied != isPast)
            {
                sv.isPastApplied = isPast;
                for (int i = 0; i < sv.arrows.Count; i++)
                {
                    var av = sv.arrows[i];
                    var mat = isPast ? GetMat(new Color(av.color.r, av.color.g, av.color.b, 0.34f), "past", 3990)
                                     : GetMat(av.color, "active", 3997);
                    av.shaftMr.sharedMaterial = mat;
                    av.headMr.sharedMaterial = mat;
                    av.outlineMr.enabled = !isPast;
                    av.headOutlineMr.enabled = !isPast;
                    av.startDotMr.enabled = !isPast;
                    av.startDotOutlineMr.enabled = !isPast;
                }
                for (int i = 0; i < sv.pulses.Count; i++)
                {
                    var pv = sv.pulses[i];
                    pv.ringMr.sharedMaterial = isPast
                        ? GetMat(new Color(pv.color.r, pv.color.g, pv.color.b, 0.24f), "pulse_past", 3990)
                        : GetMat(pv.color, "pulse_active", 3998);
                    pv.outlineMr.enabled = !isPast;
                }
            }

            // 1. Cập nhật các vòng xung tại chỗ
            Quaternion camRot = stageCamTr != null ? stageCamTr.rotation : Quaternion.identity;
            for (int i = 0; i < sv.pulses.Count; i++)
            {
                var pv = sv.pulses[i];
                if (!cay.TryGetAnchorWorldPos(pv.keyAnchor, out Vector3 wPos)) continue;
                pv.lastWorldPos = wPos;
                float baseScale = pv.isSliceScale ? 0.017f : 0.022f;
                float pulse = isPast ? 0.72f : (0.82f + 0.24f * (0.5f + 0.5f * Mathf.Sin(globalClock * 5.2f)));
                float s = baseScale * pulse;
                pv.ringTr.SetPositionAndRotation(wPos, camRot);
                pv.ringTr.localScale = Vector3.one * s;
                pv.outlineTr.SetPositionAndRotation(wPos, camRot);
                pv.outlineTr.localScale = Vector3.one * s;
            }

            // 2. Cập nhật từng mũi tên 3D mảnh gọn — bám CHÍNH XÁC điểm đầu p1 và điểm cuối p2
            for (int i = 0; i < sv.arrows.Count; i++)
            {
                var av = sv.arrows[i];
                if (!cay.TryGetAnchorWorldPos(av.keyFrom, out Vector3 p1) || !cay.TryGetAnchorWorldPos(av.keyTo, out Vector3 p2))
                    continue;

                Vector3 delta = p2 - p1;
                float dist = Mathf.Max(0.006f, delta.magnitude);

                // Đường cong mảnh nhẹ hướng về phía trước (-Z camera) vừa đủ rõ, điểm đầu p1 và điểm cuối p2 tuyệt đối giữ nguyên!
                Vector3 mid = (p1 + p2) * 0.5f;
                float archOut = Mathf.Clamp(dist * 0.16f, av.isSliceScale ? 0.014f : 0.009f, av.isSliceScale ? 0.028f : 0.036f);
                Vector3 camDir = stageCamTr != null ? (stageCamTr.position - mid).normalized : Vector3.back;
                mid += camDir * archOut;

                // Tách nhẹ nếu 2 nhánh nối gần trùng nhau
                if (av.branchCount > 1 && dist > 0.015f)
                {
                    Vector3 side = Vector3.Cross(delta.normalized, camDir).normalized;
                    float spread = ((av.branchIdx / (float)(av.branchCount - 1)) - 0.5f) * Mathf.Min(0.018f, dist * 0.16f);
                    mid += side * spread;
                }

                // Tỉ lệ mũi tên NHỎ GỌN tinh tế ("làm nhỏ thôi, vừa đủ nhìn")
                float shaftR = (av.isSliceScale ? 0.0021f : 0.0025f) * (isPast ? 0.78f : 1.0f);
                float outlineR = shaftR * 1.48f;
                float coneR = (av.isSliceScale ? 0.0056f : 0.0064f) * (isPast ? 0.80f : 1.0f);
                float coneLen = (av.isSliceScale ? 0.0142f : 0.0165f) * (isPast ? 0.80f : 1.0f);
                float dotScale = av.isSliceScale ? 0.0062f : 0.0074f;

                av.lastMidWorld = EvalQuadraticBezier(p1, mid, p2, 0.5f);

                // Chấm neo điểm đầu chính xác tại p1
                av.startDotTr.position = p1;
                av.startDotTr.localScale = Vector3.one * dotScale;
                av.startDotOutlineTr.position = p1;
                av.startDotOutlineTr.localScale = Vector3.one * (dotScale * 1.42f);

                // Đầu mũi tên hình nón: ĐỈNH nhọn trùng gốc (0,0,0) -> chạm CHÍNH XÁC điểm p2 khi t = 1!
                bool showHead = t > 0.03f;
                av.headMr.enabled = showHead;
                av.headOutlineMr.enabled = showHead && !isPast;
                if (showHead)
                {
                    Vector3 tipPos = EvalQuadraticBezier(p1, mid, p2, t);
                    Vector3 tangent = EvalQuadraticBezierTangent(p1, mid, p2, t).normalized;
                    if (tangent.sqrMagnitude < 0.0001f) tangent = delta.normalized;
                    Quaternion headRot = Quaternion.FromToRotation(Vector3.up, tangent);

                    av.headTr.SetPositionAndRotation(tipPos, headRot);
                    av.headTr.localScale = new Vector3(coneR, coneLen, coneR);
                    av.headOutlineTr.SetPositionAndRotation(tipPos, headRot);
                    av.headOutlineTr.localScale = new Vector3(coneR * 1.24f, coneLen * 1.08f, coneR * 1.24f);
                }

                // Thân ống dừng đúng trước đáy hình nón để không bao giờ thò xuyên qua mũi nhọn
                // Quy đổi sang hệ tọa độ cục bộ của av.root (vì sân khấu đặt tại StageOrigin y = -420)
                float approxLen = dist * 1.06f;
                float headFrac = Mathf.Min(0.45f, (coneLen * 0.84f) / Mathf.Max(0.01f, approxLen));
                float shaftEndT = Mathf.Max(0f, t - headFrac * Mathf.Clamp01(t * 2.6f));
                if (shaftEndT > 0.015f)
                {
                    Transform rootTr = av.root.transform;
                    Vector3 lp1 = rootTr.InverseTransformPoint(p1);
                    Vector3 lmid = rootTr.InverseTransformPoint(mid);
                    Vector3 lp2 = rootTr.InverseTransformPoint(p2);

                    RebuildTubeMesh(av.shaftMesh, lp1, lmid, lp2, shaftEndT, shaftR);
                    av.shaftMr.enabled = true;
                    if (!isPast)
                    {
                        RebuildTubeMesh(av.outlineMesh, lp1, lmid, lp2, shaftEndT, outlineR);
                        av.outlineMr.enabled = true;
                    }
                }
                else
                {
                    av.shaftMr.enabled = false;
                    av.outlineMr.enabled = false;
                }

                // Hạt sáng nhỏ lướt liên tục p1 → p2 thể hiện sinh động chiều dòng chảy
                bool showBead = !isPast && shaftEndT > 0.12f;
                av.beadMr.enabled = showBead;
                if (showBead)
                {
                    float phase = (globalClock * 0.72f + av.branchIdx * 0.35f) % 1f;
                    float uBead = phase * shaftEndT;
                    av.beadTr.position = EvalQuadraticBezier(p1, mid, p2, uBead);
                    av.beadTr.localScale = Vector3.one * (dotScale * 0.82f);
                }
            }
        }

        static Vector3 EvalQuadraticBezier(Vector3 p0, Vector3 p1, Vector3 p2, float t)
        {
            float omt = 1f - t;
            return omt * omt * p0 + 2f * omt * t * p1 + t * t * p2;
        }

        static Vector3 EvalQuadraticBezierTangent(Vector3 p0, Vector3 p1, Vector3 p2, float t)
        {
            return 2f * (1f - t) * (p1 - p0) + 2f * t * (p2 - p1);
        }

        static float EaseOutCubic(float t)
        {
            t = Mathf.Clamp01(t);
            float inv = 1f - t;
            return 1f - inv * inv * inv;
        }

        void RebuildTubeMesh(Mesh mesh, Vector3 p0, Vector3 p1, Vector3 p2, float maxT, float radius)
        {
            int rings = TubeSegs + 1;
            for (int i = 0; i < rings; i++)
                curvePts[i] = EvalQuadraticBezier(p0, p1, p2, (i / (float)TubeSegs) * maxT);

            int vCount = rings * (RadialSegs + 1);
            var verts = new Vector3[vCount];
            var tris = new int[TubeSegs * RadialSegs * 6];

            Vector3 prevRight = Vector3.right;
            for (int i = 0; i < rings; i++)
            {
                Vector3 tangent = i == 0 ? (curvePts[1] - curvePts[0]).normalized
                                : i == rings - 1 ? (curvePts[i] - curvePts[i - 1]).normalized
                                : (curvePts[i + 1] - curvePts[i - 1]).normalized;
                if (tangent.sqrMagnitude < 0.0001f) tangent = Vector3.up;

                Vector3 refUp = Mathf.Abs(Vector3.Dot(tangent, prevRight)) < 0.90f ? prevRight : Vector3.forward;
                Vector3 binorm = Vector3.Cross(tangent, refUp).normalized;
                Vector3 norm = Vector3.Cross(binorm, tangent).normalized;
                prevRight = norm;

                int row = i * (RadialSegs + 1);
                for (int j = 0; j <= RadialSegs; j++)
                {
                    float a = (j / (float)RadialSegs) * Mathf.PI * 2f;
                    verts[row + j] = curvePts[i] + (norm * Mathf.Cos(a) + binorm * Mathf.Sin(a)) * radius;
                }
            }

            int ti = 0;
            int stride = RadialSegs + 1;
            for (int i = 0; i < TubeSegs; i++)
            {
                for (int j = 0; j < RadialSegs; j++)
                {
                    int a = i * stride + j;
                    int b = a + 1;
                    int c = a + stride;
                    int d = c + 1;
                    tris[ti++] = a; tris[ti++] = b; tris[ti++] = c;
                    tris[ti++] = b; tris[ti++] = d; tris[ti++] = c;
                }
            }

            mesh.Clear();
            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
        }

        void BuildSharedPrimitives()
        {
            // Hình nón đầu mũi tên: ĐỈNH tại (0, 0, 0), đáy tại y = -1.0 (bán kính đáy = 1.0)
            sharedConeMesh = BuildTipOriginConeMesh("MuiTenDauNon", 1.0f, 1.0f, 14);
            sharedConeOutlineMesh = BuildTipOriginConeMesh("MuiTenDauVien", 1.0f, 1.0f, 14);
            sharedDotMesh = BuildUnitSphereMesh("MuiTenDiemTron", 10, 8);
            sharedRingMesh = BuildFlatRingMesh("MuiTenVongXung", 0.68f, 1.0f, 32);
            sharedRingOutlineMesh = BuildFlatRingMesh("MuiTenVongVien", 0.56f, 1.14f, 32);
        }

        static Mesh BuildTipOriginConeMesh(string name, float baseRadius, float height, int segs)
        {
            var verts = new List<Vector3>(segs * 2 + 3);
            var tris = new List<int>(segs * 6);

            // Đỉnh nhọn ở đúng (0, 0, 0)
            int tipIdx = 0;
            verts.Add(Vector3.zero);

            for (int i = 0; i <= segs; i++)
            {
                float a = (i / (float)segs) * Mathf.PI * 2f;
                verts.Add(new Vector3(Mathf.Cos(a) * baseRadius, -height, Mathf.Sin(a) * baseRadius));
            }
            for (int i = 0; i < segs; i++)
            {
                tris.Add(tipIdx); tris.Add(1 + i + 1); tris.Add(1 + i);
            }

            int centerBot = verts.Count;
            verts.Add(new Vector3(0f, -height, 0f));
            for (int i = 0; i < segs; i++)
            {
                tris.Add(centerBot); tris.Add(1 + i); tris.Add(1 + i + 1);
            }

            var m = new Mesh { name = name };
            m.SetVertices(verts);
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        static Mesh BuildUnitSphereMesh(string name, int lon, int lat)
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            for (int y = 0; y <= lat; y++)
            {
                float phi = (y / (float)lat) * Mathf.PI;
                for (int x = 0; x <= lon; x++)
                {
                    float theta = (x / (float)lon) * Mathf.PI * 2f;
                    verts.Add(new Vector3(Mathf.Sin(phi) * Mathf.Cos(theta), Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Sin(theta)) * 0.5f);
                }
            }
            int stride = lon + 1;
            for (int y = 0; y < lat; y++)
                for (int x = 0; x < lon; x++)
                {
                    int a = y * stride + x, b = a + 1, c = a + stride, d = c + 1;
                    tris.Add(a); tris.Add(b); tris.Add(c);
                    tris.Add(b); tris.Add(d); tris.Add(c);
                }
            var m = new Mesh { name = name };
            m.SetVertices(verts);
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        static Mesh BuildFlatRingMesh(string name, float rIn, float rOut, int segs)
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            for (int i = 0; i <= segs; i++)
            {
                float a = (i / (float)segs) * Mathf.PI * 2f;
                float c = Mathf.Cos(a), s = Mathf.Sin(a);
                verts.Add(new Vector3(c * rOut, s * rOut, 0f));
                verts.Add(new Vector3(c * rIn, s * rIn, 0f));
            }
            for (int i = 0; i < segs; i++)
            {
                int a = i * 2, b = a + 1, c = a + 2, d = a + 3;
                tris.Add(a); tris.Add(c); tris.Add(b);
                tris.Add(b); tris.Add(c); tris.Add(d);
                tris.Add(a); tris.Add(b); tris.Add(c);
                tris.Add(b); tris.Add(d); tris.Add(c);
            }
            var m = new Mesh { name = name };
            m.SetVertices(verts);
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        GameObject MakeMeshObj(string name, Mesh mesh, Material mat, Transform parent = null)
        {
            var go = new GameObject(name) { layer = CayHaiLaMam3D.StageLayer };
            go.transform.SetParent(parent != null ? parent : arrowRoot, false);
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return go;
        }

        Material GetMat(Color col, string tag, int renderQueue)
        {
            string key = $"{tag}_{col.r:F2}_{col.g:F2}_{col.b:F2}_{col.a:F2}_{renderQueue}";
            if (matCache.TryGetValue(key, out var existing)) return existing;

            var m = new Material(unlitShader) { hideFlags = HideFlags.DontSave };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", col);
            if (m.HasProperty("_Color")) m.SetColor("_Color", col);
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
            if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 0f);
            if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);
            if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = renderQueue;
            matCache[key] = m;
            return m;
        }

        static Color ParseColor(string hex)
        {
            return ColorUtility.TryParseHtmlString(hex, out Color c) ? c : new Color(0.22f, 0.74f, 0.98f);
        }

        void DestroyStepVisual(int key)
        {
            if (!stepVisuals.TryGetValue(key, out var sv)) return;
            for (int i = 0; i < sv.arrows.Count; i++)
            {
                if (sv.arrows[i].shaftMesh != null) Destroy(sv.arrows[i].shaftMesh);
                if (sv.arrows[i].outlineMesh != null) Destroy(sv.arrows[i].outlineMesh);
                if (sv.arrows[i].root != null) Destroy(sv.arrows[i].root);
            }
            for (int i = 0; i < sv.pulses.Count; i++)
            {
                if (sv.pulses[i].ringTr != null) Destroy(sv.pulses[i].ringTr.gameObject);
                if (sv.pulses[i].outlineTr != null) Destroy(sv.pulses[i].outlineTr.gameObject);
            }
            stepVisuals.Remove(key);
        }

        void ClearVisuals()
        {
            var keys = new List<int>(stepVisuals.Keys);
            for (int i = 0; i < keys.Count; i++) DestroyStepVisual(keys[i]);
        }

        void OnDestroy()
        {
            ClearVisuals();
            foreach (var m in matCache.Values) if (m != null) Destroy(m);
            matCache.Clear();
            if (sharedConeMesh != null) Destroy(sharedConeMesh);
            if (sharedConeOutlineMesh != null) Destroy(sharedConeOutlineMesh);
            if (sharedDotMesh != null) Destroy(sharedDotMesh);
            if (sharedRingMesh != null) Destroy(sharedRingMesh);
            if (sharedRingOutlineMesh != null) Destroy(sharedRingOutlineMesh);
        }
    }
}
