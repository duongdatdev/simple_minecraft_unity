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
            float _SunIntensity;
            float _AmbientFloor;

            struct appdata {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float2 uv2 : TEXCOORD1;
                fixed4 color : COLOR;
            };
            struct v2f {
                float2 uv : TEXCOORD0;
                fixed4 col : COLOR;
                float4 pos : SV_POSITION;
                float2 light : TEXCOORD1;
            };
            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.col = v.color;
                o.light = v.uv2;
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, i.uv);
                
                // Decode Vertex Color:
                // RGB = Biome Tint Color (White if untinted)
                float3 biomeColor = i.col.rgb;
                
                float skyLight = i.light.x;
                float blockLight = i.light.y;
                
                // Combine lights
                // Sky light is affected by sun intensity
                // Ensure _SunIntensity is at least a small value to avoid pitch black if not set
                float sun = max(_SunIntensity, 0.1); 
                float dynamicSkyLight = skyLight * sun;

                // Ensure a global ambient floor (set from DayNightCycle)
                float ambientFloor = _AmbientFloor; // expected 0..1

                float light = max(max(dynamicSkyLight, blockLight), ambientFloor);

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
                
                float3 finalRGB = lerp(untinted, tinted, mask);
                
                return fixed4(finalRGB, tex.a);
            }
            ENDCG
        }
    }
}
