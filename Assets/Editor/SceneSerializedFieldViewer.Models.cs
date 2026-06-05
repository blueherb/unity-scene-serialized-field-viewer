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
        private sealed class SceneObjectEntry
        {
            public readonly GameObject GameObject;
            public readonly string Path;
            public readonly List<ComponentEntry> Components;

            public SceneObjectEntry(GameObject gameObject, string path, List<ComponentEntry> components)
            {
                GameObject = gameObject;
                Path = path;
                Components = components;
            }
        }

        private sealed class ComponentEntry
        {
            public Component Component;
            public string DisplayName;
            public string ObjectPath;
            public string StateKey;
            public readonly List<FieldMeta> Fields = new List<FieldMeta>();
            public readonly Dictionary<string, FieldMeta> FieldsByPath = new Dictionary<string, FieldMeta>();

            public static ComponentEntry Valid(Component component, string objectPath, int componentIndex)
            {
                Type componentType = component.GetType();
                return new ComponentEntry
                {
                    Component = component,
                    DisplayName = componentType.Name,
                    ObjectPath = objectPath,
                    StateKey = objectPath + "|" + componentIndex + "|" + componentType.FullName
                };
            }
        }

        private sealed class FieldMeta
        {
            public string FieldKey;
            public string FieldPath;
            public string DisplayName;
            public string DisplayLabel;
            public UnityEngine.Object Target;
            public string TargetPath;
            public bool CanAddToGraph;
        }

        private sealed class GraphReference
        {
            public string FieldKey;
            public string SourceKey;
            public string SourceNodeKey;
            public UnityEngine.Object SourceObject;
            public Texture SourceIcon;
            public string SourceDisplayName;
            public string SourceGameObjectKey;
            public string SourceLabel;
            public string FieldPath;
            public UnityEngine.Object Target;
            public string TargetKey;
            public Texture TargetIcon;
            public string TargetLabel;
        }

        private sealed class GraphNode
        {
            public string Key;
            public Rect Rect;
            public string Label;
            public UnityEngine.Object Object;
            public Texture Icon;
            public int Depth;
            public int OutgoingCount;
            public int IncomingCount;
            public bool IsSource;
            public bool IsSelectedSource;
            public bool IsSelected;
            public bool HasStoredPosition;
            public readonly List<GraphEdge> OutgoingEdges = new List<GraphEdge>();
        }

        private sealed class GraphEdge
        {
            public string FieldKey;
            public GraphNode Source;
            public GraphNode Target;
            public string Label;
            public bool CanRemove;
            public Texture TargetIcon;
            public string TargetTypeName;
            public Color TransitionColor;
            public int SourcePortIndex;
            public int SourcePortCount = 1;
            public int TargetPortIndex;
            public int TargetPortCount = 1;
        }

        private sealed class GraphExpansionItem
        {
            public readonly GraphNode Node;
            public readonly int Depth;

            public GraphExpansionItem(GraphNode node, int depth)
            {
                Node = node;
                Depth = depth;
            }
        }

        private static class Labels
        {
            public const string ActiveSceneEn = "Active Scene";
            public const string ActiveSceneKo = "활성 씬";
            public const string AddedLinksEn = "Added Links";
            public const string AddedLinksKo = "추가된 연결";
            public const string AutoEn = "Auto";
            public const string AutoKo = "자동";
            public const string ClearEn = "Clear";
            public const string ClearKo = "지우기";
            public const string ClearGraphEn = "Clear Graph";
            public const string ClearGraphKo = "그래프 비우기";
            public const string EmptyFieldsEn = "No visible serialized fields were found in the active scene.";
            public const string EmptyFieldsKo = "활성 씬에서 표시할 직렬화 필드를 찾지 못했습니다.";
            public const string EmptyGraphEn = "Graph is empty.";
            public const string EmptyGraphKo = "그래프가 비어 있습니다.";
            public const string EnglishEn = "English";
            public const string FieldEn = "Field";
            public const string FieldKo = "필드";
            public const string GraphableReferencesEn = "Graphable Object References";
            public const string GraphableReferencesKo = "그래프 추가 가능 참조";
            public const string GraphTabEn = "Graph";
            public const string GraphTabKo = "그래프";
            public const string IncludeInactiveEn = "Include Inactive";
            public const string IncludeInactiveKo = "비활성 포함";
            public const string KoreanKo = "한국어";
            public const string MissingScriptEn = "Missing Script";
            public const string MissingScriptKo = "스크립트 누락";
            public const string NoneEn = "None";
            public const string NoneKo = "없음";
            public const string ObjectEn = "Object";
            public const string ObjectKo = "오브젝트";
            public const string ObjectsEn = "Objects";
            public const string ObjectsKo = "오브젝트";
            public const string RefreshEn = "Refresh";
            public const string RefreshKo = "새로고침";
            public const string RemoveEn = "Remove";
            public const string RemoveKo = "제거";
            public const string ResetViewEn = "Reset View";
            public const string ResetViewKo = "보기 초기화";
            public const string SearchEn = "Search";
            public const string SearchKo = "검색";
            public const string SelectEn = "Select";
            public const string SelectKo = "선택";
            public const string SerializedFieldsTabEn = "Serialized Fields";
            public const string SerializedFieldsTabKo = "직렬화 필드";
            public const string ShowEn = "Show";
            public const string ShowKo = "다시 표시";
            public const string VisibleFieldsEn = "Visible Serialized Fields";
            public const string VisibleFieldsKo = "표시 중인 직렬화 필드";
        }
    }
}