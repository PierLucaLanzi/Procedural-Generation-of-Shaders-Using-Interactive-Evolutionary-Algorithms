Shader "Custom/SampleVoronoiDelta"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        // Physically based Standard lighting model, and enable shadows on all light types
        #pragma surface surf Standard fullforwardshadows

        // Use shader model 3.0 target, to get nicer looking lighting
        #pragma target 3.0

        sampler2D _MainTex;

        struct Input
        {
            float2 uv_MainTex;
            float epsilon;
            float2 uv;
            float voronoiOffset;
            float voronoiScale;
        };

        half _Glossiness;
        half _Metallic;
        fixed4 _Color;



        // Add instancing support for this shader. You need to check 'Enable Instancing' on materials that use the shader.
        // See https://docs.unity3d.com/Manual/GPUInstancing.html for more information about instancing.
        // #pragma instancing_options assumeuniformscaling
        UNITY_INSTANCING_BUFFER_START(Props)
            // put more per-instance properties here
        UNITY_INSTANCING_BUFFER_END(Props)


        void Voronoi(float2 UV, float AngleOffset, float CellDensity,
            out float Out, out float Cells)
        {
            float2 g = floor(UV * CellDensity);
            float2 f = frac(UV * CellDensity);
            float t = 8.0;
            float3 res = float3(8.0, 0.0, 0.0);

            for (int y = -1; y <= 1; y++)
            {
                for (int x = -1; x <= 1; x++)
                {
                    float2 lattice = float2(x, y);
                    float2 offset = Unity_Voronoi_RandomVector_float(lattice + g, AngleOffset);
                    float d = distance(lattice + offset, f);

                    if (d < res.x)
                    {
                        res = float3(d, offset.x, offset.y);
                        Out = res.x;
                        Cells = res.y;
                    }
                }
            }
        }



        void output (Input IN, inout SurfaceOutputStandard o)
        {
            float epsilon = clamp(IN.epsilon, 0.001, .02);
            float voronoi0, voronoi1, voronoi2, voronoi3, voronoi4,;
            float voronoiCells0, voronoiCells1, voronoiCells2,
                voronoiCells3, voronoiCells4;
            Voronoi(IN.uv, voronoiOffset, voronoiSize, voronoi0,
                voronoiCells0);

            Voronoi(float2(IN.uv.x + epsilon, IN.uv.y), voronoiOffset,
                voronoiSize, voronoi1, voronoiCells1);
            Voronoi(float2(IN.uv.x - epsilon, IN.uv.y), voronoiOffset,
                voronoiSize, voronoi2, voronoiCells2);
            Voronoi(float2(IN.uv.x , IN.uv.y + epsilon), voronoiOffset,
                voronoiSize, voronoi3, voronoiCells3);
            Voronoi(float2(IN.uv.x , IN.uv.y - epsilon), voronoiOffset,
                voronoiSize, voronoi4, voronoiCells4);

            float delta1, delta2, delta3, delta4;
            delta1 = voronoi0 - voronoi1;
            delta1 = voronoi0 - voronoi2;
            delta1 = voronoi0 - voronoi3;
            delta1 = voronoi0 - voronoi4;








            // Albedo comes from a texture tinted by color
            fixed4 c = tex2D (_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = c.rgb;
            // Metallic and smoothness come from slider variables
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Alpha = c.a;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
