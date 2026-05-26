// ==============================
// 后厨订单详情页 - order-detail.js
// 后端地址: http://192.168.101.75:80
// ==============================

// API 文档: status=1=待出餐, 2=已出餐, 3=已取消
const DISH_STATUS = {
    PENDING: 1,
    FINISHED: 2,
    CANCELLED: 3,
};

function isPendingStatus(status) {
    return status === 1;
}

const API_BASE = 'https://api.nengjifarm.com';

let currentOrder = null;

// ========== 本地菜品状态持久化（解决后端未持久化问题） ==========
const STORAGE_KEY_PREFIX = 'order_dishes_';

function saveDishStatuses(orderId, dishList) {
    if (!orderId || !dishList) {
        console.warn('保存跳过: orderId=%s, dishList=%s', orderId, dishList);
        return;
    }
    try {
        const key = STORAGE_KEY_PREFIX + orderId;
        const value = JSON.stringify(dishList);
        localStorage.setItem(key, value);
        console.log('已保存到 localStorage [%s]: %s', key, value);
    } catch (e) {
        console.warn('保存本地菜品状态失败:', e);
    }
}

function loadLocalDishStatuses(orderId) {
    if (!orderId) return null;
    try {
        const data = localStorage.getItem(STORAGE_KEY_PREFIX + orderId);
        return data ? JSON.parse(data) : null;
    } catch (e) {
        console.warn('读取本地菜品状态失败:', e);
        return null;
    }
}

function removeLocalDishStatuses(orderId) {
    try {
        localStorage.removeItem(STORAGE_KEY_PREFIX + orderId);
    } catch (e) {}
}

/** 将本地保存的菜品状态覆盖到 API 返回的数据上 */
function applyLocalStatusOverrides(order) {
    const localDishes = loadLocalDishStatuses(order.orderId);
    if (!localDishes || !order.dishList || order.dishList.length === 0) return;

    const localMap = new Map();
    localDishes.forEach(d => {
        if (d.dishOrderDetailsId != null) {
            localMap.set(d.dishOrderDetailsId, d);
        }
    });

    order.dishList.forEach(d => {
        const local = localMap.get(d.dishOrderDetailsId);
        if (local && local.status !== undefined) {
            d.status = local.status;
        }
    });
}

async function apiFetch(path, options = {}) {
    const token = localStorage.getItem('token');
    const headers = {
        'Content-Type': 'application/json',
        ...(token ? { 'Authorization': `Bearer ${token}` } : {}),
        ...(options.headers || {})
    };
    const res = await fetch(`${API_BASE}${path}`, { ...options, headers });
    if (res.status === 401) {
        localStorage.removeItem('token');
        window.location.href = '../login/index.html';
        throw new Error('未授权，请重新登录');
    }
    return res;
}

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

