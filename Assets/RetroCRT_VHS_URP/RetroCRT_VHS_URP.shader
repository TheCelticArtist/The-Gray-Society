Shader "Custom/CRT_VHSEffect"
{
    // Versione fullscreen per la Renderer Feature "Blit" di Cyanilux (URP / Render Graph).
    // L'immagine della camera arriva in _BlitTexture (dichiarata in Blit.hlsl, non va ridichiarata).
    // Stesso nome shader e stesse proprietà dell'originale: il materiale esistente si aggiorna da solo.

    Properties
    {
        _Zoom ("Image Zoom", Float) = 1.0
        _PanX ("Image Pan X", Float) = 0.0
        _PanY ("Image Pan Y", Float) = 0.0
        _Rotation ("Image Rotation (Degrees)", Float) = 0.0
        [Toggle] _FlipX ("Flip Horizontally (Mirror)", Float) = 0.0
        [Toggle] _FlipY ("Flip Vertically", Float) = 0.0

        _Brightness ("Brightness / Self-Illumination", Range(0, 3)) = 1.2
        _GlowIntensity ("Glow / Bloom Intensity", Range(0, 1)) = 0.2
        _PixelDensity ("Pixel Density (0 = Disable)", Float) = 200.0
        _ScanlineIntensity ("Scanline Intensity", Range(0,1)) = 0.3
        _ScanlineCount ("Scanline Count", Float) = 400.0
        _NoiseIntensity ("Noise Intensity", Range(0,1)) = 0.05
        _AberrationAmount ("Color Aberration", Range(0,0.02)) = 0.003
        _VignetteIntensity ("Vignette Intensity", Range(0,5)) = 2.0

        _JitterIntensity ("Screen Jitter Intensity", Range(0,0.1)) = 0.005
        _JitterSpeed ("Screen Jitter Speed", Float) = 10.0
        _WaveDistortion ("Wave Distortion Amount", Range(0,0.05)) = 0.005
        _WaveFrequency ("Wave Frequency", Float) = 10.0
        _GlitchFlashSpeed ("Glitch Flash Speed", Float) = 2.0
        _GlitchFlashIntensity ("Glitch Flash Intensity", Range(0,1)) = 0.05

        _OverlayTex ("Overlay Video / Spritesheet (RGBA)", 2D) = "black" {}
        _OverlayBlendMode ("Blend Mode (0:Add, 1:Multiply, 2:Alpha)", Float) = 0
        _OverlayAlpha ("Overlay Opacity", Range(0,1)) = 0.3
        _Cols ("Spritesheet Columns", Float) = 1.0
        _Rows ("Spritesheet Rows", Float) = 1.0
        _AnimSpeed ("Spritesheet FPS", Float) = 12.0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        LOD 100

        // Necessari per un pass fullscreen
        ZWrite Off
        ZTest Always
        Cull Off
        Blend Off

        Pass
        {
            Name "CRT_VHSEffectPass"

            HLSLPROGRAM
            #pragma vertex Vert     // vertex shader fullscreen fornito da Blit.hlsl
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            TEXTURE2D(_OverlayTex);
            SAMPLER(sampler_OverlayTex);

            CBUFFER_START(UnityPerMaterial)
                float _Zoom;
                float _PanX;
                float _PanY;
                float _Rotation;
                float _FlipX;
                float _FlipY;

                float _Brightness;
                float _GlowIntensity;
                float _PixelDensity;
                float _ScanlineIntensity;
                float _ScanlineCount;
                float _NoiseIntensity;
                float _AberrationAmount;
                float _VignetteIntensity;

                float _JitterIntensity;
                float _JitterSpeed;
                float _WaveDistortion;
                float _WaveFrequency;
                float _GlitchFlashSpeed;
                float _GlitchFlashIntensity;

                float _OverlayBlendMode;
                float _OverlayAlpha;
                float _Cols;
                float _Rows;
                float _AnimSpeed;
            CBUFFER_END

            float Random(float2 uv)
            {
                return frac(sin(dot(uv, float2(12.9898, 78.233))) * 43758.5453123);
            }

            float3 SampleScreen(float2 uv)
            {
                return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rgb;
            }

            half4 Frag (Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float time = _Time.y;

                // --- Trasformazioni immagine (nell'originale erano nel vertex shader) ---
                float2 uv = input.texcoord - 0.5;

                float rad = radians(_Rotation);
                float cosRad = cos(rad);
                float sinRad = sin(rad);
                uv = float2(uv.x * cosRad - uv.y * sinRad, uv.x * sinRad + uv.y * cosRad);
                uv /= (_Zoom == 0.0 ? 1.0 : _Zoom);
                uv -= float2(_PanX, _PanY);
                uv += 0.5;

                if (_FlipX > 0.5) uv.x = 1.0 - uv.x;
                if (_FlipY > 0.5) uv.y = 1.0 - uv.y; // l'originale capovolgeva sempre la Y: qui non serve

                float2 baseUV = uv; // usato dall'overlay, come i.uv nell'originale

                // --- Jitter e onda ---
                float jitter = (Random(float2(floor(time * _JitterSpeed), 0.0)) - 0.5) * _JitterIntensity;
                uv.y += jitter;

                float wave = sin(uv.y * _WaveFrequency + time * 5.0) * _WaveDistortion;
                uv.x += wave;

                // --- Curvatura CRT ---
                float2 centerUV = uv - 0.5;
                float r2 = dot(centerUV, centerUV);
                uv += centerUV * r2 * 0.05;

                // --- Pixelatura ---
                if (_PixelDensity > 0.0)
                {
                    uv = floor(uv * _PixelDensity) / _PixelDensity;
                }

                // --- Aberrazione cromatica ---
                float r = SampleScreen(uv + float2(_AberrationAmount, 0)).r;
                float g = SampleScreen(uv).g;
                float b = SampleScreen(uv - float2(_AberrationAmount, 0)).b;
                float4 col = float4(r, g, b, 1.0);

                // --- Luminosità e glow ---
                col.rgb *= _Brightness;

                float luminance = dot(col.rgb, float3(0.299, 0.587, 0.114));
                col.rgb += col.rgb * luminance * _GlowIntensity;

                // --- Overlay / spritesheet ---
                float2 overlayUV = baseUV;
                if (_Cols > 1.0 || _Rows > 1.0)
                {
                    float totalFrames = _Cols * _Rows;
                    float currentFrame = fmod(floor(time * _AnimSpeed), totalFrames);
                    float colIndex = fmod(currentFrame, _Cols);
                    float rowIndex = floor(currentFrame / _Cols);

                    overlayUV.x = (overlayUV.x + colIndex) / _Cols;
                    overlayUV.y = (overlayUV.y + (_Rows - 1.0 - rowIndex)) / _Rows;
                }

                float4 overlayCol = SAMPLE_TEXTURE2D(_OverlayTex, sampler_OverlayTex, overlayUV);

                if (_OverlayBlendMode < 0.5)
                {
                    col.rgb += overlayCol.rgb * _OverlayAlpha;
                }
                else if (_OverlayBlendMode < 1.5)
                {
                    col.rgb *= lerp(float3(1, 1, 1), overlayCol.rgb, _OverlayAlpha);
                }
                else
                {
                    col.rgb = lerp(col.rgb, overlayCol.rgb, overlayCol.a * _OverlayAlpha);
                }

                // --- Scanline ---
                float scanline = sin(uv.y * _ScanlineCount * 3.14159 * 2.0);
                scanline = (scanline * 0.5) + 0.5;
                col.rgb -= (1.0 - scanline) * _ScanlineIntensity;

                // --- Rumore e flash glitch ---
                float noise = Random(uv + frac(time)) * 2.0 - 1.0;
                float flash = (sin(time * _GlitchFlashSpeed) * 0.5 + 0.5) * _GlitchFlashIntensity;
                col.rgb += noise * (_NoiseIntensity + flash * 0.1);

                // --- Vignetta ---
                // max() evita pow() su valori negativi (fuori dallo schermo) che darebbe NaN = pixel neri casuali
                float vignette = uv.x * uv.y * (1.0 - uv.x) * (1.0 - uv.y);
                vignette = saturate(pow(max(16.0 * vignette, 1e-5), _VignetteIntensity));
                col.rgb *= vignette;

                // Niente colori negativi nel buffer della camera (scanline e rumore possono sottrarre troppo)
                col.rgb = max(col.rgb, 0.0);

                return half4(col.rgb, 1.0);
            }
            ENDHLSL
        }
    }
}