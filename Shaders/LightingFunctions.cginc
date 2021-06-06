//This file contains all of the neccisary functions for lighting to work a'la standard shading.
//Feel free to add to this.
#define grayscaleVec float3(0.2125, 0.7154, 0.0721)
float pow5(float a)
{
    return a * a * a * a * a;
}

float sq(float a)
{
    return a*a;
}

float D_GGX(float NoH, float roughness)
{
    float a2 = roughness * roughness;
    float f = (NoH * a2 - NoH) * NoH + 1.0;
    return a2 / (UNITY_PI * f * f);
}

float D_GGX_Anisotropic(float NoH, const float3 h, const float3 t, const float3 b, float at, float ab)
{
    float ToH = dot(t, h);
    float BoH = dot(b, h);
    float a2 = at * ab;
    float3 v = float3(ab * ToH, at * BoH, a2 * NoH);
    float v2 = dot(v, v);
    float w2 = a2 / v2;
    return a2 * w2 * w2 * (1.0 / UNITY_PI);
}

float V_Kelemen(float LoH) {
    return 0.25 / (LoH * LoH);
}

float3 F_Schlick(float u, float3 f0)
{
    return f0 + (1.0 - f0) * pow(1.0 - u, 5.0);
}

float3 F_Schlick(const float3 f0, float f90, float VoH)
{
    // Schlick 1994, "An Inexpensive BRDF Model for Physically-Based Rendering"
    return f0 + (f90 - f0) * pow5(1.0 - VoH);
}

float3 F_FresnelLerp (float3 F0, float3 F90, float cosA)
{
    float t = pow5(1 - cosA);   // ala Schlick interpoliation
    return lerp (F0, F90, t);
}

float Fd_Burley(float roughness, float NoV, float NoL, float LoH)
{
    // Burley 2012, "Physically-Based Shading at Disney"
    float f90 = 0.5 + 2.0 * roughness * LoH * LoH;
    float lightScatter = F_Schlick(1.0, f90, NoL);
    float viewScatter  = F_Schlick(1.0, f90, NoV);
    return lightScatter * viewScatter * (1.0 / UNITY_PI);
}

float shEvaluateDiffuseL1Geomerics(float L0, float3 L1, float3 n)
{
    // average energy
    float R0 = L0;

    // avg direction of incoming light
    float3 R1 = 0.5f * L1;

    // directional brightness
    float lenR1 = length(R1);

    // linear angle between normal and direction 0-1
    //float q = 0.5f * (1.0f + dot(R1 / lenR1, n));
    //float q = dot(R1 / lenR1, n) * 0.5 + 0.5;
    float q = dot(normalize(R1), n) * 0.5 + 0.5;

    // power for q
    // lerps from 1 (linear) to 3 (cubic) based on directionality
    float p = 1.0f + 2.0f * lenR1 / R0;

    // dynamic range constant
    // should vary between 4 (highly directional) and 0 (ambient)
    float a = (1.0f - lenR1 / R0) / (1.0f + lenR1 / R0);

    return R0 * (a + (1.0f - a) * (p + 1.0f) * pow(q, p));
}

// Energy conserving wrap diffuse term, does *not* include the divide by pi
float Fd_Wrap(float NoL, float w) {
    return saturate((NoL + w) / sq(1.0 + w));
}

float V_SmithGGXCorrelated(float NoV, float NoL, float a)
{
    float a2 = a * a;
    float GGXL = NoV * sqrt((-NoL * a2 + NoL) * NoL + a2);
    float GGXV = NoL * sqrt((-NoV * a2 + NoV) * NoV + a2);
    return 0.5 / (GGXV + GGXL);
}

float Fd_Lambert()
{
    return 1.0 / UNITY_PI;
}

