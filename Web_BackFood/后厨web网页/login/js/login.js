document.getElementById('login-form').addEventListener('submit', function(e) {
    e.preventDefault();
    const username = document.getElementById('username').value;
    const password = document.getElementById('password').value;
    
    if (!username || !password) {
        document.getElementById('error-message').style.display = 'block';
        return;
    }
    
    // 简单模拟登录验证
            if (username === 'admin' && password === '123456') {
                localStorage.setItem('loggedIn', 'true');
                localStorage.setItem('username', username);
                window.location.href = '../dashboard/index.html';
            } else {
                document.getElementById('error-message').style.display = 'block';
            }
});