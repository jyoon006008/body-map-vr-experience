/* ═══════════════════════════════════════════════════════════════
   Body Map Analysis + Jemmy AI Companion Backend
   Keeps OpenAI and Meshy API keys server-side for Render deployment.
   ═══════════════════════════════════════════════════════════════ */
require('dotenv').config();
const express = require('express');
const cors = require('cors');
const path = require('path');
const fs = require('fs');

const app = express();
const PORT = process.env.PORT || 3000;
const ROOT = __dirname;
const GENERATED_DIR = path.join(ROOT, 'generated');
const MODEL = 'gpt-4o-mini';
const IMAGE_MODELS_TO_TRY = ['gpt-image-1.5', 'gpt-image-1'];

const OBJECT_REFERENCE_IMAGE_PATHS = [
  path.join(ROOT, 'jemmy-prototype-assets', 'ref01.png'),
  path.join(ROOT, 'jemmy-prototype-assets', 'ref02.png')
];

const BODY_MAP_SAMPLE_REGION_OVERRIDES = {
  0: { color_name: 'black', body_location: 'face', emotion_label: 'spiral thought', visual_shape: 'spiral', description: 'A black spiral mark covering the face area.' },
  1: { color_name: 'green', body_location: 'left arm', emotion_label: 'held tension', visual_shape: 'zigzag scribble', description: 'A green zigzag-like scribble running down the left arm.' },
  2: { color_name: 'red', body_location: 'torso', emotion_label: 'central agitation', visual_shape: 'zigzag scribble', description: 'A red zigzag scribble through the center of the torso.' },
  3: { color_name: 'blue', body_location: 'right arm', emotion_label: 'heavy guarded pressure', visual_shape: 'cross hatch', description: 'A blue cross-hatched block covering the right shoulder and arm.' },
  4: { color_name: 'blue', body_location: 'right leg', emotion_label: 'sharp downward pull', visual_shape: 'zigzag', description: 'A blue zigzag mark on the right lower leg.' },
  5: { color_name: 'orange brown', body_location: 'left upper leg', emotion_label: 'restless weight', visual_shape: 'zigzag', description: 'An orange-brown zigzag mark on the left upper leg.' },
  6: { color_name: 'yellow', body_location: 'left lower leg', emotion_label: 'thin tired energy', visual_shape: 'parallel lines', description: 'Yellow vertical lines running down the left lower leg.' },
  7: { color_name: 'purple', body_location: 'right leg and foot', emotion_label: 'spiraling stuckness', visual_shape: 'spiral lines', description: 'Purple spiral lines around the right lower leg and foot.' }
};

app.use(cors());
app.use(express.json({ limit: '50mb' }));
app.use(express.static(ROOT));

function requireOpenAIKey() {
  const key = process.env.OPENAI_API_KEY || process.env.OpenAI_API_Key;
  if (!key) throw new Error('OPENAI_API_KEY is not configured on the server.');
  return key;
}

function requireMeshyKey() {
  const key = process.env.MESHY_API_KEY || process.env.meshy_api_key || process.env.Meshy_API_Key;
  if (!key) throw new Error('MESHY_API_KEY is not configured on the server.');
  return key;
}

function dataUrlFromFile(filePath) {
  if (!filePath || !fs.existsSync(filePath)) {
    throw new Error(`File does not exist: ${filePath}`);
  }
  const ext = path.extname(filePath).toLowerCase();
  const mime = ext === '.jpg' || ext === '.jpeg' ? 'image/jpeg' : 'image/png';
  return `data:${mime};base64,${fs.readFileSync(filePath).toString('base64')}`;
}

function safeFileId(value) {
  return String(value || 'debug').replace(/[^a-zA-Z0-9_-]/g, '_');
}

async function fetchJson(url, options = {}) {
  const response = await fetch(url, options);
  const text = await response.text();
  let data = {};
  if (text) {
    try {
      data = JSON.parse(text);
    } catch {
      data = { raw: text };
    }
  }
  if (!response.ok) {
    const detail = data.error?.message || data.error || data.raw || text || response.statusText;
    throw new Error(`${response.status} ${detail}`);
  }
  return data;
}

function extractResponseText(responseJson) {
  if (responseJson.output_text) return responseJson.output_text;
  const parts = [];
  for (const item of responseJson.output || []) {
    for (const content of item.content || []) {
      if (content.type === 'output_text' || content.type === 'text') {
        parts.push(content.text || '');
      }
    }
  }
  return parts.filter(Boolean).join('\n').trim();
}

