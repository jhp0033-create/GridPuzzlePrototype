



# Unity 4방향 그리드 퍼즐 프로토타입

본 프로젝트는 4방향 그리드 매칭 및 경로 연결 메커니즘을 유니티로 구현한 **포트폴리오 제출용 기술 검증** 프로토타입입니다. 확장성 있는 C# 설계, ScriptableObject 기반의 데이터 분리, 그리고 부드러운 UX 애니메이션 연출에 초점을 맞추어 제작되었습니다.

**5일의 짧은 개발 기간** 내에 C# 코어 아키텍처 설계, 핵심 매칭 로직 구현, 연출 최적화 및 파이썬 기반 레벨 디자인 자동화 툴까지 개발 완료하여 빠른 구현 역량을 검증하고자 제작되었습니다.


---

## 🎮 게임 플레이 데모 영상

[https://github.com/user-attachments/assets/97ec6837-e042-4e04-bc46-382527c3849f](https://github.com/user-attachments/assets/940a7b36-04d2-4bb1-a8bf-167f66895767)

* **핵심 플레이 루프:**
  1. **차량 배치(Deploy):** 3개의 대기 레인(Lane)의 선두 차량 또는 임시 저장소(Storage)에 있는 차량 중 하나를 탭하여 선택 및 배치합니다.
  2. **레일 이동 및 복셀 흡수:** 배치된 차량은 그리드 외곽에 설치된 5개의 웨이포인트(레일)를 따라 이동하며, 해당 구간 방향(Top, Right, Bottom, Left)에서 노출된 외곽 복셀 중 자신과 동일한 색상의 복셀을 정원(Quota)만큼 흡수하여 제거합니다.
  3. **저장소(Storage) 시스템:** 한 바퀴를 돌고도 정원(Quota)을 채우지 못한 미완료 차량은 임시 저장소로 이동합니다. 저장소 슬롯이 가득 찬 상태에서 추가 미완료 차량이 진입하면 **게임 오버(Game Over)**가 되며, 모든 복셀을 제거하면 **스테이지 클리어(Victory)**가 됩니다.
* **시각적 피드백:** 차량 선택 시 활성화되는 아웃라인 셰이더, 부드러운 트윈 기반 연출 및 복셀 흡수/완성 시의 파티클 이펙트를 제공합니다.

---

## 📅 프로젝트 정보

* **제작 기간:** `2026.04.28 - 2026.05.02` (5일)
* **개발 엔진:** `Unity 2022.3.62f2` (LTS)
* **렌더 파이프라인:** Universal Render Pipeline (URP)
* **타겟 플랫폼:** 모바일 세로형 (720x1280 Portrait)
* **사용 패키지:** DOTween (v2), TextMeshPro

---

## 🛠️ 설계 아키텍처 및 기술적 특징

단순 구현을 넘어 **유지보수가 용이하고 성능이 최적화된 구조**를 목표로 개발했습니다:

### 1. 느슨하게 결합된 매니저 패턴 (Decoupled Manager)
단일 클래스(MonoBehaviour)가 비대해지는 것을 방지하기 위해 각 시스템의 역할을 명확히 분리하여 설계했습니다.
* **`GameManager`**: 게임의 글로벌 상태(타이틀, 플레이, 클리어, 실패) 및 전체 흐름을 통제합니다.
* **`VoxelGridManager`**: 2D 그리드 복셀 데이터를 관리하며, 4개 방향(Top, Right, Bottom, Left)으로 노출된 외곽 복셀(Outer Shell)을 캐싱하고, 블록이 흡수된 후 해당 행/열의 노출 상태를 실시간으로 업데이트하는 결정론적 시뮬레이션을 수행합니다.
* **`VehicleManager`**: 3개 대기 레인(Queue Lane) 및 임시 저장소(Storage)의 차량 배치(Deploy) 흐름과 실시간 배치 대수를 통제하며, 차량의 스케일/회전 연출을 제어합니다.
* **`InputManager`**: 모바일 터치 및 마우스 클릭 입력을 감지하여, 플레이어가 선택 가능한 레인 선두 차량 및 저장소 차량을 탭하여 상호작용할 수 있도록 이벤트를 전달합니다.
* **`SoundManager`**: 오디오 리소스를 중앙 관리하며 단발성 효과음을 경량 재생합니다.

### 2. 데이터 주도형 설계 (Data-Driven Design via ScriptableObject)
게임의 룰/레벨 설정과 실행 로직을 철저히 분리하여 설계 생산성을 높였습니다.
* **`LevelData`**: 그리드의 크기, 노드의 시작점 위치, 색상 등의 레벨 고유 정보를 파일 형태로 저장합니다.
* **`VehicleData` & `VoxelData`**: 생성될 개체들의 데이터 템플릿과 속성을 정의합니다.
* **확장성:** 기획자는 코드를 수정하지 않고 새로운 `LevelData` 에셋을 생성하는 것만으로 신규 스테이지를 손쉽게 추가할 수 있습니다.

### 3. 성능 최적화 (Memory & Logic Optimization)
* **오브젝트 풀링 (`VehiclePool`, `VoxelPool`)**: 퍼즐 블록 및 차량 리소스를 동적으로 파괴/생성하지 않고 재활용하여 가비지 컬렉션(GC) 부하 및 프레임 드랍을 사전에 차단했습니다.
* **외곽 쉘 기반 실시간 노출 갱신 알고리즘**: 경로 검증 및 흡수 여부 판단 시 매 프레임 전체 격자를 탐색하지 않고, 4방향별 노출된 외곽 복셀 목록(`exposedVoxels`)을 별도로 캐싱합니다. 복셀 제거 시에는 제거된 좌표의 행/열에 대해서만 부분 방사형 레이캐스트를 수행하여 O(1)에 준하는 효율로 노출 쉘을 갱신합니다.

### 4. 연출 및 그래픽 기법
* **DOTween 트윈 연출**: 차량의 곡선 레일 이동 보간, 스테이지 클리어/실패 시 UI 연출, 클리어 시 차량의 공전 회전 및 스케일 아웃 연출에 트윈 엔진을 사용하여 생동감 넘치는 연출을 제공합니다.
* **커스텀 아웃라인 셰이더 (`VoxelOutline.shader`)**: 셰이더 그래프 기반의 외곽선 기법을 활용하여 현재 선택되어 상호작용 대기 중인 레인 선두 차량을 시각적으로 강조합니다.

---

## ⚙️ 레벨 디자인 자동화 도구 (`Tool/LevelDesigner.py`)

기획 단계에서 그리드 레벨 배치 이미지를 준비한 뒤, 이를 분석하여 **인게임 JSON 레벨 데이터로 1초 만에 자동 생성해 주는 파이썬(Python) 빌더 도구**를 탑재하고 있습니다.
* **주요 알고리즘 및 플로우:**
  1. **컬러 팰릿 추출:** `Pillow(PIL)` 라이브러리를 통해 원본 이미지의 픽셀 데이터를 분석하고 주요 색상 5종을 동적으로 자동 추출합니다.
  2. **그리드 다운샘플링:** 색상 왜곡(블러링, 블리딩)을 방지하기 위해 `NEAREST` 보간법을 적용하여 14x16 해상도로 정밀 압축 변환합니다.
  3. **외곽선(Exposed) 연산:** 그리드 좌표의 물리적 위치 경계선(Top, Right, Bottom, Left)을 비트마스크 패턴으로 연산하여 초기 노출 상태(`exposedFaces`)를 계산합니다.
  4. **JSON 변환 출력:** 분석이 끝난 레벨 세팅을 유니티 직렬화 구조에 맞는 JSON 포맷으로 빌드하여 출력합니다.
* **의의:** 디자인 리소스와 인게임 데이터 간의 수동 매핑 작업을 완전히 자동화하여 기획-개발 생산성을 극대화하였습니다.

---

## 📂 폴더 구조
```
Assets/
 ├── Materials/        # 아웃라인 및 배경 마티리얼/셰이더
 ├── Prefabs/          # 차량, 그리드 노드, UI 팝업, 파티클 이펙트 프리팹
 ├── Scenes/           # 메인 퍼즐 플레이 게임 씬
 ├── Scripts/          # Core, Data, Editor, Managers, UI로 분리된 C# 스크립트
 └── Sound/            # 인게임 연출용 오디오 리소스
Tool/
 ├── Input/            # 분석할 기획용 레벨 이미지 (PNG/JPG)
 ├── Output/           # 자동 빌드되어 출력될 JSON LevelData 파일
 └── LevelDesigner.py  # 파이썬 이미지 파싱 엔진 및 레벨 빌더 스크립트
```

---

## 📝 사용 리소스 출처 (Resource Credits)

본 프로젝트는 개인 포트폴리오 기술 검증 및 아키텍처 R&D 목적으로 제작되었으며, 인게임 구현을 위해 활용 및 가공한 외부 에셋의 출처는 다음과 같습니다.

* **블록 모델링 및 애니메이션:** Blender를 활용한 자체 제작
* **차량 3D 모델링:** Unity Asset Store [FREE Cartoon Car Pack - Simple Vehicles](https://assetstore.unity.com/packages/3d/vehicles/land/free-cartoon-car-pack-simple-vehicles-282425) 활용
* **이펙트(VFX):** Unity Asset Store [Cartoon FX Remaster Free](https://assetstore.unity.com/packages/vfx/particles/cartoon-fx-remaster-free-109565) 활용
* **트윈 라이브러리:** [DOTween](https://assetstore.unity.com/packages/tools/animation/dotween-hotween-v2-27676)
* **폰트:** Google Fonts - [Righteous](https://fonts.google.com/specimen/Righteous) (TextMeshPro 임포트)
* **2D 이미지 리소스:** 배경 화면, 승리/패배 연출용 텍스트 (Gemini 및 ChatGPT를 활용한 자체 인공지능 생성 및 정제)
* **효과음(SFX):** 학습 및 기술 검증을 위한 레퍼런스 영상 분석 및 가공 (비상업적 연구 목적)
