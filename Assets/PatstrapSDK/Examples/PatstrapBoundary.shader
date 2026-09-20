Shader "Patstrap/Boundary"
{
    Properties
    {
        _BoundaryColor("Boundary Color", Color) = (0.1, 0.7, 1.0, 1.0)
        _NearDistance("Near Distance", Float) = 1.0
        _FarDistance("Far Distance", Float) = 8.0
        _NearBrightness("Near Brightness", Range(0, 8)) = 3.0
        _FarBrightness("Far Brightness", Range(0, 8)) = 0.35
        _Opacity("Opacity", Range(0, 1)) = 0.2
        _EdgePower("Edge Power", Range(0.1, 8)) = 2.0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Blend SrcAlpha One
            ZWrite Off
            Cull Back

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 worldPosition : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
            };

            fixed4 _BoundaryColor;
            float _NearDistance;
            float _FarDistance;
            float _NearBrightness;
            float _FarBrightness;
            float _Opacity;
            float _EdgePower;

            v2f vert(appdata input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.worldPosition = mul(unity_ObjectToWorld, input.vertex).xyz;
                output.worldNormal = UnityObjectToWorldNormal(input.normal);
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float distanceToCamera = distance(_WorldSpaceCameraPos, input.worldPosition);
                float proximity = 1.0 - saturate((distanceToCamera - _NearDistance) / max(_FarDistance - _NearDistance, 0.001));
                float brightness = lerp(_FarBrightness, _NearBrightness, proximity);

                float3 viewDirection = normalize(_WorldSpaceCameraPos - input.worldPosition);
                float edge = pow(1.0 - abs(dot(normalize(input.worldNormal), viewDirection)), _EdgePower);
                float alpha = saturate(_Opacity * (0.35 + edge * 0.65) * (0.5 + proximity * 0.5));

                return fixed4(_BoundaryColor.rgb * brightness, alpha);
            }
            ENDCG
        }
    }
}
