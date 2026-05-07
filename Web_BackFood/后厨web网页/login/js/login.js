// ==============================
// 后厨登录页 - login.js
// API: POST /api/Kitchen/login
// ==============================

const API_BASE = 'http://192.168.101.30:7240/api/Kitchen';

/** 显示/隐藏错误信息 */
function showError(msg) {
    const el = document.getElementById('error-message');
    el.textContent = msg || '账号或密码错误';
    el.style.display = 'block';
}
function hideError() {
    document.getElementById('error-message').style.display = 'none';
}

/** 设置按钮加载状态 */
function setLoading(loading) {
    const btn = document.querySelector('#login-form button[type="submit"]');
    btn.disabled = loading;
    btn.textContent = loading ? '登录中...' : '登录';
}

// 如果已登录，直接跳转到首页
window.onload = function () {
    if (localStorage.getItem('token')) {
        window.location.href = '../dashboard/index.html';
    }
};

document.getElementById('login-form').addEventListener('submit', async function (e) {
    e.preventDefault();
    hideError();

    const phoneNumber = document.getElementById('username').value.trim();
    const password = document.getElementById('password').value;

    if (!phoneNumber || !password) {
        showError('请填写账号和密码');
        return;
    }

    setLoading(true);

    try {
        const res = await fetch(`${API_BASE}/login`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ phoneNumber, password })
        });

        const json = await res.json();

        // 后端统一响应结构：{ code, message, data }，code=0或200表示成功
        if (res.ok && (json.code === 0 || json.code === 200) && json.data && json.data.token) {
            const { token, user_name, user_id, phone_number } = json.data;
            localStorage.setItem('token', token);
            localStorage.setItem('user_name', user_name || phoneNumber);
            localStorage.setItem('user_id', user_id);
            localStorage.setItem('phone_number', phone_number || phoneNumber);
            window.location.href = '../dashboard/index.html';
        } else {
            showError(json.message || json.msg || '登录失败，请检查账号密码');
        }
    } catch (err) {
        console.error('登录失败:', err);
        showError('网络异常，请检查连接后重试');
    } finally {
        setLoading(false);
    }
});
