int _ContactShadowsSampleCount;
#define SAMPLE_COUNT _ContactShadowsSampleCount

float4 _ContactShadowsData1;
#define STEPPING _ContactShadowsData1.x
#define INTENSITY_MULTIPLIER _ContactShadowsData1.y
#define JITTER _ContactShadowsData1.z
#define DISTANCE_FADE _ContactShadowsData1.w

float4 _ContactShadowsData2;
#define CONTACT_SHADOWS_MIN_DISTANCE _ContactShadowsData2.x
#define CONTACT_SHADOWS_MIN_DISTANCE_FADE _ContactShadowsData2.y
#define CONTACT_SHADOWS_MAX_DISTANCE _ContactShadowsData2.z
#define CONTACT_SHADOWS_NORMAL_BIAS _ContactShadowsData2.w

float4 _ContactShadowsData3;
#define THICKNESS_NEAR _ContactShadowsData3.x
#define THICKNESS_DEPTH_MULTIPLIER _ContactShadowsData3.y
#define VIGNETTE_SIZE _ContactShadowsData3.z


#if _LOOP_STEP_X3
    #define LOOP_STEP 3
#elif _LOOP_STEP_X2
    #define LOOP_STEP 2
#else
    #define LOOP_STEP 1
#endif

#define SHADER_API_WEBGL ((SHADER_API_GLES || SHADER_API_GLES3) && SHADER_API_DESKTOP)

#if SHADER_API_MOBILE || SHADER_API_WEBGL
    #define LOOP(index, count) UNITY_UNROLL for(int index=0;index<64;index+=LOOP_STEP) if (index < count) {
#else
    #define LOOP(index, count) for(int index=0;index<count;index+=LOOP_STEP) {
#endif
#define END_LOOP }

inline float GetLinearDepth01(float2 uv) {

    float rawDepth = SAMPLE_TEXTURE2D_X_LOD(_CameraDepthTexture, sampler_PointClamp, uv, 0).r;
    float depthPersp = Linear01Depth(rawDepth, _ZBufferParams);
    float depthOrtho = rawDepth;
    #if UNITY_REVERSED_Z
        depthOrtho = 1.0 - depthOrtho;
    #endif
    float depth01 = lerp(depthPersp, depthOrtho, unity_OrthoParams.w);
    return depth01;
}


inline float3 GetSSCoords(float3 wpos) {
    float4 pos = TransformWorldToHClip(wpos);
    pos.xyz /= pos.w;
    pos.y *= _ProjectionParams.x;
    float3 coords = pos.xyz;
    coords.xy = coords.xy * 0.5 + 0.5;

    float depthPersp = Linear01Depth(coords.z, _ZBufferParams);
    float depthOrtho = coords.z;
    #if UNITY_REVERSED_Z
        depthOrtho = 1.0 - depthOrtho;
    #endif
    coords.z = lerp(depthPersp, depthOrtho, unity_OrthoParams.w);

    return coords;
}

half4 FragContactShadows(Varyings input) : SV_Target {

    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    
    float2 uv = input.texcoord.xy;

    float rawDepth = SampleSceneDepth(uv);
    float depthPersp = Linear01Depth(rawDepth, _ZBufferParams);
    float depthOrtho = rawDepth;
    #if UNITY_REVERSED_Z
        depthOrtho = 1.0 - depthOrtho;
    #endif
    float depth01 = lerp(depthPersp, depthOrtho, unity_OrthoParams.w);

    #if defined(CONTACT_SHADOWS_AFTER_OPAQUE)
        if (depth01 >= CONTACT_SHADOWS_MAX_DISTANCE || depth01 < CONTACT_SHADOWS_MIN_DISTANCE)
            return half4(0, 0, 0, 0);
    #else
        if (depth01 >= SHADOW_MAX_DEPTH || depth01 < CONTACT_SHADOWS_MIN_DISTANCE)
            return half4(1, 0, 0, 1);
    #endif

    float3 wpos0 = GetWorldPosition(uv, rawDepth);

    #if _NORMALS_TEXTURE
        float3 norm = SampleSceneNormals(uv);
    #else
        float3 norm = GetNormalFromWPOS(wpos0);
    #endif
    wpos0 += norm * CONTACT_SHADOWS_NORMAL_BIAS;

    float3 step = _MainLightPosition.xyz * STEPPING;

    half randomVal = InterleavedGradientNoise(uv * _SourceSize.xy, 0);
    wpos0 += step * (randomVal * JITTER);

    float bias = 0.0025 * depth01;
    float thickness = THICKNESS_NEAR + depth01 * THICKNESS_DEPTH_MULTIPLIER;
    float maxDist = STEPPING * SAMPLE_COUNT;

    half shadow = 1.0;
    half dist = 0;
    LOOP(k, SAMPLE_COUNT)

        float3 wpos = wpos0 + step * k;
        float3 coords = GetSSCoords(wpos);

        if (any(floor(coords.xy)!=0)) break;
        float depth = GetLinearDepth01(coords.xy);

        float depthDiff = coords.z - depth;
        if (depthDiff > bias && depthDiff < thickness) {
            dist = STEPPING * k;
            shadow = lerp(0, DISTANCE_FADE, (float)k / SAMPLE_COUNT);
            break;
        }

    END_LOOP

    shadow = saturate( shadow + 1.0 - saturate((depth01 - CONTACT_SHADOWS_MIN_DISTANCE) / CONTACT_SHADOWS_MIN_DISTANCE_FADE) );

    // apply shadow intensity
    half4 shadowParams = GetMainLightShadowParams();
    half shadowIntensity = shadowParams.x * INTENSITY_MULTIPLIER;

    if (VIGNETTE_SIZE > 0) {
        float2 screenCoord = uv * _SourceSize;
        float2 distanceToEdges = min(screenCoord, _SourceSize - screenCoord);
        float edgeDistance = min(distanceToEdges.x, distanceToEdges.y);
        half vignette = edgeDistance / (VIGNETTE_SIZE * min(_SourceSize.x, _SourceSize.y));
        vignette = saturate(vignette);
        shadow = lerp(1, shadow, vignette);
    }

    shadow = InvLerp(shadow, shadowIntensity);

    #if defined(CONTACT_SHADOWS_AFTER_OPAQUE)
        return half4(0, 0, 0, 1.0 - shadow);
    #else
        return half4(shadow, dist, 0, 0);
    #endif
           
}
