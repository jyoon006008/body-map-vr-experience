from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
import base64
import json
import os
import time
import urllib.request
import urllib.error

ROOT = os.path.dirname(os.path.abspath(__file__))
API_KEYS_PATH = r"C:\Users\junwo\AI_Explorable_Test\api_keys.json"
MODEL = "gpt-4o-mini"
IMAGE_MODELS_TO_TRY = ["gpt-image-1.5", "gpt-image-1"]
GENERATED_DIR = os.path.join(ROOT, "generated")
OBJECT_REFERENCE_IMAGE_PATHS = [
    os.path.join(ROOT, "jemmy-prototype-assets", "ref01.png"),
    os.path.join(ROOT, "jemmy-prototype-assets", "ref02.png"),
    r"C:\Users\junwo\Downloads\ref01.png",
    r"C:\Users\junwo\Downloads\ref02.png",
]
BODY_MAP_SAMPLE_PAYLOAD_PATH = os.path.join(ROOT, "jemmy-prototype-assets", "body_map_payload.json")
BODY_MAP_SAMPLE_ANNOTATED_PATH = os.path.join(ROOT, "jemmy-prototype-assets", "body_map_annotated.png")
BODY_MAP_SAMPLE_TRANSPARENT_PATH = os.path.join(ROOT, "jemmy-prototype-assets", "body_map_transparent.png")

BODY_MAP_SAMPLE_REGION_OVERRIDES = {
    0: {"color_name": "black", "body_location": "face", "emotion_label": "spiral thought", "visual_shape": "spiral", "description": "A black spiral mark covering the face area."},
    1: {"color_name": "green", "body_location": "left arm", "emotion_label": "held tension", "visual_shape": "zigzag scribble", "description": "A green zigzag-like scribble running down the left arm."},
    2: {"color_name": "red", "body_location": "torso", "emotion_label": "central agitation", "visual_shape": "zigzag scribble", "description": "A red zigzag scribble through the center of the torso."},
    3: {"color_name": "blue", "body_location": "right arm", "emotion_label": "heavy guarded pressure", "visual_shape": "cross hatch", "description": "A blue cross-hatched block covering the right shoulder and arm."},
    4: {"color_name": "blue", "body_location": "right leg", "emotion_label": "sharp downward pull", "visual_shape": "zigzag", "description": "A blue zigzag mark on the right lower leg."},
    5: {"color_name": "orange brown", "body_location": "left upper leg", "emotion_label": "restless weight", "visual_shape": "zigzag", "description": "An orange-brown zigzag mark on the left upper leg."},
    6: {"color_name": "yellow", "body_location": "left lower leg", "emotion_label": "thin tired energy", "visual_shape": "parallel lines", "description": "Yellow vertical lines running down the left lower leg."},
    7: {"color_name": "purple", "body_location": "right leg and foot", "emotion_label": "spiraling stuckness", "visual_shape": "spiral lines", "description": "Purple spiral lines around the right lower leg and foot."},
}


def load_openai_key():
    env_key = os.environ.get("OPENAI_API_KEY") or os.environ.get("OpenAI_API_Key")
    if env_key:
        return env_key
    with open(API_KEYS_PATH, "r", encoding="utf-8-sig") as f:
        data = json.load(f)
    key = data.get("OpenAI_API_Key") or data.get("openai_api_key") or data.get("OPENAI_API_KEY")
    if not key or "YOUR_" in key:
        raise RuntimeError("OpenAI API key is missing in api_keys.json")
    return key


def load_meshy_key():
    env_key = os.environ.get("MESHY_API_KEY") or os.environ.get("meshy_api_key") or os.environ.get("Meshy_API_Key")
    if env_key:
        return env_key
    with open(API_KEYS_PATH, "r", encoding="utf-8-sig") as f:
        data = json.load(f)
    key = data.get("meshy_api_key") or data.get("Meshy_API_Key") or data.get("MESHY_API_KEY")
    if not key or "YOUR_" in key:
        raise RuntimeError("Meshy API key is missing in api_keys.json")
    return key


