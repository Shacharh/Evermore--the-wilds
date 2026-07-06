//
//  Outline.cs  (HDRP rewrite — screen-space dilation)
//  Original by Chris Nolet (2018).
//
//  This component holds outline settings and the list of renderers to outline.
//  All actual rendering is handled by OutlineCustomPass; this script just
//  self-registers so the pass knows what to draw.
//
//  No materials are managed here — the old mask/fill material approach was
//  incompatible with HDRP and has been replaced by a post-process dilation pass.
//

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[DisallowMultipleComponent]
public class Outline : MonoBehaviour
{
    public enum Mode
    {
        OutlineAll,           // always-visible border (uses dilation)
        OutlineVisible,       // border only on visible parts
        OutlineHidden,        // border only on occluded parts
        OutlineAndSilhouette, // border everywhere + x-ray fill
        SilhouetteOnly        // x-ray fill on occluded parts, no border
    }

    // ── Inspector fields ─────────────────────────────────────────────────────

    [SerializeField] private Mode  outlineMode  = Mode.SilhouetteOnly;
    [SerializeField] private Color outlineColor = Color.white;
    [SerializeField, Range(0f, 10f)] private float outlineWidth = 8f;

    [Tooltip("If assigned, ONLY these renderers are outlined.\n" +
             "Leave empty to auto-discover all MeshRenderer + SkinnedMeshRenderer children.\n" +
             "Note: with the dilation approach, ring meshes work correctly — " +
             "you no longer need to exclude them.")]
    [SerializeField] private Renderer[] overrideRenderers;

    // ── Public API (same surface as original QuickOutline) ───────────────────

    public Mode  OutlineMode  { get => outlineMode;  set => outlineMode  = value; }
    public Color OutlineColor { get => outlineColor; set => outlineColor = value; }
    public float OutlineWidth { get => outlineWidth; set => outlineWidth = value; }

    /// <summary>The renderers that OutlineCustomPass will draw for this outline.</summary>
    public Renderer[] Renderers { get; private set; }

    // ── Lifecycle ────────────────────────────────────────────────────────────

    void Awake()
    {
        if (overrideRenderers != null && overrideRenderers.Length > 0)
        {
            Renderers = overrideRenderers.Where(r => r != null).ToArray();
        }
        else
        {
            // Auto-discover. Exclude particle/trail/line renderers so VFX on the
            // same prefab don't get outlined. Also skip any renderer whose GameObject
            // has an OutlineExclude component — use that to suppress problematic child
            // meshes (e.g. a ring accessory that causes self-occlusion artefacts).
            Renderers = GetComponentsInChildren<Renderer>()
                .Where(r => r is MeshRenderer || r is SkinnedMeshRenderer)
                .Where(r => r.GetComponent<OutlineExclude>() == null)
                .ToArray();
        }
    }

    void OnEnable()  => OutlineCustomPass.Register(this);
    void OnDisable() => OutlineCustomPass.Deregister(this);
}
