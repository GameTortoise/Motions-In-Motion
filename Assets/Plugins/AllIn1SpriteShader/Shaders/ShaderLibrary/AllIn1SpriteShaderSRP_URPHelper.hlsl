#ifndef ALLIN1SPRITESHADERSRP_URPHELPER
#define ALLIN1SPRITESHADERSRP_URPHELPER

//clipPos and positionWS only used in HDRP. They're here to make both functions have the same parameters
float4 AllIn1MixFog(float4 inputColor, float4 clipPos, float3 positionWS, float fogCoord)
{
	float4 res = inputColor;

	res.rgb = MixFog(inputColor.rgb, fogCoord);

	return res;
}

#endif //ALLIN1SPRITESHADERSRP_URPHELPER