function buildPanoramaPrompt(description) {
  return 'A calming 360 equirectangular panorama safe place for VR art therapy, immersive but gentle, ' +
    'soft natural light, quiet atmosphere, no people, no text, seamless horizon, based on: ' + description;
}

function isSafePlaceThinkingDelay(text) {
  const normalized = String(text || '').trim().toLowerCase();
  if (!normalized) return false;
  return [
    '아직 생각', '생각 못', '생각이 안', '안 떠올', '떠오르지', '모르겠', '잘 모르',
    '시간', '잠깐', '기다려', 'not yet', "can't think", 'cannot think',
    'need more time', "i don't know"
  ].some(marker => normalized.includes(marker));
}

function buildJemmyInstructions(stage, personality, readyToContinue, stageUserCount) {
  return `
You are Jemmy, a kind, warm psychotherapist-like AI companion for a VR art therapy process.
Speak in natural Korean only.
Do not sound like a rigid script. Be warm, human-like, concise, and responsive.
Do not over-interpret or diagnose the user.
Briefly reflect what the user said, then ask at most one gentle question.

Current stage: ${stage}
Personality: ${JSON.stringify(personality || {})}
Stage ready_to_continue: ${readyToContinue}
Number of user messages in this stage after the latest message: ${stageUserCount}

Stage behavior:
- Intro is not a chat stage.
- Small Talk is only ice breaking. First learn the user's preferred name, then ask how their day was, then ask what feeling remains. For the first user reply, extract only the preferred name and reply like "만나서 반가워요, [name]님. 오늘 하루는 어떠셨나요?" Do not set readyToContinue true before at least 3 user messages in Small Talk. Do not ask safe-place content in Small Talk.
- Safe Place helps imagine a comforting place with enough detail for panorama generation. If the user says they have not thought of one yet, reply gently like "천천히 생각해보세요. 떠오르실 때 편하게 말해주세요." and do not ask a new content question. Once a place appears, gather details one at a time: place/space, what can be seen, light/time/weather/color, sound/scent/wind/temperature/texture, and how body/mind feels there. Do not set readyToContinue true until the place is clear and at least 2 concrete sensory/visual details have been collected, usually after 4 user replies. When ready, reply exactly: "좋아요. 지금까지 들려주신 안전한 장소의 모습과 감각들을 바탕으로 편안하게 머물 수 있는 공간을 만들어볼게요. 잠시만 기다려 주세요." Fill safePlaceDescription with a concise visual summary.
- Body Mapping follows the existing Unity flow. First ask about the whole body map impression. After a region is selected, continue like: "이제 이 감정 영역에 대해 이야기해 볼까요? 이 부분은 어떤 감정에 가까운지 말해 주세요."
- Region Reflection is not a separate user-facing stage here; it happens inside Body Mapping after a body-map region is selected.

Body Mapping / Region Reflection data contract:
- from_body_mapping is supplied by the body-map analysis / InteractiveRegion3D: region_id, body_part, color_name, color_hex, pattern, visual_description.
- from_conversation must be extracted from the therapeutic dialogue: emotion, texture, metaphor, surface, motion, weight, temperature, material.
- Required MVP fields: emotion, texture, motion, temperature, metaphor.
- Ask one open, child-friendly, therapist-like question at a time.
- Prefer this order, but follow the user's words naturally: emotion -> texture/material feeling -> surface/detail/shape -> motion/energy -> weight/temperature -> metaphor/symbol.
- Never say "I need this for 3D generation" to the user.
- If a strong feeling appears, prioritize safety, distance, and grounding over data collection.

Return ONLY valid JSON with this shape:
{
  "reply": "Jemmy's reply in Korean",
  "readyToContinue": true,
  "extractedName": "",
  "safePlaceDescription": "",
  "bodyMapSummary": "",
  "conversationExtraction": {
    "emotion": "",
    "texture": "",
    "metaphor": "",
    "material": "",
    "surface": "",
    "motion": "",
    "weight": "",
    "temperature": ""
  },
  "objectPrompt": ""
}`;
}

