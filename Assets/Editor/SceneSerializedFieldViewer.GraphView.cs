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
        private void DrawGraphPage()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label(Text(Labels.AddedLinksKo, Labels.AddedLinksEn) + ": " + graphEdges.Count, GUILayout.Width(useKorean ? 90f : 120f));

            if (GUILayout.Button(Text(Labels.ResetViewKo, Labels.ResetViewEn), EditorStyles.toolbarButton, GUILayout.Width(useKorean ? 90f : 80f)))
            {
                graphPanOffset = Vector2.zero;
                graphZoom = 1f;
                Repaint();
            }

            GUI.enabled = graphFieldKeys.Count > 0;
            if (GUILayout.Button(Text("자동 정리", "Auto Arrange"), EditorStyles.toolbarButton, GUILayout.Width(useKorean ? 80f : 95f)))
            {
                AutoArrangeGraph();
                GUIUtility.ExitGUI();
            }

            if (GUILayout.Button(Text("수동 저장", "Save State"), EditorStyles.toolbarButton, GUILayout.Width(useKorean ? 80f : 90f)))
            {
                ManualSaveGraphState();
            }

            GUI.enabled = HasManualGraphState();
            if (GUILayout.Button(Text("불러오기", "Load State"), EditorStyles.toolbarButton, GUILayout.Width(useKorean ? 75f : 90f)))
            {
                ManualLoadGraphState();
                GUIUtility.ExitGUI();
            }

            GUI.enabled = graphFieldKeys.Count > 0;
            if (GUILayout.Button(Text(Labels.ClearGraphKo, Labels.ClearGraphEn), EditorStyles.toolbarButton, GUILayout.Width(useKorean ? 95f : 90f)))
            {
                graphFieldKeys.Clear();
                graphNodePositions.Clear();
                SaveGraphFields();
                SaveGraphNodePositions();
                BuildGraph();
                Repaint();
            }

            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal(GUILayout.ExpandHeight(true));

            graphFieldPanelWidth = Mathf.Clamp(graphFieldPanelWidth, 260f, Mathf.Max(260f, position.width - 380f));
            EditorGUILayout.BeginVertical(GUILayout.Width(graphFieldPanelWidth), GUILayout.ExpandHeight(true));
            DrawSerializedFieldsPage();
            EditorGUILayout.EndVertical();

            DrawGraphFieldPanelSplitter();

            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            Rect graphRect = GUILayoutUtility.GetRect(240f, 100000f, 220f, 100000f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            DrawGraph(graphRect);
            DrawGraphEdgeList();
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawGraphFieldPanelSplitter()
        {
            Rect splitterRect = GUILayoutUtility.GetRect(6f, 6f, GUILayout.ExpandHeight(true));
            EditorGUI.DrawRect(splitterRect, new Color(0.12f, 0.12f, 0.12f, 1f));
            EditorGUIUtility.AddCursorRect(splitterRect, MouseCursor.ResizeHorizontal);

            Event currentEvent = Event.current;
            if (currentEvent.type == EventType.MouseDown && splitterRect.Contains(currentEvent.mousePosition))
            {
                isResizingGraphFieldPanel = true;
                currentEvent.Use();
            }

            if (isResizingGraphFieldPanel && currentEvent.type == EventType.MouseDrag)
            {
                graphFieldPanelWidth = Mathf.Clamp(graphFieldPanelWidth + currentEvent.delta.x, 260f, Mathf.Max(260f, position.width - 380f));
                currentEvent.Use();
                Repaint();
            }

            if (currentEvent.type == EventType.MouseUp)
            {
                isResizingGraphFieldPanel = false;
            }
        }

        private void DrawGraphEdgeList()
        {
            graphListScrollPosition = EditorGUILayout.BeginScrollView(graphListScrollPosition, GUILayout.Height(120f));

            if (graphEdges.Count == 0)
            {
                EditorGUILayout.LabelField(Text(Labels.EmptyGraphKo, Labels.EmptyGraphEn));
            }

            List<string> removedKeys = new List<string>();
            for (int i = 0; i < graphEdges.Count; i++)
            {
                GraphEdge edge = graphEdges[i];
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(edge.Source.Label.Replace("\n", " / ") + " -> " + edge.Target.Label.Replace("\n", " / ") + " : " + edge.Label);
                if (edge.CanRemove)
                {
                    if (GUILayout.Button(Text(Labels.RemoveKo, Labels.RemoveEn), GUILayout.Width(useKorean ? 55f : 70f)))
                    {
                        removedKeys.Add(edge.FieldKey);
                    }
                }
                else
                {
                    GUILayout.Label(Text(Labels.AutoKo, Labels.AutoEn), EditorStyles.miniLabel, GUILayout.Width(useKorean ? 40f : 45f));
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();

            if (removedKeys.Count == 0)
            {
                return;
            }

            for (int i = 0; i < removedKeys.Count; i++)
            {
                graphFieldKeys.Remove(removedKeys[i]);
            }

            SaveGraphFields();
            BuildGraph();
            RemoveUnusedStoredNodePositions();
            SaveGraphNodePositions();
            GUIUtility.ExitGUI();
        }

        private void RemoveUnusedStoredNodePositions()
        {
            HashSet<string> liveNodeKeys = new HashSet<string>();
            for (int i = 0; i < graphNodes.Count; i++)
            {
                liveNodeKeys.Add(graphNodes[i].Key);
            }

            List<string> removedKeys = new List<string>();
            foreach (KeyValuePair<string, Vector2> item in graphNodePositions)
            {
                if (!liveNodeKeys.Contains(item.Key))
                {
                    removedKeys.Add(item.Key);
                }
            }

            for (int i = 0; i < removedKeys.Count; i++)
            {
                graphNodePositions.Remove(removedKeys[i]);
            }
        }

        private void DrawGraph(Rect rect)
        {
            GUI.Box(rect, GUIContent.none);
            Rect viewportRect = new Rect(rect.x + 8f, rect.y + 8f, rect.width - 16f, rect.height - 16f);
            HandleGraphPan(viewportRect);

            GUI.BeginGroup(viewportRect);
            Rect localViewport = new Rect(0f, 0f, viewportRect.width, viewportRect.height);
            EditorGUI.DrawRect(localViewport, new Color(0.16f, 0.16f, 0.16f, 1f));
            GUI.Label(new Rect(12f, 8f, 160f, 20f), Mathf.RoundToInt(graphZoom * 100f) + "%", EditorStyles.whiteMiniLabel);

            if (graphEdges.Count == 0)
            {
                GUI.Label(new Rect(12f, 12f, localViewport.width - 24f, 24f), Text(Labels.EmptyGraphKo, Labels.EmptyGraphEn), EditorStyles.whiteLabel);
                GUI.EndGroup();
                return;
            }

            GUI.BeginClip(localViewport);
            HandleGraphNodeDrag();

            Handles.BeginGUI();
            for (int i = 0; i < graphEdges.Count; i++)
            {
                GraphEdge edge = graphEdges[i];
                Rect sourceRect = edge.Source.Rect;
                Rect targetRect = edge.Target.Rect;
                Vector2 from = GraphToViewPoint(new Vector2(sourceRect.xMax, GetSourcePortY(sourceRect, edge.SourcePortIndex)));
                Vector2 to = GraphToViewPoint(new Vector2(targetRect.xMin, GetTargetPortY(targetRect, edge.TargetPortIndex, edge.TargetPortCount)));
                if (!EdgeIntersectsViewport(from, to, localViewport))
                {
                    continue;
                }

                Color transitionColor = GetTransitionColor(edge);
                float tangent = Mathf.Max(90f, Mathf.Abs(to.x - from.x) * 0.45f);
                Handles.DrawBezier(
                    from,
                    to,
                    from + (Vector2.right * tangent),
                    to + (Vector2.left * tangent),
                    transitionColor,
                    null,
                    edge.CanRemove ? 3f : 2.2f);

                Handles.color = transitionColor;
                Handles.DrawSolidDisc(from, Vector3.forward, 4f);
                Handles.DrawSolidDisc(to, Vector3.forward, 4f);
            }

            Handles.EndGUI();

            for (int i = 0; i < graphNodes.Count; i++)
            {
                GraphNode node = graphNodes[i];
                Rect nodeRect = GraphToViewRect(node.Rect);
                if (!RectOverlapsPadded(nodeRect, localViewport, 24f))
                {
                    continue;
                }

                GUIStyle style = node.IsSource ? EditorStyles.helpBox : EditorStyles.objectField;
                DrawGraphNode(node, nodeRect, style);

                if (nodeRect.Contains(Event.current.mousePosition)
                    && Event.current.type == EventType.MouseDown
                    && Event.current.button == 0
                    && node.Object != null)
                {
                    Selection.activeObject = node.Object;
                    EditorGUIUtility.PingObject(node.Object);
                    Event.current.Use();
                }
            }

            DrawGraphSelectionRect();

            GUI.EndClip();
            GUI.EndGroup();
        }

        private static float GetSourcePortY(Rect nodeRect, int portIndex)
        {
            return nodeRect.y + GraphHeaderHeight + 7f + (portIndex * GraphPortSpacing) + (GraphPillHeight * 0.5f);
        }

        private static float GetTargetPortY(Rect nodeRect, int portIndex, int portCount)
        {
            if (portCount <= 1)
            {
                return nodeRect.center.y;
            }

            float usableHeight = Mathf.Max(1f, nodeRect.height - 22f);
            float step = usableHeight / (portCount - 1);
            return nodeRect.y + 11f + (step * portIndex);
        }

        private void DrawGraphNode(GraphNode node, Rect nodeRect, GUIStyle style)
        {
            DrawGraphNodeContent(node, nodeRect, style, graphZoom);
        }

        private static void DrawGraphNodeContent(GraphNode node, Rect nodeRect, GUIStyle style, float scale)
        {
            float headerHeight = GraphHeaderHeight * scale;
            Color bodyColor = node.IsSource ? new Color(0.19f, 0.22f, 0.2f, 1f) : new Color(0.18f, 0.18f, 0.18f, 1f);
            Color headerColor = node.IsSource ? new Color(0.22f, 0.29f, 0.23f, 1f) : new Color(0.22f, 0.22f, 0.22f, 1f);
            EditorGUI.DrawRect(nodeRect, bodyColor);
            EditorGUI.DrawRect(new Rect(nodeRect.x, nodeRect.y, nodeRect.width, headerHeight), headerColor);
            DrawOutline(nodeRect, new Color(0.07f, 0.07f, 0.07f, 1f), 1f);

            Rect iconRect = new Rect(nodeRect.x + (9f * scale), nodeRect.y + (7f * scale), 20f * scale, 20f * scale);
            if (node.Icon != null)
            {
                GUI.DrawTexture(iconRect, node.Icon, ScaleMode.ScaleToFit);
            }

            DrawGraphNodeHeader(node, nodeRect, scale);
            DrawGraphNodeOutputPills(node, nodeRect, scale);

            if (node.IsSelected)
            {
                DrawOutline(nodeRect, new Color(0.35f, 0.72f, 1f, 1f), 2f);
            }

            if (node.IsSelectedSource)
            {
                DrawOutline(nodeRect, new Color(1f, 0.82f, 0.18f, 1f), 3f);
            }
        }

        private static void DrawGraphNodeHeader(GraphNode node, Rect nodeRect, float scale)
        {
            string title = node.Label;
            string path = string.Empty;
            int lineBreakIndex = node.Label.IndexOf('\n');
            if (lineBreakIndex >= 0)
            {
                title = node.Label.Substring(0, lineBreakIndex);
                path = node.Label.Substring(lineBreakIndex + 1);
            }

            GUIStyle titleStyle = GetScaledStyle(EditorStyles.whiteMiniLabel, scale);
            GUIStyle pathStyle = GetScaledStyle(EditorStyles.miniLabel, scale);
            Rect titleRect = new Rect(nodeRect.x + (35f * scale), nodeRect.y + (6f * scale), nodeRect.width - (44f * scale), 18f * scale);
            Rect pathRect = new Rect(nodeRect.x + (35f * scale), nodeRect.y + (27f * scale), nodeRect.width - (44f * scale), 20f * scale);
            GUI.Label(titleRect, title, titleStyle);
            GUI.Label(pathRect, path, pathStyle);
        }

        private static void DrawGraphNodeOutputPills(GraphNode node, Rect nodeRect, float scale)
        {
            if (node.OutgoingEdges.Count == 0)
            {
                return;
            }

            GUIStyle typeStyle = GetScaledStyle(EditorStyles.miniLabel, scale);
            GUIStyle fieldStyle = GetScaledStyle(EditorStyles.whiteMiniLabel, scale);
            for (int i = 0; i < node.OutgoingEdges.Count; i++)
            {
                GraphEdge edge = node.OutgoingEdges[i];
                float y = nodeRect.y + ((GraphHeaderHeight + 7f + (edge.SourcePortIndex * GraphPortSpacing)) * scale);
                Rect pillRect = new Rect(nodeRect.x + (12f * scale), y, nodeRect.width - (28f * scale), GraphPillHeight * scale);
                Color transitionColor = GetTransitionColor(edge);
                EditorGUI.DrawRect(pillRect, new Color(0.11f, 0.11f, 0.11f, 0.95f));
                DrawOutline(pillRect, transitionColor, 1f);
                Rect iconRect = new Rect(pillRect.x + (6f * scale), pillRect.y + (4f * scale), 16f * scale, 16f * scale);
                if (edge.TargetIcon != null)
                {
                    GUI.DrawTexture(iconRect, edge.TargetIcon, ScaleMode.ScaleToFit);
                }

                GUI.Label(new Rect(pillRect.x + (27f * scale), pillRect.y + (2f * scale), 72f * scale, pillRect.height - (4f * scale)), edge.TargetTypeName, typeStyle);
                GUI.Label(new Rect(pillRect.x + (100f * scale), pillRect.y + (2f * scale), pillRect.width - (126f * scale), pillRect.height - (4f * scale)), edge.Label, fieldStyle);
                EditorGUI.DrawRect(new Rect(nodeRect.xMax - (11f * scale), y + (7f * scale), 8f * scale, 8f * scale), transitionColor);
            }
        }

        private static GUIStyle GetScaledStyle(GUIStyle source, float scale)
        {
            GUIStyle style = new GUIStyle(source);
            int baseSize = source.fontSize > 0 ? source.fontSize : 11;
            style.fontSize = Mathf.Max(6, Mathf.RoundToInt(baseSize * scale));
            style.clipping = TextClipping.Clip;
            return style;
        }

        private static void DrawOutline(Rect rect, Color color, float thickness)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
        }

        private static bool EdgeIntersectsViewport(Vector2 from, Vector2 to, Rect viewport)
        {
            Rect edgeBounds = Rect.MinMaxRect(
                Mathf.Min(from.x, to.x),
                Mathf.Min(from.y, to.y),
                Mathf.Max(from.x, to.x),
                Mathf.Max(from.y, to.y));
            float bezierPadding = Mathf.Max(160f, Mathf.Abs(to.x - from.x) * 0.5f);
            return RectOverlapsPadded(edgeBounds, viewport, bezierPadding);
        }

        private static bool RectOverlapsPadded(Rect rect, Rect viewport, float padding)
        {
            Rect paddedViewport = new Rect(
                viewport.x - padding,
                viewport.y - padding,
                viewport.width + (padding * 2f),
                viewport.height + (padding * 2f));
            return rect.Overlaps(paddedViewport);
        }

        private void DrawGraphSelectionRect()
        {
            if (!isSelectingGraphNodes)
            {
                return;
            }

            Rect graphRect = GetRectFromPoints(graphSelectionStart, graphSelectionEnd);
            Rect viewRect = GraphToViewRect(graphRect);
            EditorGUI.DrawRect(viewRect, new Color(0.25f, 0.55f, 1f, 0.18f));
            DrawOutline(viewRect, new Color(0.35f, 0.72f, 1f, 0.95f), 1f);
        }

        private static Rect GetRectFromPoints(Vector2 a, Vector2 b)
        {
            return Rect.MinMaxRect(
                Mathf.Min(a.x, b.x),
                Mathf.Min(a.y, b.y),
                Mathf.Max(a.x, b.x),
                Mathf.Max(a.y, b.y));
        }

        private void HandleGraphPan(Rect viewportRect)
        {
            Event currentEvent = Event.current;
            if (!viewportRect.Contains(currentEvent.mousePosition))
            {
                return;
            }

            if (isDraggingGraphNode && currentEvent.type == EventType.MouseDrag)
            {
                return;
            }

            if (currentEvent.type == EventType.ScrollWheel)
            {
                if (currentEvent.control)
                {
                    ZoomGraphAtMouse(viewportRect, currentEvent.mousePosition, -currentEvent.delta.y * 0.06f);
                }
                else
                {
                    graphPanOffset -= currentEvent.delta * (24f / graphZoom);
                }

                currentEvent.Use();
                Repaint();
                return;
            }

            if (currentEvent.type == EventType.MouseDrag && (currentEvent.button == 1 || currentEvent.button == 2))
            {
                graphPanOffset += currentEvent.delta / graphZoom;
                currentEvent.Use();
                Repaint();
            }
        }

        private void HandleGraphNodeDrag()
        {
            Event currentEvent = Event.current;
            Vector2 graphMousePosition = ViewToGraphPoint(currentEvent.mousePosition);

            if (currentEvent.type == EventType.MouseDown && currentEvent.button == 0)
            {
                GraphNode hitNode = FindGraphNodeAt(graphMousePosition);
                if (hitNode != null)
                {
                    bool additiveSelection = currentEvent.shift || currentEvent.control || currentEvent.command;
                    if (additiveSelection)
                    {
                        if (!selectedGraphNodeKeys.Add(hitNode.Key))
                        {
                            selectedGraphNodeKeys.Remove(hitNode.Key);
                        }
                    }
                    else if (!selectedGraphNodeKeys.Contains(hitNode.Key))
                    {
                        selectedGraphNodeKeys.Clear();
                        selectedGraphNodeKeys.Add(hitNode.Key);
                    }

                    SyncGraphNodeSelectionState();
                    draggedGraphNodeKey = hitNode.Key;
                    lastGraphDragPosition = graphMousePosition;
                    isDraggingGraphNode = true;
                    didDragGraphNodes = false;
                    currentEvent.Use();
                    Repaint();
                    return;
                }

                if (!currentEvent.shift && !currentEvent.control && !currentEvent.command)
                {
                    selectedGraphNodeKeys.Clear();
                    SyncGraphNodeSelectionState();
                }

                graphSelectionStart = graphMousePosition;
                graphSelectionEnd = graphMousePosition;
                isSelectingGraphNodes = true;
                currentEvent.Use();
                Repaint();
                return;
            }

            if (isDraggingGraphNode && currentEvent.type == EventType.MouseDrag && currentEvent.button == 0)
            {
                GraphNode node = FindGraphNode(draggedGraphNodeKey);
                if (node == null)
                {
                    isDraggingGraphNode = false;
                    draggedGraphNodeKey = string.Empty;
                    return;
                }

                Vector2 delta = graphMousePosition - lastGraphDragPosition;
                MoveSelectedGraphNodes(node, delta);
                lastGraphDragPosition = graphMousePosition;
                didDragGraphNodes = didDragGraphNodes || delta.sqrMagnitude > 0.01f;
                SaveGraphNodePositions();
                currentEvent.Use();
                Repaint();
                return;
            }

            if (isSelectingGraphNodes && currentEvent.type == EventType.MouseDrag && currentEvent.button == 0)
            {
                graphSelectionEnd = graphMousePosition;
                currentEvent.Use();
                Repaint();
                return;
            }

            if (currentEvent.type == EventType.MouseUp)
            {
                if (isSelectingGraphNodes)
                {
                    SelectGraphNodesInRect(GetRectFromPoints(graphSelectionStart, graphSelectionEnd), currentEvent.shift || currentEvent.control || currentEvent.command);
                    isSelectingGraphNodes = false;
                    currentEvent.Use();
                    Repaint();
                    return;
                }

                if (isDraggingGraphNode && !didDragGraphNodes)
                {
                    GraphNode node = FindGraphNode(draggedGraphNodeKey);
                    if (node != null && node.Object != null)
                    {
                        Selection.activeObject = node.Object;
                        EditorGUIUtility.PingObject(node.Object);
                    }
                }

                isDraggingGraphNode = false;
                draggedGraphNodeKey = string.Empty;
                didDragGraphNodes = false;
            }
        }

        private GraphNode FindGraphNode(string key)
        {
            for (int i = 0; i < graphNodes.Count; i++)
            {
                if (graphNodes[i].Key == key)
                {
                    return graphNodes[i];
                }
            }

            return null;
        }

        private GraphNode FindGraphNodeAt(Vector2 graphPosition)
        {
            for (int i = graphNodes.Count - 1; i >= 0; i--)
            {
                if (graphNodes[i].Rect.Contains(graphPosition))
                {
                    return graphNodes[i];
                }
            }

            return null;
        }

        private void MoveSelectedGraphNodes(GraphNode fallbackNode, Vector2 delta)
        {
            if (selectedGraphNodeKeys.Count == 0 || !selectedGraphNodeKeys.Contains(fallbackNode.Key))
            {
                fallbackNode.Rect.position += delta;
                graphNodePositions[fallbackNode.Key] = fallbackNode.Rect.position;
                return;
            }

            for (int i = 0; i < graphNodes.Count; i++)
            {
                GraphNode node = graphNodes[i];
                if (!selectedGraphNodeKeys.Contains(node.Key))
                {
                    continue;
                }

                node.Rect.position += delta;
                graphNodePositions[node.Key] = node.Rect.position;
            }
        }

        private void SelectGraphNodesInRect(Rect selectionRect, bool additiveSelection)
        {
            if (!additiveSelection)
            {
                selectedGraphNodeKeys.Clear();
            }

            for (int i = 0; i < graphNodes.Count; i++)
            {
                if (graphNodes[i].Rect.Overlaps(selectionRect))
                {
                    selectedGraphNodeKeys.Add(graphNodes[i].Key);
                }
            }

            SyncGraphNodeSelectionState();
        }

        private void SyncGraphNodeSelectionState()
        {
            HashSet<string> liveNodeKeys = new HashSet<string>();
            for (int i = 0; i < graphNodes.Count; i++)
            {
                liveNodeKeys.Add(graphNodes[i].Key);
                graphNodes[i].IsSelected = selectedGraphNodeKeys.Contains(graphNodes[i].Key);
            }

            List<string> removedKeys = new List<string>();
            foreach (string selectedKey in selectedGraphNodeKeys)
            {
                if (!liveNodeKeys.Contains(selectedKey))
                {
                    removedKeys.Add(selectedKey);
                }
            }

            for (int i = 0; i < removedKeys.Count; i++)
            {
                selectedGraphNodeKeys.Remove(removedKeys[i]);
            }
        }

        private void ZoomGraphAtMouse(Rect viewportRect, Vector2 mousePosition, float zoomDelta)
        {
            float oldZoom = graphZoom;
            float newZoom = Mathf.Clamp(graphZoom * (1f + zoomDelta), GraphMinZoom, GraphMaxZoom);
            if (Mathf.Approximately(oldZoom, newZoom))
            {
                return;
            }

            Vector2 localMouse = mousePosition - viewportRect.position;
            Vector2 graphPointUnderMouse = (localMouse / oldZoom) - graphPanOffset;
            graphZoom = newZoom;
            graphPanOffset = (localMouse / newZoom) - graphPointUnderMouse;
        }

        private Vector2 GraphToViewPoint(Vector2 graphPoint)
        {
            return (graphPoint + graphPanOffset) * graphZoom;
        }

        private Rect GraphToViewRect(Rect graphRect)
        {
            Vector2 position = GraphToViewPoint(graphRect.position);
            return new Rect(position.x, position.y, graphRect.width * graphZoom, graphRect.height * graphZoom);
        }

        private Vector2 ViewToGraphPoint(Vector2 viewPoint)
        {
            return (viewPoint / graphZoom) - graphPanOffset;
        }


    }
}