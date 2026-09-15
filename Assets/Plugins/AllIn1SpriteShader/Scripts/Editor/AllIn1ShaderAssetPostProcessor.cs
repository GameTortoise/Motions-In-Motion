#if UNITY_EDITOR
using UnityEditor;

namespace AllIn1SpriteShader
{
	public class AllIn1ShaderAssetPostProcessor : AssetPostprocessor
	{
		static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths, bool didDomainReload)
		{
			bool needToRefreshConfig = false;
			for (int i = 0; i < importedAssets.Length; i++)
			{
				if (importedAssets[i].EndsWith(Constants.MAIN_ASSEMBLY_NAME))
				{
					needToRefreshConfig = needToRefreshConfig || true;
				}
			}

			if (needToRefreshConfig)
			{
				AllIn1ShaderImporter.ForceReimport();
			}
		}
	}
}
#endif