function normalizeJemmyResult(payload, result, stageUserCount) {
  const stage = payload.stage || '';
  if (stage === 'smallTalk' && stageUserCount >= 3 && !payload.readyToContinue) {
    result.readyToContinue = true;
    result.extractedName = '';
    result.safePlaceDescription = '';
    result.reply = '이야기해 주셔서 고마워요. 그 감정이 지금 남아 있다는 걸 함께 확인했어요. 본격적으로 감정을 탐색하기 전에, 준비가 되면 편안함을 느낄 수 있는 공간을 떠올리는 단계로 넘어가 볼게요.';
  }
  if (stage === 'safePlace' && isSafePlaceThinkingDelay(payload.text)) {
    result.readyToContinue = false;
    result.safePlaceDescription = '';
    result.reply = '괜찮아요. 천천히 생각해보세요. 떠오르실 때 편하게 말해주세요.';
    return result;
  }
  if (stage === 'safePlace' && stageUserCount < 4 && result.readyToContinue) {
    result.readyToContinue = false;
    result.safePlaceDescription = '';
    result.reply = stageUserCount <= 2
      ? '좋아요. 그 공간이 조금씩 떠오르는 것 같아요. 그곳에서 눈에 보이는 것들은 무엇이 있나요?'
      : '그곳에 조금 더 머물러 볼게요. 그 공간의 빛, 색감, 소리나 냄새는 어떤 느낌인가요?';
  }
  if (stage === 'safePlace' && stageUserCount >= 4) {
    const userTexts = (payload.messages || [])
      .filter(msg => msg.who === 'user' && msg.text)
      .map(msg => msg.text.trim());
    const summary = userTexts.slice(-5).join(' ').trim();
    if (summary) {
      result.readyToContinue = true;
      result.safePlaceDescription = result.safePlaceDescription || summary;
      if (!result.reply || result.reply.trim().endsWith('?')) {
        result.reply = '좋아요. 지금까지 들려주신 안전한 장소의 모습과 감각들을 바탕으로 편안하게 머물 수 있는 공간을 만들어볼게요. 잠시만 기다려 주세요.';
      }
    }
  }
  result.conversationExtraction = result.conversationExtraction || {};
  return result;
}

async function callJemmyOpenAI(payload) {
  if ((payload.stage || '') === 'safePlace' && isSafePlaceThinkingDelay(payload.text)) {
    return {
      reply: '괜찮아요. 천천히 생각해보세요. 떠오르실 때 편하게 말해주세요.',
      readyToContinue: false,
      extractedName: '',
      safePlaceDescription: '',
      bodyMapSummary: '',
      conversationExtraction: {},
      objectPrompt: ''
    };
  }

  const apiKey = requireOpenAIKey();
  const messages = payload.messages || [];
  const stageUserCount = messages.filter(msg => msg.who === 'user').length;
  const body = {
    model: MODEL,
    instructions: buildJemmyInstructions(payload.stage || '', payload.personality || {}, Boolean(payload.readyToContinue), stageUserCount),
    input: JSON.stringify({
      currentUserText: payload.text || '',
      messages,
      outputs: payload.outputs || {},
      userName: payload.userName || '',
      regionProfile: payload.regionProfile || {},
      bodyMappingMeta: payload.bodyMappingMeta || {}
    }),
    temperature: 0.75
  };

  const data = await fetchJson('https://api.openai.com/v1/responses', {
    method: 'POST',
    headers: {
      'Authorization': `Bearer ${apiKey}`,
      'Content-Type': 'application/json'
    },
    body: JSON.stringify(body)
  });

  const text = extractResponseText(data);
  let result;
  try {
    result = JSON.parse(text);
  } catch {
    const start = text.indexOf('{');
    const end = text.lastIndexOf('}');
    result = start >= 0 && end > start
      ? JSON.parse(text.slice(start, end + 1))
      : { reply: text, readyToContinue: false, extractedName: '', safePlaceDescription: '', bodyMapSummary: '', conversationExtraction: {}, objectPrompt: '' };
  }
  return normalizeJemmyResult(payload, result, stageUserCount);
}

