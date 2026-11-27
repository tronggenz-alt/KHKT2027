using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;
using Khkt.Demo;

namespace Khkt.Rendering
{
    /// <summary>
    /// Bộ nhớ đệm trạng thái bẩn và lưới độ sáng (24x14) đọc về bất đồng bộ từ GPU.
    /// Giúp kính ngoài Lab chỉ chạy tháp mờ khi nội dung thật sự thay đổi, còn đứng yên = 0 ms GPU.
    /// </summary>
    public static class KinhLongBlurCache
    {
        public const int LuoiRong = 24;
        public const int LuoiCao = 14;

        static int _khungBan = 4;
        static int _demBoQuaLienTiep;
        static bool _epChupMoiKhung;

        static readonly float[] _luoiSang = new float[LuoiRong * LuoiCao];
        static bool _coLuoiGpu;
        static double _thoiDiemDocCuoi = -10.0;
        static bool _dangChoReadback;

        public static int SoKhungBanConLai => _khungBan;
        public static int SoKhungBoQuaLienTiep => _demBoQuaLienTiep;
        public static bool EpChupMoiKhung { get => _epChupMoiKhung; set => _epChupMoiKhung = value; }
        public static bool CoLuoiGpu => _coLuoiGpu;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _khungBan = 4;
            _demBoQuaLienTiep = 0;
            _epChupMoiKhung = false;
            _coLuoiGpu = false;
            _dangChoReadback = false;
            _thoiDiemDocCuoi = -10.0;
        }

        public static void DanhDauBan(int soKhung = 2)
        {
            if (soKhung > _khungBan) _khungBan = soKhung;
        }

        public static bool CanRenderBlur(int width, int height, ref int lastW, ref int lastH)
        {
            if (width != lastW || height != lastH)
            {
                lastW = width;
                lastH = height;
                _khungBan = Mathf.Max(_khungBan, 3);
            }

            if (_epChupMoiKhung)
                return true;

            if (_khungBan > 0)
            {
                _khungBan--;
                _demBoQuaLienTiep = 0;
                return true;
            }

            _demBoQuaLienTiep++;
            return false;
        }

        public static void ThuYeuCauDocSang(RTHandle probeRt)
        {
            if (_dangChoReadback || probeRt == null || probeRt.rt == null) return;
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return;
            double now = Time.realtimeSinceStartupAsDouble;
            if (now - _thoiDiemDocCuoi < 0.08) return;

            _thoiDiemDocCuoi = now;
            _dangChoReadback = true;
            AsyncGPUReadback.Request(probeRt.rt, 0, TextureFormat.RGBA32, OnReadbackComplete);
        }

        static void OnReadbackComplete(AsyncGPUReadbackRequest req)
        {
            _dangChoReadback = false;
            if (req.hasError) return;
            var data = req.GetData<Color32>();
            int w = req.width;
            int h = req.height;
            if (w <= 0 || h <= 0 || data.Length < w * h) return;

            int copyW = Mathf.Min(w, LuoiRong);
            int copyH = Mathf.Min(h, LuoiCao);
            for (int y = 0; y < LuoiCao; y++)
            {
                int sy = Mathf.Clamp((y * copyH) / LuoiCao, 0, h - 1);
                for (int x = 0; x < LuoiRong; x++)
                {
                    int sx = Mathf.Clamp((x * copyW) / LuoiRong, 0, w - 1);
                    Color32 c = data[sy * w + sx];
                    float r = c.r / 255f;
                    float g = c.g / 255f;
                    float b = c.b / 255f;
                    _luoiSang[y * LuoiRong + x] = 0.2126f * r + 0.7152f * g + 0.0722f * b;
                }
            }
            _coLuoiGpu = true;
        }

