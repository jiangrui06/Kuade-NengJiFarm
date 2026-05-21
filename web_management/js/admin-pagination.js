/**
 * 农场管理后台 - 分页辅助函数
 *
 * 这些函数是纯逻辑，不依赖 Vue 组件状态。
 * 使用方式：在 Vue methods 中调用，传入对应组件的状态。
 */
(function (window) {
    'use strict';

    var AdminPagination = {

        /**
         * 计算总页数
         */
        calcTotalPages: function (total, pageSize) {
            return Math.max(Math.ceil(total / pageSize), 1);
        },

        /**
         * 计算当前页的数据切片
         */
        slicePage: function (allData, pageNum, pageSize) {
            var start = (pageNum - 1) * pageSize;
            var end = start + pageSize;
            return (allData || []).slice(start, end);
        },

        /**
         * 安全获取有效页码（在 1 和 totalPages 之间）
         */
        clampPage: function (page, totalPages) {
            return Math.min(Math.max(Number(page) || 1, 1), Math.max(totalPages, 1));
        }
    };

    window.AdminPagination = AdminPagination;
})(window);