def extract_text(response_json):
    if response_json.get("output_text"):
        return response_json["output_text"]
    parts = []
    for item in response_json.get("output", []):
        for content in item.get("content", []):
            if content.get("type") in ("output_text", "text"):
                parts.append(content.get("text", ""))
    return "\n".join(part for part in parts if part).strip()


def build_panorama_prompt(description):
    return (
        "A calming 360 equirectangular panorama safe place for VR art therapy, immersive but gentle, "
        "soft natural light, quiet atmosphere, no people, no text, seamless horizon, based on: "
        + description
    )


def build_instructions(stage, personality, ready_to_continue, stage_user_count):
    return f"""
You are Jemmy, a kind, warm psychotherapist-like AI companion for a VR art therapy process.
Speak in natural Korean only.
Do not sound like a rigid script. Be warm, human-like, concise, and responsive.
Do not over-interpret or diagnose the user.
Briefly reflect what the user said, then ask at most one gentle question.

Current stage: {stage}
Personality: {json.dumps(personality, ensure_ascii=False)}
Stage ready_to_continue: {ready_to_continue}
Number of user messages in this stage after the latest message: {stage_user_count}

Stage behavior:
- Intro is not a chat stage.
- Small Talk: this stage is only ice breaking. First learn the user's preferred name, then ask how their day was, then ask what feeling remains. For the first user reply, extract only the preferred name and reply like "만나서 반가워요, [name]님. 오늘 하루는 어떠셨나요?" Do not set readyToContinue true before at least 3 user messages in Small Talk. After the user answers what feeling remains, close the small talk warmly and set readyToContinue true. You may say that the next stage will imagine a comfortable place, but DO NOT ask any safe-place content question in Small Talk. Forbidden Small Talk questions: "어떤 공간인가요?", "어떤 모습인가요?", "어떤 소리나 냄새가 날까요?", "빛이나 색감은 어떤가요?", or any question about place/space/sensory details. Those belong only to Safe Place.
- Safe Place: help imagine a comforting place with enough detail for panorama generation. At stage entry, the UI first gives the user about 8 seconds to imagine before asking "그곳은 어떤 공간인가요?" Do not rush the user. If the user says they have not thought of one yet, cannot imagine yet, need more time, or are unsure, reply gently like "천천히 생각해보세요. 떠오르실 때 편하게 말해주세요." Do not ask a new content question in that reply and do not set readyToContinue true. Once a place appears, gather details in this order, one question at a time: (1) what kind of place/space it is, (2) what can be seen there and what is nearby, (3) light, time of day, weather, and color palette, (4) sound, scent, wind, temperature, or texture, (5) how the user's body/mind feels there. Avoid generic repeated questions like "어떤 느낌이 드나요?" if a more concrete panorama detail is missing. Do not set readyToContinue true until the place is clear and at least 2 concrete sensory/visual details have been collected, usually after 4 user replies in Safe Place. When ready, reply exactly in this form: "좋아요. 지금까지 들려주신 안전한 장소의 모습과 감각들을 바탕으로 편안하게 머물 수 있는 공간을 만들어볼게요. 잠시만 기다려 주세요." Fill safePlaceDescription with a concise visual summary for panorama generation. If the user keeps talking after readyToContinue, respond naturally, but do not repeat the same kind of sensory question.
- Body Mapping follows the existing Unity flow. First ask about the whole body map impression, like Unity's line: "다양한 색과 형태를 사용하셨네요. 먼저 전체 그림이 어떤 느낌인지 설명해 주실 수 있을까요?" After a region is selected, continue like Unity's region greeting: "이제 이 감정 영역에 대해 이야기해 볼까요? 이 부분은 어떤 감정에 가까운지 말해 주세요." Do not make the body mapping stage feel like a technical form.
- Region Reflection is not a separate user-facing stage here; it happens inside Body Mapping after a body-map region is selected. Keep Unity's natural therapeutic conversation flow, while quietly collecting the fields required by Jieun's objectification pipeline contract.

Body Mapping / Region Reflection data contract:
- from_body_mapping is supplied by the body-map analysis / InteractiveRegion3D: region_id, body_part, color_name, color_hex, pattern, visual_description.
- from_conversation must be extracted from the therapeutic dialogue: emotion, texture, metaphor, surface, motion, weight, temperature, material.

Unity-style conversation guide for a selected region:
- Ask one open, child-friendly, therapist-like question at a time.
- Prefer this order, but follow the user's words naturally: emotion -> texture/material feeling -> surface/detail/shape -> motion/energy -> weight/temperature -> metaphor/symbol.
- Required MVP fields: emotion, texture, motion, temperature, metaphor.
- Optional fields: weight, surface, material.
- Use the selected body mapping metadata as context only: color, color_hex, pattern, body_part, visual_description.
- Good natural questions include: "이 부분은 어떤 감정에 가까운가요?", "만질 수 있다면 어떤 촉감일까요?", "표면이나 모양은 어떤 느낌인가요?", "가만히 있는 느낌인가요, 움직이는 느낌인가요?", "손에 올려본다면 무겁거나 가벼울까요? 따뜻한가요, 차가운가요?", "이 감정을 물건이나 작은 존재로 비유하면 무엇과 닮았을까요?"
- If the user says an object/substance such as jelly, stone, clay, cotton, plastic, ceramic, glass, metal, smoke, water, or fabric, keep the full symbolic phrase in metaphor when appropriate, and also extract the substance as material.
- Never say "I need this for 3D generation" to the user. Keep data collection behind the scenes.
- If the user has already answered a field, do not ask the same thing again. Ask the next missing field naturally.
- If a strong feeling appears, prioritize safety, distance, and grounding over data collection.
- Set readyToContinue true only when at least these MVP fields are reasonably collected: emotion, texture, motion, temperature, metaphor. If conversation becomes long, conclude gently once at least 3 strong categories are collected.

Return ONLY valid JSON with this shape:
{{
  "reply": "Jemmy's reply in Korean",
  "readyToContinue": true or false,
  "extractedName": "name if newly learned, otherwise empty string",
  "safePlaceDescription": "safe place summary if relevant, otherwise empty string",
  "bodyMapSummary": "body map summary if relevant, otherwise empty string",
  "conversationExtraction": {{
    "emotion": "",
    "texture": "",
    "metaphor": "",
    "material": "",
    "surface": "",
    "motion": "",
    "weight": "",
    "temperature": "",
  }},
  "objectPrompt": "creative output prompt if relevant, otherwise empty string"
}}
"""


