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

Detect ALL colored/drawn emotional regions on the body silhouette.
Ignore the printed silhouette outline itself (thin black lines) — only detect marks the participant added.

For each region return:
1. "id": sequential integer starting from 0
2. "body_part": one of: head, face, neck, chest, stomach, waist, left_shoulder, left_arm, left_hand, right_shoulder, right_arm, right_hand, left_thigh, left_knee, left_shin, left_foot, right_thigh, right_knee, right_shin, right_foot
3. "side": "center", "left", or "right"
4. "color_name": English color name (black, red, blue, yellow, green, purple, orange, pink, gray, cyan, white, brown)
5. "color_hex": approximate hex code
6. "pattern": drawing pattern – one of: spiral, zigzag, cross_hatch, parallel_lines, helical_coil, scribble, dots, solid_fill, spiky_stars, wavy_lines
7. "emotion_label": inferred emotion – e.g. anger, sadness, fear, joy, anxiety, calm, confusion, love, stress, energy, tension, warmth, coldness, numbness, pain
8. "bbox": normalised bounding box {"x": 0-1, "y": 0-1, "width": 0-1, "height": 0-1} relative to the full image (top-left origin)
9. "description": structured Korean description in exactly this format:
   - 색: <color & mood>
   - 패턴: <stroke / texture type>
   - 위치: <body location detail>
   - 특징: <psychological interpretation>

Return ONLY a JSON object:  {"regions": [...]}
If nothing is found return {"regions": []}
Be thorough – detect even faint or small coloured marks.`
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
