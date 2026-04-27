// API基础URL
const API_BASE_URL = '/api';

// 订单数据
let orders = [];
let currentTab = 'pending';

// 页面加载时检查登录状态
window.onload = function() {
    if (!localStorage.getItem('loggedIn')) {
        window.location.href = '../login/index.html';
    }
    
    // 显示当前登录用户名
    const username = localStorage.getItem('username');
    if (username) {
        document.getElementById('current-username').textContent = username;
    }
    
    // 从API获取订单数据
    fetchOrders();
};

// 从API获取订单数据
async function fetchOrders() {
    const orderList = document.getElementById('order-list');
    orderList.innerHTML = '<div class="no-orders">加载中...</div>';
    
    try {
        console.log('页面URL:', window.location.href);
        console.log('API基础URL:', API_BASE_URL);
        
        const timestamp = new Date().getTime();
        const url = `${API_BASE_URL}/orders?timestamp=${timestamp}`;
        console.log('请求URL:', url);
        
        const response = await fetch(url);
        console.log('响应状态:', response.status);
        
        if (!response.ok) {
            throw new Error(`HTTP ${response.status}`);
        }
        
        const data = await response.json();
        console.log('获取到数据:', data);
        
        orders = data;
        renderOrders();
        updateRevenue();
    } catch (error) {
        console.error('获取订单失败:', error);
        orderList.innerHTML = `<div class="no-orders">获取订单失败: ${error.message}</div>`;
    }
};

// 切换标签
function switchTab(tab) {
    currentTab = tab;
    document.querySelectorAll('.tab').forEach(t => t.classList.remove('active'));
    event.currentTarget.classList.add('active');
    renderOrders();
}

// 渲染订单列表
function renderOrders() {
    const orderList = document.getElementById('order-list');
    // 待出餐：还有未处理的餐品（未出餐且未取消）
    // 已出餐：所有餐品都已处理（已出餐或已取消）
    const filteredOrders = currentTab === 'pending' 
        ? orders.filter(order => order.items.some(item => !item.status && !item.cancelled))
        : orders.filter(order => order.items.every(item => item.status || item.cancelled));
    
    if (filteredOrders.length === 0) {
        orderList.innerHTML = '<div class="no-orders">暂无' + (currentTab === 'pending' ? '待出餐' : '已出餐') + '订单</div>';
        return;
    }
    
    orderList.innerHTML = filteredOrders.map(order => {
        const completedItems = order.items.filter(item => item.status).length;
        const cancelledItems = order.items.filter(item => item.cancelled).length;
        const statusClass = currentTab === 'pending' ? '' : (cancelledItems > 0 ? 'has-cancelled' : '');
        const statusText = currentTab === 'pending' 
            ? `${completedItems}/${order.items.length}` 
            : `${completedItems}出餐${cancelledItems > 0 ? ' ' + cancelledItems + '取消' : ''}/${order.items.length}`;
        return `
            <div class="order-card" onclick="openOrderDetail('${order.id}')">
                <div class="order-header">
                    <span class="order-id">${order.id}</span>
                    <span class="order-time">${order.time}</span>
                </div>
                <div class="order-info">
                    <span class="table-number">${order.table}</span>
                    <span class="status ${statusClass}">${statusText}</span>
                </div>
            </div>
        `;
    }).join('');
}

// 打开订单详情
        function openOrderDetail(orderId) {
            localStorage.setItem('currentOrderId', orderId);
            window.location.href = '../order-detail/index.html';
        }

// 更新营业额
function updateRevenue() {
    // 只计算已完成订单中已出餐的餐品价格
    let total = 0;
    for (const order of orders) {
        // 订单完成的条件：所有餐品都已处理（已出餐或已取消）
        if (order.items.every(item => item.status || item.cancelled)) {
            // 只计算已出餐的餐品价格
            const orderRevenue = order.items
                .filter(item => item.status)
                .reduce((sum, item) => sum + (item.price * item.quantity), 0);
            total += orderRevenue;
        }
    }
    document.getElementById('total-revenue').textContent = total;
}

// 退出登录
function logout() {
    // 清除所有本地缓存数据
    localStorage.removeItem('loggedIn');
    localStorage.removeItem('username');
    localStorage.removeItem('orders');
    localStorage.removeItem('currentOrderId');
    // 跳转到登录页面
    window.location.href = '../login/index.html';
}

// 暴露订单数据给其他页面
window.orders = orders;
window.updateOrders = function(updatedOrders) {
    orders = updatedOrders;
    renderOrders();
    updateRevenue();
};

// 刷新订单数据
function refreshOrders() {
    fetchOrders();
};

// 清理缓存
async function clearCache() {
    if (confirm('确定要清理缓存并重置数据吗？这将恢复到初始状态。')) {
        try {
            // 调用API重置数据
            const resetResponse = await fetch(`${API_BASE_URL}/reset`);
            if (!resetResponse.ok) {
                throw new Error('Failed to reset server data');
            }
            
            // 清除本地缓存
            localStorage.removeItem('orders');
            localStorage.removeItem('currentOrderId');
            
            // 重新获取订单数据
            await fetchOrders();
            alert('缓存已清理，数据已重置为初始状态');
        } catch (error) {
            console.error('Error clearing cache:', error);
            alert('清理缓存失败，请刷新页面重试');
        }
    }
};