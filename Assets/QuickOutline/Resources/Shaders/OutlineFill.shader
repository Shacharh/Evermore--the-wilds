//
//  OutlineFill.shader  (HDRP rewrite)
//  Expands vertices along view-space normals to produce a solid colour outline.
//  Stencil Ref 128 (bit 7) — safely above HDRP's reserved bits 0-5.
//

Shader "Custom/Outline Fill" {
  Properties {
    [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest("ZTest", Float) = 0
    _OutlineColor("Outline Color", Color) = (1, 1, 1, 1)
    _OutlineWidth("Outline Width", Range(0, 10)) = 2
  }

  SubShader {
    Tags {
      "Queue"           = "Transparent+110"
      "RenderType"      = "Transparent"
      "DisableBatching" = "True"
    }

    Pass {
      Name "Fill"
      Cull Off
      ZTest [_ZTest]
      ZWrite Off
      Blend SrcAlpha OneMinusSrcAlpha
      ColorMask RGB

      Stencil {
        Ref  128
        Comp NotEqual
      }

      HLSLPROGRAM
      #pragma vertex   vert
      #pragma fragment frag
      #pragma multi_compile_instancing

      #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
      #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"

      CBUFFER_START(UnityPerMaterial)
        float4 _OutlineColor;
        float  _OutlineWidth;
        float  _ZTest;
      CBUFFER_END

      struct Attributes {
        float4 vertex       : POSITION;
        float3 normal       : NORMAL;
        float3 smoothNormal : TEXCOORD3;
        UNITY_VERTEX_INPUT_INSTANCE_ID
      };

      struct Varyings {
        float4 positionCS : SV_POSITION;
        float4 color      : COLOR;
        UNITY_VERTEX_OUTPUT_STEREO
      };

      Varyings vert(Attributes input) {
        Varyings output;
        UNITY_SETUP_INSTANCE_ID(input);
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

        float3 n = any(input.smoothNormal) ? input.smoothNormal : input.normal;

        // HDRP-compatible normal transform (UNITY_MATRIX_IT_MV is not available).
        float3 worldNormal = TransformObjectToWorldDir(n, true);
        float3 viewNormal  = TransformWorldToViewDir(worldNormal, true);

        float3 viewPos    = mul(UNITY_MATRIX_MV, float4(input.vertex.xyz, 1.0)).xyz;
        float3 expandedView = viewPos + viewNormal * (-viewPos.z) * _OutlineWidth / 1000.0;
        output.positionCS   = mul(UNITY_MATRIX_P, float4(expandedView, 1.0));
        output.color        = _OutlineColor;

        return output;
      }

      float4 frag(Varyings input) : SV_Target {
        return input.color;
      }
      ENDHLSL
    }
  }
}