//Lifted vertex light support from XSToon: https://github.com/Xiexe/Xiexes-Unity-Shaders
//Returns the average color of all lights and writes to a struct contraining individual colors
float3 get4VertexLightsColFalloff(inout VertexLightInformation vLight, float3 worldPos, float3 normal, inout float4 vertexLightAtten)
{
    float3 lightColor = 0;
    #if defined(VERTEXLIGHT_ON)
        float4 toLightX = unity_4LightPosX0 - worldPos.x;
        float4 toLightY = unity_4LightPosY0 - worldPos.y;
        float4 toLightZ = unity_4LightPosZ0 - worldPos.z;

        float4 lengthSq = 0;
        lengthSq += toLightX * toLightX;
        lengthSq += toLightY * toLightY;
        lengthSq += toLightZ * toLightZ;

        float4 atten = 1.0 / (1.0 + lengthSq * unity_4LightAtten0);
        float4 atten2 = saturate(1 - (lengthSq * unity_4LightAtten0 / 25));
        atten = min(atten, atten2 * atten2);
        // Cleaner, nicer looking falloff. Also prevents the "Snapping in" effect that Unity's normal integration of vertex lights has.
        vertexLightAtten = atten;

        lightColor.rgb += unity_LightColor[0] * atten.x;
        lightColor.rgb += unity_LightColor[1] * atten.y;
        lightColor.rgb += unity_LightColor[2] * atten.z;
        lightColor.rgb += unity_LightColor[3] * atten.w;

        vLight.ColorFalloff[0] = unity_LightColor[0] * atten.x;
        vLight.ColorFalloff[1] = unity_LightColor[1] * atten.y;
        vLight.ColorFalloff[2] = unity_LightColor[2] * atten.z;
        vLight.ColorFalloff[3] = unity_LightColor[3] * atten.w;

        vLight.Attenuation[0] = atten.x;
        vLight.Attenuation[1] = atten.y;
        vLight.Attenuation[2] = atten.z;
        vLight.Attenuation[3] = atten.w;
    #endif
    return lightColor;
}

//Returns the average direction of all lights and writes to a struct contraining individual directions
float3 getVertexLightsDir(inout VertexLightInformation vLights, float3 worldPos, float4 vertexLightAtten)
{
    float3 dir = float3(0,0,0);
    float3 toLightX = float3(unity_4LightPosX0.x, unity_4LightPosY0.x, unity_4LightPosZ0.x);
    float3 toLightY = float3(unity_4LightPosX0.y, unity_4LightPosY0.y, unity_4LightPosZ0.y);
    float3 toLightZ = float3(unity_4LightPosX0.z, unity_4LightPosY0.z, unity_4LightPosZ0.z);
    float3 toLightW = float3(unity_4LightPosX0.w, unity_4LightPosY0.w, unity_4LightPosZ0.w);

    float3 dirX = toLightX - worldPos;
    float3 dirY = toLightY - worldPos;
    float3 dirZ = toLightZ - worldPos;
    float3 dirW = toLightW - worldPos;

    dirX *= length(toLightX) * vertexLightAtten.x;
    dirY *= length(toLightY) * vertexLightAtten.y;
    dirZ *= length(toLightZ) * vertexLightAtten.z;
    dirW *= length(toLightW) * vertexLightAtten.w;

    vLights.Direction[0] = dirX;
    vLights.Direction[1] = dirY;
    vLights.Direction[2] = dirZ;
    vLights.Direction[3] = dirW;

    dir = (dirX + dirY + dirZ + dirW) / 4;
    return dir;
}

float4 getMetallicSmoothness(float4 metallicGlossMap)
{
    float roughness = 1-(_Glossiness * metallicGlossMap.a);
    float metallic = metallicGlossMap.r * _Metallic;
    float reflectance = metallicGlossMap.g * _Reflectance;
    return float4(metallic, reflectance, 0, roughness);
}