def call_openai(payload):
    api_key = load_openai_key()
    messages = payload.get("messages", [])
    stage_user_count = sum(1 for msg in messages if msg.get("who") == "user")
    if payload.get("stage", "") == "safePlace" and is_safe_place_thinking_delay(payload.get("text", "")):
        return {
            "reply": "괜찮아요. 천천히 생각해보세요. 떠오르실 때 편하게 말해주세요.",
            "readyToContinue": False,
            "extractedName": "",
            "safePlaceDescription": "",
            "bodyMapSummary": "",
            "conversationExtraction": {},
            "objectPrompt": "",
        }
    body = {
        "model": MODEL,
        "instructions": build_instructions(
            payload.get("stage", ""),
            payload.get("personality", {}),
            payload.get("readyToContinue", False),
            stage_user_count,
        ),
        "input": json.dumps(
            {
                "currentUserText": payload.get("text", ""),
                "messages": payload.get("messages", []),
                "outputs": payload.get("outputs", {}),
                "userName": payload.get("userName", ""),
                "regionProfile": payload.get("regionProfile", {}),
                "bodyMappingMeta": payload.get("bodyMappingMeta", {}),
            },
            ensure_ascii=False,
        ),
        "temperature": 0.75,
    }
    data = json.dumps(body, ensure_ascii=False).encode("utf-8")
    req = urllib.request.Request(
        "https://api.openai.com/v1/responses",
        data=data,
        headers={
            "Authorization": f"Bearer {api_key}",
            "Content-Type": "application/json",
        },
        method="POST",
    )
    with urllib.request.urlopen(req, timeout=60) as resp:
        raw = resp.read().decode("utf-8")
    text = extract_text(json.loads(raw))
    try:
        result = json.loads(text)
    except json.JSONDecodeError:
        start = text.find("{")
        end = text.rfind("}")
        if start >= 0 and end > start:
            result = json.loads(text[start : end + 1])
        else:
            result = {
            "reply": text,
            "readyToContinue": False,
            "extractedName": "",
            "safePlaceDescription": "",
            "bodyMapSummary": "",
            "conversationExtraction": {},
            "objectPrompt": "",
            }
    return normalize_stage_result(payload, result, stage_user_count)


