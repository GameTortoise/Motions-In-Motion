#ifndef ALLIN1SPRITESHADERSRP_HDRPHELPER
#define ALLIN1SPRITESHADERSRP_HDRPHELPER

//fogCoord only used in URP. It's here to make both functions have the same parameters
float4 AllIn1MixFog(float4 inputColor, float4 clipPos, float3 positionWS, float fogCoord)
{
	float4 res = inputColor;
    if (_FogEnabled)
    {
		PositionInputs posInput = GetPositionInput(
			clipPos.xy, 
			_ScreenSize.zw, 
			clipPos.z, 
			clipPos.w, 
			positionWS, //In HDRP is RWS 
			0
		);

		float3 V = GetWorldSpaceNormalizeViewDir(positionWS);

		float3 fogColor;
		float3 fogOpacity;
		EvaluateAtmosphericScattering(posInput, V, fogColor, fogOpacity);

		res.rgb = (res.rgb * fogOpacity) + fogColor;
		res.a *= (1 - fogOpacity);
	}

	return res;
}

#endif //ALLIN1SPRITESHADERSRP_HDRPHELPER