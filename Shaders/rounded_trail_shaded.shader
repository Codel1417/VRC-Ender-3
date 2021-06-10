// VERSION 0.3.1. Please get the latest version from:
// https://gist.github.com/lyuma/744e7fe35f7758add2f4468bb12f87f1

// Lyuma's Rounded Trail with Shading
// Based on phi16's rounded trail
// Based on Xiexe's Unity Lit Shader Templates:
// https://github.com/Xiexe/Unity-Lit-Shader-Templates

// Installation Notes:
// YOU MUST COPY ALL OF THE .cginc FILES FROM
// Unity-Lit-Shader-Templates/XSShaderTemplates/Templates/Shared
// INTO THE SAME DIRECTORY AS THIS SHADER.
/*
MIT License

Copyright (c) 2020 Xiexe, (c) 2021 Lyuma
Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/
/*
 Original file :
 Storage for distribution - phi16
 https://github.com/phi16/VRC_storage
 rounded_trail.unitypackage
 LICENSE : CC0
*/
// 2020-04-16 seeing vertex color.
// 2019-09-26 customized for QvPen v2.
// 2019-09-09 customized for QvPen.
Shader "LyumaShader/rounded_trail_with_shading"
{
	Properties
	{
		_Width ("Width", Float) = 0.03
		[ToggleUI] _DotMode ("Create dots instead of trails", Float) = 0
		[ToggleUI] _DisableEndpoint("Disable Endpoints (for Distribute by Segment", Range(0, 1)) = 0
		[ToggleUI] _DisableUVScale("Use trail renderer UV scaling", Range(0, 1)) = 0
		//[Enum(Shaded,0,Normal,1,Tangent,2,Bitangent,3,ReferenceDir,4,RoundedToFlatBlend,5,MatcapUV,6,TrailUV,7)] _DebugNormal("Debug Norm,T,B,R,UV", Float) = 0
		_MaxSegmentLength ("MaxSegmentLength", Float) = 0
_MatcapAdd ("Matcap Additive", 2D) = "black" {}
_MatcapMul ("Matcap Multiplicitive", 2D) = "white" {}
_PositionOffset("Local Position offset", Vector) = (0,0,0,0)
[Header(MAIN)]
[Enum(Unity Default, 0, Non Linear, 1)]_LightProbeMethod("Light Probe Sampling", Int) = 0
[Enum(UVs, 0, Triplanar World, 1, Triplanar Object, 2)]_TextureSampleMode("Texture Mode", Int) = 0
_TriplanarFalloff("Triplanar Blend", Range(0.5,1)) = 1
_MainTex ("Main Texture", 2D) = "white" {}
_Color ("Color", Color) = (1,1,1,1)
//#CUTOUT!_Cutoff ("Alpha Cutoff", Range(0,1)) = 0.5

[Space(16)]
[Header(NORMALS)]
_BumpMap("Normal Map", 2D) = "bump" {}
_BumpScale("Normal Scale", Range(-1,1)) = 1

[Space(16)]
[Header(METALLIC)]
_MetallicGlossMap("Metallic Map", 2D) = "white" {}
[Gamma] _Metallic("Metallic", Range(0,1)) = 0
_Glossiness("Smoothness", Range(0,1)) = 0
_Reflectance("Reflectance", Range(0,1)) = 0.5
_Anisotropy("Anisotropy", Range(-1,1)) = 0

[Space(16)]
[Header(OCCLUSION)]
_OcclusionMap("Occlusion Map", 2D) = "white" {}
_OcclusionColor("Occlusion Color", Color) = (0,0,0,1)

[Space(16)]
[Header(SUBSURFACE)]
[Enum(Off, 0, Estimate, 1)]_SubsurfaceMethod("Subsurface Scattering Method", Int) = 0
_CurvatureThicknessMap("Curvature Thickness Map", 2D) = "gray" {}
_SubsurfaceColorMap("Subsurface Color Map", 2D) = "white" {}
_SubsurfaceScatteringColor("Subsurface Color", Color) = (1,1,1,1)
_SubsurfaceInheritDiffuse("Subsurface Inherit Diffuse", Range(0,1)) = 0
_TransmissionNormalDistortion("Transmission Distortion", Range(0,3)) = 1
_TransmissionPower("Transmission Power", Range(0,3)) = 1
_TransmissionScale("Transmission Scale", Range(0,3)) = 0.1

[Space(16)]
[Header(EMISSION)]
_EmissionMap("Emission Map", 2D) = "white" {}
[HDR]_EmissionColor("Emission Color", Color) = (0,0,0,1)

[Space(16)]
[Header(CLEARCOAT)]
_ClearcoatMap("Clearcoat Map", 2D) = "white" {}
_Clearcoat("Clearcoat", Range(0,1)) = 0
_ClearcoatGlossiness("Clearcoat Smoothness", Range(0,1)) = 0.5
_ClearcoatAnisotropy("Clearcoat Anisotropy", Range(-1,1)) = 0

//#GEOM![Space(16)]
//#GEOM![Header(GEOMETRY SETTINGS)]
//#GEOM!_VertexOffset("Face Offset", float) = 0

//#TESS![Space(16)]
//#TESS![Header(GEOMETRYTESSELLATION SETTINGS)]
//#TESS![Enum(Uniform, 0, Edge Length, 1, Distance, 2)]_TessellationMode("Tessellation Mode", Int) = 1
//#TESS!_TessellationUniform("Tessellation Factor", Range(0,1)) = 0.05
//#TESS!_TessClose("Tessellation Close", Float) = 10
//#TESS!_TessFar("Tessellation Far", Float) = 50

[Space(16)]
[Header(LIGHTMAPPING HACKS)]
_SpecularLMOcclusion("Specular Occlusion", Range(0,1)) = 0
_SpecLMOcclusionAdjust("Spec Occlusion Sensitiviy", Range(0,1)) = 0.2
_LMStrength("Lightmap Strength", Range(0,1)) = 1
_RTLMStrength("Realtime Lightmap Strength", Range(0,1)) = 1
	}
	SubShader
	{
		LOD 100
		Cull Off

		Pass
		{
			Tags {"LightMode"="ForwardBase" }
			CGPROGRAM
			#pragma vertex vert
			#pragma geometry geom
			#pragma fragment frag
			
			#include "UnityCG.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
				float4 color : COLOR;
			};

			struct v2g
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
				float4 color : COLOR;
			};

			struct trailG2F
			{
				float4 vertex : SV_POSITION;
				float2 uv : TEXCOORD0;
				float totalDistance : TEXCOORD1;
				float2 dn : TEXCOORD2;
				centroid float3 normal : TEXCOORD3;
				float3 tangent : TEXCOORD4;
				float3 worldPos: TEXCOORD5;
				float4 screenPos : TEXCOORD6;
				centroid float4 color : COLOR;
			};


			struct g2f
			{
				float4 pos : SV_POSITION;
				float2 uv : TEXCOORD0;
				float2 uv1 : TEXCOORD1;
				float2 uv2 : TEXCOORD2;
				float3 btn[3] : TEXCOORD3; //TEXCOORD2, TEXCOORD3 | bitangent, tangent, worldNormal
				float3 worldPos : TEXCOORD6;
				float3 objPos : TEXCOORD7;
				float3 objNormal : TEXCOORD8;
				float4 screenPos : TEXCOORD9;
			};

			#ifndef UNITY_PASS_FORWARDBASE
				#define UNITY_PASS_FORWARDBASE
			#endif

			#define GEOMETRY			
			#include "UnityCG.cginc"
			#include "Lighting.cginc"
			#include "AutoLight.cginc"
			#ifdef UNITY_LIGHT_ATTENUATION
			#undef UNITY_LIGHT_ATTENUATION
			#endif
			#define UNITY_LIGHT_ATTENUATION(a, b, c) float a = 1.0;

			// fwidth will use derivitives along the trail because polygons across it are too narrow.
			#define tex2D(x, uv) tex2Dgrad(x, uv, fwidth(uv), fwidth(uv))

			#include "Defines.cginc"
			#include "LightingFunctions.cginc"
			#include "LightingBRDF.cginc"
			
			float _Width;
			//float _DebugNormal;
			float _DisableEndpoint;
			float _DisableUVScale;
			float _DotMode;
			float _MaxSegmentLength;
			float4 _PositionOffset;
			sampler2D _MatcapAdd;
			sampler2D _MatcapMul;
			
			v2g vert (appdata v)
			{
				v2g o;
				o.vertex = v.vertex;
				o.vertex.xyz += _PositionOffset.xyz;
				o.uv = v.uv;
				o.color = v.color;
				return o;
			}

			static float3 fwdNormal = -normalize(_WorldSpaceCameraPos);

			float3 tonormal(float2 uv, float4 origPos) {
				origPos.y *= -1;
				uv.y *= -1;
				float4 pos1 = (mul(unity_CameraInvProjection, origPos + float4(uv, 0, 0)));
				float4 pos2 = (mul(unity_CameraInvProjection, origPos));
				return normalize(mul((float3x3)unity_CameraToWorld, (pos1 - pos2).xyz));
			}

			[maxvertexcount(4)]
			void geom(triangle v2g IN[3], inout TriangleStream<trailG2F> stream) {
				trailG2F o = (trailG2F)0;
				o.uv = 0;
				o.color = IN[0].color;
				float4 firstVertex = IN[0].vertex;
				float4 secondVertex = IN[2].vertex;
				float2 firstUV = IN[0].uv;
				float2 secondUV = IN[2].uv;
				float2 midUV = IN[1].uv;
				if (distance(firstVertex.xyz, secondVertex.xyz) < 0.9999 * distance(firstVertex.xyz, IN[1].vertex.xyz)) {
					secondVertex = IN[1].vertex;
					secondUV = IN[1].uv;
					midUV = IN[2].uv;
				}
				if (abs(secondUV.x - firstUV.x) > _MaxSegmentLength) {
					return;
				}
				float3 localTangent = normalize(secondVertex.xyz - firstVertex.xyz);
				float3 worldPos0 = mul(unity_ObjectToWorld, firstVertex);
				float3 worldPos1 = mul(unity_ObjectToWorld, secondVertex);
				o.tangent = normalize(worldPos1 - worldPos0);
				
				float4 p = UnityObjectToClipPos(firstVertex);
				float4 q = UnityObjectToClipPos(secondVertex);
				float4 pFarZ = UnityObjectToClipPos(firstVertex + .95 * _Width * normalize(firstVertex.xyz - _WorldSpaceCameraPos));
				float4 qFarZ = UnityObjectToClipPos(secondVertex + .85 * _Width * normalize(secondVertex.xyz - _WorldSpaceCameraPos));
				float2 d = p.xy / p.w - q.xy / q.w;
				float aspectRatio = -_ScreenParams.y / _ScreenParams.x;
				d.x /= aspectRatio;
				o.dn.x = length(d);
				if(length(d) < 0.000001) d = float2(1, 0);
				else d = normalize(d);
				
				float2 w = _Width;
				w *= float2(aspectRatio, -1);
				w *= unity_CameraProjection._m11 / 1.732;
				float4 n = {d.yx, 0, 0};
				n.xy *= w;
				float2 origN = n.xy;
				float3 normalp = tonormal(n.xy, p);
				float3 normalq = tonormal(n.xy, q);
				float totalLength = distance(firstVertex.xyz, secondVertex) / (secondUV.x - firstUV.x);
				float startAbsDistance = lerp((1 - secondUV.x) * totalLength, firstUV.x, _DisableUVScale);
				float endAbsDistance = lerp(startAbsDistance - distance(firstVertex.xyz, secondVertex.xyz), secondUV.x, _DisableUVScale);
				if(firstUV.x + secondUV.x > midUV.x * 2 && _DotMode == 0 && (_DisableEndpoint > 0.5 || midUV.x < 0.999999)) {
					if (_MaxSegmentLength > 0 && distance(firstVertex.xyz, secondVertex.xyz) > _MaxSegmentLength) {
						return;
					}
					o.dn.x = 0;
					o.vertex = p + n;
					o.screenPos = o.vertex;
					o.vertex.z = p.z / p.w * o.vertex.w;
					o.worldPos = worldPos0;
					o.uv = -n.xy;
					o.dn.y = length(n.xy / p.w);
					o.totalDistance = startAbsDistance;
					o.normal = normalp;
					stream.Append(o);
					o.vertex = p - n;
					o.vertex.z = p.z / p.w * o.vertex.w;
					o.worldPos = worldPos0;
					o.screenPos = o.vertex;
					o.uv = n.xy;
					o.dn.y = length(n.xy / p.w);
					o.totalDistance = startAbsDistance;
					o.normal = -normalp;
					stream.Append(o);
					o.vertex = q + n;
					o.vertex.z = q.z / q.w * o.vertex.w;
					o.worldPos = worldPos1;
					o.screenPos = o.vertex;
					o.uv = -n.xy;
					o.dn.y = length(n.xy / q.w);
					o.totalDistance = endAbsDistance;
					o.normal = normalq;
					stream.Append(o);
					o.vertex = q - n;
					o.vertex.z = q.z / q.w * o.vertex.w;
					o.worldPos = worldPos1;
					o.screenPos = o.vertex;
					o.dn.y = length(n.xy / q.w);
					o.uv = n.xy;
					o.totalDistance = endAbsDistance;
					o.normal = -normalq;
					stream.Append(o);
					stream.RestartStrip();
				} else {
					o.dn.x = 1;
					w *= 2;

					n.xy = (o.uv = float2(0, 1)) * w;
					o.normal = tonormal(n.xy, p) * 2;
					o.worldPos = worldPos0;
					o.vertex = p + n;
					o.dn.y = length(n.xy / p.w);
					o.totalDistance = startAbsDistance - 2 * dot(float2(1,0), origN.xy);
					o.screenPos = o.vertex;
					if (o.vertex.z/o.vertex.w < 1) { o.vertex.z = pFarZ.z / pFarZ.w * o.vertex.w; }
					stream.Append(o);
					n.xy = (o.uv = float2(-0.866, -0.5)) * w;
					o.normal = tonormal(n.xy, p) * 2;
					o.worldPos = worldPos0;
					o.vertex = p + n;
					o.dn.y = length(n.xy / p.w);
					o.totalDistance = startAbsDistance - 2 * dot(float2(0.5, -0.866), origN.xy);
					o.screenPos = o.vertex;
					if (o.vertex.z/o.vertex.w < 1) { o.vertex.z = pFarZ.z / pFarZ.w * o.vertex.w; }
					
					stream.Append(o);
					n.xy = (o.uv = float2(0.866, -0.5)) * w;
					o.normal = tonormal(n.xy, p) * 2; // multiply by 2 because centroid of equalateral triangle
					o.worldPos = worldPos0;
					o.vertex = p + n;
					o.dn.y = length(n.xy / p.w);
					o.totalDistance = startAbsDistance - 2 * dot(float2(0.5, 0.866), origN.xy);
					o.screenPos = o.vertex;
					if (o.vertex.z/o.vertex.w < 1) { o.vertex.z = pFarZ.z / pFarZ.w * o.vertex.w; }
					stream.Append(o);
					stream.RestartStrip();
				}
			}

			fixed4 frag (trailG2F i) : SV_Target
			{
				float aspectRatio = -_ScreenParams.y / _ScreenParams.x;
				float2 w = _Width;
				w *= float2(aspectRatio, -1);
				w *= unity_CameraProjection._m11 / 1.732;
				_Color *= i.color;

				float l = length(i.uv);
				clip(0.5 - min(i.dn.x, l));
				//if (0.5 - min(i.dn.x, l) < 0) { return float4(0,0,0,1); } //clip(0.5 - min(i.dn.x, l));
				float normalLengthSq = dot(i.normal, i.normal);
				float3 dirToCam = normalize(_WorldSpaceCameraPos - i.worldPos);
				float3 origNormal = normalize(i.normal);
				float3 newNormal = dirToCam - dot(dirToCam, i.tangent) * i.tangent;
				i.normal += newNormal * sqrt(1 - normalLengthSq);
				i.normal = lerp(dirToCam, i.normal, pow(smoothstep(0.4, 1.0, saturate(.5*min(i.dn.y*_ScreenParams.x, i.dn.y*_ScreenParams.y))), 0.5));
				//min(i.dn.y/_ScreenParams.x, i.dn.y/_ScreenParams.y))); //smoothstep(1000/length(_ScreenParams.xy), 2000/length(_ScreenParams.xy), length(i.dn.y)));
				i.normal = normalize(i.normal);
				float3 refDir = abs(i.tangent.y) > 0.999 ? float3(0,0,1) : float3(0,1,0);
				float3 refDir2 = cross(i.tangent, refDir);
				if (dot(i.tangent, refDir) < 0) {
					refDir = cross(refDir2, i.tangent);
					i.tangent = -i.tangent;
				} else {
					refDir = cross(refDir2, i.tangent);
				}
				float2 matcapUV = 0.5 + 0.5 * mul((float3x3)UNITY_MATRIX_V, cross(float3(0,1,0),cross(i.normal, float3(0,1,0)))).xy;
				g2f xsData = (g2f)0;
				xsData.pos = i.vertex;
				float3 bitangent = normalize(cross(i.normal, i.tangent));
				xsData.btn[0] = bitangent;
				xsData.btn[1] = i.tangent;
				xsData.btn[2] = i.normal;
				float angle = (atan2(dot(i.normal, -refDir2), dot(i.normal, refDir))) / (6.28) + 0.5;
				xsData.uv = float2(i.totalDistance / 6.28 / _Width, angle);
				xsData.uv1 = xsData.uv2 = i.uv;
				xsData.worldPos = i.worldPos - _Width * bitangent;
				xsData.objPos = mul(unity_WorldToObject, float4(xsData.worldPos, 1.0)).xyz;
				xsData.objNormal = mul((float3x3)unity_WorldToObject, i.normal);
				xsData.screenPos = i.screenPos;
				return CustomStandardLightingBRDF(xsData) * tex2D(_MatcapMul, matcapUV) + float4(tex2D(_MatcapAdd, matcapUV).rgb, 0.0);
			}
			ENDCG
		}
	}
}
