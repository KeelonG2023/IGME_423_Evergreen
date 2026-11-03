// Outline for selected buildings (used by Evergreen.UI.BuildingOutline / BuildingClickSelector).
// Two passes, each enabled on its own material:
//   "Mask"    (SRPDefaultUnlit)  : draws the building's mesh invisibly, marking its whole silhouette in the stencil.
//   "Outline" (UniversalForward) : draws the mesh pushed outward along its normals, only where the stencil is NOT
//                                  marked, leaving a constant-width rim around the silhouette.
// Both ignore depth, so the rim is a clean silhouette (and stays visible behind other buildings), and it
// doesn't matter which way the model's faces are wound.
// The outline pushes along smoothed normals stored in UV3 so hard-edged low-poly meshes don't get gaps.
Shader "Evergreen/BuildingOutline"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (1, 0.85, 0.2, 1)
        _OutlineWidth ("Outline Width (world units)", Float) = 1
        [Toggle] _UseSmoothNormals ("Use Smooth Normals (UV3)", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry+10" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _OutlineColor;
            float _OutlineWidth;
            float _UseSmoothNormals;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "Mask"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Off
            ZWrite Off
            ZTest Always
            ColorMask 0

            Stencil
            {
                Ref 64
                WriteMask 64
                Comp Always
                Pass Replace
            }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            float4 vert(float4 positionOS : POSITION) : SV_POSITION
            {
                return TransformObjectToHClip(positionOS.xyz);
            }

            half4 frag() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            ZWrite Off
            ZTest Always

            Stencil
            {
                Ref 64
                ReadMask 64
                Comp NotEqual
            }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float3 smoothNormalOS : TEXCOORD3;
            };

            float4 vert(Attributes input) : SV_POSITION
            {
                float3 normalOS = _UseSmoothNormals > 0.5 ? input.smoothNormalOS : input.normalOS;
                if (dot(normalOS, normalOS) < 1e-6) normalOS = input.normalOS;

                // Push each vertex out along its (smoothed) normal by _OutlineWidth world units.
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = normalize(TransformObjectToWorldNormal(normalOS));
                positionWS += normalWS * _OutlineWidth;
                return TransformWorldToHClip(positionWS);
            }

            half4 frag() : SV_Target { return _OutlineColor; }
            ENDHLSL
        }
    }
}
