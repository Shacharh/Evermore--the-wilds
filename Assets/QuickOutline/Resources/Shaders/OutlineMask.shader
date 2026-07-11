//
//  OutlineMask.shader  (HDRP rewrite)
//  Writes Ref 128 to the stencil so OutlineFill can skip the model's own pixels.
//  Stencil Ref 128 (bit 7) — safely above HDRP's reserved bits 0-5.
//

Shader "Custom/Outline Mask" {
  Properties {
    [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest("ZTest", Float) = 0
  }

  SubShader {
    Tags {
      "Queue"      = "Transparent+100"
      "RenderType" = "Transparent"
    }

    Pass {
      Name "Mask"
      Cull Off
      ZTest [_ZTest]
      ZWrite Off
      ColorMask 0

      Stencil {
        Ref  128
        Pass Replace
      }

      HLSLPROGRAM
      #pragma vertex   vert
      #pragma fragment frag

      #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
      #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"

      struct Attributes {
        float4 vertex : POSITION;
      };

      struct Varyings {
        float4 positionCS : SV_POSITION;
      };

      Varyings vert(Attributes input) {
        Varyings output;
        output.positionCS = TransformObjectToHClip(input.vertex.xyz);
        return output;
      }

      float4 frag(Varyings input) : SV_Target {
        return float4(0.0, 0.0, 0.0, 0.0);
      }
      ENDHLSL
    }
  }
}