def normalize_stage_result(payload, result, stage_user_count):
    stage = payload.get("stage", "")
    if stage == "smallTalk" and stage_user_count >= 3 and not payload.get("readyToContinue", False):
        result["readyToContinue"] = True
        result["extractedName"] = ""
        result["safePlaceDescription"] = ""
        result["reply"] = (
            "이야기해 주셔서 고마워요. 그 감정이 지금 남아 있다는 걸 함께 확인했어요. "
            "본격적으로 감정을 탐색하기 전에, 준비가 되면 편안함을 느낄 수 있는 공간을 떠올리는 단계로 넘어가 볼게요."
        )
    if stage == "safePlace" and is_safe_place_thinking_delay(payload.get("text", "")):
        result["readyToContinue"] = False
        result["safePlaceDescription"] = ""
        result["reply"] = "괜찮아요. 천천히 생각해보세요. 떠오르실 때 편하게 말해주세요."
        return result
    if stage == "safePlace" and stage_user_count < 4 and result.get("readyToContinue"):
        result["readyToContinue"] = False
        result["safePlaceDescription"] = ""
        if stage_user_count <= 2:
            result["reply"] = "좋아요. 그 공간이 조금씩 떠오르는 것 같아요. 그곳에서 눈에 보이는 것들은 무엇이 있나요?"
        else:
            result["reply"] = "그곳에 조금 더 머물러 볼게요. 그 공간의 빛, 색감, 소리나 냄새는 어떤 느낌인가요?"
    if stage == "safePlace" and stage_user_count >= 4:
        user_texts = [
            msg.get("text", "").strip()
            for msg in payload.get("messages", [])
            if msg.get("who") == "user" and msg.get("text", "").strip()
        ]
        summary = " ".join(user_texts[-5:]).strip()
        if summary:
            result["readyToContinue"] = True
            result["safePlaceDescription"] = result.get("safePlaceDescription") or summary
            reply = result.get("reply", "")
            if not reply or reply.rstrip().endswith("?"):
                result["reply"] = (
                    "좋아요. 지금까지 들려주신 안전한 장소의 모습과 감각들을 바탕으로 "
                    "편안하게 머물 수 있는 공간을 만들어볼게요. 잠시만 기다려 주세요."
                )
    return result


def is_safe_place_thinking_delay(text):
    normalized = (text or "").strip().lower()
    if not normalized:
        return False
    markers = [
        "아직 생각",
        "생각 못",
        "생각이 안",
        "안 떠올",
        "떠오르지",
        "모르겠",
        "잘 모르",
        "시간",
        "잠깐",
        "기다려",
        "not yet",
        "can't think",
        "cannot think",
        "need more time",
        "i don't know",
    ]
    return any(marker in normalized for marker in markers)


def generate_safe_place_panorama(description):
    api_key = load_openai_key()
    prompt = build_panorama_prompt(description)
    last_error = ""

    for model in IMAGE_MODELS_TO_TRY:
        body = {
            "model": model,
            "prompt": prompt,
            "size": "1536x1024",
            "n": 1,
        }
        data = json.dumps(body, ensure_ascii=False).encode("utf-8")
        req = urllib.request.Request(
            "https://api.openai.com/v1/images/generations",
            data=data,
            headers={
                "Authorization": f"Bearer {api_key}",
                "Content-Type": "application/json",
            },
            method="POST",
        )
        try:
            with urllib.request.urlopen(req, timeout=180) as resp:
                raw = resp.read().decode("utf-8")
            result = json.loads(raw)
            image_data = (result.get("data") or [{}])[0].get("b64_json", "")
            if not image_data:
                last_error = f"{model}: Image API returned no b64_json data."
                continue
            return {
                "prompt": prompt,
                "model": model,
                "size": "1536x1024",
                "imageDataUrl": "data:image/png;base64," + image_data,
            }
        except urllib.error.HTTPError as e:
            detail = e.read().decode("utf-8", errors="replace")
            last_error = f"{model}: {e.code} {detail}"
        except Exception as e:
            last_error = f"{model}: {e}"

    raise RuntimeError(last_error or "Image generation failed.")


