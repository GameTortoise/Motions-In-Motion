using UnityEditor;
using UnityEngine;

namespace AllIn1SpriteShader
{
	public class MissingScriptsRemover : MonoBehaviour
	{
		private void Awake()
		{
#if UNITY_EDITOR
			GameObjectUtility.RemoveMonoBehavioursWithMissingScript(gameObject);
#endif
		}
	}
}