float3 getIndirectDiffuse(float3 normal)
{
    float3 indirectDiffuse;
    if(_LightProbeMethod == 0)
    {
        indirectDiffuse = max(0, ShadeSH9(float4(normal, 1)));
    }
    else
    {
        float3 L0 = float3(unity_SHAr.w, unity_SHAg.w, unity_SHAb.w);
        indirectDiffuse.r = shEvaluateDiffuseL1Geomerics(L0.r, unity_SHAr.xyz, normal);
        indirectDiffuse.g = shEvaluateDiffuseL1Geomerics(L0.g, unity_SHAg.xyz, normal);
        indirectDiffuse.b = shEvaluateDiffuseL1Geomerics(L0.b, unity_SHAb.xyz, normal);
    }
    return max(0, indirectDiffuse);
}

//Reflection direction, worldPos, unity_SpecCube0_ProbePosition, unity_SpecCube0_BoxMin, unity_SpecCube0_BoxMax
float3 getReflectionUV(float3 direction, float3 position, float4 cubemapPosition, float3 boxMin, float3 boxMax)
{
    #if UNITY_SPECCUBE_BOX_PROJECTION
        if (cubemapPosition.w > 0) {
            float3 factors = ((direction > 0 ? boxMax : boxMin) - position) / direction;
            float scalar = min(min(factors.x, factors.y), factors.z);
            direction = direction * scalar + (position - cubemapPosition);
        }
    #endif
    return direction;
}

float3 getBoxProjection (float3 direction, float3 position, float4 cubemapPosition, float3 boxMin, float3 boxMax)
{
    // #if defined(UNITY_SPECCUBE_BOX_PROJECTION) // For some reason this doesn't work?
        if (cubemapPosition.w > 0) {
            float3 factors =
                ((direction > 0 ? boxMax : boxMin) - position) / direction;
            float scalar = min(min(factors.x, factors.y), factors.z);
            direction = direction * scalar + (position - cubemapPosition);
        }
    // #endif
    return direction;
}

float3 getIndirectSpecular(float metallic, float roughness, float3 reflDir, float3 worldPos, float3 lightmap, float3 normal)
{
    float3 spec = float3(0,0,0);
    #if defined(UNITY_PASS_FORWARDBASE)
        float3 indirectSpecular;
        Unity_GlossyEnvironmentData envData;
        envData.roughness = roughness;
        envData.reflUVW = getBoxProjection(
            reflDir, worldPos,
            unity_SpecCube0_ProbePosition,
            unity_SpecCube0_BoxMin, unity_SpecCube0_BoxMax
        );

        float3 probe0 = Unity_GlossyEnvironment(UNITY_PASS_TEXCUBE(unity_SpecCube0), unity_SpecCube0_HDR, envData);
        float interpolator = unity_SpecCube0_BoxMin.w;
        UNITY_BRANCH
        if (interpolator < 0.99999)
        {
            envData.reflUVW = getBoxProjection(
                reflDir, worldPos,
                unity_SpecCube1_ProbePosition,
                unity_SpecCube1_BoxMin, unity_SpecCube1_BoxMax
            );
            float3 probe1 = Unity_GlossyEnvironment(UNITY_PASS_TEXCUBE_SAMPLER(unity_SpecCube1, unity_SpecCube0), unity_SpecCube0_HDR, envData);
            indirectSpecular = lerp(probe1, probe0, interpolator);
        }
        else
        {
            indirectSpecular = probe0;
        }
        float horizon = min(1 + dot(reflDir, normal), 1);
        indirectSpecular *= horizon * horizon;

        spec = indirectSpecular;
        #if defined(LIGHTMAP_ON)
            float specMultiplier = max(0, lerp(1, pow(length(lightmap), _SpecLMOcclusionAdjust), _SpecularLMOcclusion));
            spec *= specMultiplier;
        #endif
    #endif
    return spec;
}

