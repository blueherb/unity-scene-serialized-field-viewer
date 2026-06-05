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
        private void ToggleFieldInGraph(string fieldKey)
        {
            if (graphFieldKeys.Contains(fieldKey))
            {
                graphFieldKeys.Remove(fieldKey);
            }
            else
            {
                graphFieldKeys.Add(fieldKey);
            }

            SaveGraphFields();
            BuildGraph();
            RemoveUnusedStoredNodePositions();
            SaveGraphNodePositions();
        }

        private void AutoArrangeGraph()
        {
            graphNodePositions.Clear();
            SaveGraphNodePositions();
            BuildGraph();
            SaveVisibleGraphNodePositions();
            Repaint();
        }

        private bool HasManualGraphState()
        {
            return EditorPrefs.HasKey(ManualGraphFieldsPrefsPrefix + activeSceneSessionKey)
                || EditorPrefs.HasKey(ManualGraphNodePositionsPrefsPrefix + activeSceneSessionKey);
        }

        private void ManualSaveGraphState()
        {
            SaveVisibleGraphNodePositions();
            EditorPrefs.SetString(ManualGraphFieldsPrefsPrefix + activeSceneSessionKey, SerializeGraphFields());
            EditorPrefs.SetString(ManualGraphNodePositionsPrefsPrefix + activeSceneSessionKey, SerializeGraphNodePositions());
        }

        private void ManualLoadGraphState()
        {
            string fieldValue = EditorPrefs.GetString(ManualGraphFieldsPrefsPrefix + activeSceneSessionKey, string.Empty);
            string positionValue = EditorPrefs.GetString(ManualGraphNodePositionsPrefsPrefix + activeSceneSessionKey, string.Empty);

            DeserializeGraphFields(fieldValue);
            DeserializeGraphNodePositions(positionValue);
            SaveGraphFields();
            SaveGraphNodePositions();
            Refresh();
            BuildGraph();
            Repaint();
        }

        private void BuildGraph()
        {
            graphNodes.Clear();
            graphEdges.Clear();

            Dictionary<string, GraphNode> nodesByKey = new Dictionary<string, GraphNode>();
            Dictionary<int, float> nextYByDepth = new Dictionary<int, float>();
            HashSet<string> edgeKeys = new HashSet<string>();
            HashSet<string> expandedGameObjectKeys = new HashSet<string>();
            Queue<GraphExpansionItem> expansionQueue = new Queue<GraphExpansionItem>();

            for (int i = 0; i < graphFieldKeys.Count; i++)
            {
                GraphReference reference;
                if (!availableGraphReferences.TryGetValue(graphFieldKeys[i], out reference))
                {
                    continue;
                }

                GraphNode sourceNode = GetOrCreateGraphNode(
                    nodesByKey,
                    nextYByDepth,
                    reference.SourceNodeKey,
                    reference.SourceLabel,
                    reference.SourceObject,
                    reference.SourceIcon,
                    true,
                    0);
                sourceNode.IsSelectedSource = true;

                GraphNode targetNode = GetOrCreateGraphNode(
                    nodesByKey,
                    nextYByDepth,
                    reference.TargetKey,
                    reference.TargetLabel,
                    reference.Target,
                    reference.TargetIcon,
                    false,
                    1);

                AddGraphEdge(edgeKeys, reference.FieldKey, sourceNode, targetNode, reference.FieldPath, true);
                expansionQueue.Enqueue(new GraphExpansionItem(targetNode, 1));
            }

            while (expansionQueue.Count > 0)
            {
                GraphExpansionItem item = expansionQueue.Dequeue();
                GameObject targetGameObject = GetGraphExpansionGameObject(item.Node.Object);
                if (targetGameObject == null)
                {
                    continue;
                }

                string targetGameObjectKey = GetObjectKey(targetGameObject);
                if (!expandedGameObjectKeys.Add(targetGameObjectKey))
                {
                    continue;
                }

                List<GraphReference> childReferences;
                if (!graphReferencesBySourceGameObject.TryGetValue(targetGameObjectKey, out childReferences))
                {
                    continue;
                }

                for (int i = 0; i < childReferences.Count; i++)
                {
                    GraphReference childReference = childReferences[i];
                    GraphNode childTargetNode = GetOrCreateGraphNode(
                        nodesByKey,
                        nextYByDepth,
                        childReference.TargetKey,
                        childReference.TargetLabel,
                        childReference.Target,
                        childReference.TargetIcon,
                        false,
                        item.Depth + 1);

                    string edgeKey = item.Node.Key + "|" + childReference.FieldKey;
                    string edgeLabel = childReference.SourceDisplayName + "." + childReference.FieldPath;
                    if (AddGraphEdge(edgeKeys, edgeKey, item.Node, childTargetNode, edgeLabel, false))
                    {
                        expansionQueue.Enqueue(new GraphExpansionItem(childTargetNode, item.Depth + 1));
                    }
                }
            }

            LayoutGraph();
        }

        private GraphNode GetOrCreateGraphNode(
            Dictionary<string, GraphNode> nodesByKey,
            Dictionary<int, float> nextYByDepth,
            string key,
            string label,
            UnityEngine.Object targetObject,
            Texture icon,
            bool isSource,
            int depth)
        {
            GraphNode node;
            if (nodesByKey.TryGetValue(key, out node))
            {
                node.IsSource = node.IsSource || isSource;
                node.Depth = Mathf.Min(node.Depth, depth);
                return node;
            }

            float y;
            if (!nextYByDepth.TryGetValue(depth, out y))
            {
                y = 24f;
            }

            node = new GraphNode
            {
                Key = key,
                Label = label,
                Object = targetObject,
                Icon = icon,
                IsSource = isSource,
                Depth = depth,
                Rect = new Rect(24f + (depth * GraphColumnSpacing), y, GraphNodeWidth, GraphNodeBaseHeight)
            };
            nodesByKey.Add(key, node);
            graphNodes.Add(node);
            nextYByDepth[depth] = y + GraphNodeBaseHeight + GraphNodeVerticalSpacing;
            return node;
        }

        private bool AddGraphEdge(HashSet<string> edgeKeys, string edgeKey, GraphNode sourceNode, GraphNode targetNode, string label, bool canRemove)
        {
            if (!edgeKeys.Add(edgeKey))
            {
                return false;
            }

            graphEdges.Add(new GraphEdge
            {
                FieldKey = edgeKey,
                Source = sourceNode,
                Target = targetNode,
                Label = label,
                CanRemove = canRemove,
                TargetIcon = targetNode.Icon,
                TargetTypeName = GetObjectTypeLabel(targetNode.Object),
                TransitionColor = GetTransitionColor(targetNode.Object, canRemove)
            });

            return true;
        }

        private void LayoutGraph()
        {
            Dictionary<GraphNode, List<GraphEdge>> outgoingEdges = new Dictionary<GraphNode, List<GraphEdge>>();
            Dictionary<GraphNode, List<GraphEdge>> incomingEdges = new Dictionary<GraphNode, List<GraphEdge>>();

            for (int i = 0; i < graphNodes.Count; i++)
            {
                graphNodes[i].OutgoingCount = 0;
                graphNodes[i].IncomingCount = 0;
                graphNodes[i].OutgoingEdges.Clear();
                outgoingEdges[graphNodes[i]] = new List<GraphEdge>();
                incomingEdges[graphNodes[i]] = new List<GraphEdge>();
            }

            for (int i = 0; i < graphEdges.Count; i++)
            {
                GraphEdge edge = graphEdges[i];
                edge.Source.OutgoingCount++;
                edge.Target.IncomingCount++;
                outgoingEdges[edge.Source].Add(edge);
                edge.Source.OutgoingEdges.Add(edge);
                incomingEdges[edge.Target].Add(edge);
            }

            for (int i = 0; i < graphNodes.Count; i++)
            {
                GraphNode node = graphNodes[i];
                int portCount = Mathf.Max(node.OutgoingCount, node.IncomingCount);
                float outputHeight = GraphHeaderHeight + 14f + (Mathf.Max(1, node.OutgoingCount) * GraphPortSpacing);
                float inputHeight = GraphHeaderHeight + 14f + (Mathf.Max(1, node.IncomingCount) * GraphPortSpacing);
                node.Rect.height = Mathf.Max(GraphNodeBaseHeight, Mathf.Max(outputHeight, inputHeight));
            }

            AssignPortIndexes(outgoingEdges, true);
            PositionNodesByRank();
            ApplyGraphNodePositionOverrides();
            ResolveNodeOverlapsByDepth();
            AssignPortIndexes(outgoingEdges, true);
            AssignPortIndexes(incomingEdges, false);
            SyncGraphNodeSelectionState();
            SaveVisibleGraphNodePositions();
        }

        private void ApplyGraphNodePositionOverrides()
        {
            for (int i = 0; i < graphNodes.Count; i++)
            {
                graphNodes[i].HasStoredPosition = false;
                Vector2 position;
                if (graphNodePositions.TryGetValue(graphNodes[i].Key, out position))
                {
                    graphNodes[i].Rect.position = position;
                    graphNodes[i].HasStoredPosition = true;
                }
            }
        }

        private void ResolveNodeOverlapsByDepth()
        {
            Dictionary<int, List<GraphNode>> nodesByDepth = new Dictionary<int, List<GraphNode>>();
            for (int i = 0; i < graphNodes.Count; i++)
            {
                List<GraphNode> nodes;
                if (!nodesByDepth.TryGetValue(graphNodes[i].Depth, out nodes))
                {
                    nodes = new List<GraphNode>();
                    nodesByDepth.Add(graphNodes[i].Depth, nodes);
                }

                nodes.Add(graphNodes[i]);
            }

            foreach (KeyValuePair<int, List<GraphNode>> item in nodesByDepth)
            {
                List<GraphNode> nodes = item.Value;
                nodes.Sort(CompareNodesByY);

                float nextAvailableY = GraphTopPadding;
                for (int i = 0; i < nodes.Count; i++)
                {
                    GraphNode node = nodes[i];
                    if (!node.HasStoredPosition && node.Rect.y < nextAvailableY)
                    {
                        node.Rect.y = nextAvailableY;
                    }

                    nextAvailableY = Mathf.Max(nextAvailableY, node.Rect.y + node.Rect.height + GraphNodeVerticalSpacing);
                }
            }
        }

        private static int CompareNodesByY(GraphNode a, GraphNode b)
        {
            int yCompare = a.Rect.y.CompareTo(b.Rect.y);
            if (yCompare != 0)
            {
                return yCompare;
            }

            return string.Compare(a.Label, b.Label, StringComparison.OrdinalIgnoreCase);
        }

        private void SaveVisibleGraphNodePositions()
        {
            if (graphNodes.Count == 0 && graphFieldKeys.Count > 0)
            {
                return;
            }

            RemoveUnusedStoredNodePositions();
            for (int i = 0; i < graphNodes.Count; i++)
            {
                graphNodePositions[graphNodes[i].Key] = graphNodes[i].Rect.position;
            }

            SaveGraphNodePositions();
        }

        private static void AssignPortIndexes(Dictionary<GraphNode, List<GraphEdge>> edgesByNode, bool assignSource)
        {
            foreach (KeyValuePair<GraphNode, List<GraphEdge>> item in edgesByNode)
            {
                List<GraphEdge> edges = item.Value;
                edges.Sort(assignSource ? CompareSourceEdgesByFieldPriority : CompareEdgesBySourceY);
                if (assignSource)
                {
                    item.Key.OutgoingEdges.Clear();
                    item.Key.OutgoingEdges.AddRange(edges);
                }

                for (int i = 0; i < edges.Count; i++)
                {
                    if (assignSource)
                    {
                        edges[i].SourcePortIndex = i;
                        edges[i].SourcePortCount = edges.Count;
                    }
                    else
                    {
                        edges[i].TargetPortIndex = i;
                        edges[i].TargetPortCount = edges.Count;
                    }
                }
            }
        }

        private static int CompareSourceEdgesByFieldPriority(GraphEdge a, GraphEdge b)
        {
            int childCompare = b.Target.OutgoingCount.CompareTo(a.Target.OutgoingCount);
            if (childCompare != 0)
            {
                return childCompare;
            }

            int typeCompare = CompareTypeLabels(a.TargetTypeName, b.TargetTypeName);
            if (typeCompare != 0)
            {
                return typeCompare;
            }

            int labelCompare = string.Compare(a.Label, b.Label, StringComparison.OrdinalIgnoreCase);
            if (labelCompare != 0)
            {
                return labelCompare;
            }

            return CompareEdgesByTargetY(a, b);
        }

        private static int CompareTypeLabels(string a, string b)
        {
            int orderCompare = GetTypeSortOrder(a).CompareTo(GetTypeSortOrder(b));
            if (orderCompare != 0)
            {
                return orderCompare;
            }

            return string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
        }

        private static int GetTypeSortOrder(string typeName)
        {
            if (typeName == "GameObject")
            {
                return 0;
            }

            if (typeName == "Transform" || typeName == "RectTransform")
            {
                return 1;
            }

            if (typeName == "Button" || typeName == "Slider" || typeName == "TextMeshProUGUI")
            {
                return 2;
            }

            if (typeName == "AudioSource" || typeName == "AudioClip" || typeName == "AudioMixer")
            {
                return 3;
            }

            if (typeName == "Sprite" || typeName == "Texture2D" || typeName == "Material")
            {
                return 4;
            }

            return 10;
        }

        private static int CompareEdgesByTargetY(GraphEdge a, GraphEdge b)
        {
            int targetCompare = a.Target.Rect.y.CompareTo(b.Target.Rect.y);
            if (targetCompare != 0)
            {
                return targetCompare;
            }

            return string.Compare(a.Label, b.Label, StringComparison.OrdinalIgnoreCase);
        }

        private static int CompareEdgesBySourceY(GraphEdge a, GraphEdge b)
        {
            int sourceCompare = a.Source.Rect.y.CompareTo(b.Source.Rect.y);
            if (sourceCompare != 0)
            {
                return sourceCompare;
            }

            return string.Compare(a.Label, b.Label, StringComparison.OrdinalIgnoreCase);
        }

        private void PositionNodesByRank()
        {
            List<List<GraphNode>> groups = BuildConnectedGraphGroups();
            groups.Sort(CompareGraphGroups);

            float groupStartY = GraphTopPadding;
            for (int groupIndex = 0; groupIndex < groups.Count; groupIndex++)
            {
                Dictionary<int, List<GraphNode>> nodesByDepth = GroupNodesByDepth(groups[groupIndex]);
                List<int> depths = new List<int>(nodesByDepth.Keys);
                depths.Sort();

                float groupHeight = 0f;
                for (int d = 0; d < depths.Count; d++)
                {
                    int depth = depths[d];
                    List<GraphNode> nodes = nodesByDepth[depth];
                    nodes.Sort(depth == 0 ? CompareNodesByTransitionRank : CompareNodesByIncomingBarycenter);

                    float y = groupStartY;
                    for (int i = 0; i < nodes.Count; i++)
                    {
                        GraphNode node = nodes[i];
                        node.Rect.x = 24f + (node.Depth * GraphColumnSpacing);
                        node.Rect.y = y;
                        node.Rect.width = GraphNodeWidth;
                        y += node.Rect.height + GraphNodeVerticalSpacing;
                    }

                    groupHeight = Mathf.Max(groupHeight, y - groupStartY);
                }

                groupStartY += Mathf.Max(GraphNodeBaseHeight, groupHeight) + GraphGroupVerticalSpacing;
            }
        }

        private List<List<GraphNode>> BuildConnectedGraphGroups()
        {
            Dictionary<GraphNode, List<GraphNode>> adjacentNodes = new Dictionary<GraphNode, List<GraphNode>>();
            for (int i = 0; i < graphNodes.Count; i++)
            {
                adjacentNodes[graphNodes[i]] = new List<GraphNode>();
            }

            for (int i = 0; i < graphEdges.Count; i++)
            {
                GraphEdge edge = graphEdges[i];
                adjacentNodes[edge.Source].Add(edge.Target);
                adjacentNodes[edge.Target].Add(edge.Source);
            }

            List<List<GraphNode>> groups = new List<List<GraphNode>>();
            HashSet<GraphNode> visitedNodes = new HashSet<GraphNode>();
            for (int i = 0; i < graphNodes.Count; i++)
            {
                GraphNode startNode = graphNodes[i];
                if (!visitedNodes.Add(startNode))
                {
                    continue;
                }

                List<GraphNode> group = new List<GraphNode>();
                Queue<GraphNode> queue = new Queue<GraphNode>();
                queue.Enqueue(startNode);
                while (queue.Count > 0)
                {
                    GraphNode node = queue.Dequeue();
                    group.Add(node);

                    List<GraphNode> neighbors = adjacentNodes[node];
                    for (int n = 0; n < neighbors.Count; n++)
                    {
                        if (visitedNodes.Add(neighbors[n]))
                        {
                            queue.Enqueue(neighbors[n]);
                        }
                    }
                }

                groups.Add(group);
            }

            return groups;
        }

        private static Dictionary<int, List<GraphNode>> GroupNodesByDepth(List<GraphNode> nodes)
        {
            Dictionary<int, List<GraphNode>> nodesByDepth = new Dictionary<int, List<GraphNode>>();
            for (int i = 0; i < nodes.Count; i++)
            {
                List<GraphNode> depthNodes;
                if (!nodesByDepth.TryGetValue(nodes[i].Depth, out depthNodes))
                {
                    depthNodes = new List<GraphNode>();
                    nodesByDepth.Add(nodes[i].Depth, depthNodes);
                }

                depthNodes.Add(nodes[i]);
            }

            return nodesByDepth;
        }

        private static int CompareGraphGroups(List<GraphNode> a, List<GraphNode> b)
        {
            int transitionCompare = GetGroupTransitionCount(b).CompareTo(GetGroupTransitionCount(a));
            if (transitionCompare != 0)
            {
                return transitionCompare;
            }

            int sizeCompare = b.Count.CompareTo(a.Count);
            if (sizeCompare != 0)
            {
                return sizeCompare;
            }

            return string.Compare(GetGroupSortLabel(a), GetGroupSortLabel(b), StringComparison.OrdinalIgnoreCase);
        }

        private static int GetGroupTransitionCount(List<GraphNode> nodes)
        {
            int count = 0;
            for (int i = 0; i < nodes.Count; i++)
            {
                count += nodes[i].IncomingCount + nodes[i].OutgoingCount;
            }

            return count;
        }

        private static string GetGroupSortLabel(List<GraphNode> nodes)
        {
            string label = string.Empty;
            for (int i = 0; i < nodes.Count; i++)
            {
                if (string.IsNullOrEmpty(label) || string.Compare(nodes[i].Label, label, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    label = nodes[i].Label;
                }
            }

            return label;
        }

        private int CompareNodesByIncomingBarycenter(GraphNode a, GraphNode b)
        {
            float aBarycenter = GetIncomingBarycenter(a);
            float bBarycenter = GetIncomingBarycenter(b);
            int barycenterCompare = aBarycenter.CompareTo(bBarycenter);
            if (barycenterCompare != 0)
            {
                return barycenterCompare;
            }

            return CompareNodesByTransitionRank(a, b);
        }

        private float GetIncomingBarycenter(GraphNode node)
        {
            float total = 0f;
            int count = 0;
            for (int i = 0; i < graphEdges.Count; i++)
            {
                GraphEdge edge = graphEdges[i];
                if (edge.Target != node)
                {
                    continue;
                }

                total += GetSourcePortY(edge.Source.Rect, edge.SourcePortIndex);
                count++;
            }

            if (count == 0)
            {
                return node.Rect.y;
            }

            return total / count;
        }

        private static int CompareNodesByTransitionRank(GraphNode a, GraphNode b)
        {
            int aTransitions = a.OutgoingCount + a.IncomingCount;
            int bTransitions = b.OutgoingCount + b.IncomingCount;
            int transitionCompare = bTransitions.CompareTo(aTransitions);
            if (transitionCompare != 0)
            {
                return transitionCompare;
            }

            int outgoingCompare = b.OutgoingCount.CompareTo(a.OutgoingCount);
            if (outgoingCompare != 0)
            {
                return outgoingCompare;
            }

            return string.Compare(a.Label, b.Label, StringComparison.OrdinalIgnoreCase);
        }

        private static Color GetTransitionColor(GraphEdge edge)
        {
            return edge.TransitionColor;
        }

        private static Color GetTransitionColor(UnityEngine.Object target, bool canRemove)
        {
            Color color = GetTransitionBaseColor(target);
            color.a = 1f;
            return color;
        }

        private static Color GetTransitionBaseColor(UnityEngine.Object target)
        {
            string typeLabel = GetObjectTypeLabel(target);
            if (typeLabel == "GameObject")
            {
                return new Color(0.18f, 0.78f, 1f, 1f);
            }

            if (typeLabel == "Button")
            {
                return new Color(0.47f, 0.52f, 1f, 1f);
            }

            if (typeLabel == "Slider")
            {
                return new Color(0.19f, 0.86f, 0.82f, 1f);
            }

            if (typeLabel == "TextMeshProUGUI")
            {
                return new Color(1f, 0.28f, 0.82f, 1f);
            }

            if (typeLabel == "AudioSource")
            {
                return new Color(1f, 0.44f, 0.16f, 1f);
            }

            if (typeLabel == "AudioClip")
            {
                return new Color(1f, 0.78f, 0.12f, 1f);
            }

            if (typeLabel.IndexOf("AudioMixer", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return new Color(1f, 0.24f, 0.38f, 1f);
            }

            if (typeLabel == "Sprite")
            {
                return new Color(0.72f, 0.42f, 1f, 1f);
            }

            if (typeLabel == "Texture2D" || typeLabel == "Texture")
            {
                return new Color(0.52f, 0.36f, 1f, 1f);
            }

            if (typeLabel == "Material")
            {
                return new Color(0.98f, 0.42f, 0.62f, 1f);
            }

            int paletteIndex = (GetStableHash(typeLabel) & 0x7fffffff) % TypeColorPalette.Length;
            return TypeColorPalette[paletteIndex];
        }

        private static readonly Color[] TypeColorPalette =
        {
            new Color(0.35f, 0.72f, 1f, 1f),
            new Color(0.86f, 0.36f, 1f, 1f),
            new Color(0.18f, 0.86f, 0.67f, 1f),
            new Color(1f, 0.34f, 0.52f, 1f),
            new Color(0.58f, 0.88f, 0.22f, 1f),
            new Color(0.42f, 0.48f, 1f, 1f),
            new Color(1f, 0.56f, 0.18f, 1f),
            new Color(0.20f, 0.76f, 1f, 1f),
            new Color(0.98f, 0.32f, 0.86f, 1f),
            new Color(0.78f, 0.92f, 0.24f, 1f),
            new Color(0.62f, 0.42f, 1f, 1f),
            new Color(0.16f, 0.92f, 0.92f, 1f),
            new Color(1f, 0.42f, 0.30f, 1f),
            new Color(0.40f, 0.95f, 0.45f, 1f)
        };

        private static int GetStableHash(string value)
        {
            unchecked
            {
                int hash = 17;
                for (int i = 0; i < value.Length; i++)
                {
                    hash = (hash * 31) + value[i];
                }

                return hash;
            }
        }

        private static string GetObjectTypeLabel(UnityEngine.Object target)
        {
            if (target == null)
            {
                return "Null";
            }

            if (target is GameObject)
            {
                return "GameObject";
            }

            Component component = target as Component;
            if (component != null)
            {
                return component.GetType().Name;
            }

            return target.GetType().Name;
        }


    }
}