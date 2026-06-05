# Scene Reference Map for Unity

Unity 활성 씬의 serialized object reference 필드를 살펴보고, 사용자가 고른 reference만 그래프에 수동으로 추가하는 작은 에디터 툴입니다.

메서드 호출, 이벤트, 코드 콜 그래프, 프로젝트 전체 에셋 의존성은 분석하지 않습니다.

## 설치

다음 파일들을 Unity 프로젝트 안에 둡니다.

```text
Assets/Editor/UnityDependencyGraphPrototype.cs
Assets/Editor/SceneSerializedFieldViewer.GraphBuilder.cs
Assets/Editor/SceneSerializedFieldViewer.GraphView.cs
Assets/Editor/SceneSerializedFieldViewer.Models.cs
Assets/Editor/SceneSerializedFieldViewer.Scanner.cs
Assets/Editor/SceneSerializedFieldViewer.State.cs
Assets/Editor/SceneSerializedFieldViewer.Utilities.cs
```

Unity 컴파일이 끝나면 아래 메뉴 중 하나로 엽니다.

```text
Tools > Scene Serialized Field Viewer
Tools > Serialized Field Dependencies
```

두 메뉴는 같은 결합 창을 엽니다.

## 파일 구성

- `UnityDependencyGraphPrototype.cs`: 에디터 창 생명주기, toolbar, serialized field 목록 UI
- `SceneSerializedFieldViewer.Scanner.cs`: 활성 씬 스캔과 serialized field reference 수집
- `SceneSerializedFieldViewer.GraphBuilder.cs`: 그래프 생성, 자동 확장, 레이아웃, transition 색상
- `SceneSerializedFieldViewer.GraphView.cs`: 그래프 패널 렌더링, pan/zoom, 박스 선택, 노드 드래그
- `SceneSerializedFieldViewer.State.cs`: `SessionState`와 `EditorPrefs` 저장/복원
- `SceneSerializedFieldViewer.Utilities.cs`: 공용 helper 메서드
- `SceneSerializedFieldViewer.Models.cs`: 내부 데이터 모델과 label

## Serialized Fields 페이지

첫 번째 탭은 씬의 `MonoBehaviour` 컴포넌트에 있는 직렬화 필드를 보여줍니다.

`Assets/` 아래의 사용자 작성 스크립트만 포함합니다.

주요 동작:

- `Refresh`: 활성 씬을 다시 스캔합니다.
- `Include Inactive`: 비활성 GameObject도 포함합니다.
- `English` / `한국어`: 에디터 창 언어를 전환합니다.
- `Search`: GameObject 경로, 스크립트 이름, 필드 이름, 대상 이름, 대상 경로로 필터링합니다.
- 체크박스: 값이 들어 있는 object reference 필드 연결 하나를 그래프에 추가하거나 제거합니다. 체크된 행은 노란 외곽선으로 표시됩니다.

그래프에 추가할 수 있는 것은 값이 들어 있는 `SerializedPropertyType.ObjectReference` 필드뿐입니다.

## Graph 페이지

그래프 탭은 처음에는 비어 있습니다. Serialized Fields 페이지에서 `+`를 누른 연결만 표시합니다.

필드 하나를 추가하면 다음 관계가 생깁니다.

```text
MonoBehaviour component node -> serialized field edge -> referenced target node
```

여러 필드가 같은 source나 target을 공유하면 노드를 재사용합니다. 같은 필드를 여러 번 눌러도 중복 link는 만들지 않습니다. 그래프 패널 안에서는 마우스 휠이나 드래그로 화면을 이동할 수 있습니다.

노드는 Unity 에디터의 내장 아이콘을 표시합니다. source 노드는 필드를 가진 MonoBehaviour 컴포넌트 아이콘을 사용하고, target 노드는 참조된 컴포넌트, GameObject, prefab, material, audio clip, asset 등의 아이콘이나 미니 썸네일을 사용합니다.

