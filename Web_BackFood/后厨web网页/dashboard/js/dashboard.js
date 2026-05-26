// ==============================
// 后厨首页 - dashboard.js
// 后端地址: https://api.nengjifarm.com
// ==============================

const API_BASE = 'https://api.nengjifarm.com';
const PAGE_SIZE = 15;

// API 文档: status=1=待出餐, 2=已出餐, 3=已取消
const DISH_STATUS = { PENDING: 1, FINISHED: 2, CANCELLED: 3 };

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
    // 清理本地菜品状态数据
    for (let i = localStorage.length - 1; i >= 0; i--) {
        const key = localStorage.key(i);
        if (key && (key.startsWith('order_dishes_') || key.startsWith('order_completed_at_'))) {
            localStorage.removeItem(key);
        }
    }
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

/** 从 localStorage 读取本地保存的菜品状态（由 order-detail.js 写入） */
function loadLocalDishStatuses(orderId) {
    if (!orderId) return null;
    try {
        const data = localStorage.getItem('order_dishes_' + orderId);
        return data ? JSON.parse(data) : null;
    } catch (e) {
        return null;
    }
}

function saveDishStatusLocal(orderId, dishList) {
    if (!orderId || !dishList) return;
    try {
        localStorage.setItem('order_dishes_' + orderId, JSON.stringify(dishList));
    } catch (e) {}
}

// ========== 页面初始化 ==========
window.onload = function () {
    if (!localStorage.getItem('token') && !localStorage.getItem('user_name')) {
        window.location.href = '../login/index.html';
        return;
    }

    const userName = localStorage.getItem('user_name') || '后厨';
    document.getElementById('current-username').textContent = userName;
    const avatar = document.getElementById('user-avatar');
    if (avatar) avatar.textContent = userName.charAt(0).toUpperCase();

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
            document.getElementById('total-revenue').textContent =
                (data.todayTotalAmount || 0).toFixed(2);
            // stat-total-orders / stat-finished-orders 由 fetchOrders 根据实际拉取数据更新
        }
    } catch (err) {
        console.error('获取统计数据失败:', err);
    }
}

