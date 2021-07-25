Shader "Custom/ProhibitedAreaShader"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _Glossiness("Smoothness", Range(0,1)) = 0.5
        _Metallic("Metallic", Range(0,1)) = 0.0
        _StripeAmount("Amount Of Stripes", Range(0, 40)) = 20
        _StripeGap("Stripe Gap Percentage", Range(1, 98)) = 50
        _StripeRotation("Stripe Rotation", Range(0, 360)) = 0
        [Toggle(SECOND_STRIPE)]
        _SecondStripe("Second Stripe", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "Queue" = "AlphaTest" "RenderType" = "TransparentCutout" }
        LOD 200

        CGPROGRAM
        // Physically based Standard lighting model, and enable shadows on all light types
        #pragma surface surf Standard fullforwardshadows

        // Use shader model 3.0 target, to get nicer looking lighting
        #pragma target 3.0
        #pragma shader_feature SECOND_STRIPE

        sampler2D _MainTex;

        struct Input
        {
            float2 uv_MainTex;
            float3 worldPos;
        };

        half _Glossiness;
        half _Metallic;
        fixed4 _Color;
        float _StripeAmount;
        float _StripeGap;
        float _StripeRotation;

        // Add instancing support for this shader. You need to check 'Enable Instancing' on materials that use the shader.
        // See https://docs.unity3d.com/Manual/GPUInstancing.html for more information about instancing.
        // #pragma instancing_options assumeuniformscaling
        UNITY_INSTANCING_BUFFER_START(Props)
            // put more per-instance properties here
        UNITY_INSTANCING_BUFFER_END(Props)

        float2 rotatePoint(float2 pt, float2 center, float angle) {
            float sinAngle = sin(angle);
            float cosAngle = cos(angle);
            pt -= center;
            float2 r;
            r.x = pt.x * cosAngle - pt.y * sinAngle;
            r.y = pt.x * sinAngle + pt.y * cosAngle;
            r += center;
            r += float2(1000, 1000); // Make sure all points are positive
            return r;
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            float pos = rotatePoint(IN.uv_MainTex.xy, float2(0.5, 0.5), _StripeRotation / 180 * 3.14); // 0 to 2 PI
            float secondPos = pos;

            #ifdef SECOND_STRIPE
            secondPos = rotatePoint(IN.uv_MainTex.xy, float2(0.5, 0.5), (_StripeRotation + 90) % 360 / 180 * 3.14); // Rotated 90 degrees
            #endif

            float clipMain = pos % (0.1 / _StripeAmount) - _StripeGap / (1000 * _StripeAmount);
            float clipSecondary = secondPos % (0.1 / _StripeAmount) - _StripeGap / (1000 * _StripeAmount);
            if (clipMain < 0 && clipSecondary < 0) {
                clip(-1);
            }

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
    FallBack "Transparent/Cutout"
}
