// 画面全体にかける「もや」シェーダー（uGUI の RawImage 用）
// ・ノイズでゆっくり流れるもやを描く
// ・_Holes（最大4つ）の位置だけ、もやを晴らす
// ・画面中央は穴のあとに重ねるので、常にもやがかかる
Shader "UI/FogOverlay"
{
    Properties
    {
        _FogColor      ("Fog Dark",            Color) = (0.03, 0.04, 0.045, 1)
        _MistColor     ("Mist Light",          Color) = (0.53, 0.58, 0.57, 1)
        _Density       ("Density",             Range(0,1)) = 0.95
        _Scale         ("Noise Scale",         Float) = 2.2
        _Speed         ("Drift Speed",         Float) = 0.03
        _Edge          ("Edge Ragged",         Float) = 0.12
        _ClearMist     ("Wisps In Clear Area", Range(0,1)) = 0.12
        _CenterRadius  ("Center Radius",       Float) = 0.24
        _CenterDensity ("Center Density",      Range(0,1)) = 0.98
        _CenterFog     ("Center Fog Amount",   Range(0,1)) = 1
        _Aspect        ("Aspect (script sets)", Float) = 1.777
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            struct v2f     { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };

            fixed4 _FogColor, _MistColor;
            float  _Density, _Scale, _Speed, _Edge, _ClearMist;
            float  _CenterRadius, _CenterDensity, _CenterFog, _Aspect;
            float4 _Holes[4]; // x,y = 画面UV(0-1), z = 半径(画面の高さ=1), w = 開き具合(0-1)

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            float hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = hash(i);
                float b = hash(i + float2(1, 0));
                float c = hash(i + float2(0, 1));
                float d = hash(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float fbm(float2 p)
            {
                float v = 0, a = 0.5;
                for (int i = 0; i < 4; i++) { v += a * noise(p); p = p * 2.03 + 17.1; a *= 0.5; }
                return v;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // 画面中心を原点、高さを1とした座標（円が楕円にならないようにする）
                float2 p = float2((i.uv.x - 0.5) * _Aspect, i.uv.y - 0.5);
                float t = _Time.y * _Speed;

                float n1 = fbm(p * _Scale + float2(t, t * 0.6));
                float n2 = fbm(p * _Scale * 1.7 + float2(-t * 0.8, t * 0.5) + 5.2);
                float mist = saturate(n1 * 0.6 + n2 * 0.4);

                // 1) 全体のもや
                float a = _Density * lerp(0.85, 1.0, mist);

                // 2) 解放済みステージの周りを晴らす（縁はノイズでゆらぐ）
                float clearAmt = 0;
                [unroll]
                for (int k = 0; k < 4; k++)
                {
                    float r = _Holes[k].z * _Holes[k].w;
                    if (r < 0.001) continue;
                    float2 hp = float2((_Holes[k].x - 0.5) * _Aspect, _Holes[k].y - 0.5);
                    float d = length(p - hp) + (n1 - 0.5) * _Edge;
                    clearAmt = max(clearAmt, 1.0 - smoothstep(r * 0.3, r, d));
                }
                a *= (1.0 - clearAmt);
                a += clearAmt * mist * _ClearMist; // 晴れた場所にも薄いもやを少し残す

                // 3) 画面中央は穴のあとに重ねる → ずっともやがかかる
                float dc = length(p) + (n2 - 0.5) * _Edge;
                float center = 1.0 - smoothstep(_CenterRadius * 0.5, _CenterRadius, dc);
                a = max(a, center * _CenterDensity * _CenterFog);

                fixed3 col = lerp(_FogColor.rgb, _MistColor.rgb, mist * 0.6);
                return fixed4(col, saturate(a) * i.color.a);
            }
            ENDCG
        }
    }
}
