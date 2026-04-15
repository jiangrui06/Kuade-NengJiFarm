(function (window, document) {
	'use strict';

	var LOGIN_PAGE = 'login.html';
	var TOKEN_CHECK_INTERVAL = 3000;
	var ADMIN_THEME_NAME = 'warm-gold';
	var ADMIN_THEME_STYLE_ID = 'admin-theme-warm-gold';
	var activeToken = '';
	var sessionMonitorStarted = false;
	var actionButtonThemeStarted = false;
	var actionButtonThemeTimer = 0;
	var loggingOut = false;

	function safeParse(value) {
		if (!value) {
			return null;
		}

		try {
			return JSON.parse(value);
		} catch (error) {
			console.error('解析登录用户信息失败:', error);
			return null;
		}
	}

	function getToken() {
		return (window.localStorage.getItem('token') || '').trim();
	}

	function getUserInfo() {
		return safeParse(window.localStorage.getItem('userInfo'));
	}

	function clearAuth() {
		window.localStorage.removeItem('token');
		window.localStorage.removeItem('userInfo');
	}

	function normalizeUrlPath(url) {
		if (!url) {
			return '';
		}

		try {
			return new URL(url, window.location.origin).pathname;
		} catch (error) {
			return '';
		}
	}

	function isProtectedApiRequest(url) {
		var path = normalizeUrlPath(url);
		return path.indexOf('/api/') === 0;
	}

	function redirectToLogin() {
		if (window.location.pathname.toLowerCase().endsWith('/' + LOGIN_PAGE) ||
			window.location.pathname.toLowerCase().endsWith(LOGIN_PAGE)) {
			return;
		}

		window.location.href = LOGIN_PAGE;
	}

	function forceLogout(reason) {
		if (loggingOut) {
			return;
		}

		loggingOut = true;
		console.warn(reason || '登录状态已失效，正在返回登录页');
		clearAuth();
		redirectToLogin();
	}

	function ensureAuthenticated() {
		var token = getToken();

		if (!token) {
			clearAuth();
			redirectToLogin();
			return null;
		}

		activeToken = token;

		return {
			token: token,
			userInfo: getUserInfo()
		};
	}

	function getDisplayName(userInfo) {
		if (!userInfo) {
			return '已登录用户';
		}

		return userInfo.nickname || userInfo.phone || userInfo.id || '已登录用户';
	}

	function getAvatarText(userInfo) {
		var displayName = getDisplayName(userInfo);
		return (displayName || '用').toString().trim().charAt(0) || '用';
	}

	function getActionButtonType(text) {
		var normalized = (text || '').replace(/\s+/g, ' ').trim();

		if (!normalized) {
			return '';
		}

		if (normalized.indexOf('搜索') !== -1) {
			return 'search';
		}

		if (normalized.indexOf('删除') !== -1) {
			return 'delete';
		}

		if (normalized.indexOf('\u5237\u65b0') !== -1) {
			return 'refresh';
		}

		if (normalized.indexOf('\u65b0\u589e') !== -1 ||
			normalized.indexOf('\u7f16\u8f91') !== -1 ||
			normalized.indexOf('\u4fee\u6539') !== -1) {
			return 'edit';
		}

		return '';
	}

	function applyActionButtonTheme(root) {
		var scope = root && typeof root.querySelectorAll === 'function' ? root : document;
		var buttons = scope.querySelectorAll('button');

		Array.prototype.forEach.call(buttons, function (button) {
			var actionType = getActionButtonType(button.textContent);

			if (actionType) {
				button.setAttribute('data-admin-action', actionType);
			} else {
				button.removeAttribute('data-admin-action');
			}
		});
	}

	function scheduleActionButtonTheme() {
		if (actionButtonThemeTimer) {
			return;
		}

		actionButtonThemeTimer = window.setTimeout(function () {
			actionButtonThemeTimer = 0;
			applyActionButtonTheme(document);
		}, 0);
	}

	function startActionButtonTheme() {
		if (actionButtonThemeStarted) {
			return;
		}

		actionButtonThemeStarted = true;
		scheduleActionButtonTheme();

		if (document.readyState === 'loading') {
			document.addEventListener('DOMContentLoaded', scheduleActionButtonTheme);
		}

		if (typeof window.MutationObserver === 'function' && document.documentElement) {
			var observer = new window.MutationObserver(function () {
				scheduleActionButtonTheme();
			});

			observer.observe(document.documentElement, {
				childList: true,
				subtree: true,
				characterData: true
			});
		}
	}

	function ensureAdminTheme() {
		var head = document.head || document.getElementsByTagName('head')[0];
		var themeSelector = 'html[data-admin-theme="' + ADMIN_THEME_NAME + '"]';

		if (!head || document.getElementById(ADMIN_THEME_STYLE_ID)) {
			return;
		}

		document.documentElement.setAttribute('data-admin-theme', ADMIN_THEME_NAME);

		var style = document.createElement('style');
		style.id = ADMIN_THEME_STYLE_ID;
		style.type = 'text/css';
		style.textContent = [
			themeSelector + ',',
			themeSelector + ' body {',
			'	background: #FAF7F2 !important;',
			'	color: #D3A239 !important;',
			'}',
			themeSelector + ' .main-container,',
			themeSelector + ' .content,',
			themeSelector + ' .content-container,',
			themeSelector + ' .page-scroll-panel,',
			themeSelector + ' .form-scroll,',
			themeSelector + ' .page-fixed-panel,',
			themeSelector + ' .product-page,',
			themeSelector + ' .order-page,',
			themeSelector + ' .dish-page,',
			themeSelector + ' .coupon-page,',
			themeSelector + ' .user-page,',
			themeSelector + ' .form-page {',
			'	background: #FAF7F2 !important;',
			'	color: #D3A239 !important;',
			'}',
			themeSelector + ' .sidebar {',
			'	background: #F6F0E5 !important;',
			'	color: #D3A239 !important;',
			'	border-right: 1px solid rgba(211, 162, 57, 0.18) !important;',
			'}',
			themeSelector + ' .sidebar-header,',
			themeSelector + ' .sidebar-menu li {',
			'	color: #D3A239 !important;',
			'	border-color: rgba(211, 162, 57, 0.18) !important;',
			'}',
			themeSelector + ' .sidebar-menu li:hover {',
			'	background: rgba(211, 162, 57, 0.12) !important;',
			'}',
			themeSelector + ' .sidebar-menu li.active,',
			themeSelector + ' .tab.active,',
			themeSelector + ' .nav-tab.active,',
			themeSelector + ' [role="tab"][aria-selected="true"] {',
			'	background: linear-gradient(180deg, #E5B846 0%, #D3A239 100%) !important;',
			'	color: #FFFFFF !important;',
			'	border-color: #D3A239 !important;',
			'}',
			themeSelector + ' .sidebar-menu li.active *,',
			themeSelector + ' .tab.active *,',
			themeSelector + ' .nav-tab.active *,',
			themeSelector + ' [role="tab"][aria-selected="true"] * {',
			'	color: #FFFFFF !important;',
			'}',
			themeSelector + ' .top-nav,',
			themeSelector + ' .pagination,',
			themeSelector + ' .management-toolbar {',
			'	background: #FAF7F2 !important;',
			'	border-color: rgba(211, 162, 57, 0.18) !important;',
			'}',
			themeSelector + ' .visual-panel,',
			themeSelector + ' .page-intro,',
			themeSelector + ' .management-header,',
			themeSelector + ' .form-section,',
			themeSelector + ' table th {',
			'	background: #F5EFE3 !important;',
			'	border-color: rgba(211, 162, 57, 0.18) !important;',
			'}',
			themeSelector + ' .summary-card,',
			themeSelector + ' .stat-card,',
			themeSelector + ' .management-card,',
			themeSelector + ' .management-body,',
			themeSelector + ' .form-container,',
			themeSelector + ' .form-card,',
			themeSelector + ' .product-table-wrapper,',
			themeSelector + ' .order-table-wrapper,',
			themeSelector + ' .detail-table-wrapper,',
			themeSelector + ' .table-wrapper,',
			themeSelector + ' .dish-table,',
			themeSelector + ' .coupon-table,',
			themeSelector + ' .user-table,',
			themeSelector + ' .detail-section,',
			themeSelector + ' .empty-state,',
			themeSelector + ' .permission-banner {',
			'	background: #FFFDF8 !important;',
			'	border-color: rgba(211, 162, 57, 0.18) !important;',
			'	color: #D3A239 !important;',
			'}',
			themeSelector + ' .visual-badge,',
			themeSelector + ' .btn-default,',
			themeSelector + ' .pagination-btn {',
			'	background: #F1E8D8 !important;',
			'	color: #D3A239 !important;',
			'	border: 1px solid rgba(211, 162, 57, 0.22) !important;',
			'}',
			themeSelector + ' button[data-admin-action="search"],',
			themeSelector + ' button[data-admin-action="refresh"],',
			themeSelector + ' button[data-admin-action="edit"],',
			themeSelector + ' button[data-admin-action="delete"] {',
			'	transition: transform 0.15s ease, box-shadow 0.15s ease, background 0.15s ease, border-color 0.15s ease !important;',
			'}',
			themeSelector + ' button[data-admin-action="search"]:hover,',
			themeSelector + ' button[data-admin-action="refresh"]:hover,',
			themeSelector + ' button[data-admin-action="edit"]:hover,',
			themeSelector + ' button[data-admin-action="delete"]:hover {',
			'	transform: translateY(-1px) !important;',
			'	box-shadow: 0 8px 18px rgba(111, 85, 21, 0.16) !important;',
			'}',
			themeSelector + ' button[data-admin-action="search"]:disabled,',
			themeSelector + ' button[data-admin-action="refresh"]:disabled,',
			themeSelector + ' button[data-admin-action="edit"]:disabled,',
			themeSelector + ' button[data-admin-action="delete"]:disabled {',
			'	transform: none !important;',
			'	box-shadow: none !important;',
			'}',
			themeSelector + ' .pagination-btn:hover,',
			themeSelector + ' .pagination-btn.active {',
			'	background: #EAD9B5 !important;',
			'}',
			themeSelector + ' .pagination-btn:disabled {',
			'	background: #F6F0E5 !important;',
			'	color: rgba(211, 162, 57, 0.55) !important;',
			'	border-color: rgba(211, 162, 57, 0.15) !important;',
			'}',
			themeSelector + ' button[data-admin-action="search"] {',
			'	background: #F0D387 !important;',
			'	color: #7C5B12 !important;',
			'	border-color: #F0D387 !important;',
			'}',
			themeSelector + ' button[data-admin-action="refresh"] {',
			'	background: #F1E8D8 !important;',
			'	color: #D3A239 !important;',
			'	border-color: rgba(211, 162, 57, 0.22) !important;',
			'}',
			themeSelector + ' button[data-admin-action="refresh"]:hover {',
			'	background: #EAD9B5 !important;',
			'	border-color: #E5B846 !important;',
			'}',
			themeSelector + ' button[data-admin-action="search"]:hover {',
			'	background: #E8C86E !important;',
			'	border-color: #E8C86E !important;',
			'}',
			themeSelector + ' button[data-admin-action="edit"] {',
			'	background: linear-gradient(90deg, #E5B846 0%, #F0D387 100%) !important;',
			'	color: #FFFFFF !important;',
			'	border-color: #E5B846 !important;',
			'}',
			themeSelector + ' button[data-admin-action="edit"]:hover {',
			'	background: linear-gradient(90deg, #D9AE3D 0%, #E9C96F 100%) !important;',
			'	border-color: #D9AE3D !important;',
			'}',
			themeSelector + ' button[data-admin-action="delete"] {',
			'	background: #D3A239 !important;',
			'	color: #FFFDF8 !important;',
			'	border-color: #D3A239 !important;',
			'}',
			themeSelector + ' button[data-admin-action="delete"]:hover {',
			'	background: #C59222 !important;',
			'	border-color: #C59222 !important;',
			'}',
			themeSelector + ' button[data-admin-action="delete"]:disabled {',
			'	background: rgba(211, 162, 57, 0.55) !important;',
			'	border-color: rgba(211, 162, 57, 0.55) !important;',
			'	color: #FFFDF8 !important;',
			'}',
			themeSelector + ' .status-tag.inactive,',
			themeSelector + ' .status-tag.unpaid,',
			themeSelector + ' .status-tag.refunded,',
			themeSelector + ' .status-btn.disabled,',
			themeSelector + ' .status-down {',
			'	background: #D3A239 !important;',
			'	color: #FFFDF8 !important;',
			'	border-color: #D3A239 !important;',
			'}',
			themeSelector + ' .user-avatar {',
			'	background: #D3A239 !important;',
			'	color: #FAF7F2 !important;',
			'}',
			themeSelector + ' .top-nav,',
			themeSelector + ' .top-nav > div,',
			themeSelector + ' .top-nav span,',
			themeSelector + ' .top-nav strong,',
			themeSelector + ' .content h1,',
			themeSelector + ' .content h2,',
			themeSelector + ' .content h3,',
			themeSelector + ' .content h4,',
			themeSelector + ' .content h5,',
			themeSelector + ' .content h6,',
			themeSelector + ' .content p,',
			themeSelector + ' .content label,',
			themeSelector + ' .content th,',
			themeSelector + ' .content td,',
			themeSelector + ' .content .visual-panel-title,',
			themeSelector + ' .content .visual-panel-desc,',
			themeSelector + ' .content .summary-card-label,',
			themeSelector + ' .content .summary-card-number,',
			themeSelector + ' .content .summary-card-unit,',
			themeSelector + ' .content .summary-card-note,',
			themeSelector + ' .content .stat-label,',
			themeSelector + ' .content .stat-value,',
			themeSelector + ' .content .management-title,',
			themeSelector + ' .content .management-meta,',
			themeSelector + ' .content .management-arrow,',
			themeSelector + ' .content .form-title,',
			themeSelector + ' .content .section-title,',
			themeSelector + ' .content .page-intro h2,',
			themeSelector + ' .content .page-intro p,',
			themeSelector + ' .content .pagination-info,',
			themeSelector + ' .content .form-hint,',
			themeSelector + ' .content .input-tips,',
			themeSelector + ' .content .file-name,',
			themeSelector + ' .content .empty-state,',
			themeSelector + ' .content .permission-banner,',
			themeSelector + ' .content .detail-section h3 {',
			'	color: #D3A239 !important;',
			'}',
			themeSelector + ' input,',
			themeSelector + ' select,',
			themeSelector + ' textarea {',
			'	background: #FFFDF8 !important;',
			'	color: #D3A239 !important;',
			'	border-color: rgba(211, 162, 57, 0.22) !important;',
			'}',
			themeSelector + ' input::placeholder,',
			themeSelector + ' textarea::placeholder {',
			'	color: rgba(211, 162, 57, 0.6) !important;',
			'}',
			themeSelector + ' .top-nav,',
			themeSelector + ' .management-body,',
			themeSelector + ' .management-toolbar,',
			themeSelector + ' .pagination,',
			themeSelector + ' .form-container,',
			themeSelector + ' .form-card,',
			themeSelector + ' .detail-section,',
			themeSelector + ' table th,',
			themeSelector + ' table td {',
			'	border-color: rgba(211, 162, 57, 0.18) !important;',
			'}'
		].join('\n');

		head.appendChild(style);
	}

	function updateHeaderUserInfo() {
		var session = ensureAuthenticated();

		if (!session) {
			return;
		}

		var displayName = getDisplayName(session.userInfo);
		var avatarText = getAvatarText(session.userInfo);
		var userInfoBlocks = document.querySelectorAll('.user-info');

		userInfoBlocks.forEach(function (block) {
			var avatar = block.querySelector('.user-avatar');
			var textBlocks = Array.prototype.filter.call(block.children, function (child) {
				return !child.classList.contains('user-avatar');
			});

			if (avatar) {
				avatar.textContent = avatarText;
			}

			if (textBlocks.length > 0) {
				textBlocks[0].textContent = displayName;
			}
		});
	}

	function getAuthHeaders(extraHeaders) {
		var headers = Object.assign({}, extraHeaders || {});
		var token = getToken();

		if (token) {
			headers.token = token;
			headers.Authorization = 'Bearer ' + token;
		}

		return headers;
	}

	function hasTokenChanged() {
		var currentToken = getToken();

		if (!activeToken) {
			activeToken = currentToken;
			return false;
		}

		return !currentToken || currentToken !== activeToken;
	}

	function checkTokenChange() {
		if (hasTokenChanged()) {
			forceLogout('检测到登录 token 已变化，正在返回登录页');
			return false;
		}

		return true;
	}

	function startSessionMonitor() {
		if (sessionMonitorStarted) {
			return;
		}

		sessionMonitorStarted = true;
		activeToken = getToken();

		window.addEventListener('storage', function (event) {
			if (event.key === 'token') {
				checkTokenChange();
			}
		});

		window.addEventListener('focus', checkTokenChange);
		document.addEventListener('visibilitychange', function () {
			if (!document.hidden) {
				checkTokenChange();
			}
		});

		window.setInterval(checkTokenChange, TOKEN_CHECK_INTERVAL);
	}

	function wrapFetch() {
		if (typeof window.fetch !== 'function' || window.fetch.__authWrapped) {
			return;
		}

		var nativeFetch = window.fetch.bind(window);

		function getRequestUrl(input) {
			if (typeof input === 'string') {
				return input;
			}

			if (input && typeof input.url === 'string') {
				return input.url;
			}

			return '';
		}

		var wrappedFetch = function (input, init) {
			return nativeFetch(input, init).then(function (response) {
				if (isProtectedApiRequest(getRequestUrl(input)) &&
					(response.status === 401 || response.status === 403)) {
					forceLogout('登录已过期或 token 已失效，正在返回登录页');
				}

				return response;
			});
		};

		wrappedFetch.__authWrapped = true;
		window.fetch = wrappedFetch;
	}

	window.Auth = {
		requireAuth: ensureAuthenticated,
		getToken: getToken,
		getUserInfo: getUserInfo,
		getAuthHeaders: getAuthHeaders,
		clearAuth: clearAuth,
		redirectToLogin: redirectToLogin,
		updateHeaderUserInfo: updateHeaderUserInfo
	};

	ensureAdminTheme();
	startActionButtonTheme();

	if (ensureAuthenticated()) {
		wrapFetch();
		startSessionMonitor();

		if (document.readyState === 'loading') {
			document.addEventListener('DOMContentLoaded', updateHeaderUserInfo);
		} else {
			updateHeaderUserInfo();
		}
	}
})(window, document);
