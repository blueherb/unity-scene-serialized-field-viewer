# Scene Reference Map for Unity

[한국어 설명](https://github.com/blueherb/unity-scene-serialized-field-viewer/blob/main/README.ko.md)


<img width="1000" height="750" alt="image" src="https://github.com/user-attachments/assets/da3b1e0d-b6ad-4f61-9ab2-0625e3540c19" />


An intentionally small Unity Editor tool for inspecting serialized object-reference fields in the active scene and manually collecting selected references into a graph.

It does not analyze methods, events, code call graphs, or whole-project asset dependencies.

## Install

Place these files inside a Unity project:

```text
Assets/Editor/UnityDependencyGraphPrototype.cs
Assets/Editor/SceneSerializedFieldViewer.GraphBuilder.cs
Assets/Editor/SceneSerializedFieldViewer.GraphView.cs
Assets/Editor/SceneSerializedFieldViewer.Models.cs
Assets/Editor/SceneSerializedFieldViewer.Scanner.cs
Assets/Editor/SceneSerializedFieldViewer.State.cs
Assets/Editor/SceneSerializedFieldViewer.Utilities.cs
```

After Unity compiles, open either menu item:

```text
Tools > Scene Serialized Field Viewer
Tools > Serialized Field Dependencies
```

Both open the same combined window.

## File Layout

- `UnityDependencyGraphPrototype.cs`: editor window lifecycle, toolbar, serialized field list UI.
- `SceneSerializedFieldViewer.Scanner.cs`: active scene scanning and serialized field reference collection.
- `SceneSerializedFieldViewer.GraphBuilder.cs`: graph construction, auto expansion, layout, and transition colors.
- `SceneSerializedFieldViewer.GraphView.cs`: graph panel rendering, pan/zoom, box selection, and node dragging.
- `SceneSerializedFieldViewer.State.cs`: `SessionState` and `EditorPrefs` persistence.
- `SceneSerializedFieldViewer.Utilities.cs`: shared helper methods.
- `SceneSerializedFieldViewer.Models.cs`: internal data models and labels.

## Serialized Fields Page

The first tab lists serialized fields on scene `MonoBehaviour` components. The tool includes only user-created scripts whose script assets live under `Assets/`.

Controls:

- `Refresh`: rescan the active scene.
- `Include Inactive`: include inactive GameObjects.
- `English` / `Korean`: switch the editor window language.
- `Search`: filter by GameObject path, script name, field name, target name, or target path.
- Checkbox: toggle one assigned object-reference field connection in the graph. Checked rows are outlined in yellow.

Only assigned `SerializedPropertyType.ObjectReference` fields can be added to the graph.

## Graph Page

The graph tab starts empty. It shows only links that you manually add from the serialized fields page.

Each added field creates this relationship:

```text
MonoBehaviour component node -> serialized field edge -> referenced target node
```

The graph reuses source and target nodes when multiple added fields share them. It prevents duplicate links for the same field.

Nodes show Unity editor icons for the represented object. Source nodes use the component icon for the MonoBehaviour that owns the field. Target nodes use the icon or mini thumbnail for the referenced component, GameObject, prefab, material, audio clip, or other asset.

Manually added field source nodes are outlined in yellow. Edges connect from the right side of the source node to the left side of the target node.

Nodes with more transitions are ranked higher within their graph group. Independent groups are arranged vertically, and downstream nodes are placed to the right in the same order as the source node's output pills. Nodes also grow taller when they have many incoming or outgoing edges, and each edge uses a separate connection port to reduce line overlap. Use `Auto Arrange` to discard stored manual positions and rebuild this grouped layout.

Serialized field names are shown as pill slots inside the source component node. Each pill also shows the referenced target type and icon on the left. Pills are sorted by downstream child count, then target type, then field name. Transitions start from the matching pill slot and end at a target-side port with the same edge color. The pill outline, source port, target port, and wire all use the same color. The transition line uses a soft Bezier curve instead of a straight segment.

Transition colors are derived from the exact referenced target type, so the same type always uses the same color. Manual and auto-expanded links keep the same hue.

Use the mouse wheel, right-drag, or middle-drag to pan the graph. Use `Ctrl + Mouse Wheel` to zoom in or out. Zoom keeps the graph viewport size unchanged and scales only the graph content. `Reset View` resets both pan and zoom.

The graph tab keeps the serialized field viewer visible on the left. Drag the vertical splitter to resize that field panel. Nodes can be dragged directly in the graph to clean up the layout; manual node positions are stored per scene. Drag on empty graph space to box-select multiple nodes, then drag any selected node to move the selected group together. Use `Shift`, `Ctrl`, or `Cmd` while selecting to add to the current node selection.

When a referenced target belongs to a scene GameObject that also has graphable serialized object-reference fields, the graph automatically expands those downstream links. Auto-expanded links are marked as `Auto` in the link list and are removed by removing the manually added root link that led to them.

Graph selections are stored per active scene with both `SessionState` and `EditorPrefs`, so selected graph links are restored across play mode transitions and editor reloads. The window also saves graph links and node positions before play mode changes, then refreshes after the scene is stable again. If Unity still clears the visible graph during a play test, use `Save State` before testing and `Load State` after returning to edit mode to restore the manually selected links and node positions.

## What It Does Not Show

- Empty object-reference fields in the graph.
- Method lists.
- Event subscriptions or invokes.
- String-based runtime loading.
- Whole-project asset dependency graphs.

## Intent

This tool is a lightweight inspection board. The serialized field page is the source of truth, and the graph page is a manual workspace for only the dependencies you choose to examine.
