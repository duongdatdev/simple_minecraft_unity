Shader "Custom/UnlitVertexColorTexture"
{
    Properties { _MainTex ("Texture", 2D) = "white" {} }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            struct appdata {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };
            struct v2f {
                float2 uv : TEXCOORD0;
                fixed4 col : COLOR;
                float4 pos : SV_POSITION;
            };
            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.col = v.color;
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, i.uv);
                
                // Decode Vertex Color:
                // RGB = Biome Tint Color (White if untinted)
                // A   = Light Level (0..1)
                float3 biomeColor = i.col.rgb;
                float light = i.col.a;

                // Analyze pixel for masking (Side Grass)
                // 1. Grey check (Top face / Overlay): Low saturation
                float sat = max(tex.r, max(tex.g, tex.b)) - min(tex.r, min(tex.g, tex.b));
                float isGrey = step(sat, 0.1);
                
                // 2. Green check (Side face overlay): Green > Red
                // Dirt is Brown/Red (R > G).
                float isGreen = step(tex.r, tex.g) * step(tex.b, tex.g);

                // Combine masks: Tint if Grey OR Green
                float mask = max(isGrey, isGreen);
                
                // Calculate final color
                // Untinted (Dirt/Wood): Texture * Light
                float3 untinted = tex.rgb * light;
                
                // Tinted (Grass/Leaves): Texture * Biome * Light
                float3 tinted = tex.rgb * biomeColor * light;
                
                // If biomeColor is White (Wood), tinted == untinted, so mask doesn't matter.
                // If biomeColor is Green (Grass), mask selects between Dirt (Untinted) and Grass (Tinted).
                
                float3 finalRGB = lerp(untinted, tinted, mask);
                
                return fixed4(finalRGB, tex.a);
            }
            ENDCG
        }
    }
}
