//////////////////////////////////////////////////////
// Shader Packager
// Copyright (c) Jason Booth
//////////////////////////////////////////////////////
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

#if UNITY_2019_3_OR_NEWER

// Installs custom defines for render pipelines (e.g., USING_HDRP) to enable conditional compilation
// across different pipelines. Unity doesn't provide these defines out of the box, so this helper
// simplifies cross-pipeline compatibility.

namespace AllIn1SpriteShader
{
	public static class RenderPipelineChecker
	{
		private const string HDRP_PACKAGE = "HDRenderPipelineAsset";
		private const string URP_PACKAGE = "UniversalRenderPipelineAsset";

		public static bool IsHDRP
		{
			get; private set;
		}
		public static bool IsURP
		{
			get; private set;
		}
		public static bool IsStandardRP
		{
			get; private set;
		}

		public static void RefreshData()
		{
			IsHDRP = DoesTypeExist(HDRP_PACKAGE);
			IsURP = DoesTypeExist(URP_PACKAGE);

			if (!(IsHDRP || IsURP))
			{
				IsStandardRP = true;
			}

		}

		public static bool DoesTypeExist(string className)
		{
			var foundType = (from assembly in AppDomain.CurrentDomain.GetAssemblies()
				from type in GetTypesSafe(assembly)
				where type.Name == className
				select type).FirstOrDefault();

			return foundType != null;
		}

		public static IEnumerable<Type> GetTypesSafe(System.Reflection.Assembly assembly)
		{
			Type[] types;

			try
			{
				types = assembly.GetTypes();
			}
			catch (ReflectionTypeLoadException e)
			{
				types = e.Types;
			}

			return types.Where(x => x != null);
		}
	}
}
#endif