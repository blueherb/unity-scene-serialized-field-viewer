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
        private void EnsureSceneSessionLoaded()
        {
            string sceneKey = GetActiveSceneSessionKey();
            if (sceneKey == activeSceneSessionKey)
            {
                return;
            }

            activeSceneSessionKey = sceneKey;
            LoadGraphFields();
            LoadGraphNodePositions();
        }

        private static string GetActiveSceneSessionKey()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid())
            {
                return "NoScene";
            }

            if (!string.IsNullOrEmpty(activeScene.path))
            {
                return activeScene.path;
            }

            return activeScene.name + ":" + activeScene.handle;
        }

        private void LoadGraphFields()
        {
            string savedValue = SessionState.GetString(GraphFieldsSessionPrefix + activeSceneSessionKey, string.Empty);
            if (string.IsNullOrEmpty(savedValue))
            {
                savedValue = EditorPrefs.GetString(GraphFieldsPrefsPrefix + activeSceneSessionKey, string.Empty);
            }

            DeserializeGraphFields(savedValue);
        }

        private void DeserializeGraphFields(string savedValue)
        {
            graphFieldKeys.Clear();
            string[] values = savedValue.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < values.Length; i++)
            {
                if (!graphFieldKeys.Contains(values[i]))
                {
                    graphFieldKeys.Add(values[i]);
                }
            }
        }

        private void SaveGraphFields()
        {
            string savedValue = SerializeGraphFields();
            SessionState.SetString(GraphFieldsSessionPrefix + activeSceneSessionKey, savedValue);
            EditorPrefs.SetString(GraphFieldsPrefsPrefix + activeSceneSessionKey, savedValue);
        }

        private string SerializeGraphFields()
        {
            return string.Join("\n", graphFieldKeys);
        }

        private void LoadGraphNodePositions()
        {
            string savedValue = SessionState.GetString(GraphNodePositionsSessionPrefix + activeSceneSessionKey, string.Empty);
            if (string.IsNullOrEmpty(savedValue))
            {
                savedValue = EditorPrefs.GetString(GraphNodePositionsPrefsPrefix + activeSceneSessionKey, string.Empty);
            }

            DeserializeGraphNodePositions(savedValue);
        }

        private void DeserializeGraphNodePositions(string savedValue)
        {
            graphNodePositions.Clear();
            string[] values = savedValue.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < values.Length; i++)
            {
                string[] parts = values[i].Split(new[] { '\t' }, 3);
                if (parts.Length != 3)
                {
                    continue;
                }

                float x;
                float y;
                if (float.TryParse(parts[1], out x) && float.TryParse(parts[2], out y))
                {
                    graphNodePositions[parts[0]] = new Vector2(x, y);
                }
            }
        }

        private void SaveGraphNodePositions()
        {
            string savedValue = SerializeGraphNodePositions();
            SessionState.SetString(GraphNodePositionsSessionPrefix + activeSceneSessionKey, savedValue);
            EditorPrefs.SetString(GraphNodePositionsPrefsPrefix + activeSceneSessionKey, savedValue);
        }

        private string SerializeGraphNodePositions()
        {
            List<string> values = new List<string>();
            foreach (KeyValuePair<string, Vector2> item in graphNodePositions)
            {
                values.Add(item.Key + "\t" + item.Value.x + "\t" + item.Value.y);
            }

            return string.Join("\n", values);
        }

        private void LoadFoldoutState()
        {
            closedObjectPaths.Clear();
            closedObjectPaths.AddRange(LoadSessionList(ClosedObjectsSessionKey));
            closedComponentKeys.Clear();
            closedComponentKeys.AddRange(LoadSessionList(ClosedComponentsSessionKey));
        }

        private void SaveFoldoutState()
        {
            SessionState.SetString(ClosedObjectsSessionKey, string.Join("\n", closedObjectPaths));
            SessionState.SetString(ClosedComponentsSessionKey, string.Join("\n", closedComponentKeys));
        }

        private static List<string> LoadSessionList(string key)
        {
            string savedValue = SessionState.GetString(key, string.Empty);
            return new List<string>(savedValue.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries));
        }


    }
}