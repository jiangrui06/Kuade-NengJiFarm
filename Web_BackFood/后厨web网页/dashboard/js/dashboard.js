// ==============================
// 后厨首页 - dashboard.js
// 后端地址: http://192.168.101.30:7240
// ==============================

const API_BASE = 'http://192.168.101.30:7240';
const PAGE_SIZE = 15;

let currentTab = localStorage.getItem('dashboard_tab') || 'pending';
let currentPage = parseInt(localStorage.getItem('dashboard_page')) || 1;
let allOrders = [];

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
 *   1. 标准包装：{ code: 0/200, message: '...', data: ... }
 *   2. 直接返回数据：[...] 或 { ... }
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

window.addEventListener('beforeunload', function () {
    sessionStorage.setItem('dashboard_scrollY', String(window.scrollY));
});
if (history.scrollRestoration) {
    history.scrollRestoration = 'manual';
}

function clearAuth() {
    localStorage.removeItem('token');
    localStorage.removeItem('user_name');
    localStorage.removeItem('user_id');
    localStorage.removeItem('phone_number');
    localStorage.removeItem('currentOrderId');
    localStorage.removeItem('dashboard_tab');
    localStorage.removeItem('dashboard_page');
}

/** 归一化订单字段名：兼容新旧 API 响应格式 */
function normalizeOrder(o) {
    if (!o) return o;
    return {
        orderId: o.orderId ?? o.id,
        orderNo: o.orderNo ?? o.no,
        createTime: o.createTime ?? o.time,
        tableNumber: o.tableNumber ?? o.table,
        totalAmount: o.totalAmount ?? o.total,
        dishList: (o.dishList || o.items || []).map(d => ({
            dishOrderDetailsId: d.dishOrderDetailsId,
            name: d.name,
            quantity: d.quantity,
            price: d.price,
            status: d.status,
        }))
    };
}

// ========== 页面初始化 ==========
window.onload = function () {
    if (!localStorage.getItem('token') && !localStorage.getItem('user_name')) {
        window.location.href = '../login/index.html';
        return;
    }

    const userName = localStorage.getItem('user_name') || '后厨';
    document.getElementById('current-username').textContent = userName;

    document.querySelectorAll('.tab').forEach(t => {
        const tabName = t.textContent.trim() === '待出餐' ? 'pending' : 'completed';
        t.classList.toggle('active', tabName === currentTab);
    });

    fetchStatistics();
    fetchOrders();

    setInterval(() => {
        fetchStatistics();
        fetchOrders();
    }, 30000);
};

// ========== 获取今日统计数据 ==========
async function fetchStatistics() {
    try {
        const res = await apiFetch('/api/Kitchen/today-statistics');
        const data = await parseApiResponse(res);
        if (data) {
            // 兼容新旧字段名
            const amount = data.todayTotalAmount ?? data.totalAmount ?? data.total ?? 0;
            document.getElementById('total-revenue').textContent = Number(amount).toFixed(2);
        }
    } catch (err) {
        console.error('获取统计数据失败:', err);
    }
}

// ========== 获取订单列表 ==========
async function fetchOrders() {
    const orderList = document.getElementById('order-list');

    try {
        const res = await apiFetch(`/api/Kitchen/order/list?type=${currentTab === 'pending' ? 2 : 4}`);
        let data = await parseApiResponse(res);
        allOrders = Array.isArray(data) ? data : [];

        // 归一化字段名：兼容新旧 API (旧: id/no/time/table/total/items → 新: orderId/orderNo/createTime/tableNumber/totalAmount/dishList)
        allOrders = allOrders.map(normalizeOrder);

        // 打印每个订单的菜品详细信息，排查新订单不显示的问题
        console.log(`[调试] ${currentTab === 'pending' ? '待出餐' : '已出餐'} 接口返回 ${allOrders.length} 条订单`);
        allOrders.forEach(o => {
            const dishes = o.dishList || [];
            const statuses = dishes.map(d => `${d.name}=${d.status}`);
            console.log(`  ${o.orderNo} → 菜品状态: [${statuses.join(', ')}]`);
        });

        // === 客户端二次过滤 ===
        if (currentTab === 'pending') {
            const before = allOrders.length;
            // 新API: status=2=待出餐, 3=已取消, 4=已完成
            allOrders = allOrders.filter(o =>
                (o.dishList || []).some(d => d.status === 2)
            );
            console.log(`[过滤] 待出餐: ${before}条 → ${allOrders.length}条`);
        } else {
            const before = allOrders.length;
            // 已完成：没有待出餐(2)菜品的订单
            allOrders = allOrders.filter(o => {
                const dishes = o.dishList || [];
                return dishes.length > 0 && !dishes.some(d => d.status === 2);
            });
            console.log(`[过滤] 已出餐: ${before}条 → ${allOrders.length}条`);
        }

        if (currentPage > Math.ceil(allOrders.length / PAGE_SIZE)) {
            currentPage = 1;
        }
        renderOrders();

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
            const timeA = localStorage.getItem('order_completed_at_' + a.orderId);
            const timeB = localStorage.getItem('order_completed_at_' + b.orderId);
            const dateA = timeA ? parseInt(timeA) : new Date(a.createTime).getTime();
            const dateB = timeB ? parseInt(timeB) : new Date(b.createTime).getTime();
            return dateB - dateA;
        });
    }

    const totalPages = Math.ceil(orders.length / PAGE_SIZE);
    const start = (currentPage - 1) * PAGE_SIZE;
    const pageOrders = orders.slice(start, start + PAGE_SIZE);

    orderList.innerHTML = pageOrders.map(order => {
        const items = order.dishList || [];
        const total = items.length;
        // 新API: status=2=待出餐, 3=已取消, 4=已完成
        const finished = items.filter(it => it.status === 4).length;
        const cancelled = items.filter(it => it.status === 3).length;

        let progressText, progressClass;
        if (currentTab === 'pending') {
            progressText = cancelled > 0
                ? `${finished}/${total} 已出餐，${cancelled} 已取消`
                : `${finished}/${total} 已出餐`;
            progressClass = cancelled > 0 ? 'has-cancelled' : '';
        } else {
            if (cancelled > 0) {
                progressText = `已出餐 ${finished}，取消出餐 ${cancelled}`;
                progressClass = 'has-cancelled';
            } else {
                progressText = `全部出餐 ✓`;
                progressClass = 'completed';
            }
        }
        const timeStr = formatTime(order.createTime);

        return `
            <div class="order-card" onclick="openOrderDetail(${order.orderId})">
                <div class="order-header">
                    <span class="order-id">订单 ${order.orderNo || order.orderId}</span>
                    <span class="order-time">${timeStr}</span>
                </div>
                <div class="order-info">
                    <span class="table-number">🍽 ${order.tableNumber || '—'}</span>
                    <span class="status ${progressClass}">${progressText}</span>
                </div>
                <div class="order-info">
                    <span style="color:#333;font-weight:bold;">¥${Number(order.totalAmount || 0).toFixed(2)}</span>
                </div>
            </div>
        `;
    }).join('');

    orderList.innerHTML += renderPagination(totalPages);
}

// ========== 渲染分页控件 ==========
function renderPagination(totalPages) {
    if (totalPages <= 1) return '';

    let html = '<div class="pagination">';

    html += `<button class="page-btn" ${currentPage <= 1 ? 'disabled' : ''} onclick="goToPage(${currentPage - 1})">上一页</button>`;

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
        return `${month}月${day}日 ${hh}:${mm}`;
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
function logout() {
    if (!confirm('确定要退出登录吗？')) return;
    clearAuth();
    window.location.href = '../login/index.html';
}
