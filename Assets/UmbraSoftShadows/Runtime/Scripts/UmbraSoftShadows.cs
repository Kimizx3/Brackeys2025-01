using UnityEngine;

namespace Umbra {

    [ExecuteAlways]
    [HelpURL("https://kronnect.com/guides-category/umbra-soft-shadows")]
    public class UmbraSoftShadows : MonoBehaviour {

        [Tooltip("Currently used umbra profile with settings")]
        public UmbraProfile profile;
        public bool debugShadows;

        public static bool installed;
        public static bool isDeferred;

        private void OnEnable() {
            CheckProfile();
        }

        private void OnDisable()
        {
            UmbraRenderFeature.UnregisterUmbraLight(this);
        }

        void OnValidate() {
            CheckProfile();
        }

        private void Reset() {
            CheckProfile();
        }

        void CheckProfile() {
            if (profile == null) {
                profile = ScriptableObject.CreateInstance<UmbraProfile>();
                profile.name = "New Umbra Profile";
#if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(this);
#endif
            }
            UmbraRenderFeature.RegisterUmbraLight(this);
        }

    }

}