사용자가 직접 추가한 필드의 source 노드는 노란 외곽선으로 표시됩니다. transition은 source 노드의 오른쪽 끝에서 target 노드의 왼쪽 끝으로 이어집니다.

transition이 많은 노드는 같은 그래프 그룹 안에서 우선 배치됩니다. 독립된 그룹은 세로로 분리되고, downstream 노드는 source 노드의 출력 pill과 같은 순서로 우측 컬럼에 놓입니다. incoming/outgoing edge가 많은 노드는 높이가 커지고, 각 edge는 별도의 연결 port를 사용합니다. `Auto Arrange`를 누르면 저장된 수동 위치를 버리고 현재 그래프 구조 기준으로 그룹 레이아웃을 다시 만듭니다.

serialized field 이름은 source 컴포넌트 노드 내부의 pill 슬롯으로 표시됩니다. 각 pill 왼쪽에는 참조 대상의 타입과 아이콘도 함께 표시됩니다. pill은 하위 연결 수가 많은 순, target 타입 순, 필드 이름순으로 정렬됩니다. transition은 해당 pill 슬롯에서 시작하고 target 쪽에는 같은 edge 색상의 port로 도착합니다. pill 외곽선, source port, target port, wire는 모두 같은 색을 사용합니다. 선은 직선이 아니라 완만한 Bezier 곡선으로 표시됩니다.

transition 색은 참조 대상의 정확한 타입 이름에서 안정적으로 계산합니다. 따라서 같은 타입은 항상 같은 색을 사용합니다. 수동 link는 더 밝게, 자동 확장 link는 같은 계열의 낮은 채도로 표시됩니다.

마우스 휠, 우클릭 드래그, 중클릭 드래그로 그래프를 이동할 수 있고, `Ctrl + Mouse Wheel`로 축소/확대할 수 있습니다. zoom은 그래프 viewport 크기를 줄이지 않고 그래프 내용만 확대/축소합니다. `Reset View`는 이동과 zoom을 모두 초기화합니다.

그래프 탭에서는 왼쪽에 직렬화 필드 뷰가 함께 표시됩니다. 가운데 splitter를 드래그해서 필드 패널 폭을 조절할 수 있습니다. 그래프 안의 노드는 직접 드래그해서 배치할 수 있고, 수동 배치 위치는 씬별로 저장됩니다. 그래프 빈 공간을 드래그하면 여러 노드를 박스 선택할 수 있으며, 선택된 노드 중 하나를 드래그하면 선택 그룹 전체가 함께 이동합니다. `Shift`, `Ctrl`, `Cmd`를 누른 채 선택하면 기존 선택에 노드를 추가할 수 있습니다.

참조된 target이 씬 GameObject에 속하고, 그 GameObject에도 그래프에 추가 가능한 serialized object reference 필드가 있으면 downstream link를 자동으로 확장합니다. 자동 확장된 link는 목록에서 `자동`으로 표시되며, 직접 추가한 root link를 제거하면 함께 사라집니다.

그래프 구성은 활성 씬별로 `SessionState`와 `EditorPrefs`에 함께 저장됩니다. Play 모드 전환과 에디터 reload 이후에도 같은 씬의 그래프 선택을 복원할 수 있습니다. 그래도 Unity 플레이 테스트 중 보이는 그래프가 초기화되면, 테스트 전에 `수동 저장`을 누르고 Edit 모드로 돌아온 뒤 `불러오기`를 눌러 활성 link와 노드 위치를 복구할 수 있습니다.

## 보여주지 않는 것

- 값이 비어 있는 object reference 필드의 그래프 연결
- 메서드 목록
- 이벤트 구독 또는 Invoke 관계
- 문자열 기반 런타임 로딩 관계
- 프로젝트 전체 에셋 의존성 그래프

## 의도

이 도구는 가벼운 확인용 작업 보드입니다. 직렬화 필드 페이지가 원본이고, 그래프 페이지는 사용자가 직접 고른 의존성만 올려 보는 수동 작업 공간입니다.