async function generateSafePlacePanorama(description) {
  const apiKey = requireOpenAIKey();
  const prompt = buildPanoramaPrompt(description);
  let lastError = '';

  for (const model of IMAGE_MODELS_TO_TRY) {
    try {
      const result = await fetchJson('https://api.openai.com/v1/images/generations', {
        method: 'POST',
        headers: {
          'Authorization': `Bearer ${apiKey}`,
          'Content-Type': 'application/json'
        },
        body: JSON.stringify({ model, prompt, size: '1536x1024', n: 1 })
      });
      const imageData = result.data?.[0]?.b64_json;
      if (!imageData) {
        lastError = `${model}: Image API returned no b64_json data.`;
        continue;
      }
      return {
        prompt,
        model,
        size: '1536x1024',
        imageDataUrl: `data:image/png;base64,${imageData}`
      };
    } catch (error) {
      lastError = `${model}: ${error.message}`;
    }
  }
  throw new Error(lastError || 'Image generation failed.');
}

function enforceObjectificationStylePrompt(prompt) {
  const styleLock = ' Required style lock: stylized abstract emotional toy creature, soft toy proportions, ' +
    'rounded expressive silhouette, primitive simple geometry, smooth readable centered composition, ' +
    'large simple detachable bead-like googly eyes on the front upper body, graphic cartoon minimal eyes, ' +
    'use the two provided reference images only as a style bible for the toy universe, eyes, materials, and simple silhouettes; ' +
    'generate ONE new single object only, do not copy the reference sheet layout, do not generate multiple characters, ' +
    'not realistic eyes, not embedded human eyes, not biological creature, non-horror. ' +
    'Object floating in empty void, solid uniform neutral mid-gray #808080 seamless background, ' +
    'no floor plane, no ground plane, no pedestal, no cast shadow, no contact shadow, ' +
    'no ground shadow, no drop shadow, no floor reflection.';
  return String(prompt || '').trim() + styleLock;
}

async function requestObjectImageGeneration(apiKey, model, prompt) {
  return fetchJson('https://api.openai.com/v1/images/generations', {
    method: 'POST',
    headers: {
      'Authorization': `Bearer ${apiKey}`,
      'Content-Type': 'application/json'
    },
    body: JSON.stringify({
      model,
      prompt,
      size: '1024x1024',
      quality: 'high',
      n: 1
    })
  });
}

async function requestObjectImageEdit(apiKey, model, prompt, referencePaths) {
  const form = new FormData();
  form.append('model', model);
  form.append('prompt', prompt);
  form.append('input_fidelity', 'high');
  form.append('size', '1024x1024');
  form.append('quality', 'high');
  form.append('background', 'opaque');
  form.append('output_format', 'png');
  form.append('n', '1');
  for (const filePath of referencePaths) {
    const bytes = fs.readFileSync(filePath);
    form.append('image[]', new Blob([bytes], { type: 'image/png' }), path.basename(filePath));
  }
  return fetchJson('https://api.openai.com/v1/images/edits', {
    method: 'POST',
    headers: { 'Authorization': `Bearer ${apiKey}` },
    body: form
  });
}

async function generateObjectImage(prompt, regionId) {
  const apiKey = requireOpenAIKey();
  const finalPrompt = enforceObjectificationStylePrompt(prompt);
  const referencePaths = OBJECT_REFERENCE_IMAGE_PATHS.filter(filePath => fs.existsSync(filePath));
  fs.mkdirSync(GENERATED_DIR, { recursive: true });
  let lastError = '';

  for (const model of IMAGE_MODELS_TO_TRY) {
    try {
      let result;
      if (referencePaths.length) {
        try {
          result = await requestObjectImageEdit(apiKey, model, finalPrompt, referencePaths);
        } catch (editError) {
          lastError = `${model} edit: ${editError.message}`;
          result = await requestObjectImageGeneration(apiKey, model, finalPrompt);
        }
      } else {
        result = await requestObjectImageGeneration(apiKey, model, finalPrompt);
      }
      const imageData = result.data?.[0]?.b64_json;
      if (!imageData) {
        lastError = `${model}: Image API returned no b64_json data.`;
        continue;
      }
      const outputName = `object_region_${safeFileId(regionId)}.png`;
      const outputPath = path.join(GENERATED_DIR, outputName);
      fs.writeFileSync(outputPath, Buffer.from(imageData, 'base64'));
      return {
        prompt: finalPrompt,
        model,
        size: '1024x1024',
        imageDataUrl: `data:image/png;base64,${imageData}`,
        imagePath: `/generated/${outputName}`,
        referenceImagesUsed: referencePaths.map(filePath => path.basename(filePath)),
        status: 'generated'
      };
    } catch (error) {
      lastError = `${model}: ${error.message}`;
    }
  }
  throw new Error(lastError || 'Object image generation failed.');
}

