Shader "UI/AdditiveFresnel"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _FresnelPower ("Fresnel Power", Range(0.1, 5.0)) = 1.5
        _GlowExponent ("Glow Exponent", Range(0.1, 5.0)) = 1.0
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
        Lighting Off
        ZWrite Off
        Blend One One // Additive blending

        Pass
        {
        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord  : TEXCOORD0;
            };
            
            fixed4 _Color;
            sampler2D _MainTex;
            float _FresnelPower;
            float _GlowExponent;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 uv = IN.texcoord;
                float dist = distance(uv, float2(0.5, 0.5));
                float ndist = saturate(dist * 2.0); // 0 at center, 1 at edge
                
                // Fresnel: edge highlight
                float fresnel = pow(ndist, _FresnelPower);
                
                // Glow: center glow fading towards edge
                float glow = pow(saturate(1.0 - ndist), _GlowExponent);
                
                fixed4 texColor = tex2D(_MainTex, uv);
                fixed4 finalColor = _Color * IN.color;
                
                float intensity = (glow + fresnel * 0.3) * texColor.a;
                
                finalColor.rgb *= intensity;
                finalColor.a = intensity;
                return finalColor;
            }
        ENDCG
        }
    }
}
