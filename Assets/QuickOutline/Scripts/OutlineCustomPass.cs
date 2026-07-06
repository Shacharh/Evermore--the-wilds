//
//  OutlineCustomPass.cs
//  HDRP Custom Pass — screen-space dilation outline + occlusion silhouette.
//
//  Scene setup: add a Custom Pass Volume (Global, Before Post Process) and
//  add an OutlineCustomPass entry to it.
//

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

[System.Serializable]
public class OutlineCustomPass : CustomPass
{
    // ── Static registry — Outline components self-register ───────────────────

    private static readonly HashSet<Outline> _outlines = new HashSet<Outline>();

    public static void Register  (Outline o) { if (o != null) _outlines.Add(o); }
    public static void Deregister(Outline o) { _outlines.Remove(o); }

    // ── Runtime resources ────────────────────────────────────────────────────

    private Material _silhouetteMat;
    private Material _effectMat;
    private RTHandle _silhouetteRT;      // "all" silhouette mask (ZTest Always)
    private RTHandle _silhouetteVisRT;   // "visible-only" silhouette mask (ZTest GreaterEqual)

    // Allocated in Setup, not as a static initializer — MaterialPropertyBlock
    // calls native code and cannot be constructed during serialization.
    private MaterialPropertyBlock _mpb;

    protected override void Setup(ScriptableRenderContext ctx, CommandBuffer cmd)
    {
        _mpb           = new MaterialPropertyBlock();
        _silhouetteMat = CoreUtils.CreateEngineMaterial(Shader.Find("Custom/OutlineSilhouette"));
        _effectMat     = CoreUtils.CreateEngineMaterial(Shader.Find("Custom/OutlineEffect"));

        _silhouetteRT = RTHandles.Alloc(
            Vector2.one, TextureXR.slices,
            colorFormat: GraphicsFormat.R8G8B8A8_UNorm,
            useDynamicScale: true,
            name: "OutlineSilhouetteAll");

        _silhouetteVisRT = RTHandles.Alloc(
            Vector2.one, TextureXR.slices,
            colorFormat: GraphicsFormat.R8G8B8A8_UNorm,
            useDynamicScale: true,
            name: "OutlineSilhouetteVisible");

        // OutlineAll dilation pass (pass 0 of effect shader) reads _MaskTex.
        // Bind once — it always points to _silhouetteRT.
        _effectMat.SetTexture("_MaskTex", _silhouetteRT);
    }

    protected override void Execute(CustomPassContext ctx)
    {
        if (_outlines.Count == 0 || _silhouetteMat == null || _effectMat == null || _mpb == null) return;

        foreach (var outline in _outlines)
        {
            if (outline == null || !outline.isActiveAndEnabled) continue;

            Renderer[] renderers = outline.Renderers;
            if (renderers == null || renderers.Length == 0) continue;

            if (outline.OutlineMode == Outline.Mode.SilhouetteOnly)
            {
                DrawSilhouetteOnly(ctx, outline, renderers);
            }
            else
            {
                DrawOutlineAll(ctx, outline, renderers);
            }
        }
    }

    // Team-colour fill shown only where the monster is behind scene geometry.
    // Strategy: two hardware-ZTest passes whose difference = occluded region.
    //   Pass 0 (ZTest Always)       → _silhouetteRT   : full monster shape
    //   Pass 1 (ZTest GreaterEqual) → _silhouetteVisRT : visible portion only
    //   Composite pass 1 of effect shader: All - Visible = occluded → team colour
    //
    // No depth texture reads. Hardware ZTest is the only depth mechanism used,
    // so there is no SRV/DSV conflict and no dependency on _CameraDepthTexture.
    private void DrawSilhouetteOnly(CustomPassContext ctx, Outline outline, Renderer[] renderers)
    {
        _mpb.SetColor("_SilhouetteColor", Color.white);

        // ── Step 1: full silhouette (ZTest Always) ──────────────────────────
        CoreUtils.SetRenderTarget(ctx.cmd, _silhouetteRT, ctx.cameraDepthBuffer,
            ClearFlag.Color, Color.clear);

        foreach (var r in renderers)
        {
            if (r == null) continue;
            r.SetPropertyBlock(_mpb);
            int count = SubMeshCount(r);
            for (int i = 0; i < count; i++)
                ctx.cmd.DrawRenderer(r, _silhouetteMat, i, 0); // pass 0: ZTest Always
        }

        // ── Step 2: visible-only silhouette (ZTest GreaterEqual) ────────────
        // GEqual in HDRP reversed-Z: fragment_depth >= buffer_depth
        // = fragment is at the same depth as (or closer than) the scene
        // = the VISIBLE portion of the mesh.
        CoreUtils.SetRenderTarget(ctx.cmd, _silhouetteVisRT, ctx.cameraDepthBuffer,
            ClearFlag.Color, Color.clear);

        foreach (var r in renderers)
        {
            if (r == null) continue;
            r.SetPropertyBlock(_mpb);
            int count = SubMeshCount(r);
            for (int i = 0; i < count; i++)
                ctx.cmd.DrawRenderer(r, _silhouetteMat, i, 1); // pass 1: ZTest GreaterEqual
        }

        // ── Step 3: composite occluded region in team colour ─────────────────
        ctx.cmd.SetGlobalTexture("_SilAllTex",  _silhouetteRT);
        ctx.cmd.SetGlobalTexture("_SilVisTex",  _silhouetteVisRT);
        ctx.cmd.SetGlobalColor("_SilhouetteTeamColor", outline.OutlineColor);
        // Pass 1 of the effect shader: SilhouetteOccluded composite.
        HDUtils.DrawFullScreen(ctx.cmd, _effectMat, ctx.cameraColorBuffer, null, 1);
    }

    // Dilation border for effectiveness display during attack targeting.
    private void DrawOutlineAll(CustomPassContext ctx, Outline outline, Renderer[] renderers)
    {
        _mpb.SetColor("_SilhouetteColor", Color.white);

        CoreUtils.SetRenderTarget(ctx.cmd, _silhouetteRT, ctx.cameraDepthBuffer,
            ClearFlag.Color, Color.clear);

        foreach (var r in renderers)
        {
            if (r == null) continue;
            r.SetPropertyBlock(_mpb);
            int count = SubMeshCount(r);
            for (int i = 0; i < count; i++)
                ctx.cmd.DrawRenderer(r, _silhouetteMat, i, 0); // pass 0: ZTest Always
        }

        ctx.cmd.SetGlobalColor("_OutlineColor", outline.OutlineColor);
        ctx.cmd.SetGlobalInt("_OutlineWidth", Mathf.Max(1, Mathf.RoundToInt(outline.OutlineWidth)));
        // Pass 0 of the effect shader: OutlineDilation.
        HDUtils.DrawFullScreen(ctx.cmd, _effectMat, ctx.cameraColorBuffer, null, 0);
    }

    protected override void Cleanup()
    {
        CoreUtils.Destroy(_silhouetteMat);
        CoreUtils.Destroy(_effectMat);
        _silhouetteRT?.Release();
        _silhouetteRT = null;
        _silhouetteVisRT?.Release();
        _silhouetteVisRT = null;
    }

    private static int SubMeshCount(Renderer r)
    {
        if (r is MeshRenderer mr)
        {
            var mf = mr.GetComponent<MeshFilter>();
            return mf?.sharedMesh?.subMeshCount ?? 1;
        }
        if (r is SkinnedMeshRenderer smr)
            return smr.sharedMesh?.subMeshCount ?? 1;
        return 1;
    }
}
