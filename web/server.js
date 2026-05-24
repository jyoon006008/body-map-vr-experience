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
/* ── POST /api/analyze ──────────────────────────────────────── */
app.post('/api/analyze', async (req, res) => {
  try {
    const { crops } = req.body;
    if (!crops || !Array.isArray(crops)) {
      return res.status(400).json({ error: 'No crops array provided' });
    }

    const apiKey = process.env.OPENAI_API_KEY;
    if (!apiKey) {
      return res.status(500).json({ error: 'OPENAI_API_KEY not set in .env' });
    }

    console.log(`[analyze] Received ${crops.length} region crops for OpenAI labeling…`);

    const results = await Promise.all(
      crops.map(async (crop) => {
        try {
          const base64 = crop.image.startsWith('data:') ? crop.image.split(',')[1] : crop.image;

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
                  content: `You are an expert at analyzing body mapping drawings.
Analyze the attached crop image of a drawing stroke (with transparent background).

We have provided the location of this crop on the overall silhouette:
- Color category of the stroke: "${crop.color}"
- Normalized center X coordinate (0.0 to 1.0): ${crop.normalizedCenterX}
- Normalized center Y coordinate (0.0 to 1.0): ${crop.normalizedCenterY}
- Width ratio relative to full image: ${crop.widthRatio}
- Height ratio relative to full image: ${crop.heightRatio}

Your task is to identify and return:
1. "body_part": The physical body part where this stroke is drawn (e.g., "arm", "hand", "leg", "foot", "thigh", "chest and stomach", "face", "shoulder", "hip", "head", "neck"). Do NOT include the words "left" or "right" in this field.
2. "pattern": The drawing stroke pattern style (e.g., "looped scribble", "spiral", "zigzag", "cross_hatch", "parallel_lines", "solid_fill", "dot").
3. "visual_description": A strict, objective Korean description of the drawing stroke.
   - You MUST ONLY describe: color, location, line shape, repetitive patterns, density.
   - You MUST NOT describe: emotions, feelings, mental states, therapeutic interpretations, meaning interpretations, personality traits.
   - Example ALLOWED: "화면 오른쪽 부위에 초록색의 반복적인 둥근 낙서형 선이 촘촘하게 그려져 있습니다."
   - Example FORBIDDEN: "불안함이 느껴지는 나선형 선", "차분한 치유의 감정을 시사하는 초록색 칠"
4. "confidence": Detection confidence (0.0 to 1.0).

Return ONLY a JSON object of this structure:
{
  "body_part": "arm",
  "pattern": "looped scribble",
  "visual_description": "화면 오른쪽 부위에 초록색의 반복적인 둥근 낙서형 선이 촘촘하게 그려져 있습니다.",
  "confidence": 0.95
}`
                },
                {
                  role: 'user',
                  content: [
                    {
                      type: 'text',
                      text: `Analyze this transparent crop image of a "${crop.color}" stroke at location X=${crop.normalizedCenterX.toFixed(3)}, Y=${crop.normalizedCenterY.toFixed(3)}.`
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

          if (!response.ok) {
            const errText = await response.text();
            console.error(`[analyze] OpenAI crop error for ID ${crop.id}:`, response.status, errText);
            return { id: crop.id, error: `OpenAI error: ${errText}` };
          }

          const data = await response.json();
          const content = data.choices[0].message.content;
          const parsed = JSON.parse(content);

          return {
            id: crop.id,
            body_part: parsed.body_part || '',
            pattern: parsed.pattern || 'scribble',
            visual_description: parsed.visual_description || '',
            confidence: parsed.confidence || 0.5
          };
        } catch (e) {
          console.error(`[analyze] Error processing crop ID ${crop.id}:`, e);
          return { id: crop.id, error: e.message };
        }
      })
    );

    console.log(`[analyze] Successfully processed all ${crops.length} crops`);
    res.json({ regions: results });
  } catch (err) {
    console.error('[analyze] Global Error:', err);
    res.status(500).json({ error: err.message });
  }
});

app.post('/api/log', (req, res) => {
  console.log('[browser-log]', req.body.message);
  res.json({ ok: true });
});

/* ── Start ──────────────────────────────────────────────────── */
app.listen(PORT, () => {
  console.log(`\n  🎨  Body Map server → http://localhost:${PORT}`);
  console.log(`  OpenAI key : ${process.env.OPENAI_API_KEY ? '✓ loaded' : '✗ MISSING – add to .env'}`);
  console.log(`  Tripo key  : ${process.env.TRIPO3D_API_KEY ? '✓ loaded' : '✗ MISSING – add to .env'}\n`);
});
