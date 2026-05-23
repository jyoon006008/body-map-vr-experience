const fs = require('fs');
const vm = require('vm');

// Test helper functions directly
const codeToTest = `
function bboxOverlapRatio(b1, b2) {
  const x_overlap = Math.max(0, Math.min(b1.x + b1.width, b2.x + b2.width) - Math.max(b1.x, b2.x));
  const y_overlap = Math.max(0, Math.min(b1.y + b1.height, b2.y + b2.height) - Math.max(b1.y, b2.y));
  const intersection = x_overlap * y_overlap;
  if (intersection === 0) return 0;
  const union = (b1.width * b1.height) + (b2.width * b2.height) - intersection;
  return intersection / union;
}

function bboxDistance(b1, b2) {
  const x1_min = b1.x, x1_max = b1.x + b1.width;
  const y1_min = b1.y, y1_max = b1.y + b1.height;
  const x2_min = b2.x, x2_max = b2.x + b2.width;
  const y2_min = b2.y, y2_max = b2.y + b2.height;

  const distX = Math.max(0, Math.max(x1_min, x2_min) - Math.min(x1_max, x2_max));
  const distY = Math.max(0, Math.max(y1_min, y2_min) - Math.min(y1_max, y2_max));
  return Math.sqrt(distX * distX + distY * distY);
}

// Real isColorCompatible and getStandardColorCategory are extracted from index.html

function centerDistance(b1, b2) {
  const cx1 = b1.x + b1.width / 2;
  const cy1 = b1.y + b1.height / 2;
  const cx2 = b2.x + b2.width / 2;
  const cy2 = b2.y + b2.height / 2;
  return Math.sqrt((cx1 - cx2) ** 2 + (cy1 - cy2) ** 2);
}

function classifyPixelColorFromAlignedDrawing(alignedDrwData, idx) {
  const i4 = idx * 4;
  const pr = alignedDrwData[i4], pg = alignedDrwData[i4+1], pb = alignedDrwData[i4+2];
  if (pr > 200 && pg > 200 && pb > 200) return 'white';
  if (pr < 50 && pg > 200 && pb > 200) return 'cyan';
  if (pr < 50 && pg > 200 && pb < 150) return 'green';
  if (pr < 50 && pg < 150 && pb > 200) return 'blue';
  return 'black';
}
`;

const html = fs.readFileSync('web/index.html', 'utf8');

function extractFunction(name) {
  const startIndex = html.indexOf(`function ${name}`);
  if (startIndex === -1) throw new Error(`Could not find function ${name}`);
  
  let braceCount = 0;
  let inString = false;
  let stringChar = '';
  let i = startIndex;
  
  while (i < html.length) {
    const char = html[i];
    if (char === '"' || char === "'" || char === '`') {
      if (!inString) {
        inString = true;
        stringChar = char;
      } else if (stringChar === char && html[i-1] !== '\\') {
        inString = false;
      }
    }
    
    if (!inString) {
      if (char === '{') braceCount++;
      else if (char === '}') {
        braceCount--;
        if (braceCount === 0) {
          return html.substring(startIndex, i + 1);
        }
      }
    }
    i++;
  }
  return '';
}

const fGetStandardColorCategory = extractFunction('getStandardColorCategory');
const fIsColorCompatible = extractFunction('isColorCompatible');
const fColorDistance = extractFunction('colorDistance');
const fGetVerticalOverlapRatio = extractFunction('getVerticalOverlapRatio');
const fSplitIntoComponents = extractFunction('splitIntoComponents');
const fSplitAndReassignOutliers = extractFunction('splitAndReassignOutliers');
const fReassignSmallFragments = extractFunction('reassignSmallFragments');
const fMergeSameColorRegions = extractFunction('mergeSameColorRegions');
const fIsRegionCategoryCompatible = extractFunction('isRegionCategoryCompatible');
const fReassignmentScore = extractFunction('reassignmentScore');
const fNearestMaskPixelDistance = extractFunction('nearestMaskPixelDistance');
const fGetCentroid = extractFunction('getCentroid');
const fCentroidDistance = extractFunction('centroidDistance');
const fRecomputeRegionBBoxAndColor = extractFunction('recomputeRegionBBoxAndColor');
const fCanMergeFragment = extractFunction('canMergeFragment');
const fFixMisplacedBlueFragmentsInGreen = extractFunction('fixMisplacedBlueFragmentsInGreen');

const fullCode = codeToTest + '\n' +
  fGetStandardColorCategory + '\n' +
  fIsColorCompatible + '\n' +
  fColorDistance + '\n' +
  fGetVerticalOverlapRatio + '\n' +
  fSplitIntoComponents + '\n' +
  fSplitAndReassignOutliers + '\n' +
  fReassignSmallFragments + '\n' +
  fMergeSameColorRegions + '\n' +
  fIsRegionCategoryCompatible + '\n' +
  fReassignmentScore + '\n' +
  fNearestMaskPixelDistance + '\n' +
  fGetCentroid + '\n' +
  fCentroidDistance + '\n' +
  fRecomputeRegionBBoxAndColor + '\n' +
  fCanMergeFragment + '\n' +
  fFixMisplacedBlueFragmentsInGreen;

const sandbox = {
  console: console,
  Math: Math,
  Set: Set,
  Uint8Array: Uint8Array,
  Int32Array: Int32Array,
  Array: Array
};

vm.createContext(sandbox);
vm.runInNewContext(fullCode, sandbox);

console.log('--- START REGION ALGORITHM UNIT TESTS ---');

const workW = 400;
const workH = 400;
const bodyHeight = 300;

