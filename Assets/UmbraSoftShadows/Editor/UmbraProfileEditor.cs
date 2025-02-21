using UnityEditor;
using UnityEngine;

namespace Umbra {

    [CustomEditor(typeof(UmbraProfile))]
    public class UmbraProfileEditor : Editor {

        SerializedProperty shadowSource;
        SerializedProperty enableContactHardening, contactStrength, contactStrengthKnee, normalsSource;
        SerializedProperty occludersCount, occludersSearchRadius, sampleCount, lightSize, distantSpread;
        SerializedProperty blurIterations, blurType, blurSpread, blurEdgeSharpness, posterization;
        SerializedProperty overlayShadows, overlayShadowsIntensity, overlayShadowsColor;
        SerializedProperty preserveEdges, blurEdgeTolerance;
        SerializedProperty blurDepthAttenStart, blurDepthAttenLength, blurGrazingAttenuation;
        SerializedProperty blendCascades, cascade1BlendingStrength, cascade2BlendingStrength, cascade3BlendingStrength;
        SerializedProperty cascade1Scale, cascade2Scale, cascade3Scale, cascade4Scale;
        SerializedProperty loopStepOptimization, frameSkipOptimization, skipFrameMaxCameraDisplacement, skipFrameMaxCameraRotation, downsample;
        SerializedProperty style, maskTexture, maskScale;
        SerializedProperty contactShadows, contactShadowsInjectionPoint, contactShadowsSampleCount, contactShadowsStepping, contactShadowsThicknessNear, contactShadowsThicknessDistanceMultiplier, contactShadowsJitter;
        SerializedProperty contactShadowsIntensityMultiplier, contactShadowsDistanceFade, contactShadowsStartDistance, contactShadowsStartDistanceFade, contactShadowsNormalBias, contactShadowsVignetteSize;
        SerializedProperty transparentReceiverPlane, receiverPlaneAltitude;

        static GUIStyle titleLabelStyle;
        static Color titleColor;