def generate_object_image(prompt, region_id):
    api_key = load_openai_key()
    prompt = enforce_objectification_style_prompt(prompt)
    last_error = ""
    os.makedirs(GENERATED_DIR, exist_ok=True)
    reference_uris = load_object_reference_data_uris()

    for model in IMAGE_MODELS_TO_TRY:
        try:
            if reference_uris:
                result = request_object_image_edit(api_key, model, prompt, reference_uris)
            else:
                result = request_object_image_generation(api_key, model, prompt)
            image_data = (result.get("data") or [{}])[0].get("b64_json", "")
            if not image_data:
                last_error = f"{model}: Image API returned no b64_json data."
                continue
            safe_id = "".join(ch if ch.isalnum() or ch in ("-", "_") else "_" for ch in str(region_id))
            output_path = os.path.join(GENERATED_DIR, f"object_region_{safe_id}.png")
            with open(output_path, "wb") as f:
                f.write(base64.b64decode(image_data))
            return {
                "prompt": prompt,
                "model": model,
                "size": "1024x1024",
                "imageDataUrl": "data:image/png;base64," + image_data,
                "imagePath": output_path,
                "referenceImagesUsed": [
                    os.path.basename(path)
                    for path in OBJECT_REFERENCE_IMAGE_PATHS
                    if os.path.exists(path)
                ],
                "status": "generated",
            }
        except urllib.error.HTTPError as e:
            detail = e.read().decode("utf-8", errors="replace")
            last_error = f"{model}: {e.code} {detail}"
        except Exception as e:
            last_error = f"{model}: {e}"

    raise RuntimeError(last_error or "Object image generation failed.")


def request_object_image_generation(api_key, model, prompt):
    body = {
        "model": model,
        "prompt": prompt,
        "size": "1024x1024",
        "quality": "high",
        "n": 1,
    }
    data = json.dumps(body, ensure_ascii=False).encode("utf-8")
    req = urllib.request.Request(
        "https://api.openai.com/v1/images/generations",
        data=data,
        headers={
            "Authorization": f"Bearer {api_key}",
            "Content-Type": "application/json",
        },
        method="POST",
    )
    with urllib.request.urlopen(req, timeout=180) as resp:
        return json.loads(resp.read().decode("utf-8"))


def request_object_image_edit(api_key, model, prompt, reference_uris):
    try:
        return request_object_image_edit_json(api_key, model, prompt, reference_uris)
    except Exception as json_error:
        paths = [path for path in OBJECT_REFERENCE_IMAGE_PATHS if os.path.exists(path)]
        try:
            return request_object_image_edit_multipart(api_key, model, prompt, paths, "image")
        except Exception as multipart_error:
            try:
                return request_object_image_edit_multipart(api_key, model, prompt, paths, "image[]")
            except Exception as multipart_array_error:
                raise RuntimeError(
                    "Reference image edit failed. "
                    f"json={json_error}; multipart_image={multipart_error}; multipart_image_array={multipart_array_error}"
                )


def request_object_image_edit_json(api_key, model, prompt, reference_uris):
    body = {
        "model": model,
        "prompt": prompt,
        "images": [{"image_url": uri} for uri in reference_uris],
        "input_fidelity": "high",
        "size": "1024x1024",
        "quality": "high",
        "background": "opaque",
        "output_format": "png",
        "n": 1,
    }
    data = json.dumps(body, ensure_ascii=False).encode("utf-8")
    req = urllib.request.Request(
        "https://api.openai.com/v1/images/edits",
        data=data,
        headers={
            "Authorization": f"Bearer {api_key}",
            "Content-Type": "application/json",
        },
        method="POST",
    )
    with urllib.request.urlopen(req, timeout=180) as resp:
        return json.loads(resp.read().decode("utf-8"))