/** 归一化订单字段名：兼容新旧 API 响应格式 */
function normalizeOrder(o) {
    if (!o) return o;
    return {
        orderId: o.orderId ?? o.id,
        orderNo: o.orderNo ?? o.no,
        createTime: o.createTime ?? o.time,
        tableNumber: o.tableNumber ?? o.table,
        remark: o.remark ?? '',
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
    const avatar = document.getElementById('user-avatar');
    if (avatar) avatar.textContent = userName.charAt(0).toUpperCase();

    const orderId = localStorage.getItem('currentOrderId');
    if (!orderId) {
        window.location.href = '../dashboard/index.html';
        return;
    }

    fetchOrderDetail(orderId);
};

// ========== 获取订单详情 ==========
async function fetchOrderDetail(orderId) {
    const itemsList = document.getElementById('items-list');
    itemsList.innerHTML = '<div style="text-align:center;padding:60px 20px;color:#999;">加载中...</div>';
    hideError();

    try {
        const res = await apiFetch(`/api/Kitchen/order/detail?orderId=${orderId}`);
        currentOrder = await parseApiResponse(res);
        if (!currentOrder) {
            throw new Error(`订单 #${orderId} 不存在`);
        }
        currentOrder = normalizeOrder(currentOrder);

        // 用本地保存的菜品状态覆盖 API 返回的数据
        applyLocalStatusOverrides(currentOrder);

        console.log('订单详情(已应用本地覆盖):', currentOrder);
        renderOrderDetail();
    } catch (err) {
        console.error('获取订单详情失败:', err);

        // 如果 API 失败但本地有缓存数据，用本地数据渲染
        const localDishes = loadLocalDishStatuses(orderId);
        if (localDishes && localDishes.length > 0) {
            currentOrder = {
                orderId: orderId,
                orderNo: orderId,
                createTime: null,
                tableNumber: '—',
                remark: '',
                totalAmount: 0,
                dishList: localDishes,
            };
            console.log('API失败，使用本地缓存渲染:', currentOrder);
            renderOrderDetail();
            return;
        }

        itemsList.innerHTML = `
            <div style="text-align:center;padding:60px 20px;color:#999;">
                获取订单详情失败：${err.message}<br>
                <button onclick="goBack()" style="margin-top:16px;padding:8px 20px;background:#4CAF50;color:#fff;border:none;border-radius:4px;cursor:pointer;">返回首页</button>
            </div>`;
    }
}

// ========== 渲染订单详情 ==========
function renderOrderDetail() {
    if (!currentOrder) return;

    document.getElementById('order-id').textContent = currentOrder.orderNo || currentOrder.orderId;
    document.getElementById('order-time').textContent = formatTime(currentOrder.createTime);
    document.getElementById('table-number').textContent = currentOrder.tableNumber || '—';
    document.getElementById('order-remark').textContent = currentOrder.remark || '无';

    const dishList = currentOrder.dishList || [];

    const totalAmount = Number(currentOrder.totalAmount || 0);
    const completedAmount = dishList
        .filter(d => d.status === DISH_STATUS.FINISHED)
        .reduce((sum, d) => sum + (Number(d.price || 0) * Number(d.quantity || 1)), 0);

    document.getElementById('total-amount').textContent = `¥${totalAmount.toFixed(2)}`;
    document.getElementById('completed-amount').textContent = `¥${completedAmount.toFixed(2)}`;

    const itemsList = document.getElementById('items-list');
    itemsList.innerHTML = dishList.map((dish) => {
        const status = dish.status;
        const isFinished = status === DISH_STATUS.FINISHED;
        const isCancelled = status === DISH_STATUS.CANCELLED;
        const dishOrderDetailsId = dish.dishOrderDetailsId;

        let statusLabel, statusClass, buttonsHtml;

        if (isFinished) {
            statusLabel = '已出餐';
            statusClass = 'completed';
            buttonsHtml = `<button class="action-btn confirm" disabled>已出餐</button>`;
        } else if (isCancelled) {
            statusLabel = '已取消';
            statusClass = 'cancelled';
            buttonsHtml = `<button class="action-btn danger" disabled>已取消</button>`;
        } else {
            statusLabel = '待出餐';
            statusClass = 'pending';
            buttonsHtml = `
                <button class="action-btn confirm" onclick="markDishFinished(${dishOrderDetailsId}, this)">出餐</button>
                <button class="action-btn danger" onclick="markDishCancelled(${dishOrderDetailsId}, this)">取消</button>
            `;
        }

        return `
            <div class="dish-item" id="dish-${dish.dishOrderDetailsId}">
                <div class="dish-info">
                    <span class="dish-name">${dish.name || '未知菜品'}</span>
                </div>
                <div class="dish-meta">
                    <span class="dish-qty">×${dish.quantity || 1}</span>
                    ${dish.price != null ? `<span class="dish-price">¥${Number(dish.price).toFixed(2)}</span>` : ''}
                </div>
                <div class="dish-actions">
                    <span class="status-badge ${statusClass}" id="status-text-${dish.dishOrderDetailsId}">
                        ${statusLabel}
                    </span>
                    ${buttonsHtml}
                </div>
            </div>
        `;
    }).join('');

    // 每次渲染都把当前菜品状态写入 localStorage，确保数据同步
    saveDishStatuses(currentOrder.orderId, currentOrder.dishList);
}

// ========== 标记菜品为已出餐 ==========
async function markDishFinished(dishOrderDetailsId, btn) {
    // 操作前检查：菜品必须是待出餐状态
    const currentDish = (currentOrder.dishList || []).find(d => d.dishOrderDetailsId === dishOrderDetailsId);
    if (!currentDish) { showError('菜品不存在'); return; }
    if (currentDish.status !== DISH_STATUS.PENDING) {
        showError(`该菜品已被"${currentDish.status === DISH_STATUS.FINISHED ? '出餐' : '取消出餐'}"，无法重复操作，请刷新页面`);
        return;
    }

    console.log('准备提交 dishOrderDetailsId:', dishOrderDetailsId, typeof dishOrderDetailsId);
    btn.disabled = true;
    btn.classList.add('loading');
    btn.textContent = '提交中...';
    hideError();

    try {
        const res = await apiFetch('/api/Kitchen/dish/finish', {
            method: 'POST',
            body: JSON.stringify({ dishOrderDetailsId })
        });

        const json = await res.json();
        console.log('出餐接口返回:', json);

        if (!res.ok || (json.code !== 0 && json.code !== 200)) {
            throw new Error(json.message || json.msg || '接口返回错误');
        }
        const data = json.data || json;

        const dish = (currentOrder.dishList || []).find(d => d.dishOrderDetailsId === dishOrderDetailsId);
        if (dish) dish.status = DISH_STATUS.FINISHED;

        saveDishStatuses(currentOrder.orderId, currentOrder.dishList);

        btn.textContent = '已出餐';
        btn.disabled = true;
        btn.className = 'action-btn confirm';

        const statusEl = document.getElementById(`status-text-${dishOrderDetailsId}`);
        if (statusEl) {
            statusEl.textContent = '已出餐';
            statusEl.className = 'status-badge completed';
        }

        const itemEl = document.getElementById(`dish-${dishOrderDetailsId}`);
        if (itemEl) {
            const actionsContainer = itemEl.querySelector('.dish-actions');
            if (actionsContainer) {
                actionsContainer.querySelectorAll('.action-btn').forEach(b => b.remove());
                const doneBtn = document.createElement('button');
                doneBtn.className = 'action-btn confirm';
                doneBtn.disabled = true;
                doneBtn.textContent = '已出餐';
                actionsContainer.appendChild(doneBtn);
            }
        }

        updateCompletedAmount();

        // 本地判断整单是否全部完成（不依赖后端 allFinished 字段）
        const dishList = currentOrder?.dishList || [];
        const allDone = dishList.length > 0 && dishList.every(d => d.status === DISH_STATUS.FINISHED || d.status === DISH_STATUS.CANCELLED);
        if (allDone) {
            try {
                localStorage.setItem('order_completed_at_' + currentOrder.orderId, Date.now().toString());
            } catch (e) {}
            setTimeout(() => {
                const done = dishList.filter(d => d.status === DISH_STATUS.FINISHED || d.status === DISH_STATUS.CANCELLED).length;
                alert(`订单所有菜品已全部出餐！（${done}/${dishList.length}）`);
            }, 200);
        }

    } catch (err) {
        console.error('标记出餐失败:', err);
        showError(`操作失败：${err.message}`);
        btn.disabled = false;
        btn.textContent = '出餐';
    } finally {
        btn.classList.remove('loading');
    }
}

// ========== 取消菜品出餐 ==========
async function markDishCancelled(dishOrderDetailsId, btn) {
    // 操作前检查：菜品必须是待出餐状态
    const currentDish = (currentOrder.dishList || []).find(d => d.dishOrderDetailsId === dishOrderDetailsId);
    if (!currentDish) { showError('菜品不存在'); return; }
    if (currentDish.status !== DISH_STATUS.PENDING) {
        showError(`该菜品已被"${currentDish.status === DISH_STATUS.FINISHED ? '出餐' : '取消出餐'}"，无法重复操作，请刷新页面`);
        return;
    }

    if (!confirm('确定要取消该菜品吗？取消后该菜品将不再出餐。')) return;

    btn.disabled = true;
    btn.classList.add('loading');
    btn.textContent = '提交中...';
    hideError();

    try {
        const res = await apiFetch('/api/Kitchen/dish/cancel', {
            method: 'POST',
            body: JSON.stringify({ dishOrderDetailsId })
        });

        const json = await res.json();
        console.log('取消出餐接口返回:', json);

        if (!res.ok || (json.code !== 0 && json.code !== 200)) {
            throw new Error(json.message || json.msg || '接口返回错误');
        }

        const dish = (currentOrder.dishList || []).find(d => d.dishOrderDetailsId === dishOrderDetailsId);
        if (dish) {
            dish.status = DISH_STATUS.CANCELLED;
        }

        // 保存到本地，确保刷新后状态不丢失
        saveDishStatuses(currentOrder.orderId, currentOrder.dishList);

        const statusEl = document.getElementById(`status-text-${dishOrderDetailsId}`);
        if (statusEl) {
            statusEl.textContent = '已取消';
            statusEl.className = 'status-badge cancelled';
        }

        const itemEl = document.getElementById(`dish-${dishOrderDetailsId}`);
        if (itemEl) {
            const actionsContainer = itemEl.querySelector('.dish-actions');
            if (actionsContainer) {
                actionsContainer.querySelectorAll('.action-btn').forEach(b => b.remove());
                const doneBtn = document.createElement('button');
                doneBtn.className = 'action-btn danger';
                doneBtn.disabled = true;
                doneBtn.textContent = '已取消';
                actionsContainer.appendChild(doneBtn);
            }
        }

        updateCompletedAmount();

    } catch (err) {
        console.error('取消出餐失败:', err);
        showError(`操作失败：${err.message}`);
        btn.disabled = false;
        btn.textContent = '取消出餐';
    } finally {
        btn.classList.remove('loading');
    }
}

/** 更新已出餐金额显示 */
function updateCompletedAmount() {
    const dishList = currentOrder?.dishList || [];
    const completedAmount = dishList
        .filter(d => d.status === DISH_STATUS.FINISHED)
        .reduce((sum, d) => sum + (Number(d.price || 0) * Number(d.quantity || 1)), 0);
    const amountEl = document.getElementById('completed-amount');
    if (amountEl) amountEl.textContent = `¥${completedAmount.toFixed(2)}`;
}

// ========== 工具函数 ==========

function formatTime(isoStr) {
    if (!isoStr) return '—';
    try {
        const d = new Date(isoStr);
        const pad = n => String(n).padStart(2, '0');
        return `${d.getFullYear()}-${pad(d.getMonth()+1)}-${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}`;
    } catch {
        return isoStr;
    }
}

function showError(msg) {
    const el = document.getElementById('error-message');
    el.textContent = msg;
    el.style.display = 'block';
}

function hideError() {
    const el = document.getElementById('error-message');
    if (el) el.style.display = 'none';
}

function goBack() {
    window.location.href = '../dashboard/index.html';
}
