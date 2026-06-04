const fs = require("fs");
const path = "F:/blobs/web_management/order-coupon.html";
const content = fs.readFileSync(path, "utf-8");
const lines = content.split("\n");

// Get the character after 管 in line 412
const l412 = lines[411];
const idx = l412.indexOf("管");
console.log("Char at pos", idx + 1);
console.log("Code point (hex):", l412.codePointAt(idx + 1).toString(16));
console.log("Char repr:", JSON.stringify(l412[idx + 1]));
console.log("Char:", l412[idx + 1]);

// The char in the file
const fileChar = l412[idx + 1];

// Create a simple blob with U+FFFD and compare
const ff = String.fromCodePoint(0xfffd);
console.log("FFFD from code:", JSON.stringify(ff));
console.log("FFFD matches file:", ff === fileChar);
console.log("FFFD byte length:", Buffer.from(ff).length);
console.log("fileChar byte length:", Buffer.from(fileChar).length);
console.log("fileChar bytes:", Buffer.from(fileChar).toString("hex"));
console.log("FFFD bytes:", Buffer.from(ff).toString("hex"));
