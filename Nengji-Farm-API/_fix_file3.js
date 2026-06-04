const fs = require("fs");
const path = "F:/blobs/web_management/order-coupon.html";
let content = fs.readFileSync(path, "utf-8");

// The issue: each corrupted CJK char is replaced by U+FFFD (often followed by U+003F)
// Fix: replace known patterns based on preceding character

// Map: preceding_char -> correct_completion (to replace U+FFFD + optional U+003F)
const correctionMap = {
  "管": "管理",
  "状": "状态",
  "订": "订单",
  "登": "登录",
  "失": "失败",
  "获": "获取",
  "重": "重试",
  "成": "成功",
  "退": "退款",
  "隐": "隐藏",
  "表": "表单",
  "数": "数据",
  "张": "张</",
  "码": "码</",
  "信": "信息",
  "败": "失败",
  "功": "功能",
  "试": "重试",
  "取": "获取",
  "据": "数据",
  "踪": "跟踪",
  "组": "数组",
  "核": "核销",
  "单": "订单",
  "加": "加载",
  "描": "描述",
  "符": "符合",
  "吗": "吗？",
  "带": "加载",
  "中": "中...</", // 加载中...
  "款": "款)",
  "态": "状态",
  "簿": "簿记",
  "备": "备注",
  "复": "回复",
  "填": "填）",
  "入": "输入",
  "理": "处理",
  "注": "备注",
};

// Also handle common multi-char patterns where U+FFFD is at a specific position
const multiCharFixes = [
  // 已核销状? -> 已核销状态
  ["已核销状" + "�", "已核销状态"],
  // 退款中状? -> 退款中状态
  ["退款中状" + "�", "退款中状态"],
  // 请求失? -> 请求失败
  ["请求失" + "�", "请求失败"],
  // 订单详情失? -> 订单详情失败
  ["订单详情失" + "�", "订单详情失败"],
  // 券类订单列表失? -> 券类订单列表失败
  ["券类订单列表失" + "�", "券类订单列表失败"],
  // 状态列表失? -> 状态列表失败
  ["状态列表失" + "�", "状态列表失败"],
  // 驳回退款请求失? -> 驳回退款请求失败
  ["驳回退款请求失" + "�", "驳回退款请求失败"],
  // 退款请求失? -> 退款请求失败
  ["退款请求失" + "�", "退款请求失败"],
  // 核销请求失? -> 核销请求失败
  ["核销请求失" + "�", "核销请求失败"],
  // 已核销? -> 已核销吗 (in modal prompt)
  ["已核销�", "已核销吗"],
  // 无? -> 无法
  ["无�", "无法"],
  // 用户信? -> 用户信息
  ["用户信" + "�", "用户信息"],
  // 退款原因（来自用户? -> 退款原因（来自用户）
  ["退款原因（来自用户" + "�", "退款原因（来自用户）"],
  // 驳回退款弹窗已合并到退款弹窗中，此弹窗已隐? -> 隐藏
  ["此弹窗已隐" + "�", "此弹窗已隐藏"],
  // 核销二维? -> 核销二维码
  ["核销二维" + "�", "核销二维码"],
  // 确认退? -> 确认退款 (different context)
  ["确认退" + "�", "确认退款"],
  // 驳回退? -> 驳回退款
  ["驳回退" + "�", "驳回退款"],
  // 驳回退款表? -> 驳回退款表单
  ["驳回退款表" + "�", "驳回退款表单"],
  // 管理员回? -> 管理员回复
  ["管理员回" + "�", "管理员回复"],
  // 请输入处理备? -> 请输入处理备注
  ["请输入处理备" + "�", "请输入处理备注"],
  // （选填? -> （选填）
  ["（选填" + "�", "（选填）"],
  // 退款弹窗中的驳回表? -> 退款弹窗中的驳回表单
  ["退款弹窗中的驳回表" + "�", "退款弹窗中的驳回表单"],
  // 需要同时满足：订单在退款中状? -> 状态
  ["在退款中状" + "�", "在退款中状态"],
  // 先显示加载状? -> 先显示加载状态
  ["先显示加载状" + "�", "先显示加载状态"],
  // 优先?refundInfo -> 优先从refundInfo
  ["优先" + "�" + "refundInfo", "优先从refundInfo"],
  // 中获? -> 中获取
  ["中获" + "�", "中获取"],
  // 其次?refundRecord -> 其次从refundRecord
  ["其次" + "�" + "refundRecord", "其次从refundRecord"],
  // 最后从其他字段获? -> 最后从其他字段获取
  ["从其他字段获" + "�", "从其他字段获取"],
  // 获取订单详情以补充电话等信? -> 获取订单详情以补充电话等信息
  ["补充电话等信" + "�", "补充电话等信息"],
  // API调用的方? -> API调用的方式
  ["API调用的方" + "�", "API调用的方式"],
  // 电话字段检? -> 电话字段检查
  ["电话字段检" + "�", "电话字段检查"],
  // 订单ID缺失，无法退? -> 退款
  ["无法退" + "�", "无法退款"],
  // 退款失败，请重? -> 重试
  ["请重" + "�", "请重试"],
  // 退款成? -> 退款成功
  ["退款成" + "�", "退款成功"],
  // 网络错误，退款请求失败，请重? -> 请重试
  ["失败，请重" + "�", "失败，请重试"],
  // 网络错误，驳回退款请求失败，请重? -> 请重试
  ["失败，请重" + "�", "失败，请重试"], // duplicate but fine
  // 加载中? -> 加载中
  ["加载" + "�", "加载中"],
  // 退出登? -> 退出登录
  ["退出登" + "�", "退出登录"],
  // 活动券管? -> 活动券管理
  ["活动券管" + "�", "活动券管理"],
  // 全部状? -> 全部状态
  ["全部状" + "�", "全部状态"],
  // 订单号? -> 订单号 (as th)
  ["订单" + "�" + "/th>", "订单号</th>"],
  // 暂无符合条件的券类订? -> 暂无符合条件的券类订单
  ["券类订" + "�" + "/div>", "券类订单</div>"],
  // 正常退款按钮：待核销、已核销状? -> 状态
  ["已核销状" + "�" + "-->", "已核销状态-->"],
  // 退款中状态：显示退款按钮（包含处理退款和驳回退款功? -> 功能
  ["退款功" + "�" + "-->", "退款功能）-->"],
  // 响应状? -> 响应状态
  ["响应状" + "�", "响应状态"],
  // 响应内? -> 响应内容
  ["响应内" + "�", "响应内容"],
  // 与描述不? -> 与描述不符
  ["与描述不" + "�", "与描述不符"],
  // 确定要退出登录吗? -> 吗？
  ["登录吗" + "�", "登录吗？"],
  // 已支付? -> 已支付
  ["已支" + "�" + ") {", "已支付) {"],
];

