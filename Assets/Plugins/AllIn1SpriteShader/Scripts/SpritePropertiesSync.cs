using UnityEngine;
using UnityEngine.Rendering;

namespace AllIn1SpriteShader
{
	[ExecuteInEditMode]
	[RequireComponent(typeof(SpriteRenderer))]
	public class SpritePropertiesSync : MonoBehaviour
	{
		public enum CastShadowMode
		{
			[InspectorName("Off")] OFF,
			[InspectorName("One Sided")] ONE_SIDED,
			[InspectorName("Two Sided")] TWO_SIDED
		}

		private static int PROPID_SPRITE_FLIP = Shader.PropertyToID("_SpriteFlip");

		public SpriteRenderer spr;
		public bool isMaterialDrivenByAnimator;

		[SerializeField] private CastShadowMode shadowCastingMode = CastShadowMode.TWO_SIDED;

		private MaterialPropertyBlock matPropBlock;

		public void Start()
		{
			if(spr == null) spr = GetComponent<SpriteRenderer>();
			if(spr == null)
			{
				Debug.LogWarning($"Sprite Renderer is null in SpritePropertiesSync-{gameObject.name}", gameObject);
				enabled = false;
				return;
			}
			matPropBlock = new MaterialPropertyBlock();
			UpdateRendererShadowCastingMode();
		}

		private void LateUpdate()
		{
			if(spr == null || spr.sharedMaterial == null)
			{
				Debug.LogError($"Incorrect setup in SpritePropertiesSync-{gameObject.name}", gameObject);
				enabled = false;
				return;
			}
			
#if UNITY_EDITOR
			if (matPropBlock == null) matPropBlock = new MaterialPropertyBlock();
			UpdateRendererShadowCastingMode();
#endif

			UpdateMaterial();
		}

		private void UpdateRendererShadowCastingMode()
		{
			switch (shadowCastingMode)
			{
				case CastShadowMode.OFF:
					spr.shadowCastingMode = ShadowCastingMode.Off;
					break;
				case CastShadowMode.ONE_SIDED:
					spr.shadowCastingMode = ShadowCastingMode.On;
					break;
				case CastShadowMode.TWO_SIDED:
					spr.shadowCastingMode = ShadowCastingMode.TwoSided;
					break;
			}
		}

		private void UpdateMaterial()
		{
			spr.GetPropertyBlock(matPropBlock);

			float flipX = spr.flipX ? -1f : 1f;
			float flipY = spr.flipY ? -1f : 1f;
			Vector3 localScale = spr.transform.localScale;
			float scaleSign = Mathf.Sign(localScale.x * localScale.y * localScale.z * flipX * flipY);
			Vector4 vecFlip = new Vector4(flipX, flipY, flipX, flipY);
			if (Application.isPlaying && isMaterialDrivenByAnimator)
			{
				vecFlip.x = 1.0f;
				vecFlip.y = 1.0f;
			}
			vecFlip.z = scaleSign;

			matPropBlock.SetVector(PROPID_SPRITE_FLIP, vecFlip);
			spr.SetPropertyBlock(matPropBlock);
		}
	}
}