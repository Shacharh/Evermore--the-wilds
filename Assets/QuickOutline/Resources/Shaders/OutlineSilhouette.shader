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

    // Pass 0 — full silhouette fill, ZTest Always.
    //   Used by OutlineAll (dilation border mask).
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

    // Pass 1 — depth + presence encoder.
    //   Renders into a float2 RT (_MonsterDepthMaskTex):
    //     R = clip-space NDC depth (i.posCS.z, range [0..1])
    //     G = 1.0 (monster is present at this pixel)
    //   RT is cleared to (0,0) so G==0 means no monster.
    //   No depth buffer is bound, ZTest Always — this is a pure data write.
    //   Used by SilhouetteOnly: the composite pass compares this depth against
    //   _CameraDepthTexture to determine which pixels are occluded.
    Pass {
      Name "Silhouette_DepthEncode"
      Cull   Off
      ZTest  Always
      ZWrite Off
      // No Blend — we need exact float values in the RT.
      Blend  Off

      HLSLPROGRAM
      #pragma vertex   vert
      #pragma fragment frag
      #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
      #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"

      struct Attributes { float4 vertex : POSITION; };
      struct Varyings   { float4 posCS  : SV_POSITION; };
      Varyings vert(Attributes i) { Varyings o; o.posCS = TransformObjectToHClip(i.vertex.xyz); return o; }

      float4 frag(Varyings i) : SV_Target {
        // R = our vertex shader's NDC depth (matches the composite comparison).
        // G = presence flag.
        return float4(i.posCS.z, 1.0, 0.0, 1.0);
      }
      ENDHLSL
    }
  }
}