async function generateMeshy3D(imageDataUrl, imagePath, regionId) {
  const apiKey = requireMeshyKey();
  let imageUrl = String(imageDataUrl || '').trim();
  if (!imageUrl && imagePath) {
    const localPath = imagePath.startsWith('/generated/')
      ? path.join(ROOT, imagePath.replace(/^\//, ''))
      : imagePath;
    imageUrl = dataUrlFromFile(localPath);
  }
  if (!imageUrl) {
    throw new Error('Generated image slot is empty. Create or attach an image before generating 3D.');
  }

  const createResult = await fetchJson('https://api.meshy.ai/openapi/v1/image-to-3d', {
    method: 'POST',
    headers: {
      'Authorization': `Bearer ${apiKey}`,
      'Content-Type': 'application/json'
    },
    body: JSON.stringify({
      image_url: imageUrl,
      should_texture: true,
      should_remesh: true,
      target_formats: ['glb']
    })
  });
  const taskId = createResult.result;
  if (!taskId) throw new Error('Meshy did not return a task id.');

  let modelUrl = '';
  let status = '';
  for (let i = 0; i < 100; i += 1) {
    await new Promise(resolve => setTimeout(resolve, 5000));
    const task = await fetchJson(`https://api.meshy.ai/openapi/v1/image-to-3d/${taskId}`, {
      headers: { 'Authorization': `Bearer ${apiKey}` }
    });
    status = task.status || '';
    if (status === 'SUCCEEDED') {
      modelUrl = (task.model_urls?.glb || '').trim();
      break;
    }
    if (status === 'FAILED') {
      throw new Error(task.task_error?.message || 'Meshy task failed.');
    }
  }
  if (!modelUrl) throw new Error(`Meshy task did not finish. Last status: ${status || 'unknown'}`);

  fs.mkdirSync(GENERATED_DIR, { recursive: true });
  const outputName = `region_${safeFileId(regionId)}.glb`;
  const outputPath = path.join(GENERATED_DIR, outputName);
  const response = await fetch(modelUrl);
  if (!response.ok) throw new Error(`Failed to download Meshy model: ${response.status}`);
  fs.writeFileSync(outputPath, Buffer.from(await response.arrayBuffer()));

  return {
    taskId,
    modelUrl,
    modelPath: `/generated/${outputName}`,
    status: 'generated'
  };
}

function loadBodyMapSample() {
  const payloadPath = path.join(ROOT, 'jemmy-prototype-assets', 'body_map_payload.json');
  const annotatedPath = path.join(ROOT, 'jemmy-prototype-assets', 'body_map_annotated.png');
  const transparentPath = path.join(ROOT, 'jemmy-prototype-assets', 'body_map_transparent.png');
  const payload = JSON.parse(fs.readFileSync(payloadPath, 'utf8'));
  for (const region of payload.regions || []) {
    const override = BODY_MAP_SAMPLE_REGION_OVERRIDES[region.id];
    if (override) {
      for (const [key, value] of Object.entries(override)) {
        if (!region[key]) region[key] = value;
      }
    }
  }
  return {
    payload,
    annotatedImageDataUrl: dataUrlFromFile(annotatedPath),
    transparentImageDataUrl: dataUrlFromFile(transparentPath),
    files: {
      payload: 'jemmy-prototype-assets/body_map_payload.json',
      annotated: 'jemmy-prototype-assets/body_map_annotated.png',
      transparent: 'jemmy-prototype-assets/body_map_transparent.png'
    }
  };
}

/* ── POST /api/analyze ──────────────────────────────────────── */
app.post('/api/analyze', async (req, res) => {
  try {
    const { crops } = req.body;
    if (!crops || !Array.isArray(crops)) {
      return res.status(400).json({ error: 'No crops array provided' });
    }

    const apiKey = requireOpenAIKey();
    console.log(`[analyze] Received ${crops.length} region crops for OpenAI labeling...`);

    const results = await Promise.all(
      crops.map(async (crop) => {
        try {
          const base64 = crop.image.startsWith('data:') ? crop.image.split(',')[1] : crop.image;

          const data = await fetchJson('https://api.openai.com/v1/chat/completions', {
            method: 'POST',
            headers: {
              'Content-Type': 'application/json',
              'Authorization': `Bearer ${apiKey}`
            },
            body: JSON.stringify({
              model: 'gpt-4o-mini',
              messages: [
                {
                  role: 'system',
                  content: `You are an expert at analyzing body mapping drawings.
Analyze the attached crop image of a drawing stroke with transparent background.

Location context:
- Color category: "${crop.color}"
- Normalized center X: ${crop.normalizedCenterX}
- Normalized center Y: ${crop.normalizedCenterY}
- Width ratio: ${crop.widthRatio}
- Height ratio: ${crop.heightRatio}

Return ONLY JSON:
{
  "body_part": "arm",
  "pattern": "looped scribble",
  "visual_description": "화면 오른쪽 부위에 초록색의 반복적인 둥근 낙서형 선이 촘촘하게 그려져 있습니다.",
  "confidence": 0.95
}

Do not include left/right in body_part. The Korean visual_description must only describe color, location, line shape, repetitive patterns, and density. Do not infer emotion.`
                },
                {
                  role: 'user',
                  content: [
                    {
                      type: 'text',
                      text: `Analyze this transparent crop image of a "${crop.color}" stroke at location X=${Number(crop.normalizedCenterX).toFixed(3)}, Y=${Number(crop.normalizedCenterY).toFixed(3)}.`
                    },
                    {
                      type: 'image_url',
                      image_url: { url: `data:image/png;base64,${base64}` }
                    }
                  ]
                }
              ],
              response_format: { type: 'json_object' },
              temperature: 0.2
            })
          });

          const parsed = JSON.parse(data.choices[0].message.content);
          return {
            id: crop.id,
            body_part: parsed.body_part || '',
            pattern: parsed.pattern || 'scribble',
            visual_description: parsed.visual_description || '',
            confidence: parsed.confidence || 0.5
          };
        } catch (error) {
          console.error(`[analyze] Error processing crop ID ${crop.id}:`, error);
          return { id: crop.id, error: error.message };
        }
      })
    );

    console.log(`[analyze] Successfully processed all ${crops.length} crops`);
    res.json({ regions: results });
  } catch (error) {
    console.error('[analyze] Global Error:', error);
    res.status(500).json({ error: error.message });
  }
});

app.get('/api/body-map-sample', (req, res) => {
  try {
    res.json(loadBodyMapSample());
  } catch (error) {
    res.status(500).json({ error: error.message });
  }
});

app.post('/api/jemmy', async (req, res) => {
  try {
    res.json(await callJemmyOpenAI(req.body || {}));
  } catch (error) {
    console.error('[jemmy] Error:', error);
    res.status(500).json({ error: error.message });
  }
});

app.post('/api/safe-place-panorama', async (req, res) => {
  try {
    const description = String(req.body?.description || '').trim();
    if (!description) return res.status(400).json({ error: 'Safe place description is empty.' });
    res.json(await generateSafePlacePanorama(description));
  } catch (error) {
    console.error('[safe-place-panorama] Error:', error);
    res.status(500).json({ error: error.message });
  }
});

app.post('/api/object-image', async (req, res) => {
  try {
    const prompt = String(req.body?.prompt || '').trim();
    if (!prompt) return res.status(400).json({ error: 'Object image prompt is empty.' });
    res.json(await generateObjectImage(prompt, req.body?.regionId || 'debug'));
  } catch (error) {
    console.error('[object-image] Error:', error);
    res.status(500).json({ error: error.message });
  }
});

app.post('/api/meshy-3d', async (req, res) => {
  try {
    res.json(await generateMeshy3D(req.body?.imageDataUrl || '', req.body?.imagePath || '', req.body?.regionId || 'debug'));
  } catch (error) {
    console.error('[meshy-3d] Error:', error);
    res.status(500).json({ error: error.message });
  }
});

app.post('/api/log', (req, res) => {
  console.log('[browser-log]', req.body.message);
  res.json({ ok: true });
});

/* ── Start ──────────────────────────────────────────────────── */
app.listen(PORT, () => {
  console.log(`\n  Body Map + Jemmy server -> http://localhost:${PORT}`);
  console.log(`  OpenAI key : ${process.env.OPENAI_API_KEY ? 'loaded' : 'MISSING'}`);
  console.log(`  Meshy key  : ${process.env.MESHY_API_KEY ? 'loaded' : 'MISSING'}\n`);
});
