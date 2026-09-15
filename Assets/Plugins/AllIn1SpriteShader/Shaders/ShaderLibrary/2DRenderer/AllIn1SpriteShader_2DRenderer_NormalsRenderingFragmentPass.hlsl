#ifndef ALLIN1SPRITESHADER_2DRENDERER_NORMALSRENDERINGFRAGMENTPASS
#define ALLIN1SPRITESHADER_2DRENDERER_NORMALSRENDERINGFRAGMENTPASS

float4 NormalsRenderingFragment(FragmentDataNormalsPass i) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(i);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    half randomSeed = UNITY_ACCESS_INSTANCED_PROP(Props, _RandomSeed);
	
	float texWidth;
	float texHeight;
	_MainTex.GetDimensions(texWidth, texHeight);
	float4 texelSize = float4(1.0 / texWidth, 1 / texHeight, texWidth, texHeight);

	float2 uvRect = i.uv;
	half2 center = half2(0.5, 0.5);
	#if ATLAS_ON
	center = half2((_MaxXUV + _MinXUV) / 2.0, (_MaxYUV + _MinYUV) / 2.0);
	uvRect = half2((i.uv.x - _MinXUV) / (_MaxXUV - _MinXUV), (i.uv.y - _MinYUV) / (_MaxYUV - _MinYUV));
	#endif
	half2 centerTiled = half2(center.x *  _MainTex_ScaleAndTiling.x, center.y *  _MainTex_ScaleAndTiling.y);

	#if CLIPPING_ON
	half2 tiledUv = half2(i.uv.x / _MainTex_ScaleAndTiling.x, i.uv.y / _MainTex_ScaleAndTiling.y);
	#if ATLAS_ON
	tiledUv = half2((tiledUv.x - _MinXUV) / (_MaxXUV - _MinXUV), (tiledUv.y - _MinYUV) / (_MaxYUV - _MinYUV));
	#endif
	clip((1 - _ClipUvUp) - tiledUv.y);
	clip(tiledUv.y - _ClipUvDown);
	clip((1 - _ClipUvRight) - tiledUv.x);
	clip(tiledUv.x - _ClipUvLeft);
	#endif

    #if RADIALCLIPPING_ON
	half2 tiledUv2 = half2(i.uv.x / _MainTex_ScaleAndTiling.x, i.uv.y / _MainTex_ScaleAndTiling.y);
	#if ATLAS_ON
	tiledUv2 = half2((tiledUv2.x - _MinXUV) / (_MaxXUV - _MinXUV), (tiledUv2.y - _MinYUV) / (_MaxYUV - _MinYUV));
	#endif
	half startAngle = _RadialStartAngle - _RadialClip;
    half endAngle = _RadialStartAngle + _RadialClip2;
    half offset0 = clamp(0, 360, startAngle + 360);
    half offset360 = clamp(0, 360, endAngle - 360);
    half2 atan2Coord = half2(lerp(-1, 1, tiledUv2.x), lerp(-1, 1, tiledUv2.y));
    half atanAngle = atan2(atan2Coord.y, atan2Coord.x) * 57.3; // angle in degrees
    if(atanAngle < 0) atanAngle = 360 + atanAngle;
    if(atanAngle >= startAngle && atanAngle <= endAngle) discard;
    if(atanAngle <= offset360) discard;
    if(atanAngle >= offset0) discard;
	#endif

	#if TEXTURESCROLL_ON && ATLAS_ON
	i.uv = half2(_MinXUV + ((_MaxXUV - _MinXUV) * (abs(((_Time.y + randomSeed) * _TextureScrollXSpeed) + uvRect.x) % 1)),
	_MinYUV + ((_MaxYUV - _MinYUV) * (abs(((_Time.y + randomSeed) * _TextureScrollYSpeed) + uvRect.y) % 1)));
	#endif

	#if OFFSETUV_ON
	#if ATLAS_ON
	i.uv = half2(_MinXUV + ((_MaxXUV - _MinXUV) * (abs((_OffsetUvX + uvRect.x) % 1))),
	_MinYUV + ((_MaxYUV - _MinYUV) * (abs(_OffsetUvY + uvRect.y) % 1)));
	#else
	i.uv += half2(_OffsetUvX, _OffsetUvY);
	#endif
	#endif

	#if POLARUV_ON
	i.uv = half2(atan2(i.uv.y, i.uv.x) / (2.0f * 3.141592653589f), length(i.uv));
	i.uv *= _MainTex_ScaleAndTiling.xy;
	#endif

	#if TWISTUV_ON
	#if ATLAS_ON
	_TwistUvPosX = ((_MaxXUV - _MinXUV) * _TwistUvPosX) + _MinXUV;
	_TwistUvPosY = ((_MaxYUV - _MinYUV) * _TwistUvPosY) + _MinYUV;
	#endif
	half2 tempUv = i.uv - half2(_TwistUvPosX *  _MainTex_ScaleAndTiling.x, _TwistUvPosY *  _MainTex_ScaleAndTiling.y);
	_TwistUvRadius *= (_MainTex_ScaleAndTiling.x + _MainTex_ScaleAndTiling.y) / 2;
	half percent = (_TwistUvRadius - length(tempUv)) / _TwistUvRadius;
	half theta = percent * percent * (2.0 * sin(_TwistUvAmount)) * 8.0;
	half s = sin(theta);
	half c = cos(theta);
	half beta = max(sign(_TwistUvRadius - length(tempUv)), 0.0);
	tempUv = half2(dot(tempUv, half2(c, -s)), dot(tempUv, half2(s, c))) * beta +	tempUv * (1 - beta);
	tempUv += half2(_TwistUvPosX *  _MainTex_ScaleAndTiling.x, _TwistUvPosY *  _MainTex_ScaleAndTiling.y);
	i.uv = tempUv;
	#endif

	#if FISHEYE_ON
	half bind = length(centerTiled);
	half2 dF = i.uv - centerTiled;
	half dFlen = length(dF);
	half fishInt = (3.14159265359 / bind) * (_FishEyeUvAmount + 0.001);
	i.uv = centerTiled + (dF / (max(0.0001, dFlen))) * tan(dFlen * fishInt) * bind / tan(bind * fishInt);
	#endif

	#if PINCH_ON
	half2 dP = i.uv - centerTiled;
	half pinchInt = (3.141592 / length(centerTiled)) * (-_PinchUvAmount + 0.001);
	i.uv = centerTiled + normalize(dP) * atan(length(dP) * -pinchInt * 10.0) * 0.5 / atan(-pinchInt * 5);
	#endif

	#if ZOOMUV_ON
	i.uv -= centerTiled;
	i.uv = i.uv * _ZoomUvAmount;
	i.uv += centerTiled;
	#endif

	#if DOODLE_ON
	half2 uvCopy = uvRect;
	_HandDrawnSpeed = (floor((_Time.x + randomSeed) * 20 * _HandDrawnSpeed) / _HandDrawnSpeed) * _HandDrawnSpeed;
	uvCopy.x = sin((uvCopy.x * _HandDrawnAmount + _HandDrawnSpeed) * 4);
	uvCopy.y = cos((uvCopy.y * _HandDrawnAmount + _HandDrawnSpeed) * 4);
	i.uv = lerp(i.uv, i.uv + uvCopy, 0.0005 * _HandDrawnAmount);
	#endif

	#if SHAKEUV_ON
	half xShake = sin((_Time.x + randomSeed) * _ShakeUvSpeed * 50) * _ShakeUvX;
	half yShake = cos((_Time.x + randomSeed) * _ShakeUvSpeed * 50) * _ShakeUvY;
	i.uv += half2(xShake * 0.012, yShake * 0.01);
	#endif

	#if RECTSIZE_ON
	i.uv = i.uv.xy * (_RectSize).xx + (((-_RectSize * 0.5) + 0.5)).xx;
	#endif

	#if DISTORT_ON
	#if ATLAS_ON
    i.uvDistTex.x = i.uvDistTex.x * (1 / (_MaxXUV - _MinXUV));
	i.uvDistTex.y = i.uvDistTex.y * (1 / (_MaxYUV - _MinYUV));
	#endif
    i.uvDistTex.x += ((_Time.x + randomSeed) * _DistortTexXSpeed) % 1;
	i.uvDistTex.y += ((_Time.x + randomSeed) * _DistortTexYSpeed) % 1;
	half distortAmnt = (ALLIN1_SAMPLE_TEXTURE_2D(_DistortTex, i.uvDistTex).r - 0.5) * 0.2 * _DistortAmount;
	i.uv.x += distortAmnt;
	i.uv.y += distortAmnt;
	#endif

    #if WARP_ON
    half2 warpUv = half2(i.uv.x / _MainTex_ScaleAndTiling.x, i.uv.y / _MainTex_ScaleAndTiling.y);
	#if ATLAS_ON
	warpUv = half2((warpUv.x - _MinXUV) / (_MaxXUV - _MinXUV), (warpUv.y - _MinYUV) / (_MaxYUV - _MinYUV));
	#endif
	const float tau = 6.283185307179586;
    float xWarp = (_Time.y + randomSeed) * _WarpSpeed + warpUv.x * tau / _WarpScale;
    float yWarp = (_Time.y + randomSeed) * _WarpSpeed + warpUv.y * tau / _WarpScale;
    float2 warp = float2(sin(xWarp), sin(yWarp)) * _WarpStrength;
    i.uv += warp;
	#endif

	#if WAVEUV_ON
	float2 uvWave = half2(_WaveX *  _MainTex_ScaleAndTiling.x, _WaveY *  _MainTex_ScaleAndTiling.y) - i.uv;
    uvWave %= 1;
	#if ATLAS_ON
	uvWave = half2(_WaveX, _WaveY) - uvRect;
	#endif
	uvWave.x *= _ScreenParams.x / _ScreenParams.y;
    float waveTime = _Time.y + randomSeed;
	float angWave = (sqrt(dot(uvWave, uvWave)) * _WaveAmount) - ((waveTime *  _WaveSpeed));
	i.uv = i.uv + uvWave * sin(angWave) * (_WaveStrength / 1000.0);
	#endif

	#if ROUNDWAVEUV_ON
	half xWave = ((0.5 * _MainTex_ScaleAndTiling.x) - uvRect.x);
	half yWave = ((0.5 * _MainTex_ScaleAndTiling.y) - uvRect.y) * (texelSize.w / texelSize.z);
	half ripple = -sqrt(xWave*xWave + yWave* yWave);
	i.uv += sin((ripple + (_Time.y + randomSeed) * (_RoundWaveSpeed/10.0)) / 0.015) * (_RoundWaveStrength/10.0);
	#endif

	#if WIND_ON
	half windOffset = sin((_Time.x + randomSeed) * _GrassSpeed * 10);
	half2 windCenter = half2(0.5, 0.1);
	#if ATLAS_ON
	windCenter.x = ((_MaxXUV - _MinXUV) * windCenter.x) + _MinXUV;
	windCenter.y = ((_MaxYUV - _MinYUV) * windCenter.y) + _MinYUV;
	#endif
	#if !MANUALWIND_ON
	i.uv.x = fmod(abs(lerp(i.uv.x, i.uv.x + (_GrassWind * 0.01 * windOffset), uvRect.y)), 1);
	#else
	i.uv.x = fmod(abs(lerp(i.uv.x, i.uv.x + (_GrassWind * 0.01 * _GrassManualAnim), uvRect.y)), 1);
	windOffset = _GrassManualAnim;
	#endif
	half2 delta = i.uv - windCenter;
	half delta2 = dot(delta.xy, delta.xy);
	half2 delta_offset = delta2 * windOffset;
	i.uv = i.uv + half2(delta.y, -delta.x) * delta_offset * _GrassRadialBend;
	#endif

	#if TEXTURESCROLL_ON && !ATLAS_ON
	i.uv.x += ((_Time.y + randomSeed) * _TextureScrollXSpeed) % 1;
	i.uv.y += ((_Time.y + randomSeed) * _TextureScrollYSpeed) % 1;
	#endif

	#if PIXELATE_ON
    half aspectRatio = texelSize.x / texelSize.y;
	half2 pixelSize = float2(_PixelateSize, _PixelateSize * aspectRatio);
	i.uv = floor(i.uv * pixelSize) / pixelSize;
	#endif

	half4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
	half originalAlpha = col.a;
	col *= i.color;
	#if PREMULTIPLYALPHA_ON
	col.rgb *= col.a;
	#endif

	#if GLITCH_ON
	half2 uvGlitch = uvRect;
	uvGlitch.y -= 0.5;
	half lineNoise = pow(rand2(floor(uvGlitch * half2(24., 19.) * _GlitchSize) * 4.0, randomSeed, _GlitchSpeed), 3.0) * _GlitchAmount
		* pow(rand2(floor(uvGlitch * half2(38., 14.) * _GlitchSize) * 4.0, randomSeed, _GlitchSpeed), 3.0);
	col = ALLIN1_SAMPLE_TEXTURE_2D(_MainTex, i.uv + half2(lineNoise * 0.02 * rand2(half2(2.0, 1), randomSeed, _GlitchSpeed), 0)) * i.color;
	#endif

	#if CHROMABERR_ON
	half4 r = ALLIN1_SAMPLE_TEXTURE_2D(_MainTex, i.uv + half2(_ChromAberrAmount/10, 0)) * i.color;
	half4 b = ALLIN1_SAMPLE_TEXTURE_2D(_MainTex, i.uv + half2(-_ChromAberrAmount/10, 0)) * i.color;
	col = half4(r.r * r.a, col.g, b.b * b.a, max(max(r.a, b.a) * _ChromAberrAlpha, col.a));
	#endif

	#if BLUR_ON
    #if ATLAS_ON
    #if !BLURISHD_ON
	col = BlurHD(i.uv, _MainTex, sampler_MainTex, _BlurIntensity, (_MaxXUV - _MinXUV), (_MaxYUV - _MinYUV)) * i.color;
    #else
    col = Blur(i.uv, _MainTex, sampler_MainTex, _BlurIntensity * (_MaxXUV - _MinXUV)) * i.color;
    #endif
    #else
    #if !BLURISHD_ON
	col = BlurHD(i.uv, _MainTex, sampler_MainTex, _BlurIntensity, 1, 1) * i.color;
    #else
    col = Blur(i.uv, _MainTex, sampler_MainTex, _BlurIntensity) * i.color;
    #endif
    #endif
    #endif

	half luminance = 0;
	#if GHOST_ON
	luminance = 0.3 * col.r + 0.59 * col.g + 0.11 * col.b;
	half4 ghostResult;
	ghostResult.a = saturate(luminance - _GhostTransparency) * col.a;
	ghostResult.rgb = col.rgb * (luminance + _GhostColorBoost);
	col = lerp(col, ghostResult, _GhostBlend);
	#endif

	#if OVERLAY_ON
    half2 overlayUvs = i.uv;
    overlayUvs.x += ((_Time.y + randomSeed) * _OverlayTextureScrollXSpeed) % 1;
	overlayUvs.y += ((_Time.y + randomSeed) * _OverlayTextureScrollYSpeed) % 1;
	half4 overlayCol = ALLIN1_SAMPLE_TEXTURE_2D(_OverlayTex, CUSTOM_TRANSFORM_TEX(overlayUvs, _OverlayTex_ScaleAndTiling));
	overlayCol.rgb *= _OverlayColor.rgb * _OverlayGlow;
	#if !OVERLAYMULT_ON
	overlayCol.rgb *= overlayCol.a * _OverlayColor.rgb * _OverlayColor.a * _OverlayBlend;
	col.rgb += overlayCol.rgb;
	#else
	overlayCol.a *= _OverlayColor.a;
	col = lerp(col, col * overlayCol, _OverlayBlend);
	#endif
	#endif

	//OUTLINE-------------------------------------------------------------
	#if OUTBASE_ON
		#if OUTBASEPIXELPERF_ON
		half2 destUv = half2(_OutlinePixelWidth * texelSize.x, _OutlinePixelWidth * texelSize.y);
		#else
		half2 destUv = half2(_OutlineWidth * texelSize.x * 200, _OutlineWidth * texelSize.y * 200);
		#endif

		#if OUTDIST_ON
		i.uvOutDistTex.x += ((_Time.x + randomSeed) * _OutlineDistortTexXSpeed) % 1;
		i.uvOutDistTex.y += ((_Time.x + randomSeed) * _OutlineDistortTexYSpeed) % 1;
		#if ATLAS_ON
		i.uvOutDistTex = half2((i.uvOutDistTex.x - _MinXUV) / (_MaxXUV - _MinXUV), (i.uvOutDistTex.y - _MinYUV) / (_MaxYUV - _MinYUV));
		#endif
		half outDistortAmnt = (ALLIN1_SAMPLE_TEXTURE_2D(_OutlineDistortTex, i.uvOutDistTex).r - 0.5) * 0.2 * _OutlineDistortAmount;
		destUv.x += outDistortAmnt;
		destUv.y += outDistortAmnt;
		#endif

		half spriteLeft = ALLIN1_SAMPLE_TEXTURE_2D(_MainTex, i.uv + half2(destUv.x, 0)).a;
		half spriteRight = ALLIN1_SAMPLE_TEXTURE_2D(_MainTex, i.uv - half2(destUv.x, 0)).a;
		half spriteBottom = ALLIN1_SAMPLE_TEXTURE_2D(_MainTex, i.uv + half2(0, destUv.y)).a;
		half spriteTop = ALLIN1_SAMPLE_TEXTURE_2D(_MainTex, i.uv - half2(0, destUv.y)).a;
		half result = spriteLeft + spriteRight + spriteBottom + spriteTop;

		#if OUTBASE8DIR_ON
		half spriteTopLeft = ALLIN1_SAMPLE_TEXTURE_2D(_MainTex, i.uv + half2(destUv.x, destUv.y)).a;
		half spriteTopRight = ALLIN1_SAMPLE_TEXTURE_2D(_MainTex, i.uv + half2(-destUv.x, destUv.y)).a;
		half spriteBotLeft = ALLIN1_SAMPLE_TEXTURE_2D(_MainTex, i.uv + half2(destUv.x, -destUv.y)).a;
		half spriteBotRight = ALLIN1_SAMPLE_TEXTURE_2D(_MainTex, i.uv + half2(-destUv.x, -destUv.y)).a;
		result = result + spriteTopLeft + spriteTopRight + spriteBotLeft + spriteBotRight;
		#endif
					
		result = step(0.05, saturate(result));

		#if OUTTEX_ON
		i.uvOutTex.x += ((_Time.x + randomSeed) * _OutlineTexXSpeed) % 1;
		i.uvOutTex.y += ((_Time.x + randomSeed) * _OutlineTexYSpeed) % 1;
		#if ATLAS_ON
		i.uvOutTex = half2((i.uvOutTex.x - _MinXUV) / (_MaxXUV - _MinXUV), (i.uvOutTex.y - _MinYUV) / (_MaxYUV - _MinYUV));
		#endif
		half4 tempOutColor = ALLIN1_SAMPLE_TEXTURE_2D(_OutlineTex, i.uvOutTex);
		#if OUTGREYTEXTURE_ON
		luminance = 0.3 * tempOutColor.r + 0.59 * tempOutColor.g + 0.11 * tempOutColor.b;
		tempOutColor = half4(luminance, luminance, luminance, 1);
		#endif
		tempOutColor *= _OutlineColor;
		_OutlineColor = tempOutColor;
		#endif

		result *= (1 - originalAlpha) * _OutlineAlpha;

		half4 outline = _OutlineColor * i.color.a;
		outline.rgb *= _OutlineGlow;
		outline.a = result;
		#if ONLYOUTLINE_ON
		col = outline;
		#else
		col = lerp(col, outline, result);
		#endif
	#endif
	//-----------------------------------------------------------------------------

	#if FADE_ON
	half2 tiledUvFade1	= CUSTOM_TRANSFORM_TEX(i.uv, _FadeTex_ScaleAndTiling);
	half2 tiledUvFade2	= CUSTOM_TRANSFORM_TEX(i.uv, _FadeBurnTex_ScaleAndTiling);
	#if ATLAS_ON
	tiledUvFade1 = half2((tiledUvFade1.x - _MinXUV) / (_MaxXUV - _MinXUV), (tiledUvFade1.y - _MinYUV) / (_MaxYUV - _MinYUV));
	tiledUvFade2 = half2((tiledUvFade2.x - _MinXUV) / (_MaxXUV - _MinXUV), (tiledUvFade2.y - _MinYUV) / (_MaxYUV - _MinYUV));
	#endif
	half fadeTemp = ALLIN1_SAMPLE_TEXTURE_2D(_FadeTex, tiledUvFade1).r;
	half fade = smoothstep(_FadeAmount, _FadeAmount + _FadeBurnTransition, fadeTemp);
	half fadeBurn = saturate(smoothstep(_FadeAmount - _FadeBurnWidth, _FadeAmount - _FadeBurnWidth + 0.1, fadeTemp) * _FadeAmount);
	col.a *= fade;
	_FadeBurnColor.rgb *= _FadeBurnGlow;
	col += fadeBurn * ALLIN1_SAMPLE_TEXTURE_2D(_FadeBurnTex, tiledUvFade2) * _FadeBurnColor * originalAlpha * (1 - col.a);
	#endif

	#if HOLOGRAM_ON
	half totalHologram = _HologramStripesAmount + _HologramUnmodAmount;
	half hologramYCoord = ((uvRect.y + (((_Time.x + randomSeed) % 1) * _HologramStripesSpeed)) % totalHologram) / totalHologram;
	hologramYCoord = abs(hologramYCoord);
	half alpha = RemapFloat(saturate(hologramYCoord - (_HologramUnmodAmount/totalHologram)), 0.0, 1.0, _HologramMinAlpha, saturate(_HologramMaxAlpha));
	half hologramMask = max(sign((_HologramUnmodAmount/totalHologram) - hologramYCoord), 0.0);
	half4 hologramResult = col;
	hologramResult.a *= lerp(alpha, 1, hologramMask);
	hologramResult.rgb *= max(1, _HologramMaxAlpha * max(sign(hologramYCoord - (_HologramUnmodAmount/totalHologram)), 0.0));
	hologramMask = 1 - step(0.01,hologramMask);
	hologramResult.rgb += hologramMask * _HologramStripeColor * col.a;
	col = lerp(col, hologramResult, _HologramBlend);
	#endif

	#if FLICKER_ON
	col.a *= saturate(col.a * step(frac(0.05 + (_Time.w + randomSeed) * _FlickerFreq), 1 - _FlickerPercent) + _FlickerAlpha);
	#endif

	col.a *= _Alpha;

	#if ALPHACUTOFF_ON
	clip((1 - _AlphaCutoffValue) - (1 - col.a) - 0.01);
	#endif

	#if ALPHAROUND_ON
	col.a = step(_AlphaRoundThreshold, col.a);
	#endif

	#if ALPHAOUTLINE_ON
	half alphaOutlineRes = pow(1 - col.a, max(_AlphaOutlinePower, 0.0001)) * step(_AlphaOutlineMinAlpha, col.a) * _AlphaOutlineBlend;
	col.rgb = lerp(col.rgb, _AlphaOutlineColor.rgb * _AlphaOutlineGlow, alphaOutlineRes);
	col.a = lerp(col.a, 1, alphaOutlineRes > 1);
	#endif

	col *= _Color;
	originalAlpha = col.a;

	half4 normalSample = ALLIN1_SAMPLE_TEXTURE_2D(_NormalMap, i.uv);
            	
	#if BLUR_ON
	#if !BLURISHD_ON
	normalSample = BlurHD(i.uv, _NormalMap, sampler_NormalMap, _BlurIntensity, 1, 1);
	#else
	normalSample = Blur(i.uv, _NormalMap, sampler_NormalMap, _BlurIntensity);
	#endif
	#endif

	#if MOTIONBLUR_ON
	_MotionBlurAngle = _MotionBlurAngle * 3.1415926;
	#define rot(n) mul(n, half2x2(cos(_MotionBlurAngle), -sin(_MotionBlurAngle), sin(_MotionBlurAngle), cos(_MotionBlurAngle)))
	_MotionBlurDist = _MotionBlurDist * 0.005;
	normalSample.rgb += ALLIN1_SAMPLE_TEXTURE_2D(_NormalMap, i.uv + rot(half2(-_MotionBlurDist, -_MotionBlurDist)));
	normalSample.rgb += ALLIN1_SAMPLE_TEXTURE_2D(_NormalMap, i.uv + rot(half2(-_MotionBlurDist * 2, -_MotionBlurDist * 2)));
	normalSample.rgb += ALLIN1_SAMPLE_TEXTURE_2D(_NormalMap, i.uv + rot(half2(-_MotionBlurDist * 3, -_MotionBlurDist * 3)));
	normalSample.rgb += ALLIN1_SAMPLE_TEXTURE_2D(_NormalMap, i.uv + rot(half2(-_MotionBlurDist * 4, -_MotionBlurDist * 4)));
	normalSample.rgb += ALLIN1_SAMPLE_TEXTURE_2D(_NormalMap, i.uv + rot(half2(_MotionBlurDist, _MotionBlurDist)));
	normalSample.rgb += ALLIN1_SAMPLE_TEXTURE_2D(_NormalMap, i.uv + rot(half2(_MotionBlurDist * 2, _MotionBlurDist * 2)));
	normalSample.rgb += ALLIN1_SAMPLE_TEXTURE_2D(_NormalMap, i.uv + rot(half2(_MotionBlurDist * 3, _MotionBlurDist * 3)));
	normalSample.rgb += ALLIN1_SAMPLE_TEXTURE_2D(_NormalMap, i.uv + rot(half2(_MotionBlurDist * 4, _MotionBlurDist * 4)));
	normalSample.rgb = normalSample.rgb / 9;
	#endif

    half3 normalTS = UnpackNormal(normalSample);
    normalTS.xy *= _NormalStrength;

    return NormalsRenderingShared(col, normalTS, i.tangentWS.xyz, i.bitangentWS.xyz, i.normalWS.xyz);
}

#endif //ALLIN1SPRITESHADER_2DRENDERER_NORMALSRENDERINGFRAGMENTPASS