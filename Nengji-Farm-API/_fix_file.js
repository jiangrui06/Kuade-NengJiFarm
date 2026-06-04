const fs = require("fs");
const path = "F:/blobs/web_management/order-coupon.html";
let content = fs.readFileSync(path, "utf-8");

const replacements = [
  // Menu items
  ["活动券管" + "�" + "/li>", "活动券管理</li>"],
  ["退出登" + "�" + "/button>", "退出登录</button>"],

  // Status filter
  ["全部状" + "�" + "/option>", "全部状态</option>"],

  // Table headers
  ["订单状" + "�" + "/th>", "订单状态</th>"],

  // Empty state
  ["暂无符合条件的券类订" + "�" + "/div>", "暂无符合条件的券类订单</div>"],

  // Detail modal
  ["核销二维" + "�" + "/div>", "核销二维码</div>"],
  ["状" + "�" + "/div>", "状态</div>"],
  ["张" + "�" + "/div>", "张</div>"],

  // Writeoff modal
  ["确认将订" + "�" + "<strong>", "确认将订单<strong>"],

  // Refund modal
  ["确认退" + "�" + "/div>", "确认退款</div>"],
  ["驳回退" + "�" + "/div>", "驳回退款</div>"],
  ["退款原因（来自用户" + "�" + "-->", "退款原因（来自用户）-->"],
  ["退款原因（来自用户" + "�" + "/div>", "退款原因（来自用户）</div>"],
  ["驳回退款表" + "�" + "-->", "驳回退款表单-->"],
  ["管理员回复" + "�" + "<span", "管理员回复<span"],
  ["请输入处理备" + "�" + ">", "请输入处理备注>"],

  // Comments
  ["正常退款按钮：待核销、已核销状" + "�" + "-->", "正常退款按钮：待核销、已核销状态-->"],
  ["退款中状态：显示退款按钮（包含处理退款和驳回退款功" + "�" + "-->", "退款中状态：显示退款按钮（包含处理退款和驳回退款功能）-->"],
  ["已核销状" + "�" + "-->", "已核销状态-->"],
  ["弹窗已隐" + "�" + "-->", "弹窗已隐藏-->"],
  ["状" + "�" + "-->", "状态-->"],

  // JS comments - ending with U+FFFD
  ["退款弹窗中的驳回表" + "�", "退款弹窗中的驳回表单"],
  ["需要同时满足：订单在退款中状" + "�" + "AND 有退款ID", "需要同时满足：订单在退款中状态 AND 有退款ID"],
  ["先显示加载状" + "�", "先显示加载状态"],
  ["优先" + "�" + "refundInfo 中获" + "�", "优先从 refundInfo 中获取"],
  ["其次" + "�" + "refundRecord 中获" + "�", "其次从 refundRecord 中获取"],
  ["最后从其他字段获" + "�", "最后从其他字段获取"],
  ["获取订单详情以补充电话等信" + "�", "获取订单详情以补充电话等信息"],
  ["API调用的方" + "�", "API调用的方式"],

  // console.log/error messages
  ["获取状态列表失" + "�", "获取状态列表失败"],
  ["响应状" + "�" + ":", "响应状态:"],
  ["响应内" + "�" + ":", "响应内容:"],
  ["JSON解析失" + "�" + ":", "JSON解析失败:"],
  ["API返回失" + "�" + ":", "API返回失败:"],
  ["响应数据不是数" + "�" + ":", "响应数据不是数组:"],
  ["获取券类订单列表失" + "�" + ":", "获取券类订单列表失败:"],
  ["请求券类订单详" + "�" + ":", "请求券类订单详情:"],
  ["券类订单详情数" + "�" + ":", "券类订单详情数据:"],
  ["电话字段检" + "�" + ":", "电话字段检查:"],
  ["获取订单详情失" + "�" + ":", "获取订单详情失败:"],
  ["获取券类订单详情失" + "�" + ":", "获取券类订单详情失败:"],
  ["核销请求失" + "�" + ":", "核销请求失败:"],
  ["退款请求失" + "�" + ":", "退款请求失败:"],
  ["驳回退款请求失" + "�" + ":", "驳回退款请求失败:"],
  ["获取状态列表失" + "�" + ":", "获取状态列表失败:"],

  // alert messages
  ["订单ID缺失，无法退" + "�" + ")", "订单ID缺失，无法退款)"],
  ["退款失败，请重" + "�" + ")", "退款失败，请重试)"],
  ["退款成" + "�" + ")", "退款成功)"],
  ["网络错误，退款请求失败，请重" + "�" + ")", "网络错误，退款请求失败，请重试)"],
  ["网络错误，驳回退款请求失败，请重" + "�" + ")", "网络错误，驳回退款请求失败，请重试)"],
  ["驳回退款失败，请重" + "�" + ")", "驳回退款失败，请重试)"],

  // JS status string corruptions in canWriteOff/canRefund/isRefunding functions
  // These have multiple U+FFFD chars. We'll use the context approach.
  // Line 881: return order.statusName === 'ɿɿɿɿɿɿ';
  // Fix: anything between ' after statusName === that ends with '
  // Actually, use specific approach for each known location

  // getPaymentStatusClass
  ["已支" + "�" + ") {", "已支付) {"],

  // reasonMap
  ["与描述不" + "�" + ",", "与描述不符,"],
  ["确定要退出登录吗" + "�" + ")", "确定要退出登录吗？)"],

  // Sidebar
  ["活动券管" + "�" + "/li>", "活动券管理</li>"],

  // "�" in data comments
  ["加载" + "�" + "..", "加载中..."],

  // closing </span> with U+FFFD before
  ["（选填" + "�" + "</span>", "（选填）</span>"],
];

for (const [from, to] of replacements) {
  content = content.split(from).join(to);
}

// Now handle the status string corruptions in JS functions
// These have variable-length U+FFFD sequences
// Use regex for these
content = content.replace(/'(?:��)+'/g, "'待核销'");  // 5+ U+FFFD chars
content = content.replace(/'���˿�'/g, "'已退款'");
content = content.replace(/'�Ѻ���'/g, "'已核销'");
content = content.replace(/'�˿���'/g, "'退款中'");

// Fix remaining single U+FFFD
// Check for "�" surrounded by Chinese characters where we know the meaning
content = content.replace(/"已核" /g, "已核销");  // line where description says 已核销

fs.writeFileSync(path, content, "utf-8");
console.log("All fixes applied");

// Final verification
const final = fs.readFileSync(path, "utf-8");
const lines = final.split("\n");
let remaining = 0;
for (let i = 0; i < lines.length; i++) {
  if (lines[i].includes("�")) {
    console.log("REMAINING at line " + (i + 1) + ": " + JSON.stringify(lines[i]).slice(0, 120));
    remaining++;
  }
}
console.log(remaining > 0 ? "❌ " + remaining + " remaining" : "✅ All U+FFFD fixed");
