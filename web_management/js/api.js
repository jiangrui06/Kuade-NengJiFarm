(function (window) {
	'use strict';

	var DEFAULT_BASE_URL = 'http://192.168.101.75:80';
	var BASE_URL_STORAGE_KEY = 'farmApiBaseUrl';

	function getBaseURL() {
		return (window.localStorage.getItem(BASE_URL_STORAGE_KEY) || DEFAULT_BASE_URL).replace(/\/+$/, '');
	}

	function setBaseURL(value) {
		var nextValue = String(value || '').trim().replace(/\/+$/, '');
		if (!nextValue) {
			window.localStorage.removeItem(BASE_URL_STORAGE_KEY);
			return getBaseURL();
		}
		window.localStorage.setItem(BASE_URL_STORAGE_KEY, nextValue);
		return nextValue;
	}

	function resetBaseURL() {
		window.localStorage.removeItem(BASE_URL_STORAGE_KEY);
		return getBaseURL();
	}

	function isAbsoluteURL(value) {
		return /^(https?:)?\/\//i.test(String(value || ''));
	}

	function appendParams(url, params) {
		if (!params) {
			return url;
		}

		var parsedUrl = new URL(url, window.location.href);
		Object.keys(params).forEach(function (key) {
			var value = params[key];
			if (value === undefined || value === null) {
				return;
			}
			if (Array.isArray(value)) {
				value.forEach(function (item) {
					parsedUrl.searchParams.append(key, item);
				});
				return;
			}
			parsedUrl.searchParams.set(key, value);
		});
		return parsedUrl.toString();
	}

	function buildURL(path, params) {
		var text = String(path || '').trim();
		var url = isAbsoluteURL(text)
			? text
			: getBaseURL() + (text.charAt(0) === '/' ? text : '/' + text);
		return appendParams(url, params);
	}

	function assetURL(value) {
		var url = String(value || '').trim();
		if (!url || isAbsoluteURL(url) || url.indexOf('data:') === 0) {
			return url;
		}
		return getBaseURL() + (url.charAt(0) === '/' ? url : '/' + url);
	}

	function getAuthHeaders(extraHeaders) {
		if (window.Auth && typeof window.Auth.getAuthHeaders === 'function') {
			return window.Auth.getAuthHeaders(extraHeaders || {});
		}

		var headers = Object.assign({}, extraHeaders || {});
		var token = (window.localStorage.getItem('token') || '').trim();
		if (token) {
			headers.token = token;
			headers.Authorization = 'Bearer ' + token;
		}
		return headers;
	}

	function parseMaybeJSON(text) {
		if (!text) {
			return {};
		}
		try {
			return JSON.parse(text);
		} catch (error) {
			return { message: text };
		}
	}

	function createAPIError(message, response, data) {
		var error = new Error(message || '请求失败');
		error.status = response ? response.status : 0;
		error.data = data || null;
		return error;
	}

	function isSuccessResponse(data) {
		var codeText = String(data && data.code != null ? data.code : '').toLowerCase();
		var message = String((data && (data.message || data.msg)) || '').trim().toLowerCase();
		return !data
			|| data.code == null
			|| Number(data.code) === 0
			|| Number(data.code) === 200
			|| codeText === 'success'
			|| data.success === true
			|| message === 'success'
			|| message.indexOf('成功') !== -1;
	}

	function getErrorMessage(data, fallback) {
		var message = String((data && (data.message || data.msg)) || '').trim();
		if (!message || /^success$/i.test(message) || message.indexOf('成功') !== -1) {
			return fallback || '请求失败';
		}
		return message;
	}

	async function request(path, options) {
		var config = Object.assign({ method: 'GET', auth: true, responseType: 'json' }, options || {});
		var method = String(config.method || 'GET').toUpperCase();
		var headers = config.auth === false
			? Object.assign({}, config.headers || {})
			: getAuthHeaders(config.headers || {});
		var body = config.body;

		if (config.data instanceof FormData) {
			body = config.data;
			delete headers['Content-Type'];
			delete headers['content-type'];
		} else if (config.data !== undefined && config.data !== null) {
			body = JSON.stringify(config.data);
			if (!headers['Content-Type'] && !headers['content-type']) {
				headers['Content-Type'] = 'application/json';
			}
		}

		var response = await fetch(buildURL(path, config.params), {
			method: method,
			headers: headers,
			body: method === 'GET' || method === 'HEAD' ? undefined : body
		});

		if (response.status === 401 || response.status === 403) {
			if (window.Auth && typeof window.Auth.clearAuth === 'function') {
				window.Auth.clearAuth();
			}
			if (window.Auth && typeof window.Auth.redirectToLogin === 'function') {
				window.Auth.redirectToLogin();
			}
		}

		if (config.responseType === 'blob') {
			if (!response.ok) {
				throw createAPIError('请求失败：' + response.status, response, null);
			}
			return response.blob();
		}

		var text = await response.text();
		var data = config.responseType === 'text' ? text : parseMaybeJSON(text);
		if (!response.ok) {
			throw createAPIError(getErrorMessage(data, '请求失败：' + response.status), response, data);
		}
		return data;
	}

	function upload(file, fields) {
		var formData = new FormData();
		formData.append('file', file);
		Object.keys(fields || {}).forEach(function (key) {
			formData.append(key, fields[key]);
		});
		return request('/api/common/upload', {
			method: 'POST',
			data: formData
		});
	}

	function getUploadURL(result) {
		var data = result && result.data;
		return typeof data === 'string'
			? data
			: (data && (data.url || data.path || data.fileUrl || data.fileName || data.filename || data.name)) || '';
	}

	function list(path, params) {
		return request(path, { params: params || {} });
	}

	function detail(path, id) {
		return request(path, { params: { id: id } });
	}

	function post(path, data) {
		return request(path, { method: 'POST', data: data || {} });
	}

	function put(path, data) {
		return request(path, { method: 'PUT', data: data || {} });
	}

	window.FarmAPI = {
		config: {
			defaultBaseURL: DEFAULT_BASE_URL,
			getBaseURL: getBaseURL,
			setBaseURL: setBaseURL,
			resetBaseURL: resetBaseURL
		},
		url: buildURL,
		assetURL: assetURL,
		request: request,
		isSuccessResponse: isSuccessResponse,
		getErrorMessage: getErrorMessage,
		common: {
			upload: upload,
			getUploadURL: getUploadURL
		},
		auth: {
			login: function (data) {
				return request('/api/back-user/login', { method: 'POST', auth: false, data: data });
			}
		},
		product: {
			list: function (params) { return list('/api/product/list', params); },
			stats: function () { return request('/api/product/stats'); },
			detail: function (id) { return detail('/api/product/detail', id); },
			add: function (data) { return post('/api/product/add', data); },
			edit: function (data) { return post('/api/product/edit', data); },
			editPut: function (data) { return put('/api/product/edit', data); },
			delete: function (id) { return post('/api/product/delete', { id: id }); },
			deleteBatch: function (ids) { return post('/api/product/deleteBatch', { ids: ids }); },
			categories: function () { return request('/api/product/categories'); },
			units: function () { return request('/api/product/units'); }
		},
		dish: {
			list: function (params) { return list('/api/dish/list', params); },
			detail: function (id) { return detail('/api/dish/detail', id); },
			add: function (data) { return post('/api/dish/add', data); },
			edit: function (data) { return post('/api/dish/edit', data); },
			delete: function (id) { return post('/api/dish/delete', { id: id }); },
			deleteByQuery: function (id) { return request('/api/dish/delete', { method: 'POST', params: { id: id } }); }
		},
		activity: {
			list: function (params) { return list('/api/activity/list', params); },
			detail: function (id) { return detail('/api/activity/detail', id); },
			add: function (data) { return post('/api/activity/add', data); },
			edit: function (data) { return post('/api/activity/edit', data); },
			editPut: function (data) { return put('/api/activity/edit', data); },
			delete: function (id) { return post('/api/activity/delete', { id: id }); },
			deleteBatch: function (ids) { return post('/api/activity/deleteBatch', { ids: ids }); }
		},
		productOrder: {
			list: function (params) { return list('/api/product/order/list', params); },
			detail: function (orderNo) { return request('/api/product/order/detail', { params: { orderNo: orderNo } }); },
			updateStatus: function (data) { return post('/api/product/order/updateStatus', data); }
		},
		dishOrder: {
			list: function (params) { return list('/api/dish/order/list', params); },
			detail: function (orderNo) { return request('/api/dish/order/detail', { params: { orderNo: orderNo } }); }
		},
		activityOrder: {
			list: function (params) { return list('/api/activity-order/list', params); },
			detail: function (orderNo) { return request('/api/activity-order/detail', { params: { orderNo: orderNo } }); },
			refund: function (data) { return post('/api/activity-order/refund', data); }
		},
		table: {
			list: function (params) { return list('/api/table/list', params); },
			detail: function (id) { return request('/api/table/detail/' + encodeURIComponent(id)); },
			add: function (data) { return post('/api/table/add', data); },
			edit: function (data) { return post('/api/table/edit', data); },
			delete: function (id) { return post('/api/table/delete', { id: id }); },
			status: function (data) { return post('/api/table/status', data); }
		},
		user: {
			list: function (params) { return list('/api/back-user/list', params); },
			detail: function (id) { return request('/api/back-user/' + encodeURIComponent(id)); },
			add: function (data) { return post('/api/back-user/add', data); },
			edit: function (data) { return post('/api/back-user/edit', data); },
			delete: function (id) { return post('/api/back-user/delete', { id: id }); }
		}
	};
})(window);
