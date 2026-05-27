# Body Map VR Experience (Art Therapy → AI Conversation & 3D Synthesis)

본 프로젝트는 미술 치료(Art Therapy)의 바디 매핑(Body Mapping) 기법을 가상현실(VR) 및 AI 기술과 융합한 인터랙티브 심리 치료 플랫폼입니다. 
사용자가 웹에서 그린 감정 그림을 실시간으로 분석하여 몸의 부위별 감정을 추출하고, 이를 Unity 3D/VR 환경에 띄워 대화형 AI(Therapist)와 음성으로 대화하며 감정을 입체적인 3D 오브젝트로 구체화(Synthesis)합니다.

---

## 📂 프로젝트 구조 (Directory Structure)

```text
body-map-vr-experience/
├── web/                  # 웹 클라이언트 (바디 맵 이미지 분석 및 전송)
│   ├── index.html        # 메인 웹 페이지 (Canvas 분석 알고리즘 내장)
│   ├── human_silhouette  # 기본 실루엣 이미지 (PNG/JPG)
│   └── README.md         # 웹 Pages 배포 가이드라인
│
├── unity/                # Unity Standalone 프로젝트 소스 (동료 협업용 코드)
│   ├── Assets/           # C# 스크립트, 씬, 리소스 파일
│   ├── ProjectSettings/  # Unity 프로젝트 설정 (PlayerSettings 등)
│   └── Packages/         # com.unity.cloud.gltfast 등의 패키지 정의
│
├── builds/               # [Git 제외] 빌드 독립 실행 파일 폴더 (G Drive 연동)
│   ├── Windows/          # Windows 64-bit 실행 파일 (.exe)
│   └── Mac/              # macOS 실행 파일 (.app Bundle)
│
└── README.md             # 프로젝트 통합 매뉴얼 (본 문서)
```

> [!NOTE]
> `builds/` 폴더는 용량 초과 문제(GitHub 100MB 제한)로 인해 Git 추적에서 제외(`.gitignore`)되어 있습니다. 완성된 단일 zip 패키지는 구글 드라이브를 통해 배포됩니다.

---

## 🔑 필수 API 키 설정 가이드 (API Keys Configuration)

본 프로젝트의 AI 음성 대화(Whisper, GPT-4o-mini, TTS) 및 3D 모델 실시간 생성(Tripo3D) 기능을 사용하기 위해서는 API 키 설정이 필수적입니다.

### 1. api_keys.json 생성 및 위치
프로젝트 루트 폴더(`unity/` 또는 빌드 폴더)에 아래 형식의 `api_keys.json` 파일을 생성하여 입력해야 합니다.

```json
{
  "OpenAI_API_Key": "YOUR_OPENAI_API_KEY_HERE",
  "Tripo3D_API_Key": "YOUR_TRIPO3D_API_KEY_HERE"
}
```

> [!WARNING]
> 이 파일은 API 개인 비밀키를 포함하므로 절대 GitHub 등 공공 저장소에 업로드(Commit)하지 마십시오. `.gitignore`에 의해 커밋 대상에서 제외됩니다.

---

## 🗺️ 멀티 씬 및 환경 선택 시스템 (Scenes & Environments)

이번 버전에 새로 추가된 **멀티 씬 라우팅 및 맵 선택 시스템** 구성입니다:

1. **BootstrapScene**: 게임 기동 시 최초 진입하며 필수 오디오 및 글로벌 매니저 클래스들을 초기화합니다.
2. **MapSelectionScene**: 사용자가 VR 치료를 진행할 가상 환경을 시각적으로 선택하는 UI 씬입니다.
   - **Japanese Garden (일본식 정원)**: 평화롭고 고요한 동양풍 정원 테마.
   - **Desert Oasis (사막 오아시스)**: 따뜻하고 아늑한 사막의 오아시스 테마.
3. **LoadingScene**: 대용량 가상 환경 에셋을 비동기식으로 안전하게 로드하고 로딩 진행 상태를 표시합니다.
4. **VRArtTherapyScene**: 실제 바디 맵이 스폰되고 AI 치료사와 마이크 음성 대화를 진행하는 핵심 메인 공간입니다.
5. **Env_URP_Garden / Env_URP_Desert**: 백그라운드로 로드되는 환경 전용 서브 씬들입니다.

---

## 🛠️ 바디 맵 자동 정렬 및 배치 최적화 (Placement Fixes)

