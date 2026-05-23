# Body Mapping Emotion Detector (Art Therapy → Unity VR)

This repository contains the client-side Web application for the Body Mapping Analysis pipeline. It runs fully client-side on any static web host, including **GitHub Pages**, and uses canvas-based pixel differentiation and clustering to segment drawings from a silhouette sheet and identify emotions.

It then streams the processed transparent texture and emotional metadata in real-time to a running **Unity VR client** on the local network (`http://localhost:8200`).

---

## Folder Contents

- `index.html`: The main web application implementing image loading, pixel difference analysis, morphological cleanup, color/emotion classification, and the local Unity integration POST requests.
- `human_silhouette.png` / `human_silhouette.jpg`: The template blank silhouette image.
- `sample_body_map.png`: A demo drawing used to test the pipeline instantly without requiring a new scan.

---

## Getting Started: Host on GitHub Pages

Follow these steps to upload this code to GitHub and host the website:

1. **Create a GitHub Repository**:
   - Go to [github.com](https://github.com/) and create a new repository (e.g., `body-map-detector`). Do NOT initialize with a README.
2. **Initialize Local Repository**:
   Open a terminal (Git Bash, Command Prompt, or PowerShell) inside the `body-map-detector` folder on your machine:
   ```bash
   git init
   git add .
   git commit -m "Initial commit of body map analyzer"
   ```
3. **Link and Push to GitHub**:
   Replace `YOUR_USERNAME` and `YOUR_REPO_NAME` with your actual GitHub username and repository name:
   ```bash
   git branch -M main
   git remote add origin https://github.com/YOUR_USERNAME/YOUR_REPO_NAME.git
   git push -u origin main
   ```
4. **Enable GitHub Pages**:
   - Go to your repository page on GitHub.
   - Click on the **Settings** tab at the top.
   - Click on **Pages** in the left sidebar.
   - Under **Build and deployment** -> **Branch**, select `main` and `/ (root)` folder, then click **Save**.
   - After 1–2 minutes, GitHub will show your live site URL (e.g., `https://YOUR_USERNAME.github.io/YOUR_REPO_NAME/`).

---

## How It Works

1. **Template Diffing**: When you upload a participant's drawing, the page resizes it and draws both the template silhouette (`human_silhouette.png`) and the drawing on hidden HTML5 canvases.
2. **Stroke Extraction**: It runs a pixel-by-pixel color diff. Any pixel displaying a color difference greater than the threshold (which is not paper-white noise) is marked as a drawing pixel.
3. **Erosion/Dilation**: A morphological open passes through to clean tiny scanning artifacts or speckles.
4. **Color & Emotion Clustering**: 
   - A BFS flood fill groups neighboring pixels of the same color into regions.
   - Color is analyzed in the HSV space (highly robust to varying lighting and shadows).
   - Emotions are classified by color and body location according to standard art therapy psychological models:
     - **Red on Chest/Head**: Anger / High Mental Stress.
     - **Blue/Cyan on Chest/Throat**: Sadness / Grief / Suppression.
     - **Purple on Abdomen/Head**: Fear / Traumatic Anxiety.
     - **Green on Chest/Head**: Peace / Healing / Safety.
     - **Yellow on Hands/Head**: Social connection / Attention seeking / Joy.
5. **Unity Stream**: When the analysis completes, the site sends a JSON POST request to `http://localhost:8200/upload` containing the background-removed Base64 transparent texture and a JSON array of the regions.

---

## Integration with Unity

1. Run the Unity project containing the `BodyMapReceiver.cs` script.
2. Enter Play mode in Unity. The screen status on the web page will light up green (**🟢 Unity Active**).
3. Click **▶ Extract & Analyze** on the website.
4. The transparent avatar image and emotional 3D objects will instantly appear in front of you in the Unity scene!

*최신 파이프라인 정렬 및 분석 성능 고도화 연동 완료.*
