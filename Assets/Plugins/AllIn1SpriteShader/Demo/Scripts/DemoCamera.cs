using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace AllIn1SpriteShader
{
    public class DemoCamera : MonoBehaviour
    {
        [SerializeField] private Transform targetedItem = null;
        [SerializeField] private All1ShaderDemoController demoController = null;
        [SerializeField] private float speed = 0;
        private Vector3 offset;
        private Vector3 target;
        private bool canUpdate = false;

        private void Awake()
        {
            EnablePostProcessingForURP();
            offset = transform.position - targetedItem.position;
            StartCoroutine(SetCamAfterStart());
        }

        private void EnablePostProcessingForURP()
        {
            #if UNITY_2019_3_OR_NEWER
            try
            {
                Type urpCameraDataType = Type.GetType("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");
                if (urpCameraDataType == null)
                {
                    return;
                }

                Camera cam = GetComponent<Camera>();
                if (cam == null)
                {
                    Debug.LogWarning("[DemoCamera] No Camera component found on GameObject", this);
                    return;
                }

                Component urpCameraData = cam.GetComponent(urpCameraDataType);
                if (urpCameraData == null)
                {
                    urpCameraData = cam.gameObject.AddComponent(urpCameraDataType);
                    Debug.Log("[DemoCamera] Added UniversalAdditionalCameraData component for URP post processing", this);
                }

                PropertyInfo renderPostProcessingProperty = urpCameraDataType.GetProperty("renderPostProcessing");
                if (renderPostProcessingProperty != null && renderPostProcessingProperty.CanWrite)
                {
                    bool currentValue = (bool)renderPostProcessingProperty.GetValue(urpCameraData);
                    if (!currentValue)
                    {
                        renderPostProcessingProperty.SetValue(urpCameraData, true);
                        Debug.Log("[DemoCamera] Enabled URP post processing on camera", this);
                    }
                }
                else
                {
                    Debug.LogWarning("[DemoCamera] Could not find or access 'renderPostProcessing' property. URP version might not support this feature.", this);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[DemoCamera] Failed to enable URP post processing: {e.Message}", this);
            }
            #endif
        }

        private void Update()
        {
            if (!canUpdate) return;
            target.y = demoController.GetCurrExpositor() * demoController.expositorDistance;
            transform.position = Vector3.Lerp(transform.position, target, speed * Time.deltaTime);
        }

        private IEnumerator SetCamAfterStart()
        {
            yield return null;
            transform.position = targetedItem.position + offset;
            target = transform.position;
            canUpdate = true;
        }
    }
}