float3 getDirectSpecular(float roughness, float ndh, float vdn, float ndl, float ldh, float3 f0, float3 halfVector, float3 tangent, float3 bitangent, float anisotropy)
{
    #if !defined(LIGHTMAP_ON)
        float rough = max(roughness * roughness, 0.0045);
        float Dn = D_GGX(ndh, rough);
        float3 F = F_Schlick(ldh, f0);
        float V = V_SmithGGXCorrelated(vdn, ndl, rough);
        float3 directSpecularNonAniso = max(0, (Dn * V) * F);

        anisotropy *= saturate(5.0 * roughness);
        float at = max(rough * (1.0 + anisotropy), 0.001);
        float ab = max(rough * (1.0 - anisotropy), 0.001);
        float D = D_GGX_Anisotropic(ndh, halfVector, tangent, bitangent, at, ab);
        float3 directSpecularAniso = max(0, (D * V) * F);

        return lerp(directSpecularNonAniso, directSpecularAniso, saturate(abs(_Anisotropy * 100)) ) * 3; // * 100 to prevent blending, blend because otherwise tangents are fucked on lightmapped object
    #else
        return 0;
    #endif
}

void initBumpedNormalTangentBitangent(float4 normalMap, inout float3 bitangent, inout float3 tangent, inout float3 normal)
{
    float3 tangentNormal = UnpackScaleNormal(normalMap, _BumpScale);
    float3 calcedNormal = normalize
    (
		tangentNormal.x * tangent +
		tangentNormal.y * bitangent +
		tangentNormal.z * normal
    );

    normal = normalize(calcedNormal);
    tangent = normalize(cross(normal, bitangent));
    bitangent = normalize(cross(normal, tangent));
}

float3 getAnisotropicReflectionVector(float3 viewDir, float3 bitangent, float3 tangent, float3 normal, float roughness, float anisotropy)
{
    //_Anisotropy = lerp(-0.2, 0.2, sin(_Time.y / 20)); //This is pretty fun
    float3 anisotropicDirection = anisotropy >= 0.0 ? bitangent : tangent;
    float3 anisotropicTangent = cross(anisotropicDirection, viewDir);
    float3 anisotropicNormal = cross(anisotropicTangent, anisotropicDirection);
    float bendFactor = abs(anisotropy) * saturate(5.0 * roughness);
    float3 bentNormal = normalize(lerp(normal, anisotropicNormal, bendFactor));
    return reflect(-viewDir, bentNormal);
}

float3 getRealtimeLightmap(float2 uv, float3 worldNormal)
{
    float2 realtimeUV = uv * unity_DynamicLightmapST.xy + unity_DynamicLightmapST.zw;
    float4 bakedCol = UNITY_SAMPLE_TEX2D(unity_DynamicLightmap, realtimeUV);
    float3 realtimeLightmap = DecodeRealtimeLightmap(bakedCol);

    #ifdef DIRLIGHTMAP_COMBINED
        float4 realtimeDirTex = UNITY_SAMPLE_TEX2D_SAMPLER(unity_DynamicDirectionality, unity_DynamicLightmap, realtimeUV);
        realtimeLightmap += DecodeDirectionalLightmap (realtimeLightmap, realtimeDirTex, worldNormal);
    #endif

    return realtimeLightmap * _RTLMStrength;
}

float3 getLightmap(float2 uv, float3 worldNormal, float3 worldPos)
{
    float2 lightmapUV = uv * unity_LightmapST.xy + unity_LightmapST.zw;
    float4 bakedColorTex = UNITY_SAMPLE_TEX2D(unity_Lightmap, lightmapUV);
    float3 lightMap = DecodeLightmap(bakedColorTex);

    #ifdef DIRLIGHTMAP_COMBINED
        fixed4 bakedDirTex = UNITY_SAMPLE_TEX2D_SAMPLER (unity_LightmapInd, unity_Lightmap, lightmapUV);
        lightMap = DecodeDirectionalLightmap(lightMap, bakedDirTex, worldNormal);
    #endif
    return lightMap * _LMStrength;
}

