# 감정 구체화 파이프라인 — 데이터 연동 안내

> **대상:** 바디맵(이미지 분석) · 대화(STT/채팅) · Unity 프론트 팀  
> **프로젝트:** `DEV_objectify`  
> **목적:** 앞단에서 뽑은 정보를 구체화 파이프라인에 연결하기 위한 계약(Contract) 정리

---

## 1. 목적

미술치료 세션에서 **바디맵 + 대화** 정보를 받아, OpenAI 이미지 생성 → (선택) 배경 제거 → Meshy 3D → Unity 씬 표시까지 이어지는 파이프라인입니다.

**연결해야 할 구간은 파이프라인 입구입니다.**  
바디맵 업로드·색상/패턴 분석이 끝나면, 아래 형식으로 `StartPipeline`에 넘기면 됩니다.

---

## 2. 파이프라인 진입점 (연결 API)

Unity 씬의 `EmotionObjectificationPipeline` 컴포넌트:

```csharp
void StartPipeline(
    List<BodyMapAIController.MessageData> conversation,
    InteractiveRegion3D region
);
```

| 인자 | 설명 |
|------|------|
| `conversation` | 치료사–참가자 대화 로그 (`role`, `content`) |
| `region` | **선택된 신체 영역 1개**의 바디맵 메타데이터 |
| `currentUserId` | Inspector 또는 코드 설정 → `UserData/{userId}/` 저장 경로 |

**로컬 테스트:** `PipelineTestHelper` → 인스펙터 우클릭 **「파이프라인 시뮬레이션 시작」** (동일 인터페이스).

**관련 파일**

| 파일 | 역할 |
|------|------|
| `Assets/Scripts/ArtTherapy/EmotionObjectificationPipeline.cs` | 오케스트레이터 |
| `Assets/Scripts/ArtTherapy/EmotionDataModel.cs` | 데이터 타입 정의 |
| `Assets/Scripts/ArtTherapy/PromptGenerator.cs` | GPT 프롬프트 생성 |
| `Assets/Scripts/ArtTherapy/OpenAIImageGenerator.cs` | 이미지 API |
| `Assets/Scripts/ArtTherapy/Meshy3DConverter.cs` | 3D 변환 |
| `Assets/Scripts/ArtTherapy/UserDataPaths.cs` | 저장 경로 |

---

## 3. 저장되는 JSON 구조

1단계 직후 및 파이프라인 완료 시:

`UserData/{userId}/prompts/region_{id}.json`

### 3.1 `from_body_mapping` — **바디맵 팀이 채워야 함**