def request_object_image_edit_multipart(api_key, model, prompt, reference_paths, image_field_name):
    fields = {
        "model": model,
        "prompt": prompt,
        "input_fidelity": "high",
        "size": "1024x1024",
        "quality": "high",
        "background": "opaque",
        "output_format": "png",
        "n": "1",
    }
    files = []
    for path in reference_paths:
        files.append((image_field_name, os.path.basename(path), "image/png", read_binary(path)))
    data, content_type = encode_multipart_form(fields, files)
    req = urllib.request.Request(
        "https://api.openai.com/v1/images/edits",
        data=data,
        headers={
            "Authorization": f"Bearer {api_key}",
            "Content-Type": content_type,
        },
        method="POST",
    )
    with urllib.request.urlopen(req, timeout=180) as resp:
        return json.loads(resp.read().decode("utf-8"))


def read_binary(path):
    with open(path, "rb") as f:
        return f.read()


def encode_multipart_form(fields, files):
    boundary = f"----JemmyBoundary{int(time.time() * 1000)}"
    chunks = []
    for name, value in fields.items():
        chunks.extend([
            f"--{boundary}\r\n".encode("utf-8"),
            f'Content-Disposition: form-data; name="{name}"\r\n\r\n'.encode("utf-8"),
            str(value).encode("utf-8"),
            b"\r\n",
        ])
    for field_name, filename, content_type, content in files:
        chunks.extend([
            f"--{boundary}\r\n".encode("utf-8"),
            f'Content-Disposition: form-data; name="{field_name}"; filename="{filename}"\r\n'.encode("utf-8"),
            f"Content-Type: {content_type}\r\n\r\n".encode("utf-8"),
            content,
            b"\r\n",
        ])
    chunks.append(f"--{boundary}--\r\n".encode("utf-8"))
    return b"".join(chunks), f"multipart/form-data; boundary={boundary}"


def load_object_reference_data_uris():
    uris = []
    for path in OBJECT_REFERENCE_IMAGE_PATHS:
        if os.path.exists(path):
            uris.append(image_path_to_data_uri(path))
    return uris


def enforce_objectification_style_prompt(prompt):
    style_lock = (
        " Required style lock: stylized abstract emotional toy creature, soft toy proportions, "
        "rounded expressive silhouette, primitive simple geometry, smooth readable centered composition, "
        "large simple detachable bead-like googly eyes on the front upper body, graphic cartoon minimal eyes, "
        "use the two provided reference images only as a style bible for the toy universe, eyes, materials, and simple silhouettes; "
        "generate ONE new single object only, do not copy the reference sheet layout, do not generate multiple characters, "
        "not realistic eyes, not embedded human eyes, not biological creature, non-horror. "
        "Object floating in empty void, solid uniform neutral mid-gray #808080 seamless background, "
        "no floor plane, no ground plane, no pedestal, no cast shadow, no contact shadow, "
        "no ground shadow, no drop shadow, no floor reflection."
    )
    return (prompt or "").strip() + style_lock


def image_path_to_data_uri(path):
    if not path or not os.path.exists(path):
        raise RuntimeError(f"Generated image file does not exist: {path}")
    ext = os.path.splitext(path)[1].lower()
    mime = "image/png" if ext == ".png" else "image/jpeg"
    with open(path, "rb") as f:
        return f"data:{mime};base64," + base64.b64encode(f.read()).decode("ascii")


def request_json(url, method="GET", body=None, headers=None, timeout=60):
    data = None if body is None else json.dumps(body, ensure_ascii=False).encode("utf-8")
    req = urllib.request.Request(url, data=data, headers=headers or {}, method=method)
    with urllib.request.urlopen(req, timeout=timeout) as resp:
        raw = resp.read().decode("utf-8")
    return json.loads(raw) if raw else {}


