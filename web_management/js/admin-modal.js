/**
 * 农场管理后台 - 删除弹窗辅助函数
 */
(function (window) {
    'use strict';

    var AdminModal = {

        /**
         * 生成删除确认消息
         */
        getDeleteMessage: function (targetIds, singleLabel) {
            if (targetIds && targetIds.length > 1) {
                return '确定要删除已选择的 ' + targetIds.length + ' 条数据吗？';
            }
            return '确定要删除该' + (singleLabel || '数据') + '吗？';
        },

        /**
         * 生成删除警告文字
         */
        getDeleteWarningText: function (targetIds, label) {
            if (!targetIds || !targetIds.length) {
                return '';
            }
            return '已选择 ' + targetIds.length + ' 条数据';
        }
    };

    window.AdminModal = AdminModal;
})(window);
