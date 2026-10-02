Shader "Greenhouse/UnlitColor"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;

            struct VertexInput { float4 vertex : POSITION; };
            struct VertexOutput { float4 vertex : SV_POSITION; };

            VertexOutput vert(VertexInput input)
            {
                VertexOutput output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                return output;
            }

            fixed4 frag(VertexOutput input) : SV_Target
            {
                return _Color;
            }
            ENDCG
        }
    }
}
