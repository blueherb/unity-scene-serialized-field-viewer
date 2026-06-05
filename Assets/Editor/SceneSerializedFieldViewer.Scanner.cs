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
        private void Refresh()
        {
            EnsureSceneSessionLoaded();

            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid() || !activeScene.isLoaded)
            {
                return;
            }

            entries.Clear();
            availableGraphReferences.Clear();
            graphReferencesBySourceGameObject.Clear();

            GameObject[] roots = activeScene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                CollectGameObject(roots[i].transform, roots[i].name);
            }

            if (graphFieldKeys.Count == 0 || availableGraphReferences.Count > 0)
            {
                BuildGraph();
            }
        }

        private void CollectGameObject(Transform transform, string path)
        {
            GameObject gameObject = transform.gameObject;
            if (includeInactive || gameObject.activeInHierarchy)
            {
                List<ComponentEntry> componentEntries = CollectComponents(gameObject, path);
                if (componentEntries.Count > 0)
                {
                    entries.Add(new SceneObjectEntry(gameObject, path, componentEntries));
                }
            }

            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                CollectGameObject(child, path + "/" + child.name);
            }
        }

        private List<ComponentEntry> CollectComponents(GameObject gameObject, string objectPath)
        {
            List<ComponentEntry> componentEntries = new List<ComponentEntry>();
            Component[] components = gameObject.GetComponents<Component>();

            for (int i = 0; i < components.Length; i++)
            {
                Component component = components[i];
                if (component == null)
                {
                    continue;
                }

                MonoBehaviour behaviour = component as MonoBehaviour;
                if (behaviour == null)
                {
                    continue;
                }

                if (!IsProjectScript(behaviour))
                {
                    continue;
                }

                ComponentEntry entry = ComponentEntry.Valid(component, objectPath, i);
                CollectFields(entry);

                if (entry.Fields.Count == 0)
                {
                    continue;
                }

                if (!ComponentHasVisibleSearchMatch(entry))
                {
                    continue;
                }

                componentEntries.Add(entry);
            }

            return componentEntries;
        }

        private void CollectFields(ComponentEntry entry)
        {
            SerializedObject serializedObject = new SerializedObject(entry.Component);
            SerializedProperty property = serializedObject.GetIterator();
            bool enterChildren = true;

            while (property.NextVisible(enterChildren))
            {
                enterChildren = false;

                if (property.propertyPath == "m_Script")
                {
                    continue;
                }

                string fieldKey = GetFieldKey(entry, property.propertyPath);
                FieldMeta fieldMeta = BuildFieldMeta(entry, property, fieldKey);
                entry.Fields.Add(fieldMeta);
                entry.FieldsByPath[property.propertyPath] = fieldMeta;

                if (fieldMeta.CanAddToGraph)
                {
                    GraphReference graphReference = BuildGraphReference(entry, fieldMeta);
                    availableGraphReferences[fieldMeta.FieldKey] = graphReference;
                    AddGraphReferenceBySourceGameObject(graphReference);
                }
            }
        }

        private FieldMeta BuildFieldMeta(ComponentEntry entry, SerializedProperty property, string fieldKey)
        {
            UnityEngine.Object target = property.propertyType == SerializedPropertyType.ObjectReference
                ? property.objectReferenceValue
                : null;

            string targetPath = target != null ? GetTargetPath(target) : string.Empty;
            string displayLabel = entry.ObjectPath + " / " + entry.DisplayName + " / " + property.propertyPath;

            return new FieldMeta
            {
                FieldKey = fieldKey,
                FieldPath = property.propertyPath,
                DisplayName = property.displayName,
                DisplayLabel = displayLabel,
                Target = target,
                TargetPath = targetPath,
                CanAddToGraph = target != null && property.propertyType == SerializedPropertyType.ObjectReference
            };
        }

        private GraphReference BuildGraphReference(ComponentEntry entry, FieldMeta fieldMeta)
        {
            return new GraphReference
            {
                FieldKey = fieldMeta.FieldKey,
                SourceKey = entry.StateKey,
                SourceNodeKey = GetObjectKey(entry.Component),
                SourceObject = entry.Component,
                SourceIcon = GetObjectIcon(entry.Component),
                SourceDisplayName = entry.DisplayName,
                SourceGameObjectKey = GetObjectKey(entry.Component.gameObject),
                SourceLabel = entry.DisplayName + "\n" + entry.ObjectPath,
                FieldPath = fieldMeta.FieldPath,
                Target = fieldMeta.Target,
                TargetKey = GetObjectKey(fieldMeta.Target),
                TargetIcon = GetObjectIcon(fieldMeta.Target),
                TargetLabel = fieldMeta.Target.name + "\n" + ShortenPath(fieldMeta.TargetPath)
            };
        }

        private void AddGraphReferenceBySourceGameObject(GraphReference graphReference)
        {
            List<GraphReference> references;
            if (!graphReferencesBySourceGameObject.TryGetValue(graphReference.SourceGameObjectKey, out references))
            {
                references = new List<GraphReference>();
                graphReferencesBySourceGameObject.Add(graphReference.SourceGameObjectKey, references);
            }

            references.Add(graphReference);
        }

        private bool ComponentHasVisibleSearchMatch(ComponentEntry entry)
        {
            if (string.IsNullOrWhiteSpace(searchText)
                || MatchesSearch(entry.ObjectPath)
                || MatchesSearch(entry.DisplayName))
            {
                return true;
            }

            for (int i = 0; i < entry.Fields.Count; i++)
            {
                if (FieldMatchesSearch(entry.Fields[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private bool FieldMatchesSearch(FieldMeta fieldMeta)
        {
            if (string.IsNullOrWhiteSpace(searchText))
            {
                return true;
            }

            return MatchesSearch(fieldMeta.FieldPath)
                || MatchesSearch(fieldMeta.DisplayName)
                || MatchesSearch(fieldMeta.Target != null ? fieldMeta.Target.name : string.Empty)
                || MatchesSearch(fieldMeta.TargetPath);
        }

        private bool MatchesSearch(string value)
        {
            return !string.IsNullOrEmpty(value)
                && value.IndexOf(searchText.Trim(), StringComparison.OrdinalIgnoreCase) >= 0;
        }


    }
}