const greenColor = { r: 51, g: 255, b: 136 };
const cyanColor = { r: 51, g: 255, b: 238 };
const blueColor = { r: 51, g: 136, b: 255 };
const redColor = { r: 255, g: 51, b: 85 };

// Helper to make a component
function createMockRegion(color, pixels, avgColor) {
  let minX = workW, maxX = 0, minY = workH, maxY = 0;
  for (const idx of pixels) {
    const cx = idx % workW;
    const cy = Math.floor(idx / workW);
    if (cx < minX) minX = cx;
    if (cx > maxX) maxX = cx;
    if (cy < minY) minY = cy;
    if (cy > maxY) maxY = cy;
  }
  return {
    color: color,
    pixels: pixels,
    bbox: {
      x: minX,
      y: minY,
      width: maxX - minX + 1,
      height: maxY - minY + 1
    },
    avgColor: avgColor
  };
}

const alignedDrwData = new Uint8Array(workW * workH * 4);

function fillPixelsWithColor(pixels, r, g, b) {
  for (const idx of pixels) {
    alignedDrwData[idx * 4] = r;
    alignedDrwData[idx * 4 + 1] = g;
    alignedDrwData[idx * 4 + 2] = b;
    alignedDrwData[idx * 4 + 3] = 255;
  }
}

// Test color compatibility
console.log('isRegionCategoryCompatible(blue, green) ->', sandbox.isRegionCategoryCompatible('blue', 'green')); // Expected: false
console.log('isRegionCategoryCompatible(blue, cyan) ->', sandbox.isRegionCategoryCompatible('blue', 'cyan'));   // Expected: true
console.log('isRegionCategoryCompatible(green, cyan) ->', sandbox.isRegionCategoryCompatible('green', 'cyan')); // Expected: true

if (sandbox.isRegionCategoryCompatible('blue', 'green')) throw new Error('blue & green must not be compatible');
if (!sandbox.isRegionCategoryCompatible('blue', 'cyan')) throw new Error('blue & cyan must be compatible');

// Test 2: Left arm green + cyan merge
const greenPixels = [];
for (let y = 100; y < 110; y++) {
  for (let x = 50; x < 60; x++) greenPixels.push(y * workW + x);
}
const cyanPixels = [];
for (let y = 110; y < 120; y++) {
  for (let x = 52; x < 62; x++) cyanPixels.push(y * workW + x);
}
fillPixelsWithColor(greenPixels, 33, 255, 120);
fillPixelsWithColor(cyanPixels, 33, 255, 230);

const rGreen = createMockRegion('green', greenPixels, greenColor);
const rCyan = createMockRegion('cyan', cyanPixels, cyanColor);

let candidates = [rGreen, rCyan];
let merged = sandbox.mergeSameColorRegions(candidates, workW, workH, bodyHeight, alignedDrwData);
if (merged.length !== 1) throw new Error('Green and Cyan on left arm did not merge!');

// Test 3: Far apart same color components do NOT merge
const blueArmPixels = [];
for (let y = 100; y < 110; y++) {
  for (let x = 300; x < 310; x++) blueArmPixels.push(y * workW + x);
}
const blueLegPixels = [];
for (let y = 280; y < 290; y++) {
  for (let x = 300; x < 310; x++) blueLegPixels.push(y * workW + x);
}
fillPixelsWithColor(blueArmPixels, 33, 120, 255);
fillPixelsWithColor(blueLegPixels, 33, 120, 255);

const rBlueArm = createMockRegion('blue', blueArmPixels, blueColor);
const rBlueLeg = createMockRegion('blue', blueLegPixels, blueColor);

candidates = [rBlueArm, rBlueLeg];
merged = sandbox.mergeSameColorRegions(candidates, workW, workH, bodyHeight, alignedDrwData);
if (merged.length !== 2) throw new Error('Far apart blue components incorrectly merged!');

// Test 4: fixMisplacedBlueFragmentsInGreen
const smallBluePixels = [];
for (let y = 112; y < 115; y++) {
  for (let x = 55; x < 57; x++) smallBluePixels.push(y * workW + x);
}
fillPixelsWithColor(smallBluePixels, 33, 120, 255);

const parentGreenPixels = [...greenPixels, ...smallBluePixels];
const rParentGreen = createMockRegion('green', parentGreenPixels, greenColor);

const nearbyBluePixels = [];
for (let y = 100; y < 110; y++) {
  for (let x = 75; x < 85; x++) nearbyBluePixels.push(y * workW + x);
}
fillPixelsWithColor(nearbyBluePixels, 33, 120, 255);
const rNearbyBlue = createMockRegion('blue', nearbyBluePixels, blueColor);

let regionsList = [rParentGreen, rNearbyBlue];
console.log('Before fixMisplacedBlueFragmentsInGreen:', regionsList.map(r => `${r.color} (size=${r.pixels.length})`));

regionsList = sandbox.fixMisplacedBlueFragmentsInGreen(regionsList, workW, workH, alignedDrwData);
console.log('After fixMisplacedBlueFragmentsInGreen:', regionsList.map(r => `${r.color} (size=${r.pixels.length})`));

const greenAfter = regionsList.find(r => r.color === 'green');
const blueAfter = regionsList.find(r => r.color === 'blue');

if (greenAfter.pixels.length !== 100) {
  throw new Error(`Misplaced blue component was not removed from green parent! Final size: ${greenAfter.pixels.length}`);
}
if (blueAfter.pixels.length !== 106) {
  throw new Error(`Misplaced blue component was not merged into blue target! Final size: ${blueAfter.pixels.length}`);
}

console.log('✅ ALL COMPATIBILITY & REASSIGNMENT TESTS PASSED SUCCESSFULLY!');
