const API_BASE = 'https://api.nengjifarm.com';

function showError(msg) {
    const el = document.getElementById('error-message');
    const span = el.querySelector('span');
    if (span) span.textContent = msg || '账号或密码错误';
    el.style.display = 'flex';
}
function hideError() {
    document.getElementById('error-message').style.display = 'none';
}

function setLoading(loading) {
    const btn = document.querySelector('#login-form button[type="submit"]');
    btn.disabled = loading;
    btn.classList.toggle('loading', loading);
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
            throw new Error('账号或密码错误');
        }

        if (!res.ok && !json.code) {
            throw new Error('账号或密码错误');
        }

        let userData = json;

        if (json && typeof json === 'object' && 'code' in json) {
            if (json.code === 0 || json.code === 200) {
                userData = json.data;
            } else {
                throw new Error(json.message || json.msg || '账号或密码错误');
            }
        }

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
        showError('账号或密码错误');
    } finally {
        setLoading(false);
    }
});