| 필드 | 의미 | 매핑 (`InteractiveRegion3D`) |
|------|------|------------------------------|
| `color` | 색 이름 (예: 붉은색) | `colorName` |
| `color_hex` | HEX (예: #D93A3A) | `colorHex` |
| `pattern` | 패턴 (예: 나선형 소용돌이) | `pattern` |
| `body_part` | 신체 부위 (예: chest) | `bodyLocation` |
| `visual_description` | 영역 설명 | `description` |

> **중요:** 이 파이프라인은 **바디맵 원본 이미지를 직접 분석하지 않습니다.**  
> `InteractiveRegion3D`에 값이 들어와야 JSON에 기록되고, GPT·이미지 프롬프트에도 반영됩니다.  
> **앞단(업로드 + 색상/패턴 분석) → `InteractiveRegion3D` 생성**이 연동의 핵심입니다.

### 3.2 `from_conversation` — **GPT가 대화(+바디맵 힌트)에서 추출**

| 필드 | 의미 | 출처 |
|------|------|------|
| `emotion` | 감정 요약 | GPT |
| `texture` | 촉각/질감 | GPT |
| `metaphor` | 비유 | GPT |
| `material` | 토이 재질 (영문) | GPT |
| `surface` | 표면 표현 | GPT |
| `motion` | 자세/움직임 | GPT |
| `weight` | 무게감 | GPT |
| `temperature` | 온도감(색/분위기) | GPT |

GPT 요청 시 **대화 transcript**와 **바디맵 텍스트**를 함께 넣습니다.  
JSON에서는 `from_body_mapping` / `from_conversation`으로 **저장 출처만** 나뉩니다.

### 3.3 파이프라인이 추가 생성하는 필드

| 필드 | 설명 |
|------|------|
| `image_prompt` | 최종 이미지 API용 영문 프롬프트 |
| `assets.image_path` | `images/region_{id}.png` |
| `assets.image_rmb_path` | `images/region_{id}_rmb.png` (배경 제거) |
| `assets.model_path` | `models/region_{id}.glb` (`imageOnlyMode` 꺼짐일 때) |

---

## 4. 데이터 흐름

```
[바디맵 팀 — 연결 예정]
  원본 이미지 업로드 → 색/패턴/영역 분석
       ↓
  InteractiveRegion3D {
    id, colorName, colorHex, pattern, bodyLocation, description
  }

[대화 팀]
  STT / 채팅 로그
       ↓
  List<MessageData> { role: "user"|"assistant", content }

       ↓
  EmotionObjectificationPipeline.StartPipeline(conversation, region)

[구체화 파이프라인 — 구현됨]
  ① GPT: 프롬프트 + from_conversation 생성
  ② OpenAI Images: region_{id}.png
  ②.5 배경 제거: region_{id}_rmb.png
  ③ Meshy: _rmb.png → region_{id}.glb (전체 모드)
  ④ Unity ObjectSpawner: 2D 미리보기 / 3D 스폰
```

---

## 5. 연동 타입 스펙 (복사용)

### `InteractiveRegion3D`

```csharp
public class InteractiveRegion3D
{
    public int id;               // region_{id}.json과 동일 권장
    public string colorName;     // 분석된 색 이름
    public string colorHex;      // 분석된 HEX (#RRGGBB)
    public string pattern;       // 분석된 패턴 라벨
    public string bodyLocation;  // 신체 부위 (영문 slug 권장: chest, abdomen …)
    public string description;   // 영역 시각 설명
}
```

| 우선순위 | 필드 |
|----------|------|
| **MVP** | `id`, `colorName`, `colorHex`, `pattern` |
| **권장** | + `bodyLocation`, `description` (프롬프트·3D 배치 품질에 영향) |

### `BodyMapAIController.MessageData`

```csharp
public class MessageData
{
    public string role;    // "user" | "assistant"  ("system"은 GPT 입력에서 제외됨)
    public string content;
}
```

### 사용자 ID

- `EmotionObjectificationPipeline.currentUserId` 또는 `UserDataPaths.SetCurrentUser(userId)`
- 형식: `user_{id}` (예: `test1` → `user_test1`)
- 저장 루트 (에디터): `{프로젝트}/UserData/{userId}/`

---

## 6. 연동 체크리스트

- [ ] 참가자가 **한 영역**을 선택·확정한 뒤 해당 `region`으로 `StartPipeline` 호출
- [ ] `region.id`와 파일명 `region_{id}` 일치
- [ ] `currentUserId`가 로그인 사용자와 동기화
- [ ] 동일 세션의 `conversation` 리스트를 함께 전달
- [ ] 3D까지 필요 시 `imageOnlyMode = false` (Inspector)
- [ ] 결과 확인: `UserData/{userId}/prompts/`, `images/`, `models/`

---

## 7. 이미지 프롬프트 참고 (바디맵 팀 선택)

Style Bible·레퍼런스 이미지는 `Assets/style_bible/` · `Assets/objectification_config.json`에서 관리합니다.

바디맵에서 넘긴 값이 프롬프트에 쓰이는 방식:

| 입력 | 사용처 |
|------|--------|
| `colorName`, `colorHex` | GPT user 메시지 + 최종 `image_prompt`의 color accent |
| `pattern`, `description` | GPT user 메시지 (감정·형태 해석 힌트) |
| 대화 | GPT → `from_conversation` + `image_prompt` 슬롯 |

상세: `PromptGenerator.BuildProductionImagePrompt()` — 고정 토이 시리즈 문구 + GPT 변동 슬롯 조립.

---

## 8. 미구현 / 오해 방지

| 항목 | 상태 |
|------|------|
| 바디맵 **원본 PNG**를 파이프라인이 Vision으로 직접 분석 | ❌ |
| `from_body_mapping` 자동 추출 | ❌ → 앞단에서 `InteractiveRegion3D` 채우기 |
| 웹/Firebase 업로드 | ❌ 현재 로컬 `UserData/` |

---

## 9. 연동 구현 제안 (다음 스프린트)

1. 바디맵 분석 완료 콜백에서 `InteractiveRegion3D` 인스턴스 생성  
2. 대화 모듈에서 `List<MessageData>` 준비  
3. Unity에서 `EmotionObjectificationPipeline.StartPipeline(...)` 호출  
4. (선택) `OnPipelineSuccess` / `OnPipelineError`로 UI 피드백  

```csharp
// 예시 (의사 코드)
var region = new InteractiveRegion3D {
    id = selectedRegionId,
    colorName = analysis.ColorName,
    colorHex = analysis.ColorHex,
    pattern = analysis.Pattern,
    bodyLocation = analysis.BodyPart,
    description = analysis.Description
};
pipeline.currentUserId = UserDataPaths.NormalizeUserId(participantId);
pipeline.StartPipeline(conversationMessages, region);
```

---

## 10. 한 줄 요약 (슬랙/노션용)

> **바디맵 팀은 원본 이미지 분석 후 `InteractiveRegion3D`(색·패턴·부위·설명)를 채워 주고, 대화 팀은 `MessageData` 리스트를 넘기면 됩니다. 구체화 파이프라인은 그 두 입력만 받아 GPT → 이미지 → rmb → Meshy까지 처리하며, 바디맵 이미지 파일 자체는 아직 읽지 않습니다.**

---

*문서 버전: 2026-05-27 · 문의: 구체화(Unity) 담당*