        private void OnEnable () {
            if (target == null) return;
            titleColor = EditorGUIUtility.isProSkin ? new Color(0.52f, 0.66f, 0.9f) : new Color(0.12f, 0.16f, 0.4f);

            shadowSource = serializedObject.FindProperty("shadowSource");
            sampleCount = serializedObject.FindProperty("sampleCount");
            enableContactHardening = serializedObject.FindProperty("enableContactHardening");
            contactStrength = serializedObject.FindProperty("contactStrength");
            contactStrengthKnee = serializedObject.FindProperty("contactStrengthKnee");
            distantSpread = serializedObject.FindProperty("distantSpread");
            occludersCount = serializedObject.FindProperty("occludersCount");
            occludersSearchRadius = serializedObject.FindProperty("occludersSearchRadius");
            lightSize = serializedObject.FindProperty("lightSize");
            blurIterations = serializedObject.FindProperty("blurIterations");
            blurType = serializedObject.FindProperty("blurType");
            blurSpread = serializedObject.FindProperty("blurSpread");
            blurEdgeSharpness = serializedObject.FindProperty("blurEdgeSharpness");
            posterization = serializedObject.FindProperty("posterization");
            preserveEdges = serializedObject.FindProperty("preserveEdges");
            blurEdgeTolerance = serializedObject.FindProperty("blurEdgeTolerance");
            blurDepthAttenStart = serializedObject.FindProperty("blurDepthAttenStart");
            blurDepthAttenLength = serializedObject.FindProperty("blurDepthAttenLength");
            blurGrazingAttenuation = serializedObject.FindProperty("blurGrazingAttenuation");
            overlayShadows = serializedObject.FindProperty("overlayShadows");
            overlayShadowsIntensity = serializedObject.FindProperty("overlayShadowsIntensity");
            overlayShadowsColor = serializedObject.FindProperty("overlayShadowsColor");
            normalsSource = serializedObject.FindProperty("normalsSource");
            blendCascades = serializedObject.FindProperty("blendCascades");
            cascade1BlendingStrength = serializedObject.FindProperty("cascade1BlendingStrength");
            cascade2BlendingStrength = serializedObject.FindProperty("cascade2BlendingStrength");
            cascade3BlendingStrength = serializedObject.FindProperty("cascade3BlendingStrength");
            cascade1Scale = serializedObject.FindProperty("cascade1Scale");
            cascade2Scale = serializedObject.FindProperty("cascade2Scale");
            cascade3Scale = serializedObject.FindProperty("cascade3Scale");
            cascade4Scale = serializedObject.FindProperty("cascade4Scale");
            loopStepOptimization = serializedObject.FindProperty("loopStepOptimization");
            frameSkipOptimization = serializedObject.FindProperty("frameSkipOptimization");
            skipFrameMaxCameraDisplacement = serializedObject.FindProperty("skipFrameMaxCameraDisplacement");
            skipFrameMaxCameraRotation = serializedObject.FindProperty("skipFrameMaxCameraRotation");
            downsample = serializedObject.FindProperty("downsample");
            style = serializedObject.FindProperty("style");
            maskTexture = serializedObject.FindProperty("maskTexture");
            maskScale = serializedObject.FindProperty("maskScale");
            contactShadows = serializedObject.FindProperty("contactShadows");
            contactShadowsInjectionPoint = serializedObject.FindProperty("contactShadowsInjectionPoint");
            contactShadowsSampleCount = serializedObject.FindProperty("contactShadowsSampleCount");
            contactShadowsStepping = serializedObject.FindProperty("contactShadowsStepping");
            contactShadowsThicknessNear = serializedObject.FindProperty("contactShadowsThicknessNear");
            contactShadowsThicknessDistanceMultiplier = serializedObject.FindProperty("contactShadowsThicknessDistanceMultiplier");
            contactShadowsJitter = serializedObject.FindProperty("contactShadowsJitter");
            contactShadowsDistanceFade = serializedObject.FindProperty("contactShadowsDistanceFade");
            contactShadowsStartDistance = serializedObject.FindProperty("contactShadowsStartDistance");
            contactShadowsStartDistanceFade = serializedObject.FindProperty("contactShadowsStartDistanceFade");
            contactShadowsNormalBias = serializedObject.FindProperty("contactShadowsNormalBias");
            contactShadowsVignetteSize = serializedObject.FindProperty("contactShadowsVignetteSize");
            contactShadowsIntensityMultiplier = serializedObject.FindProperty("contactShadowsIntensityMultiplier");
            transparentReceiverPlane = serializedObject.FindProperty("transparentReceiverPlane");
            receiverPlaneAltitude = serializedObject.FindProperty("receiverPlaneAltitude");
        }

