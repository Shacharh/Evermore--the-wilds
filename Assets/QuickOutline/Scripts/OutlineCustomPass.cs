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
    private RTHandle _silhouetteRT;       // RGBA8 mask for OutlineAll dilation
    private RTHandle _monsterDepthMaskRT; // RG float: R=NDC depth, G=presence

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
            name: "OutlineSilhouetteBuffer");

        // Float RT for the team-silhouette depth encoding pass.
        // R = monster NDC depth from our vertex shader.
        // G = presence flag (1.0 where monster rendered, cleared to 0).
        // Using R16G16_SFloat — sufficient precision for NDC depth [0..1].
        _monsterDepthMaskRT = RTHandles.Alloc(
            Vector2.one, TextureXR.slices,
            colorFormat: GraphicsFormat.R16G16_SFloat,
            useDynamicScale: true,
            name: "MonsterDepthMask");

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
                DrawSilhouetteOnly(ctx, outline, renderers);
            else
                DrawOutlineAll(ctx, outline, renderers);
        }
    }

    // Team-colour fill, visible only where the monster is occluded by scene geometry.
    //
    // Step 1 — encode the monster's NDC depth into _monsterDepthMaskRT.
    //   No DSV is bound: ZTest Always, no depth read/write needed.
    //
    // Step 2 — composite onto the camera.
    //   No DSV is bound: _CameraDepthTexture (HDRP global) is therefore accessible
    //   as an SRV in the fragment shader without SRV/DSV conflict.
    //   The composite shader compares the two depths with a threshold to isolate
    //   only the truly occluded region.
    private void DrawSilhouetteOnly(CustomPassContext ctx, Outline outline, Renderer[] renderers)
    {
        // ── Step 1: render monster depth + presence into float RT ────────────
        // Bind NO depth buffer — ZTest Always, so depth testing is irrelevant,
        // and we must not bind cameraDepthBuffer as DSV here or the SRV will
        // be nulled in the composite step.
        CoreUtils.SetRenderTarget(ctx.cmd, _monsterDepthMaskRT, ClearFlag.Color,
            new Color(0, 0, 0, 0));

        foreach (var r in renderers)
        {
            if (r == null) continue;
            r.SetPropertyBlock(_mpb); // no per-renderer color needed here
            int count = SubMeshCount(r);
            for (int i = 0; i < count; i++)
                ctx.cmd.DrawRenderer(r, _silhouetteMat, i, 1); // pass 1: depth encoder
        }

        // ── Step 2: composite — draw team colour on occluded pixels ──────────
        // Render to cameraColorBuffer with NO depth buffer so cameraDepthBuffer
        // is NOT bound as DSV → _CameraDepthTexture is accessible as SRV.
        CoreUtils.SetRenderTarget(ctx.cmd, ctx.cameraColorBuffer, ClearFlag.None);

        ctx.cmd.SetGlobalTexture("_MonsterDepthMaskTex", _monsterDepthMaskRT);
        ctx.cmd.SetGlobalColor("_SilhouetteTeamColor", outline.OutlineColor);
        // Pass 1 of the effect shader: SilhouetteOccluded.
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
        HDUtils.DrawFullScreen(ctx.cmd, _effectMat, ctx.cameraColorBuffer, null, 0);
    }

    protected override void Cleanup()
    {
        CoreUtils.Destroy(_silhouetteMat);
        CoreUtils.Destroy(_effectMat);
        _silhouetteRT?.Release();
        _silhouetteRT = null;
        _monsterDepthMaskRT?.Release();
        _monsterDepthMaskRT = null;
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
