(function (window, document) {
	'use strict';

	var LOGIN_PAGE = 'login.html';
	var TOKEN_CHECK_INTERVAL = 3000;
	var ADMIN_THEME_NAME = 'warm-gold';
	var ADMIN_THEME_STYLE_ID = 'admin-theme-warm-gold';
	var SIDEBAR_ENHANCEMENT_STYLE_ID = 'admin-sidebar-orders';
	var SIDEBAR_ORDER_STORAGE_KEY = 'adminSidebarOrderMenuExpanded';
	var SIDEBAR_DISH_STORAGE_KEY = 'adminSidebarDishMenuExpanded';
	var SIDEBAR_USER_STORAGE_KEY = 'adminSidebarUserMenuExpanded';
	var activeToken = '';
	var sessionMonitorStarted = false;
	var actionButtonThemeStarted = false;
	var actionButtonThemeTimer = 0;
	var sidebarEnhancementTimer = 0;
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

	function scheduleSidebarEnhancement() {
		if (sidebarEnhancementTimer) {
			return;
		}

		sidebarEnhancementTimer = window.setTimeout(function () {
			sidebarEnhancementTimer = 0;
			enhanceSidebarMenus(document);
		}, 0);
	}

	function getCurrentPageName() {
		var pathname = window.location.pathname || '';
		var segments = pathname.split('/');
		return (segments[segments.length - 1] || '').toLowerCase();
	}

	function normalizeSidebarText(value) {
		return (value || '').replace(/\s+/g, '');
	}

	function matchesSidebarLabel(label, variants) {
		if (!label || !Array.isArray(variants)) {
			return false;
		}

		for (var i = 0; i < variants.length; i += 1) {
			if (label.indexOf(variants[i]) !== -1) {
				return true;
			}
		}

		return false;
	}

	function resolveSidebarPage(pageName) {
		switch ((pageName || '').toLowerCase()) {
			case 'user.html':
			case 'user-add.html':
			case 'user-edit.html':
				return 'user.html';
			case 'product-add.html':
			case 'product-edit.html':
				return 'product.html';
			case 'order.html':
			case 'order-dish.html':
			case 'order-dish-detail.html':
				return 'order-dish.html';
			case 'order-product.html':
			case 'order-product-detail.html':
				return 'order-product.html';
			case 'order-coupon.html':
				return 'order-coupon.html';
			case 'order-subscription.html':
				return 'order-subscription.html';
			case 'dish-add.html':
			case 'dish-edit.html':
				return 'dish.html';
			case 'table-form.html':
				return 'table.html';
			case 'coupon-add.html':
			case 'coupon-edit.html':
				return 'coupon.html';
			case 'subscription-add.html':
			case 'subscription-edit.html':
				return 'subscription.html';
			default:
				return (pageName || '').toLowerCase();
		}
	}

	function getSidebarPageByLabel(label) {
		if (!label) {
			return '';
		}

		if (matchesSidebarLabel(label, ['产品管理', '浜у搧绠＄悊'])) {
			return 'product.html';
		}
		if (matchesSidebarLabel(label, ['点餐管理', '菜品管理', '鑿滃搧绠＄悊'])) {
			return 'dish.html';
		}
		if (matchesSidebarLabel(label, ['券类管理', '鍒哥被绠＄悊'])) {
			return 'coupon.html';
		}
		if (matchesSidebarLabel(label, ['认购一亩田管理', '璁よ喘涓€浜╃敯绠＄悊'])) {
			return 'subscription.html';
		}
		if (matchesSidebarLabel(label, ['用户管理', '鐢ㄦ埛绠＄悊'])) {
			return 'user.html';
		}

		return '';
	}

	function isOrderSidebarPage(pageName) {
		return pageName === 'order-dish.html' ||
			pageName === 'order-product.html' ||
			pageName === 'order-coupon.html' ||
			pageName === 'order-subscription.html';
	}

	function isDishSidebarPage(pageName) {
		return pageName === 'dish.html' || pageName === 'table.html';
	}

	function isUserSidebarPage(pageName) {
		return pageName === 'user.html' ||
			pageName === 'user-add.html' ||
			pageName === 'user-edit.html';
	}

	function readOrderMenuExpanded(defaultValue) {
		try {
			var savedState = window.sessionStorage.getItem(SIDEBAR_ORDER_STORAGE_KEY);
			if (savedState === 'true') {
				return true;
			}
			if (savedState === 'false') {
				return false;
			}
		} catch (error) {
			console.warn('读取订单侧边栏状态失败:', error);
		}

		return defaultValue;
	}

	function writeOrderMenuExpanded(isExpanded) {
		try {
			window.sessionStorage.setItem(SIDEBAR_ORDER_STORAGE_KEY, isExpanded ? 'true' : 'false');
		} catch (error) {
			console.warn('保存订单侧边栏状态失败:', error);
		}
	}

	function readDishMenuExpanded(defaultValue) {
		try {
			var savedState = window.sessionStorage.getItem(SIDEBAR_DISH_STORAGE_KEY);
			if (savedState === 'true') {
				return true;
			}
			if (savedState === 'false') {
				return false;
			}
		} catch (error) {
			console.warn('读取菜品侧边栏状态失败:', error);
		}

		return defaultValue;
	}

	function writeDishMenuExpanded(isExpanded) {
		try {
			window.sessionStorage.setItem(SIDEBAR_DISH_STORAGE_KEY, isExpanded ? 'true' : 'false');
		} catch (error) {
			console.warn('保存菜品侧边栏状态失败:', error);
		}
	}

	function readUserMenuExpanded(defaultValue) {
		try {
			var savedState = window.sessionStorage.getItem(SIDEBAR_USER_STORAGE_KEY);
			if (savedState === 'true') {
				return true;
			}
			if (savedState === 'false') {
				return false;
			}
		} catch (error) {
			console.warn('璇诲彇鐢ㄦ埛渚ц竟鏍忕姸鎬佸け璐?', error);
		}

		return defaultValue;
	}

	function writeUserMenuExpanded(isExpanded) {
		try {
			window.sessionStorage.setItem(SIDEBAR_USER_STORAGE_KEY, isExpanded ? 'true' : 'false');
		} catch (error) {
			console.warn('淇濆瓨鐢ㄦ埛渚ц竟鏍忕姸鎬佸け璐?', error);
		}
	}

	function setOrderMenuExpanded(group, shouldOpen) {
		if (!group) {
			return;
		}

		var toggle = group.querySelector('.sidebar-group-toggle');
		group.classList.toggle('open', !!shouldOpen);

		if (toggle) {
			toggle.setAttribute('aria-expanded', shouldOpen ? 'true' : 'false');
		}
	}

	function createOrderSubmenuItem(label, href) {
		var item = document.createElement('li');
		var link = document.createElement('a');
		link.className = 'sidebar-submenu-link';
		link.href = href;
		link.textContent = label;
		link.setAttribute('data-sidebar-page', href.toLowerCase());
		item.appendChild(link);
		return item;
	}

	function createOrderSidebarGroup() {
		var group = document.createElement('li');
		var toggle = document.createElement('button');
		var label = document.createElement('span');
		var arrow = document.createElement('span');
		var submenu = document.createElement('ul');

		group.className = 'sidebar-group';
		group.setAttribute('data-sidebar-group', 'orders');

		toggle.type = 'button';
		toggle.className = 'sidebar-group-toggle';
		toggle.setAttribute('aria-expanded', 'false');

		label.className = 'sidebar-group-label';
		label.textContent = '订单管理';

		arrow.className = 'sidebar-group-arrow';
		arrow.textContent = '▾';

		submenu.className = 'sidebar-submenu';
		submenu.appendChild(createOrderSubmenuItem('菜品订单', 'order-dish.html'));
		submenu.appendChild(createOrderSubmenuItem('产品订单', 'order-product.html'));
		submenu.appendChild(createOrderSubmenuItem('券类订单', 'order-coupon.html'));
		submenu.appendChild(createOrderSubmenuItem('一亩田订单', 'order-subscription.html'));

		toggle.appendChild(label);
		toggle.appendChild(arrow);
		group.appendChild(toggle);
		group.appendChild(submenu);

		return group;
	}

	function createDishSidebarGroup() {
		var group = document.createElement('li');
		var toggle = document.createElement('button');
		var label = document.createElement('span');
		var arrow = document.createElement('span');
		var submenu = document.createElement('ul');

		group.className = 'sidebar-group';
		group.setAttribute('data-sidebar-group', 'dishes');

		toggle.type = 'button';
		toggle.className = 'sidebar-group-toggle';
		toggle.setAttribute('aria-expanded', 'false');

		label.className = 'sidebar-group-label';
		label.textContent = '点餐管理';

		arrow.className = 'sidebar-group-arrow';
		arrow.textContent = '▾';

		submenu.className = 'sidebar-submenu';
		submenu.appendChild(createOrderSubmenuItem('菜品管理', 'dish.html'));
		submenu.appendChild(createOrderSubmenuItem('餐桌管理', 'table.html'));

		toggle.appendChild(label);
		toggle.appendChild(arrow);
		group.appendChild(toggle);
		group.appendChild(submenu);

		return group;
	}

	function createUserSidebarGroup() {
		var group = document.createElement('li');
		var toggle = document.createElement('button');
		var label = document.createElement('span');
		var arrow = document.createElement('span');
		var submenu = document.createElement('ul');

		group.className = 'sidebar-group';
		group.setAttribute('data-sidebar-group', 'users');

		toggle.type = 'button';
		toggle.className = 'sidebar-group-toggle';
		toggle.setAttribute('aria-expanded', 'false');

		label.className = 'sidebar-group-label';
		label.textContent = '用户管理';

		arrow.className = 'sidebar-group-arrow';
		arrow.textContent = '▾';

		submenu.className = 'sidebar-submenu';
		submenu.appendChild(createOrderSubmenuItem('用户管理', 'user.html'));

		toggle.appendChild(label);
		toggle.appendChild(arrow);
		group.appendChild(toggle);
		group.appendChild(submenu);

		return group;
	}

	function bindSidebarNavigationItem(item, page) {
		if (!item || !page || item.__sidebarBound) {
			return;
		}

		item.__sidebarBound = true;
		item.addEventListener('click', function (event) {
			if (event.target && typeof event.target.closest === 'function' && event.target.closest('a, button')) {
				return;
			}

			window.location.href = page;
		});
	}

	function bindOrderSidebarGroup(group) {
		if (!group || group.__sidebarBound) {
			return;
		}

		var toggle = group.querySelector('.sidebar-group-toggle');
		if (!toggle) {
			return;
		}

		group.__sidebarBound = true;
		toggle.addEventListener('click', function () {
			var shouldOpen = !group.classList.contains('open');
			setOrderMenuExpanded(group, shouldOpen);
			writeOrderMenuExpanded(shouldOpen);
		});
	}

	function bindDishSidebarGroup(group) {
		if (!group || group.__sidebarBound) {
			return;
		}

		var toggle = group.querySelector('.sidebar-group-toggle');
		if (!toggle) {
			return;
		}

		group.__sidebarBound = true;
		toggle.addEventListener('click', function () {
			var shouldOpen = !group.classList.contains('open');
			setOrderMenuExpanded(group, shouldOpen);
			writeDishMenuExpanded(shouldOpen);
		});
	}

	function bindUserSidebarGroup(group) {
		if (!group || group.__sidebarBound) {
			return;
		}

		var toggle = group.querySelector('.sidebar-group-toggle');
		if (!toggle) {
			return;
		}

		group.__sidebarBound = true;
		toggle.addEventListener('click', function () {
			var shouldOpen = !group.classList.contains('open');
			setOrderMenuExpanded(group, shouldOpen);
			writeUserMenuExpanded(shouldOpen);
		});
	}

	function updateSidebarActiveState(menu) {
		if (!menu) {
			return;
		}

		var currentPage = resolveSidebarPage(getCurrentPageName());
		var menuItems = menu.children;
		var orderGroup = menu.querySelector('[data-sidebar-group="orders"]');
		var dishGroup = menu.querySelector('[data-sidebar-group="dishes"]');
		var userGroup = menu.querySelector('[data-sidebar-group="users"]');
		var orderLinks = orderGroup ? orderGroup.querySelectorAll('.sidebar-submenu-link') : [];
		var dishLinks = dishGroup ? dishGroup.querySelectorAll('.sidebar-submenu-link') : [];
		var userLinks = userGroup ? userGroup.querySelectorAll('.sidebar-submenu-link') : [];
		var hasActiveOrderChild = false;
		var hasActiveDishChild = false;
		var hasActiveUserChild = false;
		var i;

		for (i = 0; i < menuItems.length; i += 1) {
			if (menuItems[i].getAttribute('data-sidebar-group') !== 'orders' &&
				menuItems[i].getAttribute('data-sidebar-group') !== 'dishes' &&
				menuItems[i].getAttribute('data-sidebar-group') !== 'users') {
				menuItems[i].classList.remove('active');
			}
		}

		for (i = 0; i < menuItems.length; i += 1) {
			var page = resolveSidebarPage((menuItems[i].getAttribute('data-sidebar-page') || '').toLowerCase());
			if (page && page === currentPage) {
				menuItems[i].classList.add('active');
			}
		}

		if (orderGroup) {
			orderGroup.classList.remove('active');
		}
		if (dishGroup) {
			dishGroup.classList.remove('active');
		}
		if (userGroup) {
			userGroup.classList.remove('active');
		}

		for (i = 0; i < orderLinks.length; i += 1) {
			var link = orderLinks[i];
			var linkPage = resolveSidebarPage((link.getAttribute('data-sidebar-page') || '').toLowerCase());
			var isActive = !!linkPage && linkPage === currentPage;
			link.classList.toggle('active', isActive);
			if (isActive) {
				hasActiveOrderChild = true;
			}
		}

		for (i = 0; i < dishLinks.length; i += 1) {
			var dishLink = dishLinks[i];
			var dishLinkPage = resolveSidebarPage((dishLink.getAttribute('data-sidebar-page') || '').toLowerCase());
			var isDishActive = !!dishLinkPage && dishLinkPage === currentPage;
			dishLink.classList.toggle('active', isDishActive);
			if (isDishActive) {
				hasActiveDishChild = true;
			}
		}

		for (i = 0; i < userLinks.length; i += 1) {
			var userLink = userLinks[i];
			var userLinkPage = resolveSidebarPage((userLink.getAttribute('data-sidebar-page') || '').toLowerCase());
			var isUserActive = !!userLinkPage && userLinkPage === currentPage;
			userLink.classList.toggle('active', isUserActive);
			if (isUserActive) {
				hasActiveUserChild = true;
			}
		}

		if (orderGroup) {
			orderGroup.classList.toggle('active', hasActiveOrderChild);
			setOrderMenuExpanded(orderGroup, hasActiveOrderChild || readOrderMenuExpanded(isOrderSidebarPage(currentPage)));
		}
		if (dishGroup) {
			dishGroup.classList.toggle('active', hasActiveDishChild);
			setOrderMenuExpanded(dishGroup, hasActiveDishChild || readDishMenuExpanded(isDishSidebarPage(currentPage)));
		}
		if (userGroup) {
			userGroup.classList.toggle('active', hasActiveUserChild);
			setOrderMenuExpanded(userGroup, hasActiveUserChild || readUserMenuExpanded(isUserSidebarPage(currentPage)));
		}
	}

	function enhanceSidebarMenu(menu) {
		if (!menu) {
			return;
		}

		var menuItems = menu.children;
		var orderGroup = menu.querySelector('[data-sidebar-group="orders"]');
		var dishGroup = menu.querySelector('[data-sidebar-group="dishes"]');
		var userGroup = menu.querySelector('[data-sidebar-group="users"]');
		var i;

		if (!orderGroup) {
			for (i = 0; i < menuItems.length; i += 1) {
				var label = normalizeSidebarText(menuItems[i].textContent);
				if (matchesSidebarLabel(label, ['订单管理', '璁㈠崟绠＄悊'])) {
					orderGroup = createOrderSidebarGroup();
					menu.replaceChild(orderGroup, menuItems[i]);
					break;
				}
			}
		}

		menuItems = menu.children;
		if (!dishGroup) {
			for (i = 0; i < menuItems.length; i += 1) {
				var itemLabel = normalizeSidebarText(menuItems[i].textContent);
				if (matchesSidebarLabel(itemLabel, ['点餐管理', '菜品管理', '鑿滃搧绠＄悊'])) {
					dishGroup = createDishSidebarGroup();
					menu.replaceChild(dishGroup, menuItems[i]);
					break;
				}
			}
		}

		menuItems = menu.children;
		for (i = 0; i < menuItems.length; i += 1) {
			var item = menuItems[i];
			if (item.getAttribute('data-sidebar-group') === 'orders' ||
				item.getAttribute('data-sidebar-group') === 'dishes' ||
				item.getAttribute('data-sidebar-group') === 'users') {
				continue;
			}

			var page = getSidebarPageByLabel(normalizeSidebarText(item.textContent));
			if (page) {
				item.setAttribute('data-sidebar-page', page);
				bindSidebarNavigationItem(item, page);
			}
		}

		bindOrderSidebarGroup(orderGroup);
		bindDishSidebarGroup(dishGroup);
		bindUserSidebarGroup(userGroup);
		updateSidebarActiveState(menu);
	}

	function enhanceSidebarMenus(root) {
		var scope = root && typeof root.querySelectorAll === 'function' ? root : document;
		var menus = scope.querySelectorAll('.sidebar .sidebar-menu');

		Array.prototype.forEach.call(menus, function (menu) {
			enhanceSidebarMenu(menu);
		});
	}

	function ensureSidebarEnhancementStyles() {
		var head = document.head || document.getElementsByTagName('head')[0];

		if (!head || document.getElementById(SIDEBAR_ENHANCEMENT_STYLE_ID)) {
			return;
		}

		var style = document.createElement('style');
		style.id = SIDEBAR_ENHANCEMENT_STYLE_ID;
		style.type = 'text/css';
		style.textContent = [
			'.sidebar-menu .sidebar-group {',
			'	padding: 0 !important;',
			'	background: transparent;',
			'}',
			'.sidebar-menu .sidebar-group-toggle {',
			'	width: 100%;',
			'	display: flex;',
			'	align-items: center;',
			'	justify-content: space-between;',
			'	padding: 12px 20px;',
			'	border: none;',
			'	background: transparent;',
			'	color: inherit;',
			'	font: inherit;',
			'	text-align: left;',
			'	cursor: pointer;',
			'}',
			'.sidebar-menu .sidebar-group-label {',
			'	flex: 1;',
			'	min-width: 0;',
			'}',
			'.sidebar-menu .sidebar-group-toggle:hover {',
			'	background: #2d3748;',
			'}',
			'.sidebar-menu .sidebar-group-arrow {',
			'	display: inline-flex;',
			'	align-items: center;',
			'	justify-content: center;',
			'	width: 12px;',
			'	flex-shrink: 0;',
			'	line-height: 1;',
			'	font-size: 12px;',
			'	transition: transform 0.2s ease;',
			'}',
			'.sidebar-menu .sidebar-group.open > .sidebar-group-toggle .sidebar-group-arrow {',
			'	transform: rotate(180deg);',
			'}',
			'.sidebar-menu .sidebar-group.active > .sidebar-group-toggle {',
			'	background: #3182ce;',
			'	color: #ffffff;',
			'}',
			'.sidebar-menu .sidebar-submenu {',
			'	display: none;',
			'	list-style: none;',
			'	margin: 0;',
			'	padding: 4px 0 8px;',
			'	background: rgba(255, 255, 255, 0.05);',
			'}',
			'.sidebar-menu .sidebar-group.open > .sidebar-submenu {',
			'	display: block;',
			'}',
			'.sidebar-menu .sidebar-submenu li {',
			'	padding: 0;',
			'}',
			'.sidebar-menu .sidebar-submenu-link {',
			'	display: block;',
			'	padding: 10px 20px 10px 44px;',
			'	color: rgba(255, 255, 255, 0.82);',
			'	text-decoration: none;',
			'	font-size: 14px;',
			'	white-space: nowrap;',
			'	transition: background 0.2s ease, color 0.2s ease;',
			'}',
			'.sidebar-menu .sidebar-submenu-link:hover {',
			'	background: rgba(255, 255, 255, 0.08);',
			'	color: #ffffff;',
			'}',
			'.sidebar-menu .sidebar-submenu-link.active {',
			'	background: rgba(49, 130, 206, 0.18);',
			'	color: #ffffff;',
			'	font-weight: 600;',
			'}',
			'html[data-admin-theme="warm-gold"] .sidebar-menu .sidebar-group-toggle {',
			'	color: #D3A239 !important;',
			'}',
			'html[data-admin-theme="warm-gold"] .sidebar-menu .sidebar-group-toggle:hover,',
			'html[data-admin-theme="warm-gold"] .sidebar-menu .sidebar-group.open > .sidebar-group-toggle {',
			'	background: rgba(211, 162, 57, 0.12) !important;',
			'}',
			'html[data-admin-theme="warm-gold"] .sidebar-menu .sidebar-group.active > .sidebar-group-toggle {',
			'	background: linear-gradient(180deg, #E5B846 0%, #D3A239 100%) !important;',
			'	color: #FFFFFF !important;',
			'}',
			'html[data-admin-theme="warm-gold"] .sidebar-menu .sidebar-submenu {',
			'	background: rgba(211, 162, 57, 0.06) !important;',
			'}',
			'html[data-admin-theme="warm-gold"] .sidebar-menu .sidebar-submenu-link {',
			'	color: rgba(211, 162, 57, 0.86) !important;',
			'}',
			'html[data-admin-theme="warm-gold"] .sidebar-menu .sidebar-submenu-link:hover {',
			'	background: rgba(211, 162, 57, 0.1) !important;',
			'	color: #C59222 !important;',
			'}',
			'html[data-admin-theme="warm-gold"] .sidebar-menu .sidebar-submenu-link.active {',
			'	background: rgba(229, 184, 70, 0.2) !important;',
			'	color: #C59222 !important;',
			'}'
		].join('\n');

		head.appendChild(style);
	}

	function startActionButtonTheme() {
		if (actionButtonThemeStarted) {
			return;
		}

		actionButtonThemeStarted = true;
		scheduleActionButtonTheme();
		scheduleSidebarEnhancement();

		if (document.readyState === 'loading') {
			document.addEventListener('DOMContentLoaded', scheduleActionButtonTheme);
			document.addEventListener('DOMContentLoaded', scheduleSidebarEnhancement);
		}

		if (typeof window.MutationObserver === 'function' && document.documentElement) {
			var observer = new window.MutationObserver(function () {
				scheduleActionButtonTheme();
				scheduleSidebarEnhancement();
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
	ensureSidebarEnhancementStyles();
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
