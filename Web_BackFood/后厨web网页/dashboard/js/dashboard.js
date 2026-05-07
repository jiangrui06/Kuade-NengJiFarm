// ==============================
// 后厨首页 - dashboard.js
// API:
//   GET /api/Kitchen/order/list?type=0|1
//   GET /api/Kitchen/today-statistics
//   POST /api/Kitchen/logout
// ==============================

const API_BASE = 'http://192.168.101.30:7240/api/Kitchen';

/** 当前激活的 tab：'pending'(待出餐) | 'completed'(已出餐) */
let currentTab = 'pending';

/** 封装带 token 的请求，返回原始 Response */
async function apiFetch(path, options = {}) {
    const token = localStorage.getItem('token');
    const headers = {
        'Content-Type': 'application/json',
        ...(token ? { 'Authorization': `Bearer ${token}` } : {}),
        ...(options.headers || {})
    };
    const res = await fetch(`${API_BASE}${path}`, { ...options, headers });
    if (res.status === 401) {
        clearAuth();
        window.location.href = '../login/index.html';
        throw new Error('未授权，请重新登录');
    }
    return res;
}

/**
 * 解析后端响应，兼容两种格式：
 *   1. 标准包装：{ code: 0/200, message: '...', data: ... }，code=0或200表示成功
 *   2. 直接返回数据：[...] 或 { ... }
 * 成功返回 data，失败抛出含 message 的 Error
 */
async function parseApiResponse(res) {
    if (!res.ok) throw new Error(`HTTP ${res.status}`);
    const json = await res.json();
    console.log('API返回:', json);
    // 标准包装格式，code=0或200表示成功
    if (json && typeof json === 'object' && 'code' in json) {
        if (json.code === 0 || json.code === 200) return json.data;
        throw new Error(json.message || json.msg || '接口返回错误');
    }
    // 直接返回数据
    return json;
}

/** 清除认证信息 */
function clearAuth() {
    localStorage.removeItem('token');
    localStorage.removeItem('user_name');
    localStorage.removeItem('user_id');
    localStorage.removeItem('phone_number');
    localStorage.removeItem('currentOrderId');
}

// ========== 页面初始化 ==========
window.onload = function () {
    if (!localStorage.getItem('token')) {
        window.location.href = '../login/index.html';
        return;
    }

    // 显示用户名
    const userName = localStorage.getItem('user_name') || '后厨';
    document.getElementById('current-username').textContent = userName;

    // 加载统计数据和订单列表
    fetchStatistics();
    fetchOrders();

    // 每 30 秒自动刷新
    setInterval(() => {
        fetchStatistics();
        fetchOrders();
    }, 30000);
};

// ========== 获取今日统计数据 ==========
async function fetchStatistics() {
    try {
        const res = await apiFetch('/today-statistics');
        const data = await parseApiResponse(res);
        if (data && data.todayTotalAmount != null) {
            document.getElementById('total-revenue').textContent =
                Number(data.todayTotalAmount).toFixed(2);
        }
    } catch (err) {
        console.error('获取统计数据失败:', err);
    }
}

// ========== 获取订单列表 ==========
async function fetchOrders() {
    const orderList = document.getElementById('order-list');
    orderList.innerHTML = '<div class="no-orders">加载中...</div>';

    // type: 0=待出餐, 1=已出餐
    const type = currentTab === 'pending' ? 0 : 1;

    try {
        const res = await apiFetch(`/order/list?type=${type}`);
        const data = await parseApiResponse(res);
        renderOrders(Array.isArray(data) ? data : []);
    } catch (err) {
        console.error('获取订单失败:', err);
        orderList.innerHTML = `<div class="no-orders">获取订单失败：${err.message}</div>`;
    }
}

// ========== 渲染订单列表 ==========
function renderOrders(orders) {
    const orderList = document.getElementById('order-list');

    if (!orders || orders.length === 0) {
        const label = currentTab === 'pending' ? '待出餐' : '已出餐';
        orderList.innerHTML = `<div class="no-orders">暂无${label}订单</div>`;
        return;
    }

    orderList.innerHTML = orders.map(order => {
        // items 是简要菜品列表，统计出餐进度
        const items = order.items || [];
        const total = items.length;
        // status: 0或1=未出餐，2=已出餐
        const finished = items.filter(it => it.status === 2).length;

        const progressText = currentTab === 'pending'
            ? `${finished}/${total} 已出餐`
            : `全部出餐 ✓`;

        const progressClass = currentTab === 'pending' ? '' : 'completed';

        // 格式化时间（ISO 8601 → 可读时间）
        const timeStr = formatTime(order.time);

        return `
            <div class="order-card" onclick="openOrderDetail(${order.id})">
                <div class="order-header">
                    <span class="order-id">订单 ${order.no || order.id}</span>
                    <span class="order-time">${timeStr}</span>
                </div>
                <div class="order-info">
                    <span class="table-number">🍽 ${order.table || '—'}</span>
                    <span class="status ${progressClass}">${progressText}</span>
                </div>
                <div class="order-info">
                    <span style="color:#999;font-size:13px;">
                        菜品：${items.map(it => it.name).join('、') || '—'}
                    </span>
                    <span style="color:#333;font-weight:bold;">¥${Number(order.total || 0).toFixed(2)}</span>
                </div>
            </div>
        `;
    }).join('');
}

/** 格式化 ISO 时间 */
function formatTime(isoStr) {
    if (!isoStr) return '—';
    try {
        const d = new Date(isoStr);
        const hh = String(d.getHours()).padStart(2, '0');
        const mm = String(d.getMinutes()).padStart(2, '0');
        const month = d.getMonth() + 1;
        const day = d.getDate();
        return `${month}/${day} ${hh}:${mm}`;
    } catch {
        return isoStr;
    }
}

// ========== 切换 Tab ==========
function switchTab(tab) {
    currentTab = tab;
    document.querySelectorAll('.tab').forEach(t => t.classList.remove('active'));
    event.currentTarget.classList.add('active');
    fetchOrders();
}

// ========== 跳转订单详情 ==========
function openOrderDetail(orderId) {
    localStorage.setItem('currentOrderId', orderId);
    window.location.href = '../order-detail/index.html';
}

// ========== 退出登录 ==========
async function logout() {
    if (!confirm('确定要退出登录吗？')) return;
    try {
        // 调用登出接口（忽略失败，强制清除本地状态）
        await apiFetch('/logout', { method: 'POST' }).catch(() => {});
    } finally {
        clearAuth();
        window.location.href = '../login/index.html';
    }
}

// ========== 清理缓存（刷新数据） ==========
function clearCache() {
    if (confirm('确定要刷新数据吗？')) {
        fetchStatistics();
        fetchOrders();
    }
}