// ========== 获取订单列表 ==========
async function fetchOrders() {
    const orderList = document.getElementById('order-list');

    try {
        // 同时拉取待出餐(type=2)和已完成(type=3)的订单，合并后用本地状态重新分类
        const [resPending, resCompleted] = await Promise.all([
            apiFetch('/api/Kitchen/order/list?type=2'),
            apiFetch('/api/Kitchen/order/list?type=3')
        ]);

        let pendingData = await parseApiResponse(resPending);
        let completedData = await parseApiResponse(resCompleted);

        // 记录哪些订单来自"已完成"API，用于无菜品数据时的分类判断
        const completedOrderIds = new Set();
        (Array.isArray(completedData) ? completedData : []).forEach(o => {
            const id = o.orderId ?? o.id;
            if (id != null) completedOrderIds.add(id);
        });

        // 合并去重（以先出现的为准）
        const orderMap = new Map();
        (Array.isArray(pendingData) ? pendingData : []).forEach(o => {
            const id = o.orderId ?? o.id;
            if (id != null && !orderMap.has(id)) orderMap.set(id, o);
        });
        (Array.isArray(completedData) ? completedData : []).forEach(o => {
            const id = o.orderId ?? o.id;
            if (id != null && !orderMap.has(id)) orderMap.set(id, o);
        });

        allOrders = [...orderMap.values()].map(normalizeOrder);

        // 更新"今日订单"统计为厨房实际可见的订单数（不含后厨不可见的待付款/已取消订单）
        document.getElementById('stat-total-orders').textContent = allOrders.length;

        // 用本地保存的菜品状态覆盖 API 数据
        allOrders.forEach(o => {
            const localDishes = loadLocalDishStatuses(o.orderId);
            if (localDishes && localDishes.length > 0) {
                if (o.dishList.length === 0) {
                    o.dishList = localDishes;
                } else {
                    const localMap = new Map();
                    localDishes.forEach(d => {
                        if (d.dishOrderDetailsId != null) localMap.set(d.dishOrderDetailsId, d);
                    });
                    o.dishList.forEach(d => {
                        const local = localMap.get(d.dishOrderDetailsId);
                        if (local) {
                            if (local.status !== undefined) d.status = local.status;
                            // 列表接口可能不返回价格和数量，从本地缓存补充
                            if (!d.price || d.price === 0) d.price = local.price;
                            if (!d.quantity || d.quantity === 0) d.quantity = local.quantity;
                        }
                    });
                }
            }
        });

        // 补充已出餐但价格缺失的菜品：从订单详情接口获取
        const needPriceOrders = allOrders.filter(o =>
            (o.dishList || []).some(d => d.status === DISH_STATUS.FINISHED && (!d.price || d.price === 0 || !d.quantity || d.quantity === 0))
        );
        if (needPriceOrders.length > 0) {
            console.log(`需要从详情接口补充价格的订单: ${needPriceOrders.length} 个`);
            await Promise.all(needPriceOrders.map(async order => {
                try {
                    const res = await apiFetch(`/api/Kitchen/order/detail?orderId=${order.orderId}`);
                    const detail = await parseApiResponse(res);
                    if (detail) {
                        const detailOrder = normalizeOrder(detail);
                        const detailMap = new Map();
                        (detailOrder.dishList || []).forEach(d => {
                            if (d.dishOrderDetailsId != null) detailMap.set(d.dishOrderDetailsId, d);
                        });
                        let updated = false;
                        order.dishList.forEach(d => {
                            const detailDish = detailMap.get(d.dishOrderDetailsId);
                            if (detailDish) {
                                if (!d.price || d.price === 0) { d.price = detailDish.price; updated = true; }
                                if (!d.quantity || d.quantity === 0) { d.quantity = detailDish.quantity; updated = true; }
                            }
                        });
                        if (updated) {
                            saveDishStatusLocal(order.orderId, order.dishList);
                            console.log(`  ✓ 已补充订单 ${order.orderNo} 的价格数据`);
                        }
                    }
                } catch (err) {
                    console.warn(`  获取订单 ${order.orderId} 详情失败:`, err);
                }
            }));
        }

        // 计算已完成订单数（所有菜品均已处理，无待出餐）
        const completedCount = allOrders.filter(o => {
            const dishes = o.dishList || [];
            return dishes.length > 0 && dishes.every(d => d.status !== DISH_STATUS.PENDING);
        }).length;
        document.getElementById('stat-finished-orders').textContent = completedCount;

        // 根据菜品实际出餐状态分类：有待出餐→待出餐Tab，全部处理完→已出餐Tab
        const filtered = allOrders.filter(o => {
            const dishes = o.dishList || [];
            // 没有菜品数据时，按后端返回的订单类型决定显示在哪个 Tab
            if (dishes.length === 0) return completedOrderIds.has(o.orderId) ? currentTab === 'completed' : currentTab === 'pending';

            if (currentTab === 'pending') {
                // 待出餐 Tab：还有菜品是待出餐状态的订单
                return dishes.some(d => d.status === DISH_STATUS.PENDING);
            } else {
                // 已完成 Tab：所有菜品都已处理（没有待出餐的）
                return dishes.every(d => d.status !== DISH_STATUS.PENDING);
            }
        });

        console.log(`[${currentTab}] 合并后 ${allOrders.length} 条，过滤后 ${filtered.length} 条`);
        filtered.forEach(o => {
            const dishes = o.dishList || [];
            const statuses = dishes.map(d => `${d.name}=${d.status}`);
            console.log(`  ${o.orderNo} → [${statuses.join(', ')}]`);
        });

        allOrders = filtered;

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
        const label = currentTab === 'pending' ? '待出餐' : '已完成订单';
        orderList.innerHTML = `<div class="no-orders">暂无${label}订单</div>`;
        return;
    }

    // 待出餐按下单时间倒序（最新的在上面）
    if (currentTab === 'pending') {
        orders.sort((a, b) => {
            const timeA = a.createTime ? new Date(a.createTime).getTime() : 0;
            const timeB = b.createTime ? new Date(b.createTime).getTime() : 0;
            return timeB - timeA;
        });
    }

    // 已出餐按完成时间倒序（最新完成的在上面）
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
        // API文档: status=1=待出餐, 2=已出餐, 3=已取消
        const finished = items.filter(it => it.status === DISH_STATUS.FINISHED).length;
        const cancelled = items.filter(it => it.status === DISH_STATUS.CANCELLED).length;

        let progressText, progressClass, cardClass;
        if (total === 0) {
            progressText = currentTab === 'pending' ? '待出餐' : '已出餐';
            progressClass = currentTab === 'pending' ? 'pending' : 'completed';
            cardClass = currentTab === 'pending' ? 'card-pending' : 'card-completed';
        } else if (currentTab === 'pending') {
            cardClass = 'card-pending';
            progressText = cancelled > 0
                ? `${finished}/${total} 已出餐，${cancelled} 已取消`
                : `${finished}/${total} 已出餐`;
            progressClass = cancelled > 0 ? 'has-cancelled' : 'pending';
        } else {
            cardClass = 'card-completed';
            if (cancelled > 0) {
                progressText = `已出餐 ${finished}，取消 ${cancelled}`;
                progressClass = 'has-cancelled';
            } else {
                progressText = `全部出餐`;
                progressClass = 'completed';
            }
        }

        const dotClass = progressClass === 'pending' ? 'pending' : (progressClass === 'has-cancelled' ? '' : 'completed');
        const timeStr = formatTime(order.createTime);

        return `
            <div class="order-card ${cardClass}" onclick="openOrderDetail(${order.orderId})">
                <div class="order-card-top">
                    <span class="order-no">#${order.orderNo || order.orderId}</span>
                    <span class="order-time">${timeStr}</span>
                </div>
                <div class="order-card-body">
                    <span class="order-table">
                        <svg viewBox="0 0 16 16" fill="none" width="14" height="14" stroke="currentColor" stroke-width="1.4" stroke-linecap="round" stroke-linejoin="round">
                            <rect x="1" y="3" width="14" height="10" rx="1"/>
                            <path d="M5 7h6M5 10h4"/>
                        </svg>
                        ${order.tableNumber || '—'}
                    </span>
                    <span class="order-amount">¥${Number(order.totalAmount || 0).toFixed(2)}</span>
                </div>
                <div class="order-card-footer">
                    <span class="progress-badge ${progressClass}">
                        ${dotClass ? `<span class="progress-dot ${dotClass}"></span>` : ''}
                        ${progressText}
                    </span>
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
