// ==============================
// 后厨首页 - dashboard.js
// API:
//   GET /api/Kitchen/order/list?type=0|1
//   GET /api/Kitchen/today-statistics
//   POST /api/Kitchen/logout
// ==============================

const API_BASE = 'http://192.168.101.30:7240/api/Kitchen';
const PAGE_SIZE = 15;

/** 当前激活的 tab：'pending'(待出餐) | 'completed'(已出餐) */
let currentTab = localStorage.getItem('dashboard_tab') || 'pending';
/** 当前页码 */
let currentPage = parseInt(localStorage.getItem('dashboard_page')) || 1;
/** 全部订单数据（不分页，用于前端分页切割） */
let allOrders = [];

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
    if (json && typeof json === 'object' && 'code' in json) {
        if (json.code === 0 || json.code === 200) return json.data;
        throw new Error(json.message || json.msg || '接口返回错误');
    }
    return json;
}

// 刷新前保存滚动位置，刷新后恢复
window.addEventListener('beforeunload', function () {
    sessionStorage.setItem('dashboard_scrollY', String(window.scrollY));
});
// 禁止浏览器默认的滚动恢复（与我们手动恢复冲突）
if (history.scrollRestoration) {
    history.scrollRestoration = 'manual';
}

/** 清除认证信息 */
function clearAuth() {
    localStorage.removeItem('token');
    localStorage.removeItem('user_name');
    localStorage.removeItem('user_id');
    localStorage.removeItem('phone_number');
    localStorage.removeItem('currentOrderId');
    localStorage.removeItem('dashboard_tab');
    localStorage.removeItem('dashboard_page');
}

// ========== 页面初始化 ==========
window.onload = function () {
    if (!localStorage.getItem('token')) {
        window.location.href = '../login/index.html';
        return;
    }

    const userName = localStorage.getItem('user_name') || '后厨';
    document.getElementById('current-username').textContent = userName;

    // 恢复上次的 tab 激活状态
    document.querySelectorAll('.tab').forEach(t => {
        const tabName = t.textContent.trim() === '待出餐' ? 'pending' : 'completed';
        t.classList.toggle('active', tabName === currentTab);
    });

    fetchStatistics();
    fetchOrders();

    // 每 30 秒自动刷新（只刷新数据，不闪烁）
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

    const type = currentTab === 'pending' ? 0 : 1;

    try {
        const res = await apiFetch(`/order/list?type=${type}`);
        const data = await parseApiResponse(res);
        allOrders = Array.isArray(data) ? data : [];
        // 切页时复位页码
        if (currentPage > Math.ceil(allOrders.length / PAGE_SIZE)) {
            currentPage = 1;
        }
        renderOrders();
        // 恢复刷新前的滚动位置（仅在初次加载时）
        const savedY = sessionStorage.getItem('dashboard_scrollY');
        if (savedY) {
            requestAnimationFrame(() => {
                window.scrollTo(0, parseInt(savedY));
                sessionStorage.removeItem('dashboard_scrollY');
            });
        }
    } catch (err) {
        console.error('获取订单失败:', err);
        orderList.innerHTML = `<div class="no-orders">获取订单失败：${err.message}</div>`;
    }
}

// ========== 渲染订单列表（当前页） ==========
function renderOrders() {
    const orderList = document.getElementById('order-list');
    let orders = [...allOrders];

    if (!orders || orders.length === 0) {
        const label = currentTab === 'pending' ? '待出餐' : '已出餐';
        orderList.innerHTML = `<div class="no-orders">暂无${label}订单</div>`;
        return;
    }

    // 已出餐按完成时间倒序
    if (currentTab === 'completed') {
        orders.sort((a, b) => {
            const timeA = localStorage.getItem('order_completed_at_' + a.id);
            const timeB = localStorage.getItem('order_completed_at_' + b.id);
            const dateA = timeA ? parseInt(timeA) : new Date(a.time).getTime();
            const dateB = timeB ? parseInt(timeB) : new Date(b.time).getTime();
            return dateB - dateA;
        });
    }

    // 分页切割
    const totalPages = Math.ceil(orders.length / PAGE_SIZE);
    const start = (currentPage - 1) * PAGE_SIZE;
    const pageOrders = orders.slice(start, start + PAGE_SIZE);

    // 渲染订单卡片
    orderList.innerHTML = pageOrders.map(order => {
        const items = order.items || [];
        const total = items.length;
        const finished = items.filter(it => it.status === 2).length;

        const progressText = currentTab === 'pending'
            ? `${finished}/${total} 已出餐`
            : `全部出餐 ✓`;

        const progressClass = currentTab === 'pending' ? '' : 'completed';
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

    // 渲染分页控件
    orderList.innerHTML += renderPagination(totalPages);
}

// ========== 渲染分页控件 ==========
function renderPagination(totalPages) {
    if (totalPages <= 1) return '';

    let html = '<div class="pagination">';

    // 上一页
    html += `<button class="page-btn" ${currentPage <= 1 ? 'disabled' : ''} onclick="goToPage(${currentPage - 1})">上一页</button>`;

    // 页码
    const maxVisible = 5;
    let startPage = Math.max(1, currentPage - Math.floor(maxVisible / 2));
    let endPage = Math.min(totalPages, startPage + maxVisible - 1);
    if (endPage - startPage + 1 < maxVisible) {
        startPage = Math.max(1, endPage - maxVisible + 1);
    }

    if (startPage > 1) {
        html += `<button class="page-btn" onclick="goToPage(1)">1</button>`;
        if (startPage > 2) html += `<span class="page-info">...</span>`;
    }

    for (let i = startPage; i <= endPage; i++) {
        html += `<button class="page-btn ${i === currentPage ? 'active' : ''}" onclick="goToPage(${i})">${i}</button>`;
    }

    if (endPage < totalPages) {
        if (endPage < totalPages - 1) html += `<span class="page-info">...</span>`;
        html += `<button class="page-btn" onclick="goToPage(${totalPages})">${totalPages}</button>`;
    }

    // 下一页
    html += `<button class="page-btn" ${currentPage >= totalPages ? 'disabled' : ''} onclick="goToPage(${currentPage + 1})">下一页</button>`;

    html += `<span class="page-info">第 ${currentPage}/${totalPages} 页</span>`;
    html += '</div>';

    return html;
}

// ========== 切换页码 ==========
function goToPage(page) {
    currentPage = page;
    localStorage.setItem('dashboard_page', String(page));
    renderOrders();
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
    currentPage = 1;
    localStorage.setItem('dashboard_tab', tab);
    localStorage.setItem('dashboard_page', '1');
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
        await apiFetch('/logout', { method: 'POST' }).catch(() => {});
    } finally {
        clearAuth();
        window.location.href = '../login/index.html';
    }
}
