Shader "Custom/OutlineSilhouette" {
  Properties {
    _SilhouetteColor("Silhouette Color", Color) = (1, 1, 1, 1)
  }

  SubShader {
    Tags {
      "RenderType"      = "Transparent"
      "Queue"           = "Transparent+50"
      "DisableBatching" = "True"
    }

    // Pass 0 — full silhouette fill (ZTest Always).
    //   Used by both OutlineAll (dilation border mask) and the first sub-pass
    //   of SilhouetteOnly (team indicator "all" mask).
    Pass {
      Name "Silhouette_Always"
      Cull   Off
      ZTest  Always
      ZWrite Off
      Blend  SrcAlpha OneMinusSrcAlpha

      HLSLPROGRAM
      #pragma vertex   vert
      #pragma fragment frag
      #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
      #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"

      CBUFFER_START(UnityPerMaterial)
        float4 _SilhouetteColor;
      CBUFFER_END

      struct Attributes { float4 vertex : POSITION; };
      struct Varyings   { float4 posCS  : SV_POSITION; };
      Varyings vert(Attributes i) { Varyings o; o.posCS = TransformObjectToHClip(i.vertex.xyz); return o; }
      float4 frag() : SV_Target { return _SilhouetteColor; }
      ENDHLSL
    }

    // Pass 1 — visible-only fill (ZTest GreaterEqual).
    //   In HDRP reversed-Z (near=1, far=0) hardware ZTest is NOT flipped by
    //   Unity for custom passes.  GEqual therefore means:
    //     fragment_depth >= buffer_depth
    //   i.e. the fragment is at the same depth as (or closer than) the scene —
    //   exactly the VISIBLE portion of the mesh.
    //   Used as the second sub-pass of SilhouetteOnly: subtract from the "all"
    //   mask to isolate the occluded region.
    Pass {
      Name "Silhouette_Visible"
      Cull   Off
      ZTest  GEqual
      ZWrite Off
      Blend  SrcAlpha OneMinusSrcAlpha

      HLSLPROGRAM
      #pragma vertex   vert
      #pragma fragment frag
      #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
      #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"

      CBUFFER_START(UnityPerMaterial)
        float4 _SilhouetteColor;
      CBUFFER_END

      struct Attributes { float4 vertex : POSITION; };
      struct Varyings   { float4 posCS  : SV_POSITION; };
      Varyings vert(Attributes i) { Varyings o; o.posCS = TransformObjectToHClip(i.vertex.xyz); return o; }
      float4 frag() : SV_Target { return _SilhouetteColor; }
      ENDHLSL
    }
  }
}
