const fs = require('fs');
const vm = require('vm');

try {
  const html = fs.readFileSync('web/index.html', 'utf8');
  // Find script content
  const startTag = '<script>';
  const endTag = '</script>';
  const startIndex = html.indexOf(startTag);
  const endIndex = html.lastIndexOf(endTag);
  
  if (startIndex === -1 || endIndex === -1) {
    console.error('Could not find script tags');
    process.exit(1);
  }
  
  const jsCode = html.substring(startIndex + startTag.length, endIndex);
  
  // Verify js syntax
  new vm.Script(jsCode);
  console.log('✅ index.html JavaScript syntax is clean!');
} catch (err) {
  console.error('❌ Syntax error detected in index.html script tag:');
  console.error(err);
  process.exit(1);
}
