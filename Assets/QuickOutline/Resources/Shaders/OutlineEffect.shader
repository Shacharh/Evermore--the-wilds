//
//  OutlineEffect.shader
//  Fullscreen dilation pass: reads a silhouette mask and outputs a coloured
//  border of _OutlineWidth pixels around any masked region.
//
//  _MaskTex, _OutlineColor, _OutlineWidth must NOT be in the Properties{} block.
//  If they are, Unity treats them as per-material values and the defaults from the
//  block override whatever SetGlobalColor/SetGlobalTexture sets at runtime.
//

Shader "Custom/OutlineEffect" {
  // No Properties — all three inputs are driven by the C# side:
  //   _effectMat.SetTexture("_MaskTex", ...)  (per-material, once in Setup)
  //   cmd.SetGlobalColor("_OutlineColor", ...) (CommandBuffer, per-outline)
  //   cmd.SetGlobalInt("_OutlineWidth",   ...) (CommandBuffer, per-outline)
  Properties {}

  SubShader {
    Tags { "Queue" = "Transparent" }

    Pass {
      Name "OutlineDilation"
      ZTest   Always
      ZWrite  Off
      Cull    Off
      Blend   SrcAlpha OneMinusSrcAlpha

      HLSLPROGRAM
      #pragma vertex   vert
      #pragma fragment frag

      #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
      #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"

      // _MaskTex is set per-material via _effectMat.SetTexture — no Properties entry.
      TEXTURE2D(_MaskTex);
      SAMPLER(sampler_MaskTex);

      // These are set per-frame via SetGlobalColor/SetGlobalInt (CommandBuffer order).
      // Declaring them outside any CBUFFER lets the globals reach them directly.
      float4 _OutlineColor;
      int    _OutlineWidth;

      struct Attributes { uint vertexID : SV_VertexID; };
      struct Varyings   { float4 posCS : SV_POSITION; float2 uv : TEXCOORD0; };

      Varyings vert(Attributes i) {
        Varyings o;
        o.posCS = GetFullScreenTriangleVertexPosition(i.vertexID);
        o.uv    = GetFullScreenTriangleTexCoord(i.vertexID);
        return o;
      }

      float4 frag(Varyings i) : SV_Target {
        float2 uv = i.uv;
        // _ScreenSize = (width, height, 1/width, 1/height)
        float2 ts = _ScreenSize.zw;

        // If this pixel is inside the model, write nothing (keep scene colour).
        float center = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, uv).a;
        if (center > 0.5) return float4(0.0, 0.0, 0.0, 0.0);

        // Circular dilation: check all neighbours within radius N.
        // [loop] suppresses the DX11 "unable to unroll loop" error — iteration count
        // is driven by the dynamic global _OutlineWidth.
        int N = clamp(_OutlineWidth, 1, 16);
        int N2 = N * N;
        [loop] for (int x = -N; x <= N; x++) {
          [loop] for (int y = -N; y <= N; y++) {
            if (x * x + y * y > N2) continue;
            float s = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, uv + float2(x, y) * ts).a;
            if (s > 0.5) return _OutlineColor;
          }
        }
        return float4(0.0, 0.0, 0.0, 0.0);
      }
      ENDHLSL
    }

    // Pass 1 — SilhouetteOccluded composite.
    //   Reads two masks produced by OutlineSilhouette passes 0 and 1:
    //     _SilAllTex  : full silhouette (ZTest Always)
    //     _SilVisTex  : visible-only silhouette (ZTest GreaterEqual)
    //   Occluded region = All - Visible.  Draws it in _SilhouetteTeamColor.
    Pass {
      Name "SilhouetteOccluded"
      ZTest  Always
      ZWrite Off
      Cull   Off
      Blend  SrcAlpha OneMinusSrcAlpha

      HLSLPROGRAM
      #pragma vertex   vert
      #pragma fragment frag
      #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
      #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"

      // Set via cmd.SetGlobalTexture before DrawFullScreen.
      TEXTURE2D(_SilAllTex);
      SAMPLER(sampler_SilAllTex);
      TEXTURE2D(_SilVisTex);
      SAMPLER(sampler_SilVisTex);

      // Per-outline team colour, set via cmd.SetGlobalColor.
      float4 _SilhouetteTeamColor;

      struct Attributes { uint vertexID : SV_VertexID; };
      struct Varyings   { float4 posCS : SV_POSITION; float2 uv : TEXCOORD0; };

      Varyings vert(Attributes i) {
        Varyings o;
        o.posCS = GetFullScreenTriangleVertexPosition(i.vertexID);
        o.uv    = GetFullScreenTriangleTexCoord(i.vertexID);
        return o;
      }

      float4 frag(Varyings i) : SV_Target {
        float allA     = SAMPLE_TEXTURE2D(_SilAllTex, sampler_SilAllTex, i.uv).a;
        float visibleA = SAMPLE_TEXTURE2D(_SilVisTex, sampler_SilVisTex, i.uv).a;
        // occluded = present in "all" mask but absent in "visible" mask
        float occluded = saturate(allA - visibleA);
        return float4(_SilhouetteTeamColor.rgb, occluded * _SilhouetteTeamColor.a);
      }
      ENDHLSL
    }
  }
}