def generate_meshy_3d(image_data_url, image_path, region_id):
    api_key = load_meshy_key()
    image_url = (image_data_url or "").strip()
    if not image_url and image_path:
        image_url = image_path_to_data_uri(image_path)
    if not image_url:
        raise RuntimeError("Generated image slot is empty. Create or attach an image before generating 3D.")

    headers = {
        "Authorization": f"Bearer {api_key}",
        "Content-Type": "application/json",
    }
    create_body = {
        "image_url": image_url,
        "should_texture": True,
        "should_remesh": True,
        "target_formats": ["glb"],
    }
    create_result = request_json(
        "https://api.meshy.ai/openapi/v1/image-to-3d",
        method="POST",
        body=create_body,
        headers=headers,
        timeout=60,
    )
    task_id = create_result.get("result")
    if not task_id:
        raise RuntimeError("Meshy did not return a task id.")

    model_url = ""
    status = ""
    task_error = ""
    for _ in range(100):
        time.sleep(5)
        task = request_json(
            f"https://api.meshy.ai/openapi/v1/image-to-3d/{task_id}",
            headers={"Authorization": f"Bearer {api_key}"},
            timeout=60,
        )
        status = task.get("status", "")
        if status == "SUCCEEDED":
            model_url = ((task.get("model_urls") or {}).get("glb") or "").strip()
            break
        if status == "FAILED":
            task_error = ((task.get("task_error") or {}).get("message") or "Meshy task failed.")
            raise RuntimeError(task_error)

    if not model_url:
        raise RuntimeError(f"Meshy task did not finish. Last status: {status or 'unknown'}")

    os.makedirs(GENERATED_DIR, exist_ok=True)
    safe_region_id = str(region_id or "debug").replace("/", "_").replace("\\", "_")
    output_path = os.path.join(GENERATED_DIR, f"region_{safe_region_id}.glb")
    req = urllib.request.Request(model_url, method="GET")
    with urllib.request.urlopen(req, timeout=180) as resp:
        with open(output_path, "wb") as f:
            f.write(resp.read())

    return {
        "taskId": task_id,
        "modelUrl": model_url,
        "modelPath": output_path,
        "status": "generated",
    }


def load_body_map_sample():
    with open(BODY_MAP_SAMPLE_PAYLOAD_PATH, "r", encoding="utf-8-sig") as f:
        payload = json.load(f)

    regions = payload.get("regions") or []
    for region in regions:
        override = BODY_MAP_SAMPLE_REGION_OVERRIDES.get(region.get("id"))
        if override:
            for key, value in override.items():
                if not region.get(key):
                    region[key] = value

    return {
        "payload": payload,
        "annotatedImageDataUrl": image_path_to_data_uri(BODY_MAP_SAMPLE_ANNOTATED_PATH),
        "transparentImageDataUrl": image_path_to_data_uri(BODY_MAP_SAMPLE_TRANSPARENT_PATH),
        "files": {
            "payload": BODY_MAP_SAMPLE_PAYLOAD_PATH,
            "annotated": BODY_MAP_SAMPLE_ANNOTATED_PATH,
            "transparent": BODY_MAP_SAMPLE_TRANSPARENT_PATH,
        },
    }