        public override void OnInspectorGUI () {

            serializedObject.Update();

            DrawSectionTitle("General Settings");
            EditorGUILayout.PropertyField(shadowSource);

            if (shadowSource.intValue == (int)ShadowSource.UmbraShadows) {
                EditorGUILayout.PropertyField(sampleCount);
                EditorGUILayout.PropertyField(lightSize);
                EditorGUILayout.PropertyField(enableContactHardening, new GUIContent("Contact Hardening"));
                if (enableContactHardening.boolValue) {
                    EditorGUI.indentLevel++;
#if UNITY_WEBGL
                EditorGUILayout.HelpBox("Contact hardening not suppoted on WebGL.", MessageType.Warning);
                GUI.enabled = false;
#endif

                    EditorGUILayout.PropertyField(contactStrength);
                    if (contactStrength.floatValue > 0) {
                        EditorGUI.indentLevel++;
                        EditorGUILayout.PropertyField(contactStrengthKnee, new GUIContent("Knee"));
                        EditorGUI.indentLevel--;
                    }
                    EditorGUILayout.PropertyField(distantSpread);
                    EditorGUILayout.PropertyField(occludersCount);
                    if (occludersCount.intValue > 0) {
                        EditorGUI.indentLevel++;
                        EditorGUILayout.PropertyField(occludersSearchRadius, new GUIContent("Search Radius"));
                        EditorGUI.indentLevel--;
                    }
                    GUI.enabled = true;
                    EditorGUI.indentLevel--;
                }
                if (UmbraSoftShadows.isDeferred) {
                    GUI.enabled = false;
                    EditorGUILayout.LabelField("Normals Source", "Gbuffer Normals");
                    GUI.enabled = true;
                }
                else {
                    EditorGUILayout.PropertyField(normalsSource);
                }
            }

            EditorGUILayout.PropertyField(contactShadows);
            if (contactShadows.boolValue) {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(contactShadowsIntensityMultiplier, new GUIContent("Intensity"));
                EditorGUILayout.PropertyField(contactShadowsDistanceFade, new GUIContent("Distance Fade"));
                EditorGUILayout.PropertyField(contactShadowsSampleCount, new GUIContent("Sample Count"));
                EditorGUILayout.PropertyField(contactShadowsStepping, new GUIContent("Stepping"));
                EditorGUILayout.PropertyField(contactShadowsThicknessNear, new GUIContent("Thickness Near"));
                EditorGUILayout.PropertyField(contactShadowsThicknessDistanceMultiplier, new GUIContent("Thickness Distance Multiplier"));
                EditorGUILayout.PropertyField(contactShadowsJitter, new GUIContent("Jitter"));
                EditorGUILayout.PropertyField(contactShadowsStartDistance, new GUIContent("Start Distance"));
                EditorGUILayout.PropertyField(contactShadowsStartDistanceFade, new GUIContent("Start Distance Fade"));
                EditorGUILayout.PropertyField(contactShadowsNormalBias, new GUIContent("Normal Bias"));
                EditorGUILayout.PropertyField(contactShadowsVignetteSize, new GUIContent("Vignette Size"));
                if (shadowSource.intValue == (int)ShadowSource.UmbraShadows) {
                    EditorGUILayout.PropertyField(contactShadowsInjectionPoint, new GUIContent("Injection Point"));
                    if (contactShadowsInjectionPoint.intValue == (int)ContactShadowsInjectionPoint.AfterOpaque) {
                        EditorGUILayout.HelpBox("Computes contact shadows and blend result over the opaque texture. This option allows contact shadows over large distances (contact shadows integrated into shadow texture are limited by the shadow distance).", MessageType.Info);
                    }
                }
                else {
                    GUI.enabled = false;
                    EditorGUILayout.LabelField("Injection Point", "After Opaque");
                    GUI.enabled = true;
                }
                EditorGUI.indentLevel--;
            }

            if (shadowSource.intValue == (int)ShadowSource.UmbraShadows) {
                DrawSectionTitle("Look");
                EditorGUILayout.PropertyField(style);
                switch (style.intValue) {
                    case (int)Style.Textured:
                        EditorGUILayout.PropertyField(maskTexture, new GUIContent("Mask Texture"));
                        EditorGUILayout.PropertyField(maskScale, new GUIContent("Scale"));
                        break;
                    case (int)Style.Default:
                        EditorGUILayout.PropertyField(blurIterations);
                        if (blurIterations.intValue > 0) {
                            EditorGUILayout.PropertyField(blurType);
                            EditorGUILayout.PropertyField(blurSpread, new GUIContent("Spread"));
                            EditorGUILayout.PropertyField(blurEdgeTolerance, new GUIContent("Edge Tolerance"));
                            EditorGUILayout.PropertyField(blurEdgeSharpness, new GUIContent("Edge Sharpness"));
                            EditorGUILayout.PropertyField(posterization);
                            if (!transparentReceiverPlane.boolValue) {
                                EditorGUILayout.PropertyField(blurDepthAttenStart, new GUIContent("Blur Distance Attenuation", "Distance where blur starts reducing"));
                                EditorGUI.indentLevel++;
                                EditorGUILayout.PropertyField(blurDepthAttenLength, new GUIContent("Attenuation Length", "Length of the blur attenuation"));
                                EditorGUI.indentLevel--;
                                EditorGUILayout.PropertyField(blurGrazingAttenuation, new GUIContent("Grazing Angle Attenuation"));
                            }
                        }
                        break;
                }
                EditorGUILayout.PropertyField(overlayShadows);
                if (overlayShadows.boolValue) {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(overlayShadowsIntensity, new GUIContent("Intensity"));
                    EditorGUILayout.PropertyField(overlayShadowsColor, new GUIContent("Color"));
                    EditorGUI.indentLevel--;
                }
            }

            DrawSectionTitle("Advanced");
            EditorGUILayout.PropertyField(downsample);
            if (downsample.boolValue) {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(preserveEdges, new GUIContent("Edge Preserve"));
                EditorGUI.indentLevel--;
            }
            if (shadowSource.intValue == (int)ShadowSource.UmbraShadows) {
                EditorGUILayout.PropertyField(loopStepOptimization, new GUIContent("Loop Optimization"));
            }
            EditorGUILayout.PropertyField(frameSkipOptimization, new GUIContent("Frame Skip Optimization"));
            if (frameSkipOptimization.boolValue) {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(skipFrameMaxCameraDisplacement, new GUIContent("Max Camera Displacement"));
                EditorGUILayout.PropertyField(skipFrameMaxCameraRotation, new GUIContent("Max Camera Rotation"));
                EditorGUI.indentLevel--;
            }
            if (shadowSource.intValue == (int)ShadowSource.UmbraShadows) {
                EditorGUILayout.PropertyField(blendCascades);
                if (blendCascades.boolValue) {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(cascade1BlendingStrength, new GUIContent("Cascade 1 Blending"));
                    EditorGUILayout.PropertyField(cascade2BlendingStrength, new GUIContent("Cascade 2 Blending"));
                    EditorGUILayout.PropertyField(cascade3BlendingStrength, new GUIContent("Cascade 3 Blending"));
                    if (cascade1BlendingStrength.floatValue > 5f || cascade2BlendingStrength.floatValue > 5f || cascade3BlendingStrength.floatValue > 5f) {
                        EditorGUILayout.HelpBox("Setting a high value in blending can introduce artifacts between the cascades. Reduce this value if this happens.", MessageType.Info);
                    }
                    EditorGUI.indentLevel--;
                }
                EditorGUILayout.PropertyField(cascade1Scale);
                EditorGUILayout.PropertyField(cascade2Scale);
                EditorGUILayout.PropertyField(cascade3Scale);
                EditorGUILayout.PropertyField(cascade4Scale);
            }
            EditorGUILayout.PropertyField(transparentReceiverPlane);
            if (transparentReceiverPlane.boolValue) {
                EditorGUI.indentLevel++;
                if (contactShadows.boolValue) {
                    EditorGUILayout.HelpBox("Contact Shadows are not compatible with the transparent receiver plane option.", MessageType.Warning);
                }
                EditorGUILayout.PropertyField(receiverPlaneAltitude, new GUIContent("Altitude"));
                EditorGUI.indentLevel--;
            }
            serializedObject.ApplyModifiedProperties();
        }

        void DrawSectionTitle (string s) {
            if (titleLabelStyle == null) {
                GUIStyle skurikenModuleTitleStyle = "ShurikenModuleTitle";
                titleLabelStyle = new GUIStyle(skurikenModuleTitleStyle);
                titleLabelStyle.contentOffset = new Vector2(5f, -2f);
                titleLabelStyle.normal.textColor = titleColor;
                titleLabelStyle.fixedHeight = 22;
                titleLabelStyle.fontStyle = FontStyle.Bold;
            }

            EditorGUILayout.Separator();
            GUILayout.Label(s, titleLabelStyle);
        }

    }

}