스프라이트가 지형에 묻히거나 공중에 뜨는 현상을 해결하기 위해 **스마트 지형 안착 시스템**이 적용되었습니다:
- **방향 교정**: 바디 맵 스프라이트 소환 시 Y축을 180도 회전시켜 스폰과 동시에 항상 카메라/플레이어를 정면으로 바라보도록 수정되었습니다.
- **레이캐스트 바닥 밀착**: 스폰 시점 하단 방향으로 가상의 레이(Ray)를 발사하여 바닥 콜라이더 위치(`groundY`)를 동적으로 검출합니다.
- **경계 피팅**: 스프라이트의 크기 절반(`spriteHeight / 2f`)을 검출된 바닥 좌표에 더하여, 하단부 경계선이 완벽하게 가상 땅바닥에 닿은 채 서 있도록 고정했습니다.

---

## 🖥️ Standalone 빌드 버그 수정 및 안정화 패치 (Standalone Build Optimization)

이번 버전에 Standalone 빌드(Windows `.exe` / macOS `.app`)에서의 주요 버그 및 편의성 개선사항이 대거 수정 및 반영되었습니다:
- **TTS 오디오 빌드 무음 해결 (WAV 전환)**: 기존 `.mp3` 포맷의 TTS 응답을 무압축 PCM 방식의 `.wav` 포맷(OpenAI Speech API `"response_format": "wav"`)으로 변경하고 Unity에서 `AudioType.WAV`로 디코딩하도록 수정하였습니다. 이로 인해 빌드 플레이어 환경에서 오디오 코덱 부재로 소리가 나지 않던 현상이 완벽하게 해결되었습니다.
- **AI 헬퍼 구체(Sphere) Magenta 렌더링 수정 (URP 셰이더 적용)**: 빌드 시 컴파일되지 않던 레거시 Fresnel 셰이더 기반의 머티리얼(`OrbCore.mat`, `OrbAura.mat`)을 URP 표준 `Universal Render Pipeline/Lit` 셰이더로 마이그레이션하여 Standalone 환경에서도 깨지지 않고 영롱한 시안색 발광 구체(AI Companion)가 정상적으로 표시됩니다.
- **자막 스킵 방지 (대기 시간 로직 추가)**: TTS 오디오가 누락되거나 의도적으로 오디오 출력을 거치는 동안 자막이 바로 넘어가 버리는 현상을 방지하기 위해, 오디오 재생 실패 및 비질의(non-listening) 대화 구간에 글자 수에 따른 동적 가독 대기 시간(Reading Delay Fallback, 최소 2.5초 ~ 최대 7.5초)을 도입하였습니다.
- **API Key 로드 경로 다중화 (macOS 번들 대응)**: `api_keys.json`을 찾지 못해 OpenAI API 호출이 실패하던 현상을 해결하기 위해, macOS App Bundle의 내/외부 경로(`dataPath`, `GetCurrentDirectory`, `persistentDataPath` 등) 총 5곳의 후보군을 순차적으로 자동 탐색하는 로버스트 로직을 탑재했습니다.
- **마이크 입력 및 권한 획득 처리**: macOS 환경에서의 마이크 오기동 문제를 예방하기 위해 빌드 시 `Info.plist`에 `NSMicrophoneUsageDescription` 문구를 주입하도록 `BuildScript.cs`를 보강했으며, 씬 진입 시 플레이어에게 `Application.RequestUserAuthorization`을 통해 마이크 권한 동의 팝업을 명시적으로 요청하도록 개선했습니다.
- **자연스러운 사용자 이름 추출**: AI의 인사말에서 사용자 이름을 물어볼 때 전제 문장(예: "안녕 내 이름은 윤준우야")을 통째로 이름 필드에 저장하지 않고, `gpt-4o-mini`를 이용해 문맥상 이름 부분만 필터링하여 저장하는 인공지능 기반 이름 추출 프로세스(`ExtractNameRoutine`)를 추가했습니다.
- **첫 자막 가독성 확보**: 언어 선택 완료 후 인트로의 튜토리얼 대화창이 비활성화되거나 폰트 렌더링 문제로 보이지 않던 이슈를 해결하기 위해 시작 시 패널 활성화 상태를 강제하고 `ApplyKoreanFont`를 초기 텍스트 컴포넌트 전체에 선제 적용하였습니다.

---

## 🤝 다른 Unity 프로젝트와의 합치기/협업 가이드 (Merge Guide)

