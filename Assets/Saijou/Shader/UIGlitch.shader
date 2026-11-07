Shader "UI/Glitch"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _Intensity ("Intensity (script)", Range(0, 1)) = 0
        _GlitchTime ("Glitch Time (script)", Float) = 0
        _SliceCount ("Slice Count", Float) = 24
        _MaxShift ("Max Shift", Range(0, 0.3)) = 0.08
        _RGBSplit ("RGB Split", Range(0, 0.1)) = 0.02
        _NoiseAmount ("Noise Amount", Range(0, 1)) = 0.35
        _StreakAmount ("Streak Amount (横線ザザザ)", Range(0, 1)) = 0.3

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
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
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_ST;

            float _Intensity;
            float _GlitchTime;
            float _SliceCount;
            float _MaxShift;
            float _RGBSplit;
            float _NoiseAmount;
            float _StreakAmount;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                OUT.color = v.color * _Color;
                return OUT;
            }

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 uv = IN.texcoord;

                // 時間をコマ送りにして、カクカクしたノイズにする(24fps)
                float t = floor(_GlitchTime * 24.0);

                // 横スライスごとに左右へずらす(強さが上がるほど多くのスライスがずれる)
                float slice = floor(uv.y * _SliceCount);
                float r = hash21(float2(slice, t));
                float active = step(1.0 - _Intensity * 0.6, r) * step(0.001, _Intensity);
                float shift = (hash21(float2(slice + 17.0, t)) - 0.5) * 2.0 * _MaxShift * active * _Intensity;
                uv.x += shift;

                // 上から下へ流れる太い帯：帯の中は横に大きくちぎれる
                float bandPos = 1.0 - frac(_GlitchTime * 1.6);
                float band = smoothstep(0.12, 0.0, abs(IN.texcoord.y - bandPos));
                float bandRow = floor(IN.texcoord.y * 120.0);
                uv.x += (hash21(float2(bandRow, t + 5.0)) - 0.5) * 2.0 * _MaxShift * 2.0 * band * _Intensity;

                // RGBずらし
                float split = _RGBSplit * _Intensity * (0.5 + active);
                fixed4 cR = tex2D(_MainTex, uv + float2(split, 0));
                fixed4 cG = tex2D(_MainTex, uv);
                fixed4 cB = tex2D(_MainTex, uv - float2(split, 0));
                fixed4 col = fixed4(cR.r, cG.g, cB.b, max(cR.a, max(cG.a, cB.a)));
                col = (col + _TextureSampleAdd) * IN.color;

                // ザラザラしたノイズ
                float n = hash21(floor(IN.texcoord * 160.0) + t);
                col.rgb += (n - 0.5) * _NoiseAmount * _Intensity * col.a;

                // 細い横線のザザザ：行ごとにランダムな長さのノイズ線が走る
                float rowFine = floor(IN.texcoord.y * _SliceCount * 8.0);
                float on = step(1.0 - _Intensity * _StreakAmount, hash21(float2(rowFine, t + 3.0)));
                float sStart = hash21(float2(rowFine, t + 7.0));
                float sLen = hash21(float2(rowFine, t + 11.0)) * 0.7;
                float inStreak = on * step(sStart, IN.texcoord.x) * step(IN.texcoord.x, sStart + sLen);
                float sNoise = hash21(float2(floor(IN.texcoord.x * 90.0), rowFine + t));
                col.rgb = lerp(col.rgb, sNoise.xxx, inStreak * 0.85 * col.a);

                // 流れる帯の中は少し明るくする
                col.rgb += band * 0.12 * _Intensity * col.a;

                #ifdef UNITY_UI_CLIP_RECT
                col.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                return col;
            }
            ENDCG
        }
    }
}
