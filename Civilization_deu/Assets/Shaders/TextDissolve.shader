Shader "Custom/TextDissolve"
{
    Properties
    {
        [PerRendererData]_MainTex ("Font Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _DissolveTex ("Dissolve Noise", 2D) = "gray" {}
        _DissolveAmount ("Dissolve Amount", Range(0,1)) = 0
        _EdgeWidth ("Edge Width", Range(0,0.5)) = 0.1
        _EdgeColor ("Edge Color", Color) = (1,0.5,0,1)
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

        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float2 texcoord : TEXCOORD0;
                float4 color    : COLOR;
            };

            struct v2f
            {
                float4 pos   : SV_POSITION;
                float2 uv    : TEXCOORD0;
                fixed4 color : COLOR;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;

            sampler2D _DissolveTex;
            float4 _DissolveTex_ST;
            float _DissolveAmount;
            float _EdgeWidth;
            fixed4 _EdgeColor;

            v2f vert (appdata_t v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);

                // Text 组件的 Color 会写进 v.color，这里再乘一个材质的 _Color 作为整体 Tint
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // 1. 基于字体贴图的 Alpha 裁剪字形
                fixed4 fontSample = tex2D(_MainTex, i.uv);
                float alpha = fontSample.a;

                fixed4 col;
                col.rgb = i.color.rgb;         // 颜色来自 Text（Legacy）组件
                col.a   = i.color.a * alpha;   // 透明度：Text 的 alpha * 字体贴图 alpha

                if (col.a <= 0.001)
                    discard;

                // 2. 完全溶解的终点判断：
                //    当 _DissolveAmount 接近 1 时，直接全部丢弃，保证最终完全消失
                if (_DissolveAmount >= 0.999)
                    discard;

                // 3. 溶解噪声
                float2 noiseUV = TRANSFORM_TEX(i.uv, _DissolveTex);
                float noise = tex2D(_DissolveTex, noiseUV).r;

                float threshold = _DissolveAmount;

                // 4. 动态边缘宽度：越接近 1，边缘越细
                //    0   -> edgeWidthEff = _EdgeWidth （初始边缘粗）
                //    0.5 -> edgeWidthEff = _EdgeWidth * 0.5
                //    1   -> edgeWidthEff = 0 
                float edgeWidthEff = _EdgeWidth * (1.0 - threshold);
                edgeWidthEff = max(edgeWidthEff, 0.0001);   // 避免除零

                // 5. 计算 d 并按“可见/边缘/已溶解”分类
                float d = noise - threshold;

                // 完全被溶解区域：在当前阈值左侧再减去一个边缘宽度
                if (d < -edgeWidthEff)
                    discard;

                // 6. 边缘插值：
                //    d = -edgeWidthEff 时 edge = 0（刚被溶解）
                //    d = 0            时 edge = 1（主体区域）
                float edge = saturate((d + edgeWidthEff) / edgeWidthEff);

                // 7. 边缘颜色与主体颜色混合
                fixed4 finalCol = lerp(_EdgeColor, col, edge);
                finalCol.a *= edge;

                return finalCol;
            }
            ENDCG
        }
    }

    FallBack "UI/Default"
}