동료분이 본인의 작업 중인 Unity 프로젝트에 본 플랫폼 기능들을 이식하고 병합(Merge)할 때, 아래 단계에 따라 안전하게 합칠 수 있습니다:

### Step 1. 리소션 패키지 설치
본 프로젝트의 3D 실시간 생성 및 로드(GLTFast)를 위해 다음 패키지가 대상 프로젝트에 설치되어 있어야 합니다.
- Unity Package Manager에서 **gltfast** 패키지를 설치합니다 (`com.unity.cloud.gltfast`).

### Step 2. 수정 및 추가된 Assets 복사
GitHub 저장소의 `unity/Assets/` 디렉토리 아래 파일들을 작업 중인 Unity 프로젝트의 `Assets/` 폴더에 복사해 덮어씌웁니다.

#### 💡 필수 핵심 스크립트 파일 목록:
1. **바디 맵 생성 및 지면 밀착**: `Assets/Scripts/BodyMapReceiver.cs`
2. **인게임 편집 및 스프라이트 병합**: `Assets/Scripts/InteractiveRegion3D.cs`
3. **멀티 플랫폼 빌드 파이프라인**: `Assets/Editor/BuildScript.cs` (micro-description 수정본 포함)
4. **AI 통신 및 제어**: `Assets/Scripts/BodyMapAIController.cs` & `PromptToPython.cs`
5. **맵 선택 및 시스템 매니저**:
   - `Assets/Scripts/MapSelectionManager.cs`
   - `Assets/Scripts/GameManager.cs`
   - `Assets/Scripts/SceneFlowManager.cs`
   - `Assets/Scripts/LoadingScreenManager.cs`

#### 💡 필수 씬(Scene) 복사:
* `Assets/Scenes/` 하위의 모든 `.unity` 및 `.meta` 파일 (특히 `BootstrapScene.unity`, `MapSelectionScene.unity`, `LoadingScene.unity`, `VRArtTherapyScene.unity`)

### Step 3. 빌드 세팅 구성
프로젝트 빌드 시 Unity 메뉴 `File` -> `Build Settings`로 이동하거나 `BuildScript.PerformBuilds()`를 활용해 아래 순서대로 6개의 씬이 빌드 목록에 포함되도록 등록해 주어야 합니다.
1. `Assets/Scenes/BootstrapScene.unity` (0번 인덱스 - 시작 씬)
2. `Assets/Scenes/MapSelectionScene.unity`
3. `Assets/Scenes/LoadingScene.unity`
4. `Assets/Scenes/VRArtTherapyScene.unity`
5. `Assets/Scenes/Env_URP_Garden.unity`
6. `Assets/Scenes/Env_URP_Desert.unity`

---

## 📥 최신 단일 빌드 다운로드 (Downloads via Google Drive)

표준 PKZIP 규격으로 빌드되어 기본 탐색기 및 Finder에서 안정적으로 열리는 통합 단일 실행 압축 파일입니다.

* **Windows 64-bit 패키지**: [BodyMapVR_Windows.zip (Google Drive)](file:///G:/내%20드라이브/BodyMapVR_Windows.zip)
* **macOS Standalone 패키지**: [BodyMapVR_Mac.zip (Google Drive)](file:///G:/내%20드라이브/BodyMapVR_Mac.zip)

### 🍏 macOS 실행 권한 및 격리 해제 (Gatekeeper / Quarantine Bypass)
macOS에서 인터넷(구글 드라이브 등)을 통해 다운로드한 빌드 파일(`BodyMapVR_Mac.zip`)을 압축 해제하고 실행할 경우, 게이트키퍼 보안 정책에 의해 "앱이 손상되었거나 개발자를 확인할 수 없어 열 수 없다"는 에러가 발생합니다.

이 문제를 해결하기 위해 앱을 실행하기 전 터미널(Terminal)을 열고 다음 명령어를 순서대로 입력해 주세요.

1. **압축이 해제된 폴더로 이동**:
   ```bash
   cd /path/to/extracted/folder
   ```
   *(압축을 푼 폴더명을 터미널에 드래그 앤 드롭하면 경로가 자동으로 입력됩니다.)*

2. **애플 격리(Quarantine) 속성 제거**:
   ```bash
   xattr -cr BodyMapVR.app
   ```

3. **앱 내부 실행 파일 권한 부여**:
   ```bash
   chmod -R +x BodyMapVR.app
   ```
