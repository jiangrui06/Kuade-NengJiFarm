const fs = require("fs");
let content = fs.readFileSync("F:/blobs/web_management/order-coupon.html", "utf-8");

// Fix missing < in closing tags (that lost their < due to encoding misalignment)
// Be careful: /div> could also appear in URLs or JS strings
// We need to match only HTML closing tags
content = content.replace(/\/(div|li|th|option|span|strong)\>/g, "</$1>");

// Fix specific issues from the character-by-character recovery
content = content.replace("订订单/th>", "订单号</th>");
content = content.replace("已支) {", "已支付) {");
content = content.replace("与描述不,", "与描述不符,");
content = content.replace("优先refundInfo", "优先从 refundInfo");
content = content.replace("其次refundRecord", "其次从 refundRecord");
content = content.replace("电话字段检", "电话字段检查");
content = content.replace("加载..", "加载中...");
content = content.replace("加载状态?", "加载状态");

// Fix refund reason
content = content.replace("退款原因（来自用户/div>", "退款原因（来自用户）</div>");
content = content.replace("退款原因（来自用户-->", "退款原因（来自用户）-->");

// Fix textarea placeholder
content = content.replace("请输入处理备注?>", '请输入处理备注">');

// Fix admin reply span
content = content.replace("管理员回复?<span", "管理员回复<span");

// Fix user-avatar
content = content.replace('<div class="user-avatar">/div>', '<div class="user-avatar">用</div>');

// Fix alert messages (missing closing quote)
// Since the alerts lost their closing ', we need to fix them carefully
// Pattern: alert('text without closing quote);
content = content.replace("alert('订单ID缺失，无法退款);", "alert('订单ID缺失，无法退款');");
content = content.replace("alert(FarmAPI.getErrorMessage(data, '退款失败，请重试));", "alert(FarmAPI.getErrorMessage(data, '退款失败，请重试'));");
content = content.replace("alert('退款成功);", "alert('退款成功');");
content = content.replace("alert('网络错误，退款请求失败，请重试);", "alert('网络错误，退款请求失败，请重试');");
content = content.replace("alert(FarmAPI.getErrorMessage(data, '驳回退款失败，请重试));", "alert(FarmAPI.getErrorMessage(data, '驳回退款失败，请重试'));");
content = content.replace("alert('网络错误，驳回退款请求失败，请重试);", "alert('网络错误，驳回退款请求失败，请重试');");

// Fix reasonMap
content = content.replace("'not_as_described': '与描述不,", "'not_as_described': '与描述不符',");

// Fix quantity display with "张"
content = content.replace("{{ activeCouponOrder.quantity || 1 }} /div>", "{{ activeCouponOrder.quantity || 1 }} 张</div>");

// Fix 核销二维码
content = content.replace("核销二维码/div>", "核销二维码</div>");

// Fix "状态/div>" in detail modal
content = content.replace("状态/div>", "状态</div>");

// Fix user loading text
content = content.replace("userPhone: '加载..'", "userPhone: '加载中...'");

// Fix confirmWriteOff modal
content = content.replace("确认将订单<strong>", "确认将订单 <strong>");

// Fix check in getPaymentStatusClass
content = content.replace("已支) {", "已支付) {");

// Fix (选填） back to （选填）
content = content.replace("（选填)/span>", "（选填）</span>");

// Fix quantity display in activeCouponOrder modal (line ~553)
// This one lost the "张" before </div>
content = content.replace(/(quantity \|\| 1 \}) <\/div>/g, "$1 张</div>");

// Fix button text in refund modal
// Line 666-667 had garbled content - check if button text is correct

fs.writeFileSync("F:/blobs/web_management/order-coupon.html", content, "utf-8");
console.log("Done");

// Quick verification of key lines
const lines = content.split("\n");
const checkLines = [411, 421, 424, 444, 462, 463, 486, 496, 552, 555, 561, 611, 622, 646, 648, 651, 654, 658, 659, 671, 675, 806, 880, 921, 1004, 1005, 1012, 1018, 1050, 1064, 1065, 1126, 1151, 1156, 1158, 1159, 1211, 1218, 1219, 1235, 1288, 1299];
for (const ln of checkLines) {
  if (ln < lines.length) {
    const line = lines[ln - 1];
    // Only show lines that have issues
    if (line.includes("FFFD") || line.includes("/div>") && !line.includes("</div>")) {
      console.log("ISSUE at line " + ln + ": " + JSON.stringify(line).slice(0, 100));
    }
  }
}
console.log("Check complete");
