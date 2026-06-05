using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SceneSerializedFieldViewer.Editor
{
    public sealed partial class SceneSerializedFieldViewer : EditorWindow
    {
        private const string GraphFieldsSessionPrefix = "SceneSerializedFieldViewer.GraphFields.";
        private const string ClosedObjectsSessionKey = "SceneSerializedFieldViewer.ClosedObjects";
        private const string ClosedComponentsSessionKey = "SceneSerializedFieldViewer.ClosedComponents";
        private const string GraphFieldsPrefsPrefix = "SceneSerializedFieldViewer.PersistentGraphFields.";
        private const string GraphNodePositionsSessionPrefix = "SceneSerializedFieldViewer.GraphNodePositions.";
        private const string GraphNodePositionsPrefsPrefix = "SceneSerializedFieldViewer.PersistentGraphNodePositions.";
        private const string ManualGraphFieldsPrefsPrefix = "SceneSerializedFieldViewer.ManualGraphFields.";
        private const string ManualGraphNodePositionsPrefsPrefix = "SceneSerializedFieldViewer.ManualGraphNodePositions.";

        private readonly List<SceneObjectEntry> entries = new List<SceneObjectEntry>();
        private readonly Dictionary<string, GraphReference> availableGraphReferences = new Dictionary<string, GraphReference>();
        private readonly Dictionary<string, List<GraphReference>> graphReferencesBySourceGameObject = new Dictionary<string, List<GraphReference>>();
        private readonly List<string> graphFieldKeys = new List<string>();
        private readonly List<string> closedObjectPaths = new List<string>();
        private readonly List<string> closedComponentKeys = new List<string>();
        private readonly List<GraphNode> graphNodes = new List<GraphNode>();
        private readonly List<GraphEdge> graphEdges = new List<GraphEdge>();
        private readonly HashSet<string> selectedGraphNodeKeys = new HashSet<string>();

        private const float GraphColumnSpacing = 650f;
        private const float GraphNodeWidth = 320f;
        private const float GraphNodeBaseHeight = 58f;
        private const float GraphNodeVerticalSpacing = 52f;
        private const float GraphPortSpacing = 30f;
        private const float GraphTopPadding = 24f;
        private const float GraphHeaderHeight = 52f;
        private const float GraphPillHeight = 26f;
        private const float GraphGroupVerticalSpacing = 96f;
        private const float GraphMinZoom = 0.45f;
        private const float GraphMaxZoom = 1.75f;

        private Vector2 fieldScrollPosition;
        private Vector2 graphPanOffset;
        private Vector2 graphListScrollPosition;
        private readonly Dictionary<string, Vector2> graphNodePositions = new Dictionary<string, Vector2>();
        private float graphZoom = 1f;
        private float graphFieldPanelWidth = 360f;
        private string searchText = string.Empty;
        private string activeSceneSessionKey = string.Empty;
        private string draggedGraphNodeKey = string.Empty;
        private Vector2 lastGraphDragPosition;
        private Vector2 graphSelectionStart;
        private Vector2 graphSelectionEnd;
        private int selectedTab;
        private bool includeInactive;
        private bool useKorean = true;
        private bool isResizingGraphFieldPanel;
        private bool isDraggingGraphNode;
        private bool isSelectingGraphNodes;
        private bool didDragGraphNodes;
        private GUIStyle boldFoldoutStyle;

        [MenuItem("Tools/Scene Serialized Field Viewer")]
        [MenuItem("Tools/Serialized Field Dependencies")]
        private static void Open()
        {
            SceneSerializedFieldViewer window = GetWindow<SceneSerializedFieldViewer>();
            window.titleContent = new GUIContent("Scene Serialized Fields");
            window.minSize = new Vector2(760f, 460f);
            window.Refresh();
        }

        private void OnEnable()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            LoadFoldoutState();
            EnsureSceneSessionLoaded();
            Refresh();
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            SaveGraphFields();
            SaveGraphNodePositions();
        }

        private void OnHierarchyChange()
        {
            Refresh();
            Repaint();
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            SaveGraphFields();
            SaveGraphNodePositions();
            EditorApplication.delayCall += RefreshAfterPlayModeChange;
        }

        private void RefreshAfterPlayModeChange()
        {
            EnsureSceneSessionLoaded();
            LoadGraphFields();
            LoadGraphNodePositions();
            Refresh();
            Repaint();
        }

        private void OnGUI()
        {
            EnsureStyles();
            EnsureSceneSessionLoaded();

            DrawToolbar();
            selectedTab = GUILayout.Toolbar(selectedTab, TabLabels);
            EditorGUILayout.Space(4f);

            if (selectedTab == 0)
            {
                DrawSerializedFieldsPage();
            }
            else
            {
                DrawGraphPage();
            }
        }

        private void EnsureStyles()
        {
            if (boldFoldoutStyle != null)
            {
                return;
            }

            boldFoldoutStyle = new GUIStyle(EditorStyles.foldout)
            {
                fontStyle = FontStyle.Bold
            };
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Button(Text(Labels.RefreshKo, Labels.RefreshEn), EditorStyles.toolbarButton, GUILayout.Width(80f)))
            {
                Refresh();
            }

            EditorGUI.BeginChangeCheck();
            includeInactive = GUILayout.Toggle(includeInactive, Text(Labels.IncludeInactiveKo, Labels.IncludeInactiveEn), EditorStyles.toolbarButton, GUILayout.Width(useKorean ? 95f : 115f));
            useKorean = GUILayout.Toggle(useKorean, useKorean ? Labels.EnglishEn : Labels.KoreanKo, EditorStyles.toolbarButton, GUILayout.Width(70f));
            if (EditorGUI.EndChangeCheck())
            {
                Refresh();
            }

            GUILayout.Space(8f);
            GUILayout.Label(Text(Labels.SearchKo, Labels.SearchEn), GUILayout.Width(useKorean ? 35f : 45f));

            EditorGUI.BeginChangeCheck();
            searchText = GUILayout.TextField(searchText, EditorStyles.toolbarSearchField);
            if (EditorGUI.EndChangeCheck())
            {
                Refresh();
            }

            if (GUILayout.Button(Text(Labels.ClearKo, Labels.ClearEn), EditorStyles.toolbarButton, GUILayout.Width(55f)))
            {
                searchText = string.Empty;
                Refresh();
                GUI.FocusControl(null);
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawSerializedFieldsPage()
        {
            DrawSummary();

            fieldScrollPosition = EditorGUILayout.BeginScrollView(fieldScrollPosition);

            if (entries.Count == 0)
            {
                EditorGUILayout.HelpBox(Text(Labels.EmptyFieldsKo, Labels.EmptyFieldsEn), MessageType.Info);
            }

            for (int i = 0; i < entries.Count; i++)
            {
                DrawObjectEntry(entries[i]);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawSummary()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            int fieldCount = 0;
            int graphCandidateCount = 0;

            for (int i = 0; i < entries.Count; i++)
            {
                for (int c = 0; c < entries[i].Components.Count; c++)
                {
                    ComponentEntry componentEntry = entries[i].Components[c];
                    fieldCount += componentEntry.Fields.Count;
                    for (int f = 0; f < componentEntry.Fields.Count; f++)
                    {
                        if (componentEntry.Fields[f].CanAddToGraph)
                        {
                            graphCandidateCount++;
                        }
                    }
                }
            }

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(Text(Labels.ActiveSceneKo, Labels.ActiveSceneEn), activeScene.IsValid() ? activeScene.name : Text(Labels.NoneKo, Labels.NoneEn));
            EditorGUILayout.LabelField(Text(Labels.ObjectsKo, Labels.ObjectsEn), entries.Count.ToString());
            EditorGUILayout.LabelField(Text(Labels.VisibleFieldsKo, Labels.VisibleFieldsEn), fieldCount.ToString());
            EditorGUILayout.LabelField(Text(Labels.GraphableReferencesKo, Labels.GraphableReferencesEn), graphCandidateCount.ToString());
            EditorGUILayout.EndVertical();
        }

        private void DrawObjectEntry(SceneObjectEntry entry)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();

            bool isOpen = !closedObjectPaths.Contains(entry.Path);
            bool newOpen = EditorGUILayout.Foldout(isOpen, entry.Path, true, boldFoldoutStyle);
            if (newOpen != isOpen)
            {
                SetListState(closedObjectPaths, entry.Path, !newOpen);
                SaveFoldoutState();
            }

            if (GUILayout.Button(Text(Labels.SelectKo, Labels.SelectEn), GUILayout.Width(useKorean ? 55f : 60f)))
            {
                Selection.activeGameObject = entry.GameObject;
                EditorGUIUtility.PingObject(entry.GameObject);
            }

            EditorGUILayout.EndHorizontal();

            if (newOpen)
            {
                EditorGUI.indentLevel++;
                for (int i = 0; i < entry.Components.Count; i++)
                {
                    DrawComponentEntry(entry.Components[i]);
                }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawComponentEntry(ComponentEntry entry)
        {
            bool isOpen = !closedComponentKeys.Contains(entry.StateKey);
            bool newOpen = EditorGUILayout.Foldout(isOpen, entry.DisplayName, true);
            if (newOpen != isOpen)
            {
                SetListState(closedComponentKeys, entry.StateKey, !newOpen);
                SaveFoldoutState();
            }

            if (!newOpen)
            {
                return;
            }

            EditorGUI.indentLevel++;

            if (entry.Component == null)
            {
                EditorGUILayout.HelpBox(Text(Labels.MissingScriptKo, Labels.MissingScriptEn), MessageType.Warning);
                EditorGUI.indentLevel--;
                return;
            }

            SerializedObject serializedObject = new SerializedObject(entry.Component);
            serializedObject.Update();
            EditorGUI.BeginChangeCheck();

            SerializedProperty property = serializedObject.GetIterator();
            bool enterChildren = true;
            bool componentMatchesSearch = MatchesSearch(entry.ObjectPath) || MatchesSearch(entry.DisplayName);

            while (property.NextVisible(enterChildren))
            {
                enterChildren = false;

                if (property.propertyPath == "m_Script")
                {
                    continue;
                }

                FieldMeta fieldMeta;
                if (!entry.FieldsByPath.TryGetValue(property.propertyPath, out fieldMeta))
                {
                    continue;
                }

                if (!componentMatchesSearch && !FieldMatchesSearch(fieldMeta))
                {
                    continue;
                }

                DrawSerializedProperty(entry, fieldMeta, property);
            }

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(entry.Component, "Edit Serialized Field");
                if (serializedObject.ApplyModifiedProperties())
                {
                    MarkDirty(entry.Component);
                    Refresh();
                    GUIUtility.ExitGUI();
                }
            }

            EditorGUI.indentLevel--;
        }

        private void DrawSerializedProperty(ComponentEntry entry, FieldMeta fieldMeta, SerializedProperty property)
        {
            bool canAddToGraph = fieldMeta.CanAddToGraph && availableGraphReferences.ContainsKey(fieldMeta.FieldKey);
            bool isAlreadyAdded = graphFieldKeys.Contains(fieldMeta.FieldKey);

            if (!fieldMeta.CanAddToGraph)
            {
                EditorGUILayout.PropertyField(property, true);
                GUI.enabled = true;
                return;
            }

            float rowHeight = EditorGUI.GetPropertyHeight(property, true);
            Rect rowRect = EditorGUILayout.GetControlRect(true, rowHeight);
            Rect propertyRect = new Rect(rowRect.x, rowRect.y, rowRect.width - 24f, rowRect.height);
            Rect toggleRect = new Rect(rowRect.xMax - 18f, rowRect.y + Mathf.Max(0f, (EditorGUIUtility.singleLineHeight - 16f) * 0.5f), 16f, 16f);

            EditorGUI.PropertyField(propertyRect, property, true);

            if (isAlreadyAdded)
            {
                DrawOutline(rowRect, new Color(1f, 0.82f, 0.18f, 1f), 1f);
            }

            GUI.enabled = canAddToGraph;
            bool shouldAddToGraph = GUI.Toggle(toggleRect, isAlreadyAdded, GUIContent.none);
            GUI.enabled = true;
            if (shouldAddToGraph != isAlreadyAdded)
            {
                ToggleFieldInGraph(fieldMeta.FieldKey);
                GUIUtility.ExitGUI();
            }

            GUI.enabled = true;
        }

        private string[] TabLabels
        {
            get
            {
                return new[]
                {
                    Text(Labels.SerializedFieldsTabKo, Labels.SerializedFieldsTabEn),
                    Text(Labels.GraphTabKo, Labels.GraphTabEn)
                };
            }
        }

        private string Text(string korean, string english)
        {
            return useKorean ? korean : english;
        }


    }
}