class Handler(SimpleHTTPRequestHandler):
    def __init__(self, *args, **kwargs):
        super().__init__(*args, directory=ROOT, **kwargs)

    def end_headers(self):
        self.send_header("Cache-Control", "no-store")
        self.send_header("Access-Control-Allow-Origin", "*")
        self.send_header("Access-Control-Allow-Headers", "Content-Type, Authorization")
        self.send_header("Access-Control-Allow-Methods", "GET, POST, OPTIONS")
        super().end_headers()

    def do_OPTIONS(self):
        self.send_response(204)
        self.end_headers()

    def do_GET(self):
        if self.path == "/api/body-map-sample":
            self.handle_body_map_sample()
            return
        super().do_GET()

    def do_POST(self):
        if self.path == "/api/jemmy":
            self.handle_jemmy()
            return
        if self.path == "/api/safe-place-panorama":
            self.handle_safe_place_panorama()
            return
        if self.path == "/api/object-image":
            self.handle_object_image()
            return
        if self.path == "/api/meshy-3d":
            self.handle_meshy_3d()
            return
        self.send_error(404)

    def handle_jemmy(self):
        try:
            length = int(self.headers.get("Content-Length", "0"))
            payload = json.loads(self.rfile.read(length).decode("utf-8"))
            result = call_openai(payload)
            data = json.dumps(result, ensure_ascii=False).encode("utf-8")
            self.send_response(200)
            self.send_header("Content-Type", "application/json; charset=utf-8")
            self.send_header("Content-Length", str(len(data)))
            self.end_headers()
            self.wfile.write(data)
        except urllib.error.HTTPError as e:
            detail = e.read().decode("utf-8", errors="replace")
            self.send_error(e.code, detail)
        except Exception as e:
            self.send_error(500, str(e))

    def handle_safe_place_panorama(self):
        try:
            length = int(self.headers.get("Content-Length", "0"))
            payload = json.loads(self.rfile.read(length).decode("utf-8"))
            description = (payload.get("description") or "").strip()
            if not description:
                raise RuntimeError("Safe place description is empty.")
            result = generate_safe_place_panorama(description)
            data = json.dumps(result, ensure_ascii=False).encode("utf-8")
            self.send_response(200)
            self.send_header("Content-Type", "application/json; charset=utf-8")
            self.send_header("Content-Length", str(len(data)))
            self.end_headers()
            self.wfile.write(data)
        except urllib.error.HTTPError as e:
            detail = e.read().decode("utf-8", errors="replace")
            self.send_error(e.code, detail)
        except Exception as e:
            self.send_error(500, str(e))

    def handle_object_image(self):
        try:
            length = int(self.headers.get("Content-Length", "0"))
            payload = json.loads(self.rfile.read(length).decode("utf-8"))
            prompt = (payload.get("prompt") or "").strip()
            if not prompt:
                raise RuntimeError("Object image prompt is empty.")
            result = generate_object_image(prompt, payload.get("regionId", "debug"))
            data = json.dumps(result, ensure_ascii=False).encode("utf-8")
            self.send_response(200)
            self.send_header("Content-Type", "application/json; charset=utf-8")
            self.send_header("Content-Length", str(len(data)))
            self.end_headers()
            self.wfile.write(data)
        except urllib.error.HTTPError as e:
            detail = e.read().decode("utf-8", errors="replace")
            self.send_error(e.code, detail)
        except Exception as e:
            self.send_error(500, str(e))

    def handle_meshy_3d(self):
        try:
            length = int(self.headers.get("Content-Length", "0"))
            payload = json.loads(self.rfile.read(length).decode("utf-8"))
            result = generate_meshy_3d(
                payload.get("imageDataUrl", ""),
                payload.get("imagePath", ""),
                payload.get("regionId", "debug"),
            )
            data = json.dumps(result, ensure_ascii=False).encode("utf-8")
            self.send_response(200)
            self.send_header("Content-Type", "application/json; charset=utf-8")
            self.send_header("Content-Length", str(len(data)))
            self.end_headers()
            self.wfile.write(data)
        except urllib.error.HTTPError as e:
            detail = e.read().decode("utf-8", errors="replace")
            self.send_error(e.code, detail)
        except Exception as e:
            self.send_error(500, str(e))

    def handle_body_map_sample(self):
        try:
            result = load_body_map_sample()
            data = json.dumps(result, ensure_ascii=False).encode("utf-8")
            self.send_response(200)
            self.send_header("Content-Type", "application/json; charset=utf-8")
            self.send_header("Content-Length", str(len(data)))
            self.end_headers()
            self.wfile.write(data)
        except Exception as e:
            self.send_error(500, str(e))


if __name__ == "__main__":
    host = os.environ.get("HOST") or ("0.0.0.0" if os.environ.get("PORT") else "127.0.0.1")
    port = int(os.environ.get("PORT", "8765"))
    server = ThreadingHTTPServer((host, port), Handler)
    print(f"Jemmy API server running at http://{host}:{port}")
    server.serve_forever()
