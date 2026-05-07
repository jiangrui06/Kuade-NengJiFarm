// ==============================
// 订单详情页 - order-detail.js
// API:
//   GET  /api/Kitchen/order/detail?orderId={id}
//   POST /api/Kitchen/dish/finish  { dishOrderDetailsId }
// ==============================

const API_BASE = 'http://192.168.101.30:7240/api/Kitchen';

/** 当前订单详情数据 */
let currentOrder = null;

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
        localStorage.removeItem('token');
        window.location.href = '../login/index.html';
        throw new Error('未授权，请重新登录');
    }
    return res;
}

/**
 * 解析后端响应，兼容两种格式：
 *   1. 标准包装：{ code: 0/200, message: '...', data: ... }，code=0或200表示成功
 *   2. 直接返回数据：{...} 或 [...]
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

// ========== 页面初始化 ==========
window.onload = function () {
    if (!localStorage.getItem('token')) {
        window.location.href = '../login/index.html';
        return;
    }

    // 显示用户名
    const userName = localStorage.getItem('user_name') || '后厨';
    document.getElementById('current-username').textContent = userName;

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
        const res = await apiFetch(`/order/detail?orderId=${orderId}`);
        currentOrder = await parseApiResponse(res);
        console.log('订单详情:', currentOrder);
        console.log('菜品列表:', currentOrder.dishList);
        if (currentOrder.dishList && currentOrder.dishList.length > 0) {
            console.log('第一个菜品对象:', JSON.stringify(currentOrder.dishList[0]));
        }
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

    // 填充头部信息
    document.getElementById('order-id').textContent = currentOrder.orderNo || currentOrder.orderId;
    document.getElementById('order-time').textContent = formatTime(currentOrder.createTime);
    document.getElementById('table-number').textContent = currentOrder.tableNumber || '—';

    const dishList = currentOrder.dishList || [];

    // 统计已出餐金额（status===2 为已出餐）
    const completedAmount = dishList
        .filter(d => d.status === 2)
        .reduce((sum, d) => sum + (Number(d.price || 0) * Number(d.quantity || 1)), 0);

    const itemsList = document.getElementById('items-list');
    itemsList.innerHTML = `
        ${dishList.map((dish) => {
            const isFinished = dish.status === 2;
            const statusLabel = isFinished ? '已出餐' : '待出餐';
            const statusClass = isFinished ? 'completed' : 'pending';
            // dish.id 是后端返回的菜品明细 ID
            const dishOrderDetailsId = dish.id;

            return `
                <div class="item" id="dish-${dish.id}">
                    <div class="item-info">
                        <span class="item-name">${dish.name || '未知菜品'}</span>
                        <span class="item-quantity">x${dish.quantity || 1}</span>
                        ${dish.price != null ? `<span class="item-price">¥${Number(dish.price).toFixed(2)}</span>` : ''}
                    </div>
                    <div class="item-status">
                        <span class="status-text ${statusClass}" id="status-text-${dish.id}">
                            ${statusLabel}
                        </span>
                        <div class="button-group">
                            <button
                                class="complete-button"
                                id="btn-${dish.id}"
                                ${isFinished ? 'disabled' : ''}
                                onclick="markDishFinished(${dishOrderDetailsId}, this)">
                                ${isFinished ? '已出餐' : '出餐'}
                            </button>
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
    console.log('实际发送的body:', JSON.stringify({ dishOrderDetailsId }));
    btn.disabled = true;
    btn.classList.add('loading');
    btn.textContent = '提交中...';
    hideError();

    try {
        const res = await apiFetch('/dish/finish', {
            method: 'POST',
            body: JSON.stringify({ dishOrderDetailsId })
        });

        const json = await res.json();
        console.log('出餐接口返回:', json);

        // 真实后端返回：code=0或200成功，code=400失败；message字段
        if (!res.ok || (json.code !== 0 && json.code !== 200)) {
            throw new Error(json.message || json.msg || '接口返回错误');
        }
        const data = json.data || json;

        // 本地更新菜品状态（status → 2）
        const dish = (currentOrder.dishList || []).find(d => d.id === dishOrderDetailsId);
        if (dish) {
            dish.status = 2;
        }

        // 更新按钮和状态文字
        btn.textContent = '已出餐';
        btn.disabled = true;
        const statusEl = document.getElementById(`status-text-${dishOrderDetailsId}`);
        if (statusEl) {
            statusEl.textContent = '已出餐';
            statusEl.className = 'status-text completed';
        }

        // 更新已出餐金额
        const completedAmount = (currentOrder.dishList || [])
            .filter(d => d.status === 2)
            .reduce((sum, d) => sum + (Number(d.price || 0) * Number(d.quantity || 1)), 0);
        const amountEl = document.getElementById('completed-amount');
        if (amountEl) amountEl.textContent = `¥${completedAmount.toFixed(2)}`;

        // 全部出餐提示
        if (data && data.allFinished === true) {
            setTimeout(() => {
                alert(`🎉 订单所有菜品已全部出餐！（${data.finishDish}/${data.totalDish}）`);
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

// ========== 工具函数 ==========

/** 格式化时间 */
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

/** 返回首页 */
function goBack() {
    window.location.href = '../dashboard/index.html';
}
