# Body Map VR Experience (Art Therapy → AI Conversation & 3D Synthesis)

본 프로젝트는 미술 치료(Art Therapy)의 바디 매핑(Body Mapping) 기법을 가상현실(VR) 및 AI 기술과 융합한 인터랙티브 심리 치료 플랫폼입니다. 
사용자가 웹에서 그린 감정 그림을 실시간으로 분석하여 몸의 부위별 감정을 추출하고, 이를 Unity 3D/VR 환경에 띄워 대화형 AI(Therapist)와 음성으로 대화하며 감정을 입체적인 3D 오브젝트로 구체화(Synthesis)합니다.

---

## 📂 프로젝트 구조 (Directory Structure)

```text
body-map-vr-experience/
├── web/                  # 웹 클라이언트 (바디 맵 이미지 분석 및 전송)
│   ├── index.html        # 메인 웹 페이지 (Canvas 분석 알고리즘 내장)
│   ├── human_silhouette  # 기본 슬렌더 실루엣 이미지 (PNG/JPG)
│   └── README.md         # 웹 Pages 배포 가이드라인
│
├── unity/                # Unity Standalone 프로젝트 소스
│   ├── Assets/           # C# 스크립트, 씬, 리소스 파일
│   ├── ProjectSettings/  # Unity 프로젝트 설정 (PlayerSettings 등)
│   └── Packages/         # com.unity.cloud.gltfast 등의 패키지 정의
│
├── builds/               # 빌드된 독립 실행형 실행 파일 폴더
│   ├── Windows/          # Windows 64-bit 실행 파일 (.exe)
│   └── Mac/              # macOS 실행 파일 (.app Bundle)
│
└── README.md             # 프로젝트 통합 매뉴얼 (본 문서)
```

---

## 🔑 필수 API 키 발급 및 설정 가이드 (API Keys Configuration)

본 프로젝트의 AI 음성 대화(Whisper, GPT-4o-mini, TTS) 및 3D 모델 실시간 생성(Tripo3D) 기능을 사용하기 위해서는 API 키 설정이 필수적입니다.

### 1. api_keys.json 템플릿 생성
프로젝트의 실행 파일과 동일한 디렉토리(빌드 폴더) 및 Unity 프로젝트 루트 폴더에 아래 형식의 `api_keys.json` 파일을 작성해야 합니다. (기본적으로 첫 실행 시 템플릿이 자동 생성됩니다.)

```json
{
  "OpenAI_API_Key": "YOUR_OPENAI_API_KEY_HERE",
  "Tripo3D_API_Key": "YOUR_TRIPO3D_API_KEY_HERE"
}
```

> [!WARNING]
> 이 파일은 API 개인 비밀키를 포함하므로 절대 GitHub 등 공공 저장소에 업로드(Commit)하지 마십시오. `.gitignore`에 자동 추가되어 커밋 대상에서 제외됩니다.

---

### 2. API 키 발급 방법 (How to Get API Keys)

#### (1) OpenAI API Key (음성 인식, 심리 대화, 음성 합성)
OpenAI API는 사용자의 음성을 텍스트로 받아 적고(Whisper), 공감 능력을 가진 미술 치료사 역할을 수행하며(GPT-4o-mini), 답변을 실감 나는 한국어 음성으로 출력(TTS)하는 데 사용됩니다.

