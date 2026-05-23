/* ═══════════════════════════════════════════════════════════════
   Body Map Analysis – Local Express Backend
   Proxies OpenAI Vision API calls so API keys stay server-side.
   ═══════════════════════════════════════════════════════════════ */
require('dotenv').config();
const express = require('express');
const cors    = require('cors');
const path    = require('path');

const app  = express();
const PORT = process.env.PORT || 3000;

app.use(cors());
app.use(express.json({ limit: '50mb' }));
app.use(express.static(path.join(__dirname)));          // serve index.html etc.

/* ── POST /api/analyze ──────────────────────────────────────── */
app.post('/api/analyze', async (req, res) => {
  try {
    const { image, imageWidth, imageHeight } = req.body;
    if (!image) return res.status(400).json({ error: 'No image provided' });

    const apiKey = process.env.OPENAI_API_KEY;
    if (!apiKey) return res.status(500).json({ error: 'OPENAI_API_KEY not set in .env' });

    const base64 = image.startsWith('data:') ? image.split(',')[1] : image;

    console.log(`[analyze] Received ${imageWidth}×${imageHeight} image, calling OpenAI…`);

    const response = await fetch('https://api.openai.com/v1/chat/completions', {
      method: 'POST',
      headers: {
        'Content-Type':  'application/json',
        'Authorization': `Bearer ${apiKey}`
      },
      body: JSON.stringify({
        model: 'gpt-4o-mini',
        messages: [
          {
            role: 'system',
            content: `You are an art therapy body mapping analysis expert.
The attached image shows a human body silhouette outline on which a participant has drawn or colored emotional expressions using crayons, colored pencils, or markers.

Analyze the image and return a JSON object with a list of "regions" matching the requested format.
For the demo image, there should be exactly 8 representative regions:
1. green left arm and hand
2. blue right arm, shoulder, and upper chest
3. orange/brown left thigh
4. yellow left lower leg
5. cyan right thigh/hip
6. purple right leg and foot
7. red left chest and stomach
8. black face spiral

For general user uploads, return only the actually visible distinct drawn semantic regions (no minimum limit, do not force 8 regions).

For each region, return a JSON object containing:
1. "id": sequential integer starting from 0.
2. "body_part": the body location (e.g., "left arm and hand", "right arm, shoulder, and upper chest", "left thigh", "left lower leg", "right thigh/hip", "right leg and foot", "left chest and stomach", "face").
3. "color": color category (e.g. "green", "blue", "orange/brown", "yellow", "cyan", "purple", "red", "black", "gray", "pink", "brown", "navy").
4. "pattern": drawing pattern / texture style (e.g., "looped scribble", "spiral", "zigzag", "cross_hatch", "parallel_lines", "solid_fill").
5. "semantic_region": a concise label describing the color and body location (e.g., "green left arm and hand").
6. "bbox": normalised bounding box with coordinates EXACTLY in range 0.0 to 1.0 relative to the full image width and height (top-left origin):
   {"x": 0.0-1.0, "y": 0.0-1.0, "width": 0.0-1.0, "height": 0.0-1.0}
7. "confidence": detection confidence value (0.0 to 1.0).
8. "color_hex": approximate hex code matching the color category.
9. "emotion_label": inferred emotion (e.g., "energy", "calm", "stress", "sadness", "anger", "tension", "love", "numbness", "pain").
10. "description": structured Korean description in exactly this format:
    - 색: <color & mood>
    - 패턴: <stroke / texture type>
    - 위치: <body location detail>
    - 특징: <psychological interpretation>

Return ONLY a JSON object of this structure:
{
  "regions": [
    {
      "id": 0,
      "body_part": "left arm and hand",
      "color": "green",
      "pattern": "looped scribble",
      "semantic_region": "green left arm and hand",
      "bbox": {"x": 0.0, "y": 0.0, "width": 0.0, "height": 0.0},
      "confidence": 0.95,
      "color_hex": "#34C759",
      "emotion_label": "calm",
      "description": "- 색: 초록색 (평온/안정)\\n- 패턴: looped scribble\\n- 위치: 왼쪽 손과 팔\\n- 특징: 신체 지도를 통해 표현된 심리적 안정감 영역입니다."
    }
  ]
}
If nothing is found, return {"regions": []}.`
          },
          {
            role: 'user',
            content: [
              {
                type: 'text',
                text: `Analyze this body mapping drawing (${imageWidth}×${imageHeight} px). Find all colored emotional regions and return structured JSON.`
              },
              {
                type: 'image_url',
                image_url: { url: `data:image/jpeg;base64,${base64}` }
              }
            ]
          }
        ],
        response_format: { type: 'json_object' },
        temperature: 0.2
      })
    });

    if (!response.ok) {
      const errText = await response.text();
      console.error('[analyze] OpenAI error:', response.status, errText);
      return res.status(response.status).json({ error: `OpenAI ${response.status}: ${errText}` });
    }

    const data    = await response.json();
    const content = data.choices[0].message.content;
    const parsed  = JSON.parse(content);

    console.log(`[analyze] OpenAI returned ${(parsed.regions || []).length} regions`);
    res.json(parsed);
  } catch (err) {
    console.error('[analyze] Error:', err);
    res.status(500).json({ error: err.message });
  }
});

/* ── Start ──────────────────────────────────────────────────── */
app.listen(PORT, () => {
  console.log(`\n  🎨  Body Map server → http://localhost:${PORT}`);
  console.log(`  OpenAI key : ${process.env.OPENAI_API_KEY ? '✓ loaded' : '✗ MISSING – add to .env'}`);
  console.log(`  Tripo key  : ${process.env.TRIPO3D_API_KEY ? '✓ loaded' : '✗ MISSING – add to .env'}\n`);
});