// Get the most intense light Dir from probes OR from a light source. Method developed by Xiexe / Merlin
float3 getLightDir(bool lightEnv, float3 worldPos)
{
    //switch between using probes or actual light direction
    float3 lightDir = lightEnv ? UnityWorldSpaceLightDir(worldPos) : unity_SHAr.xyz + unity_SHAg.xyz + unity_SHAb.xyz;

    #if !defined(POINT) && !defined(SPOT) && !defined(VERTEXLIGHT_ON) // if the average length of the light probes is null, and we don't have a directional light in the scene, fall back to our fallback lightDir
        if(length(unity_SHAr.xyz*unity_SHAr.w + unity_SHAg.xyz*unity_SHAg.w + unity_SHAb.xyz*unity_SHAb.w) == 0 && length(lightDir) < 0.1)
        {
            lightDir = float4(1, 1, 1, 0);
        }
    #endif

    return normalize(lightDir);
}

float3 getLightCol(bool lightEnv, float3 lightColor, float3 indirectDominantColor)
{
    float3 c = lightEnv ? lightColor : indirectDominantColor;
    return c;
}

float4 getClearcoatSmoothness(float4 clearcoatMap)
{
    float roughness = 1-(_ClearcoatGlossiness * clearcoatMap.a);
    roughness = clamp(roughness, 0.0045, 1.0);
    roughness = roughness * roughness;

    float reflectivity = _Clearcoat * clearcoatMap.r;
    return float4(reflectivity, 0, 0, roughness);
}

float3 getClearcoat(float3 baseColor, float reflectivity, float roughness, float ldh, float ndh, float Fr, float3 Fd)
{
    float  Dc = D_GGX(roughness, ndh);
    float  Vc = V_Kelemen(ldh);
    float  Fc = F_Schlick(0.04, ldh) * reflectivity;
    float Frc = (Dc * Vc) * Fc;

    // account for energy loss in the base layer
    return baseColor * ((Fd + Fr * (1.0 - Fc)) * (1.0 - Fc) + Frc);
}

float2 getScreenUVs(float4 screenPos)
{
    float2 uv = screenPos / (screenPos.w + 0.0000000001); //0.0x1 Stops division by 0 warning in console.
    #if UNITY_SINGLE_PASS_STEREO
        uv.xy *= float2(_ScreenParams.x * 2, _ScreenParams.y);
    #else
        uv.xy *= _ScreenParams.xy;
    #endif

    return uv;
}

float getScreenSpaceDithering(float2 screenUV)
{
    //Iestyn's RGB dither(7 asm instructions) from Portal2 X360, slightly modified for VR
    float vDither = dot(float2(171.0,231.0), screenUV.xy + _Time.y).x;
    vDither = frac(vDither / float3(103.0, 71.0, 97.0)) - float3(0.5, 0.5, 0.5);
    return (vDither / 255.0) * 0.375;
}

float getCurvature(float3 normal, float3 pos)
{
    float c = saturate((clamp(length(fwidth(normal.xyz)), 0.0, 1.0) / (length(fwidth(pos.xyz)) * 100) ));
    return c;
}

float3 getSubsurfaceFalloff(float ndl01, float ndl, float3 curvature, float3 subsurfaceColor)
{
    float curva = (1/mad(curvature.r, 0.5 - 0.0625, 0.0625) - 2) / (16 - 2);
    float oneMinusCurva = 1.0 - curva;
    float x = ndl;
    float n = ndl01;
    float3 scatter = lerp(n, subsurfaceColor, saturate((1-x) * oneMinusCurva)) * smoothstep(0.2, 1, n * oneMinusCurva);
    scatter = lerp(scatter, n, smoothstep(0, 1.1, x));
    return scatter;
}

