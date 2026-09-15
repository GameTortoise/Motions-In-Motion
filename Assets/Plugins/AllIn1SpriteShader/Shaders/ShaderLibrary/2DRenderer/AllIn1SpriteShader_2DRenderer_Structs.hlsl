#ifndef ALLIN1SPRITESHADER_2DRENDERER_STRUCTS
#define ALLIN1SPRITESHADER_2DRENDERER_STRUCTS

struct Attributes
{
    float4 vertex : POSITION;
    float2 uv : TEXCOORD0;
	half4 color : COLOR;

    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct v2f
{
    float2	uv          : TEXCOORD0;
    float4  vertex  : SV_POSITION;
    half4  color       : COLOR;
    float2	lightingUV  : TEXCOORD1;
	#if OUTTEX_ON
	half2 uvOutTex : TEXCOORD2;
	#endif
	#if OUTDIST_ON
	half2 uvOutDistTex : TEXCOORD3;
	#endif
	#if DISTORT_ON
	half2 uvDistTex : TEXCOORD4;
	#endif
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

struct VertexDataNormalsPass
{
	float4 vertex : POSITION;
	float2 uv : TEXCOORD0;
	half4 color : COLOR;
	half4 tangent : TANGENT;
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct FragmentDataNormalsPass
{
	float2 uv : TEXCOORD0;
	float4 vertex : SV_POSITION;
	half4 color : COLOR;
	half3  normalWS	: TEXCOORD1;
	half3  tangentWS : TEXCOORD2;
	half3  bitangentWS : TEXCOORD3;
	#if OUTTEX_ON
	half2 uvOutTex : TEXCOORD4;
	#endif
	#if OUTDIST_ON
	half2 uvOutDistTex : TEXCOORD5;
	#endif
	#if DISTORT_ON
	half2 uvDistTex : TEXCOORD6;
	#endif
	UNITY_VERTEX_INPUT_INSTANCE_ID
	UNITY_VERTEX_OUTPUT_STEREO
};


#endif //ALLIN1SPRITESHADER_2DRENDERER_STRUCTS