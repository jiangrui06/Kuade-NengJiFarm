// ==============================
// 后厨订单详情页 - order-detail.js
// 后端地址: http://192.168.101.30:7240
// ==============================

// 新API: status=2=待出餐, 3=已取消, 4=已完成
const DISH_STATUS = {
    PENDING: 2,
    FINISHED: 4,
    CANCELLED: 3,
};

function isPendingStatus(status) {
    return status === 2;
}

const API_BASE = 'http://192.168.101.30:7240';

let currentOrder = null;

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

    const orderId = localStorage.getItem('currentOrderId');
    if (!orderId) {
        window.location.href = '../dashboard/index.html';
        return;
    }

    fetchOrderDetail(orderId);
};

// ========== 获取订单详情（从订单列表查找，无独立详情接口） ==========
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

        console.log('订单详情:', currentOrder);
        renderOrderDetail();
    } catch (err) {
        console.error('获取订单详情失败:', err);
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

    const dishList = currentOrder.dishList || [];

    const completedAmount = dishList
        .filter(d => d.status === DISH_STATUS.FINISHED)
        .reduce((sum, d) => sum + (Number(d.price || 0) * Number(d.quantity || 1)), 0);

    const itemsList = document.getElementById('items-list');
    itemsList.innerHTML = `
        ${dishList.map((dish) => {
            const status = dish.status;
            const isFinished = status === DISH_STATUS.FINISHED;
            const isCancelled = status === DISH_STATUS.CANCELLED;
            const isPending = isPendingStatus(status);
            const dishOrderDetailsId = dish.dishOrderDetailsId;

            let statusLabel, statusClass, buttonsHtml;

            if (isFinished) {
                statusLabel = '已出餐';
                statusClass = 'completed';
                buttonsHtml = `<button class="complete-button" disabled>已出餐</button>`;
            } else if (isCancelled) {
                statusLabel = '已取消';
                statusClass = 'cancelled';
                buttonsHtml = `<button class="cancel-button" disabled>已取消</button>`;
            } else {
                statusLabel = '待出餐';
                statusClass = 'pending';
                buttonsHtml = `
                    <button class="complete-button" onclick="markDishFinished(${dishOrderDetailsId}, this)">出餐</button>
                    <button class="cancel-button" onclick="markDishCancelled(${dishOrderDetailsId}, this)">取消出餐</button>
                `;
            }

            return `
                <div class="item" id="dish-${dish.dishOrderDetailsId}">
                    <div class="item-info">
                        <span class="item-name">${dish.name || '未知菜品'}</span>
                        <span class="item-quantity">x${dish.quantity || 1}</span>
                        ${dish.price != null ? `<span class="item-price">¥${Number(dish.price).toFixed(2)}</span>` : ''}
                    </div>
                    <div class="item-status">
                        <span class="status-text ${statusClass}" id="status-text-${dish.dishOrderDetailsId}">
                            ${statusLabel}
                        </span>
                        <div class="button-group">
                            ${buttonsHtml}
                        </div>
                    </div>
                </div>
            `;
        }).join('')}
        <div class="order-total">
            <div class="total-label">订单总价：</div>
            <div class="total-amount">¥${Number(currentOrder.totalAmount || 0).toFixed(2)}</div>
        </div>
        <div class="completed-total">
            <div class="total-label">已出餐金额：</div>
            <div class="total-amount" id="completed-amount">¥${completedAmount.toFixed(2)}</div>
        </div>
    `;
}

// ========== 标记菜品为已出餐 ==========
async function markDishFinished(dishOrderDetailsId, btn) {
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
        if (dish) {
            dish.status = DISH_STATUS.FINISHED;
        }

        btn.textContent = '已出餐';
        btn.disabled = true;
        const statusEl = document.getElementById(`status-text-${dishOrderDetailsId}`);
        if (statusEl) {
            statusEl.textContent = '已出餐';
            statusEl.className = 'status-text completed';
        }

        updateCompletedAmount();

        if (data && data.allFinished === true) {
            try {
                localStorage.setItem('order_completed_at_' + currentOrder.orderId, Date.now().toString());
            } catch (e) {}
            setTimeout(() => {
                alert(`订单所有菜品已全部出餐！（${data.finishDish}/${data.totalDish}）`);
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

        const statusEl = document.getElementById(`status-text-${dishOrderDetailsId}`);
        if (statusEl) {
            statusEl.textContent = '已取消';
            statusEl.className = 'status-text cancelled';
        }

        const itemEl = document.getElementById(`dish-${dishOrderDetailsId}`);
        if (itemEl) {
            const btnGroup = itemEl.querySelector('.button-group');
            if (btnGroup) {
                btnGroup.innerHTML = `<button class="cancel-button" disabled>已取消</button>`;
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
