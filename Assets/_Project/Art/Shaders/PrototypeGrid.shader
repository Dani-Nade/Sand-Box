// World-space (triplanar) grid shader for greyboxing.
// The texture is projected in world space, so scaled/rotated blocks always show
// true-to-size grid squares - useful for judging distances and jump heights.
Shader "SandHouse/Prototype Grid"
{
    Properties
    {
        _BaseMap ("Grid Texture", 2D) = "white" {}
        _BaseColor ("Tint", Color) = (1, 1, 1, 1)
        _TextureWorldSize ("Texture World Size (m)", Float) = 2
        _BlendSharpness ("Blend Sharpness", Range(1, 16)) = 8
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                float _TextureWorldSize;
                float _BlendSharpness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float fogFactor : TEXCOORD2;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.fogFactor = ComputeFogFactor(positions.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);

                float3 weights = pow(abs(normalWS), _BlendSharpness);
                weights /= max(weights.x + weights.y + weights.z, 1e-5);

                float3 p = input.positionWS / max(_TextureWorldSize, 0.001);
                half4 texX = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, p.zy);
                half4 texY = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, p.xz);
                half4 texZ = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, p.xy);
                half3 albedo = (texX.rgb * weights.x + texY.rgb * weights.y + texZ.rgb * weights.z) * _BaseColor.rgb;

                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half nDotL = saturate(dot(normalWS, mainLight.direction));
                half3 lighting = mainLight.color * (nDotL * mainLight.shadowAttenuation * mainLight.distanceAttenuation);
                half3 ambient = SampleSH(normalWS);

                half3 color = albedo * (lighting + ambient);
                color = MixFog(color, input.fogFactor);
                return half4(color, 1);
            }
            ENDHLSL
        }

        // Reuse URP Lit's passes for shadows and depth (needed for shadows, SSAO and depth priming).
        UsePass "Universal Render Pipeline/Lit/SHADOWCASTER"
        UsePass "Universal Render Pipeline/Lit/DEPTHONLY"
        UsePass "Universal Render Pipeline/Lit/DEPTHNORMALS"
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
