// HUD 기체 실루엣용. 스프라이트의 알파(모양)만 쓰고 색은 단색(Image.color)으로 칠한다.
// 단색끼리 겹치면 부위 경계가 사라지므로, 모양 안쪽 가장자리에 외곽선을 넣는다.
//
// 외곽선은 "안쪽"에 그린다 — 픽셀 아트 스프라이트는 여백이 거의 없어서 바깥에 그리면 사각형 밖으로 잘린다.
// 이웃 텍셀을 보므로 스프라이트 시트에서 붙어 있는 옆 스프라이트를 읽을 수 있다. 시트에 1px 이상 간격을 둘 것.
// UI/Default를 바탕으로 해서 Mask·RectMask2D(스텐실·클립)를 그대로 지원한다.
Shader "UI/SilhouetteSolid"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _OutlineColor ("Outline Color", Color) = (0,0,0,0.85)
        _OutlineWidth ("Outline Width (texel)", Range(0, 4)) = 1
        _AlphaCutoff ("Alpha Cutoff", Range(0, 1)) = 0.1

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex        : SV_POSITION;
                fixed4 color         : COLOR;
                float2 texcoord      : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;

            fixed4 _OutlineColor;
            float _OutlineWidth;
            float _AlphaCutoff;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = v.texcoord;
                OUT.color = v.color * _Color;
                return OUT;
            }

            float SampleAlpha(float2 uv)
            {
                return tex2D(_MainTex, uv).a + _TextureSampleAdd.a;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float alpha = SampleAlpha(IN.texcoord);
                if (alpha < _AlphaCutoff) discard;

                // 상하좌우 이웃 중 하나라도 비어 있으면 가장자리다.
                float2 step = _MainTex_TexelSize.xy * _OutlineWidth;
                float minNeighbor = 1;
                minNeighbor = min(minNeighbor, SampleAlpha(IN.texcoord + float2( step.x, 0)));
                minNeighbor = min(minNeighbor, SampleAlpha(IN.texcoord + float2(-step.x, 0)));
                minNeighbor = min(minNeighbor, SampleAlpha(IN.texcoord + float2(0,  step.y)));
                minNeighbor = min(minNeighbor, SampleAlpha(IN.texcoord + float2(0, -step.y)));

                float edge = (_OutlineWidth > 0 && minNeighbor < _AlphaCutoff) ? 1 : 0;

                // 채움은 Image.color 단색. 외곽선은 채움 위에 덮는다(외곽선 알파만큼).
                fixed4 fill = fixed4(IN.color.rgb, 1);
                fixed4 color = lerp(fill, fixed4(_OutlineColor.rgb, 1), edge * _OutlineColor.a);
                color.a = IN.color.a;

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif

                return color;
            }
        ENDCG
        }
    }
}
