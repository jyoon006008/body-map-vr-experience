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

Analyze the image and return a JSON object with a list of "regions".
For demo images, look for at least 8 representative regions:
(black spiral head, green left arm, red chest, blue right arm, orange left thigh, cyan right thigh/hip, yellow left shin, purple right leg).
For general user uploads, do not force a minimum count, but return only the actually visible distinct drawn regions.

Detection Rules:
- Do not merge different colors into a single region.
- Do not merge spatially separated drawings into a single region.
- Do not ignore black marker drawings (e.g. black spiral drawing on the head).
- Do not create fake/imaginary regions that do not exist in the image.

For each detected region, return a JSON object containing:
1. "id": sequential integer starting from 0.
2. "body_part": one of: head, face, neck, chest, stomach, waist, left_shoulder, left_arm, left_hand, right_shoulder, right_arm, right_hand, left_thigh, left_knee, left_shin, left_foot, right_thigh, right_knee, right_shin, right_foot.
3. "side": "center", "left", or "right".
4. "color_name": English color name (black, red, blue, yellow, green, purple, orange, pink, gray, cyan, white, brown, lime, navy).
5. "color_hex": approximate hex code.
6. "pattern": drawing pattern – one of: spiral, zigzag, cross_hatch, parallel_lines, helical_coil, scribble, dots, solid_fill, spiky_stars, wavy_lines.
7. "emotion_label": inferred emotion – e.g. anger, sadness, fear, joy, anxiety, calm, confusion, love, stress, energy, tension, warmth, coldness, numbness, pain.
8. "bbox": normalised bounding box with coordinates EXACTLY in range 0.0 to 1.0 relative to the full image width and height (top-left origin):
   {"x": 0.0-1.0, "y": 0.0-1.0, "width": 0.0-1.0, "height": 0.0-1.0}
9. "description": structured Korean description in exactly this format:
   - 색: <color & mood>
   - 패턴: <stroke / texture type>
   - 위치: <body location detail>
   - 특징: <psychological interpretation>

Return ONLY a JSON object of this structure: {"regions": [...]}
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