// Also add U+FFFD + U+003F variants for some common ones
const ff = String.fromCodePoint(0xfffd);

// First pass: replace U+FFFD directly with empty (for single-char contexts)
// and U+FFFD + ? with appropriate character

// Better approach: process the entire file character by character
// For each U+FFFD, look at the character BEFORE it and use the correction map
let result = "";
let i = 0;
const text = content;

while (i < text.length) {
  const cp = text.codePointAt(i);
  const charLen = cp > 0xffff ? 2 : 1;

  if (cp === 0xfffd) {
    // Find the preceding CJK or meaningful character
    // Look backwards to find the last non-ASCII character
    let prevChar = "";
    for (let j = result.length - 1; j >= 0; j--) {
      if (result[j] >= '') {
        prevChar = result[j];
        break;
      }
      if (result[j] === '>' || result[j] === "'" || result[j] === '"' || result[j] === '=') {
        break; // Don't go past these markers
      }
    }

    // Check what follows U+FFFD
    let nextIsQuestionMark = false;
    if (i + 1 < text.length) {
      const nextCp = text.codePointAt(i + 1);
      if (nextCp === 0x003f) {
        nextIsQuestionMark = true;
      }
    }

    if (prevChar && correctionMap[prevChar]) {
      const correction = correctionMap[prevChar];
      // Remove the character we just added (the prevChar), then add the correction
      result = result.slice(0, -1) + correction;

      // Check if the correction ends with </ or similar
      if (nextIsQuestionMark) {
        i += 2; // skip U+FFFD + U+003F
      } else {
        i += 1; // skip just U+FFFD
      }
      continue;
    }

    // Fallback: try multi-char fixes by looking at context
    let matched = false;
    for (const [pattern, replacement] of multiCharFixes) {
      if (text.slice(i, i + pattern.length) === pattern) {
        result += replacement;
        i += pattern.length;
        matched = true;
        break;
      }
    }
    if (matched) continue;

    // If we can't determine the correct char, just skip U+FFFD
    if (nextIsQuestionMark) {
      i += 2;
    } else {
      i += 1;
    }
    continue;
  }

  result += charLen === 2 ? String.fromCodePoint(cp) : text[i];
  i += charLen;
}

fs.writeFileSync(path, result, "utf-8");

// Verify
const final = fs.readFileSync(path, "utf-8");
const finalLines = final.split("\n");
let remaining = 0;
for (let i = 0; i < finalLines.length; i++) {
  if (finalLines[i].includes("�")) {
    console.log("REMAINING at line " + (i + 1) + ": " + JSON.stringify(finalLines[i]).slice(0, 120));
    remaining++;
  }
}
console.log(remaining > 0 ? "❌ " + remaining + " remaining" : "✅ All U+FFFD fixed");
