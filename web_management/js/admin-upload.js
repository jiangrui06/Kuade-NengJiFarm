/**
 * 农场管理后台 - 文件上传辅助函数
 *
 * 仅包含纯逻辑/工具函数，不依赖 Vue 组件状态。
 * 各页面在 Vue methods 中调用这些函数处理上传逻辑。
 */
(function (window) {
    'use strict';

    var AdminUpload = {

        MAX_SIZE: 50 * 1024 * 1024,

        ALLOWED_EXTENSIONS: ['jpg', 'jpeg', 'png', 'gif', 'webp', 'mp4', 'mov', 'avi'],

        /**
         * 检查文件扩展名是否允许
         */
        isValidExtension: function (fileName) {
            var ext = (fileName || '').split('.').pop().toLowerCase();
            return this.ALLOWED_EXTENSIONS.indexOf(ext) !== -1;
        },

        /**
         * 检查文件大小是否允许
         */
        isValidSize: function (size) {
            return Number(size) <= this.MAX_SIZE;
        },

        /**
         * 校验上传文件
         * @returns {string|null} 错误消息，null 表示通过
         */
        validateFile: function (file) {
            if (!file) {
                return '请选择文件';
            }
            if (!this.isValidExtension(file.name)) {
                return '不支持的文件格式：' + file.name;
            }
            if (!this.isValidSize(file.size)) {
                return '文件大小不能超过 50MB';
            }
            return null;
        },

        /**
         * 上传文件到服务器
         * @returns {Promise}
         */
        uploadFile: function (file, extraFields) {
            var formData = new FormData();
            formData.append('file', file);
            if (extraFields) {
                Object.keys(extraFields).forEach(function (key) {
                    formData.append(key, extraFields[key]);
                });
            }
            if (window.FarmAPI && FarmAPI.common) {
                return FarmAPI.common.upload(file, extraFields);
            }
            return window.FarmAPI.request('/api/common/upload', {
                method: 'POST',
                data: formData
            });
        },

        /**
         * 从上传结果中提取 URL
         */
        getUploadUrl: function (result) {
            if (window.FarmAPI && FarmAPI.common) {
                return FarmAPI.common.getUploadURL(result);
            }
            var data = result && result.data;
            return typeof data === 'string'
                ? data
                : (data && (data.url || data.path || data.fileUrl || data.fileName || data.filename || data.name)) || '';
        },

        /**
         * 从文件路径中提取文件名
         */
        getFileNameFromPath: function (path) {
            var text = String(path || '').trim();
            if (!text) return '';
            var parts = text.replace(/\\/g, '/').split('/');
            return parts[parts.length - 1] || '';
        },

        /**
         * 生成视频缩略图 URL（通过封面 api）
         */
        generateVideoThumb: function (videoUrl) {
            if (!videoUrl) return '';
            var baseUrl = (FarmAPI && FarmAPI.config && FarmAPI.config.getBaseURL()) || '';
            return baseUrl + '/api/common/video-thumb?url=' + encodeURIComponent(videoUrl);
        },

        /**
         * 图片加载失败时的处理
         */
        handleMediaLoadError: function (event) {
            if (event && event.target) {
                event.target.style.display = 'none';
            }
        }
    };

    window.AdminUpload = AdminUpload;
})(window);
