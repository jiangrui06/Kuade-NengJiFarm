const fs = require("fs");
const path = "F:/blobs/web_management/order-coupon.html";

// Read current file to keep the ASCII structure
const content = fs.readFileSync(path, "utf-8");
let lines = content.split("\n");

// MANUALLY fix each remaining problematic line
// These are all the lines with U+FFFD that my replace approach couldn't match
const lineFixes = {
  // Use array approach: for each line with U+FFFD, replace its text
};

// Strategy: for each known corrupted pattern, find and replace
// The issue is U+FFFD is a singleton character, so split().join() should work on it
// Let me verify the issue

// Check what character '?' actually is in the file
const line412 = lines[411];
for (let i = 0; i < line412.length; i++) {
  const cp = line412.codePointAt(i);
  if (cp > 127) {
    console.log("Line 412 pos " + i + ": U+" + cp.toString(16).toUpperCase() + " = " + line412[i]);
    if (cp >= 0x10000) i++; // skip surrogate
  }
}

// I bet the issue is that what looks like '�' is actually being split() by a DIFFERENT character
// Let me check the exact code point
let ffdfound = 0;
let qmarkfound = 0;
for (let i = 411; i < 700; i++) {
  for (let j = 0; j < lines[i].length; j++) {
    const cp = lines[i].codePointAt(j);
    if (cp === 0xFFFD) { ffdfound++; }
    if (cp === 0x003F && lines[i][j-1] && lines[i][j-1].codePointAt(0) > 127) {
      // question mark right after CJK char could be a corruption artifact
      qmarkfound++;
    }
  }
}
console.log("U+FFFD count:", ffdfound);
console.log("? after CJK count:", qmarkfound);
