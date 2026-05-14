const API_BASE = 'http://192.168.101.11:7240';

function showError(msg) {
    const el = document.getElementById('error-message');
    el.textContent = msg || '账号或密码错误';
    el.style.display = 'block';
}
function hideError() {
    document.getElementById('error-message').style.display = 'none';
}

function setLoading(loading) {
    const btn = document.querySelector('#login-form button[type="submit"]');
    btn.disabled = loading;
    btn.textContent = loading ? '登录中...' : '登录';
}

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
        const res = await fetch(`${API_BASE}/api/Kitchen/login`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ phoneNumber, password })
        });

        let json;
        try {
            json = await res.json();
        } catch {
            throw new Error('服务器返回数据异常，请稍后重试');
        }

        if (!res.ok && !json.code) {
            throw new Error(json.message || json.msg || '登录失败，请稍后重试');
        }

        // 兼容两种响应格式：
        // 旧: { code: 0, data: { token, user_name, user_id, phone_number } }
        // 新: { token, userId, userName, phoneNumber } (直接返回，无外层包装)
        let userData = json;

        // 如果有 code 字段，走旧格式的包装解析
        if (json && typeof json === 'object' && 'code' in json) {
            if (json.code === 0 || json.code === 200) {
                userData = json.data;
            } else {
                // API 返回了业务错误码，一般为账号或密码错误
                throw new Error(json.message || json.msg || '账号或密码错误');
            }
        }

        // 从两种格式中提取字段
        const token = userData.token;
        const userId = userData.userId || userData.user_id;
        const userName = userData.userName || userData.user_name;
        const phone = userData.phoneNumber || userData.phone_number;

        if (token) {
            localStorage.setItem('token', token);
        }
        if (userId) localStorage.setItem('user_id', userId);
        localStorage.setItem('user_name', userName || phoneNumber);
        localStorage.setItem('phone_number', phone || phoneNumber);

        window.location.href = '../dashboard/index.html';
    } catch (err) {
        console.error('登录失败:', err);
        // 网络错误（fetch 无法连接到服务器），不暴露内部错误信息
        if (err instanceof TypeError) {
            showError('网络连接失败，请检查网络后重试');
        } else {
            showError(err.message || '登录失败，请重试');
        }
    } finally {
        setLoading(false);
    }
});