        public static float DocDoSang(Vector2 uvMin, Vector2 uvMax)
        {
            if (!_coLuoiGpu)
                return KinhLongNen.DocDoSangKhuVuc(uvMin, uvMax);

            int x0 = Mathf.Clamp(Mathf.FloorToInt(uvMin.x * LuoiRong), 0, LuoiRong - 1);
            int x1 = Mathf.Clamp(Mathf.CeilToInt(uvMax.x * LuoiRong) - 1, x0, LuoiRong - 1);
            int y0 = Mathf.Clamp(Mathf.FloorToInt(uvMin.y * LuoiCao), 0, LuoiCao - 1);
            int y1 = Mathf.Clamp(Mathf.CeilToInt(uvMax.y * LuoiCao) - 1, y0, LuoiCao - 1);

            float sum = 0f;
            int count = 0;
            for (int y = y0; y <= y1; y++)
            {
                int row = y * LuoiRong;
                for (int x = x0; x <= x1; x++)
                {
                    sum += _luoiSang[row + x];
                    count++;
                }
            }
            return count > 0 ? sum / count : 0.5f;
        }
    }

    /// <summary>
    /// Renderer Feature trên Base Camera duy nhất:
    ///   1) Ở AfterRenderingTransparents (sau khi Content Layer đã vẽ xong, Layer 14 KinhDieuHuong bị loại khỏi pass mặc định),
    ///      chụp _KL_NoiDung và tạo tháp ảnh mờ Dual Kawase _KL_Mo1..4 (hoặc tái sử dụng 0 ms nếu chưa dirty).
    ///   2) Gắn _KL_NoiDung và _KL_Mo1..4 vào RenderGraph rồi vẽ ngay RendererList của Layer 14 KinhDieuHuong lên activeColorTexture.
    /// KHÔNG chạy bất kỳ pass nào trong Phòng Lab hay chế độ Classic -> giữ nguyên 100% pixel Phòng Lab.
    /// </summary>
    public class KinhLongBlurFeature : ScriptableRendererFeature
    {
        [SerializeField] Shader blurShader;
        Material _mat;
        KinhLongBlurPass _pass;

        public override void Create()
        {
            if (blurShader == null)
                blurShader = Shader.Find("Hidden/Khkt/KinhLongThapMo");
            if (blurShader != null && (_mat == null || _mat.shader != blurShader))
            {
                if (_mat != null) CoreUtils.Destroy(_mat);
                _mat = CoreUtils.CreateEngineMaterial(blurShader);
            }
            _pass = new KinhLongBlurPass { renderPassEvent = RenderPassEvent.AfterRenderingTransparents };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (!GlassKit.LiquidGlass)
                return;

            Camera cam = renderingData.cameraData.camera;
            if (renderingData.cameraData.renderType != CameraRenderType.Base || !KinhLongCameraNen.IsCameraNenActive(cam))
                return;

            if (_mat == null)
            {
                if (blurShader == null) blurShader = Shader.Find("Hidden/Khkt/KinhLongThapMo");
                if (blurShader != null) _mat = CoreUtils.CreateEngineMaterial(blurShader);
            }
            if (_mat == null) return;

            _pass.SetMaterial(_mat);
            renderer.EnqueuePass(_pass);
        }

        protected override void Dispose(bool disposing)
        {
            _pass?.Dispose();
            _pass = null;
            if (_mat != null)
            {
                CoreUtils.Destroy(_mat);
                _mat = null;
            }
        }

        internal static readonly int IdNoiDung = Shader.PropertyToID("_KL_NoiDung");
        internal static readonly int IdMo1 = Shader.PropertyToID("_KL_Mo1");
        internal static readonly int IdMo2 = Shader.PropertyToID("_KL_Mo2");
        internal static readonly int IdMo3 = Shader.PropertyToID("_KL_Mo3");
        internal static readonly int IdMo4 = Shader.PropertyToID("_KL_Mo4");
        internal static readonly int IdDungNoiDung = Shader.PropertyToID("_KL_DungNoiDung");
        internal static readonly int IdBlurStep = Shader.PropertyToID("_KL_BlurStep");

        public class KinhLongBlurPass : ScriptableRenderPass
        {
            static readonly List<ShaderTagId> NavShaderTags = new List<ShaderTagId>
            {
                new ShaderTagId("SRPDefaultUnlit"),
                new ShaderTagId("UniversalForward"),
                new ShaderTagId("UniversalForwardOnly")
            };
            static readonly ShaderTagId[] TagValuesTmp = { ShaderTagId.none };

            Material _mat;
            RTHandle _noiDung;
            RTHandle _mo1Down, _mo2Down, _mo3Down, _mo4Down;
            RTHandle _mo3Up, _mo2Up, _mo1Up;
            RTHandle _probe;
            int _lastW, _lastH;

            public static Texture NoiDungRT { get; private set; }
            public static Texture Mo1RT { get; private set; }
            public static Texture Mo2RT { get; private set; }
            public static Texture Mo3RT { get; private set; }
            public static Texture Mo4RT { get; private set; }

            public void SetMaterial(Material mat) => _mat = mat;

            public void Dispose()
            {
                _noiDung?.Release();
                _mo1Down?.Release();
                _mo2Down?.Release();
                _mo3Down?.Release();
                _mo4Down?.Release();
                _mo3Up?.Release();
                _mo2Up?.Release();
                _mo1Up?.Release();
                _probe?.Release();
                _noiDung = _mo1Down = _mo2Down = _mo3Down = _mo4Down = _mo3Up = _mo2Up = _mo1Up = _probe = null;
                NoiDungRT = Mo1RT = Mo2RT = Mo3RT = Mo4RT = null;
                Shader.SetGlobalFloat(IdDungNoiDung, 0f);
            }

            void CapPhatMuc(int w, int h, GraphicsFormat colorFmt)
            {
                CapPhat(ref _noiDung, w, h, colorFmt, "_KL_NoiDung_RT");
                CapPhat(ref _mo1Down, Mathf.Max(2, w / 2), Mathf.Max(2, h / 2), colorFmt, "_KL_Mo1D_RT");
                CapPhat(ref _mo2Down, Mathf.Max(2, w / 4), Mathf.Max(2, h / 4), colorFmt, "_KL_Mo2D_RT");
                CapPhat(ref _mo3Down, Mathf.Max(2, w / 8), Mathf.Max(2, h / 8), colorFmt, "_KL_Mo3D_RT");
                CapPhat(ref _mo4Down, Mathf.Max(2, w / 16), Mathf.Max(2, h / 16), colorFmt, "_KL_Mo4_RT");
                CapPhat(ref _mo3Up, Mathf.Max(2, w / 8), Mathf.Max(2, h / 8), colorFmt, "_KL_Mo3U_RT");
                CapPhat(ref _mo2Up, Mathf.Max(2, w / 4), Mathf.Max(2, h / 4), colorFmt, "_KL_Mo2U_RT");
                CapPhat(ref _mo1Up, Mathf.Max(2, w / 2), Mathf.Max(2, h / 2), colorFmt, "_KL_Mo1U_RT");
                CapPhat(ref _probe, KinhLongBlurCache.LuoiRong, KinhLongBlurCache.LuoiCao, GraphicsFormat.R8G8B8A8_SRGB, "_KL_Probe_RT");

                NoiDungRT = _noiDung.rt;
                Mo1RT = _mo1Up.rt;
                Mo2RT = _mo2Up.rt;
                Mo3RT = _mo3Up.rt;
                Mo4RT = _mo4Down.rt;
            }

            static void CapPhat(ref RTHandle rt, int w, int h, GraphicsFormat fmt, string name)
            {
                var desc = new RenderTextureDescriptor(w, h, fmt, 0)
                {
                    msaaSamples = 1,
                    useMipMap = false,
                    autoGenerateMips = false,
                    depthStencilFormat = GraphicsFormat.None
                };
                RenderingUtils.ReAllocateHandleIfNeeded(ref rt, desc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: name);
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (_mat == null) return;

                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                TextureHandle activeColor = resourceData.activeColorTexture;
                if (!activeColor.IsValid()) return;

                int w = Mathf.Max(64, cameraData.cameraTargetDescriptor.width);
                int h = Mathf.Max(64, cameraData.cameraTargetDescriptor.height);
                GraphicsFormat colorFmt = cameraData.cameraTargetDescriptor.graphicsFormat;
                if (colorFmt == GraphicsFormat.None)
                    colorFmt = SystemInfo.GetGraphicsFormat(DefaultFormat.LDR);

                bool sizeChanged = (w != _lastW || h != _lastH || _noiDung == null);
                CapPhatMuc(w, h, colorFmt);

                bool needBlur = KinhLongBlurCache.CanRenderBlur(w, h, ref _lastW, ref _lastH) || sizeChanged;

                TextureHandle hNoiDung = renderGraph.ImportTexture(_noiDung);
                TextureHandle hMo1D = renderGraph.ImportTexture(_mo1Down);
                TextureHandle hMo2D = renderGraph.ImportTexture(_mo2Down);
                TextureHandle hMo3D = renderGraph.ImportTexture(_mo3Down);
                TextureHandle hMo4 = renderGraph.ImportTexture(_mo4Down);
                TextureHandle hMo3U = renderGraph.ImportTexture(_mo3Up);
                TextureHandle hMo2U = renderGraph.ImportTexture(_mo2Up);
                TextureHandle hMo1U = renderGraph.ImportTexture(_mo1Up);
                TextureHandle hProbe = renderGraph.ImportTexture(_probe);

                if (needBlur)
                {
                    _mat.SetFloat(IdBlurStep, 1.0f);

                    // 0: Copy ảnh Content Layer 1/1 (không chứa Layer 14)
                    using (var b = renderGraph.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(activeColor, hNoiDung, _mat, 0), passName: "KL Copy", returnBuilder: true))
                        b.AllowPassCulling(false);

                    // 1..4: Downsample Dual Kawase
                    using (var b = renderGraph.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(hNoiDung, hMo1D, _mat, 1), passName: "KL Down 1/2", returnBuilder: true))
                        b.AllowPassCulling(false);
                    using (var b = renderGraph.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(hMo1D, hMo2D, _mat, 1), passName: "KL Down 1/4", returnBuilder: true))
                        b.AllowPassCulling(false);
                    using (var b = renderGraph.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(hMo2D, hMo3D, _mat, 1), passName: "KL Down 1/8", returnBuilder: true))
                        b.AllowPassCulling(false);
                    using (var b = renderGraph.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(hMo3D, hMo4, _mat, 1), passName: "KL Down 1/16", returnBuilder: true))
                        b.AllowPassCulling(false);

                    // Upsample 8-tap Tent
                    using (var b = renderGraph.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(hMo4, hMo3U, _mat, 2), passName: "KL Up 1/8", returnBuilder: true))
                        b.AllowPassCulling(false);
                    using (var b = renderGraph.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(hMo3U, hMo2U, _mat, 2), passName: "KL Up 1/4", returnBuilder: true))
                        b.AllowPassCulling(false);
                    using (var b = renderGraph.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(hMo2U, hMo1U, _mat, 2), passName: "KL Up 1/2", returnBuilder: true))
                        b.AllowPassCulling(false);

                    using (var b = renderGraph.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(hMo4, hProbe, _mat, 0), passName: "KL Probe Luma", returnBuilder: true))
                        b.AllowPassCulling(false);

                    KinhLongBlurCache.ThuYeuCauDocSang(_probe);
                }

                // Gắn _KL_NoiDung và _KL_Mo1..4 vào RenderGraph để mọi draw call phía sau đọc được
                BindGlobalsInRenderGraph(renderGraph, hNoiDung, hMo1U, hMo2U, hMo3U, hMo4, "KL Bind Cache");

                // Vẽ toàn bộ Tầng Kính Điều Hướng (Layer 14) chồng lên activeColorTexture
                RecordNavigationDrawPass(renderGraph, frameData, resourceData, hNoiDung, hMo1U, hMo2U, hMo3U, hMo4);
            }

            class NavPassData
            {
                public RendererListHandle rl;
            }

            static void RecordNavigationDrawPass(
                RenderGraph renderGraph,
                ContextContainer frameData,
                UniversalResourceData resourceData,
                TextureHandle h0, TextureHandle h1, TextureHandle h2, TextureHandle h3, TextureHandle h4)
            {
                UniversalRenderingData renderingData = frameData.Get<UniversalRenderingData>();
                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                UniversalLightData lightData = frameData.Get<UniversalLightData>();

                var drawSettings = RenderingUtils.CreateDrawingSettings(NavShaderTags, renderingData, cameraData, lightData, SortingCriteria.CommonTransparent);
                var filterSettings = new FilteringSettings(RenderQueueRange.all, 1 << KinhLongLop.LayerDieuHuong);
                var stateBlock = new RenderStateBlock(RenderStateMask.Depth)
                {
                    depthState = new DepthState(false, CompareFunction.Always)
                };

                var tagValues = new NativeArray<ShaderTagId>(TagValuesTmp, Allocator.Temp);
                var stateBlocks = new NativeArray<RenderStateBlock>(1, Allocator.Temp);
                stateBlocks[0] = stateBlock;

                var rlParams = new RendererListParams(renderingData.cullResults, drawSettings, filterSettings)
                {
                    tagValues = tagValues,
                    stateBlocks = stateBlocks,
                    isPassTagName = false
                };

                RendererListHandle rlHdl = renderGraph.CreateRendererList(rlParams);

                using (var builder = renderGraph.AddRasterRenderPass<NavPassData>("KL Draw Navigation Layer", out var data))
                {
                    data.rl = rlHdl;
                    builder.UseRendererList(rlHdl);
                    builder.UseAllGlobalTextures(true);
                    builder.UseTexture(h0, AccessFlags.Read);
                    builder.UseTexture(h1, AccessFlags.Read);
                    builder.UseTexture(h2, AccessFlags.Read);
                    builder.UseTexture(h3, AccessFlags.Read);
                    builder.UseTexture(h4, AccessFlags.Read);
                    builder.SetRenderAttachment(resourceData.activeColorTexture, 0, AccessFlags.Write);
                    if (resourceData.activeDepthTexture.IsValid())
                        builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture, AccessFlags.Read);
                    builder.AllowPassCulling(false);
                    builder.SetRenderFunc(static (NavPassData d, RasterGraphContext ctx) =>
                    {
                        ctx.cmd.DrawRendererList(d.rl);
                    });
                }
            }
        }

        class BindGlobalPassData
        {
            public TextureHandle noiDung, mo1, mo2, mo3, mo4;
        }

        static void BindGlobalsInRenderGraph(RenderGraph renderGraph, TextureHandle h0, TextureHandle h1, TextureHandle h2, TextureHandle h3, TextureHandle h4, string passName)
        {
            using (var builder = renderGraph.AddRasterRenderPass<BindGlobalPassData>(passName, out var data))
            {
                data.noiDung = h0;
                data.mo1 = h1;
                data.mo2 = h2;
                data.mo3 = h3;
                data.mo4 = h4;
                builder.UseTexture(h0, AccessFlags.Read);
                builder.UseTexture(h1, AccessFlags.Read);
                builder.UseTexture(h2, AccessFlags.Read);
                builder.UseTexture(h3, AccessFlags.Read);
                builder.UseTexture(h4, AccessFlags.Read);
                builder.AllowPassCulling(false);
                builder.AllowGlobalStateModification(true);
                builder.SetGlobalTextureAfterPass(h0, IdNoiDung);
                builder.SetGlobalTextureAfterPass(h1, IdMo1);
                builder.SetGlobalTextureAfterPass(h2, IdMo2);
                builder.SetGlobalTextureAfterPass(h3, IdMo3);
                builder.SetGlobalTextureAfterPass(h4, IdMo4);
                builder.SetRenderFunc(static (BindGlobalPassData d, RasterGraphContext ctx) =>
                {
                    ctx.cmd.SetGlobalFloat(IdDungNoiDung, 1f);
                });
            }
        }
    }
}
