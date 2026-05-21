/**
 * 农场管理后台 - 通用工具函数
 * 依赖于 FarmAPI (api.js)
 */
(function (window) {
    'use strict';

    var AdminUtils = {

        /**
         * 格式化显示时间：兼容 "YYYY/M/D 日/HH:mm" 等多种格式，输出 "YYYY-MM-DD HH:mm"
         */
        formatDisplayTime: function (value) {
            if (!value) {
                return '';
            }
            var text = String(value).trim().replace(/\s*日\//g, ' ');
            var match = text.match(/(\d{4})[-\/年](\d{1,2})[-\/月](\d{1,2})(?:日)?\s+(\d{1,2})[:：](\d{1,2})/);
            if (!match) {
                return text;
            }
            var pad = function (item) { return String(item).padStart(2, '0'); };
            return match[1] + '-' + pad(match[2]) + '-' + pad(match[3]) + ' ' + pad(match[4]) + ':' + pad(match[5]);
        },

        /**
         * 解析图片 URL：相对路径拼接 baseUrl，绝对路径直接返回
         */
        resolveImageUrl: function (value, baseUrl) {
            var imageUrl = String(value || '').trim();
            if (!imageUrl) {
                return '';
            }
            if (/^(https?:)?\/\//i.test(imageUrl) || imageUrl.indexOf('data:') === 0) {
                return imageUrl;
            }
            var origin = baseUrl || (FarmAPI && FarmAPI.config.getBaseURL()) || '';
            return imageUrl.charAt(0) === '/'
                ? origin + imageUrl
                : origin + '/' + imageUrl;
        },

        /**
         * 从分页响应中提取 records、total、pages、pageNum
         * 兼容 { data: { records, total } }、直接数组等常见格式
         */
        extractPageData: function (data) {
            var pageData = data && typeof data === 'object' && !Array.isArray(data)
                ? (data.data || data)
                : data;
            var records = Array.isArray(pageData && pageData.records)
                ? pageData.records
                : (Array.isArray(pageData && pageData.list)
                    ? pageData.list
                    : (Array.isArray(pageData && pageData.rows)
                        ? pageData.rows
                        : (Array.isArray(pageData) ? pageData : [])));
            return {
                records: records,
                total: Number(pageData && (pageData.total != null ? pageData.total : pageData.count)) || records.length || 0,
                pages: Number(pageData && (pageData.pages != null ? pageData.pages : pageData.totalPages)) || 0,
                pageNum: Number(pageData && (pageData.pageNum != null ? pageData.pageNum : pageData.currentPage)) || 0
            };
        },

        /**
         * 标准化删除 ID：数字型转为数字，否则保留字符串
         */
        normalizeDeleteId: function (id) {
            var text = String(id != null ? id : '').trim();
            if (!text) {
                return '';
            }
            var numericId = Number(text);
            return Number.isFinite(numericId) ? numericId : text;
        }
    };

    window.AdminUtils = AdminUtils;
})(window);
