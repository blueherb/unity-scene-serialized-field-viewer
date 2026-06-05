using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SceneSerializedFieldViewer.Editor
{
    public sealed partial class SceneSerializedFieldViewer
    {
        private static void SetListState(List<string> list, string value, bool shouldContain)
        {
            if (shouldContain)
            {
                if (!list.Contains(value))
                {
                    list.Add(value);
                }

                return;
            }

            list.Remove(value);
        }

        private static string GetFieldKey(ComponentEntry entry, string propertyPath)
        {
            return entry.StateKey + "|" + propertyPath;
        }

        private static string GetObjectKey(UnityEngine.Object target)
        {
            if (target == null)
            {
                return "null";
            }

            string assetPath = AssetDatabase.GetAssetPath(target);
            if (!string.IsNullOrEmpty(assetPath))
            {
                return "asset:" + assetPath;
            }

            try
            {
                return "global:" + GlobalObjectId.GetGlobalObjectIdSlow(target).ToString();
            }
            catch (Exception)
            {
                return "instance:" + target.GetInstanceID();
            }
        }

        private static string GetTargetPath(UnityEngine.Object target)
        {
            string assetPath = AssetDatabase.GetAssetPath(target);
            if (!string.IsNullOrEmpty(assetPath))
            {
                return assetPath;
            }

            Component component = target as Component;
            if (component != null)
            {
                return GetHierarchyPath(component.transform) + " / " + component.GetType().Name;
            }

            GameObject gameObject = target as GameObject;
            if (gameObject != null)
            {
                return GetHierarchyPath(gameObject.transform);
            }

            return string.Empty;
        }

        private static GameObject GetGraphExpansionGameObject(UnityEngine.Object target)
        {
            GameObject gameObject = target as GameObject;
            if (gameObject != null && gameObject.scene.IsValid())
            {
                return gameObject;
            }

            Component component = target as Component;
            if (component != null && component.gameObject.scene.IsValid())
            {
                return component.gameObject;
            }

            return null;
        }

        private static Texture GetObjectIcon(UnityEngine.Object target)
        {
            if (target == null)
            {
                return EditorGUIUtility.IconContent("console.warnicon").image;
            }

            GUIContent objectContent = EditorGUIUtility.ObjectContent(target, target.GetType());
            if (objectContent != null && objectContent.image != null)
            {
                return objectContent.image;
            }

            Texture thumbnail = AssetPreview.GetMiniThumbnail(target);
            if (thumbnail != null)
            {
                return thumbnail;
            }

            MonoBehaviour monoBehaviour = target as MonoBehaviour;
            if (monoBehaviour != null)
            {
                MonoScript script = MonoScript.FromMonoBehaviour(monoBehaviour);
                if (script != null)
                {
                    return AssetPreview.GetMiniThumbnail(script);
                }
            }

            return EditorGUIUtility.IconContent("GameObject Icon").image;
        }

        private static string GetHierarchyPath(Transform transform)
        {
            List<string> names = new List<string>();
            Transform current = transform;

            while (current != null)
            {
                names.Add(current.name);
                current = current.parent;
            }

            names.Reverse();
            return string.Join("/", names);
        }

        private static bool IsProjectScript(MonoBehaviour behaviour)
        {
            MonoScript script = MonoScript.FromMonoBehaviour(behaviour);
            if (script == null)
            {
                return false;
            }

            string path = AssetDatabase.GetAssetPath(script);
            return !string.IsNullOrEmpty(path) && path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase);
        }

        private static void MarkDirty(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            EditorUtility.SetDirty(target);

            Component component = target as Component;
            if (component != null && component.gameObject.scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(component.gameObject.scene);
            }
        }

        private static string ShortenPath(string path)
        {
            if (string.IsNullOrEmpty(path) || path.Length <= 36)
            {
                return path;
            }

            return "..." + path.Substring(path.Length - 33);
        }

        private static Rect Offset(Rect rect, Vector2 offset)
        {
            return new Rect(rect.x + offset.x, rect.y + offset.y, rect.width, rect.height);
        }


    }
}