1. **OpenAI 개발자 플랫폼 가입**: [OpenAI API 플랫폼](https://platform.openai.com/)에 접속하여 회원 가입을 진행합니다.
2. **결제 수단 등록 (Billing)**: API 사용을 위해선 충전식 결제 카드 등록이 필요합니다.
   - 좌측 메뉴에서 **Settings** ➔ **Billing**으로 이동합니다.
   - **Add funds**를 클릭하여 최소 5달러 상당의 잔액을 충전합니다. (사용량에 따라 소액 차감되는 구조입니다.)
3. **API 키 생성**:
   - 좌측 메뉴에서 **API Keys** ➔ **Create new secret key**를 선택합니다.
   - 키 이름(예: `BodyMapVR`)을 입력하고 생성합니다.
   - 생성된 키(예: `sk-proj-...`)는 **최초 1회만 화면에 표시되므로 복사하여 안전한 곳에 즉시 저장**하고, `api_keys.json` 파일의 `OpenAI_API_Key` 항목에 붙여넣습니다.

#### (2) Tripo3D API Key (텍스트 기반 실시간 3D 감정 오브젝트 생성)
사용자와 AI의 대화가 무르익으면, AI가 대화 내용을 요약해 영어 프롬프트를 생성하고 이를 Tripo3D API로 보내 2D 감정 영역을 완전히 입체적인 3D 에셋(.GLB)으로 실시간 생성하여 공간에 소환합니다.

1. **Tripo3D 개발자 포털 가입**: [Tripo3D Developer Portal](https://platform.tripo3d.ai/)에 접속하여 가입합니다.
2. **크레딧 확인**: 신규 계정 가입 시 일정량의 무료 크레딧이 충전되어 즉시 테스트할 수 있습니다.
3. **API 키 발급**:
   - 대시보드 또는 설정 메뉴의 **API Keys** 탭으로 이동합니다.
   - **Create New Key**를 클릭하여 고유 키를 발급받습니다.
   - 생성된 키를 `api_keys.json` 파일의 `Tripo3D_API_Key` 항목에 붙여넣습니다.

---

## 🎮 인게임 조작 및 시스템 가이드 (Controls & System Guide)

Unity 빌드 실행 파일(Windows/Mac)을 실행한 뒤의 인게임 조작 방법입니다.

### 1. 감정 부위 탐색 및 대화 시작
- **이동 및 카메라**: `W/A/S/D` 키로 이동하고, 마우스 움직임으로 시점을 회전합니다. (처음 실행 시 마우스 커서가 자동으로 중앙 조준점(Aim Dot)에 고정됩니다.)
- **감정 영역 선택**: 화면 중앙에 떠 있는 감정 조각(2D 스프라이트) 조준 후 **마우스 좌클릭** 또는 **`E` 키**를 누릅니다.
  - 선택되면 해당 감정 조각에 **파란색 하이라이트**가 켜지고, 화면 우측에 **AI 대화창(Chat Log Panel)**이 나타납니다.
  - 화면 하단 중앙에는 Siri 스타일의 다채로운 **AI 반응형 구체(Siri Orb)**가 부드럽게 호흡하며 활성화됩니다.

---

### 2. Siri 모티브 AI 음성 대화 (하이브리드 V 키 조작)
대화가 켜져 있을 때 지속적인 마이크 스트리밍으로 인한 불필요한 API 요금 폭탄을 완벽히 방지하기 위해 **하이브리드 조작 방식** 및 **비용 최적화 알고리즘**이 적용되어 있습니다.

- **V 키 홀드 (Push-to-Talk)**: `V` 키를 꾹 누르고 있는 동안 마이크로 녹음이 진행되며, `V` 키를 떼는 순간 녹음이 종료되고 AI에게 전송됩니다.
- **V 키 탭 (Toggle-to-Talk)**: `V` 키를 0.35초 미만으로 가볍게 톡 누르면 마이크가 켜져 켜진 상태가 유지되고, 말을 마친 뒤 다시 `V` 키를 누르면 녹음이 중단되어 전송됩니다.
- **무음 자동 취소 (Silence Check - 비용 절감)**: 마이크가 실수로 켜지거나 아무 말도 하지 않은 상태에서 녹음이 완료될 경우, 시스템이 실시간으로 입력 볼륨(RMS)을 분석하여 **Whisper/GPT API 요청을 즉각 취소(Abort)**합니다. (불필요한 비용 발생 원천 차단)
- **컨텍스트 창 제한 (Rolling Window - 비용 절감)**: 대화가 10턴 이상 길어지더라도 입력 토큰 수가 무한정 늘어나 요금이 폭증하지 않도록, **최근 4회의 대화(8개 메시지)만 기억하는 슬라이딩 윈도우 큐**를 적용하여 대당 API 비용을 최소화합니다.

---

### 3. Siri 반응형 구체 (Siri Orb Anim) 애니메이션 피드백
화면 하단 중앙의 구체는 4개의 반투명 레이어(Cyan, Purple, Pink, Core)로 이루어져 있으며, AI의 상태에 따라 동적으로 반응합니다.
- **대기 상태 (Idle)**: 부드럽게 서로 다른 속도로 회전하고 물결치듯 천천히 커졌다 작아지는 호흡 애니메이션을 보여줍니다.
- **듣는 중 (Listening)**: 연둣빛/청록색으로 빛나며 사용자의 목소리(마이크 볼륨) 진폭에 실시간으로 격렬히 반응하여 크기가 조절됩니다.
- **생각 중 (Thinking)**: 주황빛/황금색으로 소용돌이치며 3D 소환 및 처리 과정을 표현합니다.
- **말하는 중 (AI Speaking)**: AI의 아웃풋 스피커 음량 진폭을 그대로 시각화하여 보랏빛 구체가 리드미컬하게 요동칩니다.

---

### 4. 실시간 3D 구체화 (Text-to-3D Synthesis)
- AI 대화방에서 부위의 감정 분석 및 대화가 완료되면(보통 3~4턴 대화 후), AI가 자동으로 해당 감정의 메타포를 시각화한 영어 프롬프트를 합성합니다. (예: `[[3D Prompt: an abstract spiky black sphere with red cracks representing anxiety]]`)
- 이 태그가 감지되면 Unity 백그라운드에서 Tripo3D API 작업이 비동기적으로 호출됩니다.
- 모델 생성이 성공적으로 끝나면, 실시간으로 `.GLB` 에셋을 로드(GLTFast 모듈 사용)하여 공중에 떠 있는 2D 스프라이트 자리에 물리적인 **3D 감정 모델을 생성하고 2D 이미지를 대체**합니다.

---

### 5. 인게임 편집 모드 (Edit Mode)
- 언제든 **`Tab` 키**를 누르면 편집 모드로 진입합니다. (마우스 락이 해제되어 커서가 나타납니다.)
- **다중 선택**: 여러 감정 조각들을 마우스로 클릭하여 파란색 테두리로 다중 지정할 수 있습니다.
- **합치기 (Merge)**: 두 개 이상의 조각을 선택한 후 **`M` 키**를 누르면, 해당 조각들의 위치와 텍스처를 픽셀 단위로 완벽하게 합성하여 하나의 단일 감정 영역으로 통합합니다.
- **삭제 (Delete)**: 불필요한 감정 조각을 선택한 후 **`Delete` 키** 또는 **`Backspace` 키**를 누르면 해당 감정이 씬에서 삭제됩니다.
- **종료**: 다시 **`Tab` 키**를 누르면 마우스가 다시 락인되며 탐색/대화 모드로 복귀합니다.
