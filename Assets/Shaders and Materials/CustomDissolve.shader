Shader "Custom/CustomDissolve"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
        _DissolveTex ("Dissolve Texture", 2D) = "white" {}
        _DissolveAmount ("Dissolve Amount", Range(0, 1)) = 0
        _DissolveColor ("Dissolve Color", Color) = (1, 0.5, 0, 1)
        _DissolveWidth ("Dissolve Width", Range(0, 0.5)) = 0.1
        _GlowIntensity ("Glow Intensity", Range(0, 3)) = 1
    }
    SubShader
    {
        Tags 
        { 
            "RenderType"="Opaque" 
            "Queue"="Geometry" 
        }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float2 dissolveUv : TEXCOORD1;
                UNITY_FOG_COORDS(2)
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            sampler2D _DissolveTex;
            float4 _DissolveTex_ST;
            float _DissolveAmount;
            float4 _Color;
            float4 _DissolveColor;
            float _DissolveWidth;
            float _GlowIntensity;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.dissolveUv = TRANSFORM_TEX(v.uv, _DissolveTex);
                UNITY_TRANSFER_FOG(o, o.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Основная текстура
                fixed4 col = tex2D(_MainTex, i.uv) * _Color;
                
                // Текстура растворения (шум)
                fixed4 dissolveTex = tex2D(_DissolveTex, i.dissolveUv);
                
                // Значение растворения (0 = не растворено, 1 = полностью растворено)
                float dissolveValue = dissolveTex.r - _DissolveAmount;
                
                // Создаем границу (край растворения)
                float border = smoothstep(0, _DissolveWidth, dissolveValue);
                float glow = border * _GlowIntensity;
                
                // Цвет границы
                fixed4 borderColor = _DissolveColor * glow;
                
                // Если значение меньше 0 - отсекаем (полностью прозрачный)
                clip(dissolveValue);
                
                // Смешиваем основной цвет с цветом границы
                col = col + borderColor;
                
                // Применяем туман
                UNITY_APPLY_FOG(i.fogCoord, col);
                
                return col;
            }
            ENDCG
        }
    }
    FallBack "Unlit/Color"
}