float getGeometricSpecularAA(float3 normal)
{
    float3 vNormalWsDdx = ddx(normal.xyz);
    float3 vNormalWsDdy = ddy(normal.xyz);
    float flGeometricRoughnessFactor = pow(saturate(max(dot(vNormalWsDdx.xyz,vNormalWsDdx.xyz), dot(vNormalWsDdy.xyz,vNormalWsDdy.xyz))), 0.333);
    return max(0, flGeometricRoughnessFactor);
}

//Transmission - Based on a 2011 GDC Conference from by Colin Barre-Bresebois & Marc Bouchard
//Modified by Xiexe
float3 getTransmission(float3 subsurfaceColor, float3 attenuation, float3 diffuseColor, float thickness, float3 lightDir, float3 viewDir, float3 normal, float3 lightCol, float3 indirectDiffuse)
{
    float3 H = lightDir + normal * _TransmissionNormalDistortion;
    float VdotH = pow(saturate(dot(viewDir, -H)), _TransmissionPower) * _TransmissionScale;
    float3 I = attenuation * VdotH * thickness;
    float3 transmission = I * lightCol;
    return max(0, transmission * subsurfaceColor); // Make sure it doesn't go NaN
}

//Triplanar map a texture (Object or World space), or sample it normally.
float4 texTP( sampler2D tex, float4 tillingOffset, float3 worldPos, float3 objPos, float3 worldNormal, float3 objNormal, float falloff, float2 uv)
{
    if(_TextureSampleMode != 0)
    {
        worldPos = lerp(worldPos, objPos, _TextureSampleMode - 1);
        worldNormal = lerp(worldNormal, objNormal, _TextureSampleMode - 1);

        float3 projNormal = pow(abs(worldNormal),falloff);
        projNormal /= projNormal.x + projNormal.y + projNormal.z;
        float3 nsign = sign(worldNormal);
        float4 xNorm; float4 yNorm; float4 zNorm;
        xNorm = tex2D( tex, tillingOffset.xy * worldPos.zy * float2( nsign.x, 1.0 ) + tillingOffset.zw);
        yNorm = tex2D( tex, tillingOffset.xy * worldPos.xz * float2( nsign.y, 1.0 ) + tillingOffset.zw);
        zNorm = tex2D( tex, tillingOffset.xy * worldPos.xy * float2( -nsign.z, 1.0 ) + tillingOffset.zw);

        return xNorm * projNormal.x + yNorm * projNormal.y + zNorm * projNormal.z;
    }
    else{
        return tex2D(tex, uv * tillingOffset.xy + tillingOffset.zw);
    }
}

inline float Dither8x8Bayer(int x, int y)
{
    const float dither[ 64 ] = {
    1, 49, 13, 61,  4, 52, 16, 64,
    33, 17, 45, 29, 36, 20, 48, 32,
    9, 57,  5, 53, 12, 60,  8, 56,
    41, 25, 37, 21, 44, 28, 40, 24,
    3, 51, 15, 63,  2, 50, 14, 62,
    35, 19, 47, 31, 34, 18, 46, 30,
    11, 59,  7, 55, 10, 58,  6, 54,
    43, 27, 39, 23, 42, 26, 38, 22};
    int r = y * 8 + x;
    return dither[r] / 64;
}

float getDither(float2 screenPos)
{
    float dither = Dither8x8Bayer(fmod(screenPos.x, 8), fmod(screenPos.y, 8));
    return dither;
}

void doAlpha(inout float alpha, float4 diffuse, float4 screenPos)
{
    alpha = 1;
    #if defined(ALPHATEST)
        clip(diffuse.a - _Cutoff);
    #endif

    #if defined(DITHERED)
        float2 screenUV = getScreenUVs(screenPos);
        float dither = getDither(screenUV);
        clip(diffuse.a - dither);
    #endif

    #if defined(TRANSPARENT) || defined(ALPHATOCOVERAGE)
        alpha = diffuse.a; //should be maintex * color
    #endif
}