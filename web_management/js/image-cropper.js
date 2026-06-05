/**
 * Image Cropper — 图片裁剪模块
 * 用法: 在 Vue 组件中合并 data/methods，
 *       页面中加 CSS + 模态框 HTML，处理 upload 之前调用 openCropModal
 *
 * 与 Vue 3 Options API 配合:
 *   createApp({ ...ImageCropper.composable, ...你的组件 })
 *
 * 不修改任何后端代码，纯前端 Canvas 裁剪。
 */
(function () {
    'use strict';

    var ImageCropper = {

        // ============ 合并到 Vue data() ============
        data: function () {
            return {
                cropModalVisible: false,
                cropImageSrc: '',
                cropFile: null,
                cropResolve: null,

                // 原始图片尺寸
                cropNaturalW: 0,
                cropNaturalH: 0,
                // canvas 内展示尺寸
                cropDispW: 0,
                cropDispH: 0,

                // 裁剪框位置（相对于 crop-wrap，单位 px）
                boxL: 0,
                boxT: 0,
                boxW: 0,
                boxH: 0,

                // 拖拽状态
                dragType: null,   // null | 'move' | 'nw'|'n'|'ne'|'e'|'se'|'s'|'sw'|'w'
                dragStart: { x: 0, y: 0 },
                dragBox: null,     // { l, t, w, h }
                isDragging: false,
                boundOnMove: null,
                boundOnUp: null,

                // 固定裁剪比例（宽/高），null=自由裁剪
                cropRatio: 1,
            };
        },

        // ============ 合并到 Vue methods ============
        methods: {
            /**
             * 打开裁剪弹窗，返回 Promise<File|null>
             * - 非图片文件直接返回原文件（跳过裁剪）
             * - 用户取消返回 null
             * - 用户确定返回（可能裁剪过的）File
             */
            openCropModal: function (file) {
                if (!file || !file.type || file.type.indexOf('image/') !== 0) {
                    return Promise.resolve(file);
                }

                var self = this;
                return new Promise(function (resolve) {
                    self.cropFile = file;
                    self.cropResolve = resolve;

                    var reader = new FileReader();
                    reader.onload = function (e) {
                        self.cropImageSrc = e.target.result;
                        self.cropModalVisible = true;
                    };
                    reader.readAsDataURL(file);
                });
            },

            // 图片加载完成 → 初始化裁剪框
            onCropImageLoad: function () {
                var self = this;
                self.$nextTick(function () {
                    self.initCropBox();
                });
            },

            initCropBox: function () {
                var img = selfRef(this, 'cropImg');
                var wrap = selfRef(this, 'cropWrap');
                if (!img || !wrap) return;

                this.cropNaturalW = img.naturalWidth;
                this.cropNaturalH = img.naturalHeight;
                this.cropDispW = img.offsetWidth || img.clientWidth;
                this.cropDispH = img.offsetHeight || img.clientHeight;

                // 默认裁剪框覆盖全图，使用者自行拉拽调整
                this.boxL = 0;
                this.boxT = 0;
                this.boxW = this.cropDispW;
                this.boxH = this.cropDispH;
            },

            // ===== 鼠标拖拽 =====
            onCropMouseDown: function (event, type) {
                if (event.button !== 0) return;
                event.preventDefault();

                this.dragType = type;
                this.dragStart = { x: event.clientX, y: event.clientY };
                this.dragBox = { l: this.boxL, t: this.boxT, w: this.boxW, h: this.boxH };
                this.isDragging = true;

                var self = this;
                if (!this.boundOnMove) {
                    this.boundOnMove = function (e) { self.onCropMouseMove(e); };
                    this.boundOnUp = function (e) { self.onCropMouseUp(e); };
                }
                document.addEventListener('mousemove', this.boundOnMove);
                document.addEventListener('mouseup', this.boundOnUp);
            },

            onCropMouseMove: function (event) {
                if (!this.isDragging || !this.dragType) return;

                var dx = event.clientX - this.dragStart.x;
                var dy = event.clientY - this.dragStart.y;
                var box = this.dragBox;
                var minSize = 50;
                var L = 0, T = 0, R = this.cropDispW, B = this.cropDispH;
                var type = this.dragType;

                if (type === 'move') {
                    this.boxL = clamp(box.l + dx, L, R - box.w);
                    this.boxT = clamp(box.t + dy, T, B - box.h);
                    return;
                }

                var l = box.l, t = box.t, w = box.w, h = box.h;

                if (type.indexOf('w') >= 0) {
                    l = Math.min(box.l + dx, box.l + box.w - minSize);
                    w = box.l + box.w - l;
                }
                if (type.indexOf('e') >= 0) {
                    w = Math.max(minSize, box.w + dx);
                }
                if (type.indexOf('n') >= 0) {
                    t = Math.min(box.t + dy, box.t + box.h - minSize);
                    h = box.t + box.h - t;
                }
                if (type.indexOf('s') >= 0) {
                    h = Math.max(minSize, box.h + dy);
                }

                // 限制在图片内
                if (l < L) { w -= (L - l); l = L; }
                if (t < T) { h -= (T - t); t = T; }
                if (l + w > R) { w = R - l; }
                if (t + h > B) { h = B - t; }
                w = Math.max(minSize, w);
                h = Math.max(minSize, h);

                this.boxL = l;
                this.boxT = t;
                this.boxW = w;
                this.boxH = h;
            },

            onCropMouseUp: function () {
                this.isDragging = false;
                this.dragType = null;
                document.removeEventListener('mousemove', this.boundOnMove);
                document.removeEventListener('mouseup', this.boundOnUp);
            },

            // ===== 确定 / 取消 =====
            confirmCrop: function () {
                this.cropModalVisible = false;
                var file = this.cropFile;
                var resolve = this.cropResolve;
                this.cropResolve = null;
                var src = this.cropImageSrc;
                this.cropImageSrc = '';

                if (!resolve) return;

                // 如果裁剪框≈全图，直接上传原文件
                var tol = 3;
                var isFull =
                    Math.abs(this.boxW - this.cropDispW) <= tol &&
                    Math.abs(this.boxH - this.cropDispH) <= tol &&
                    Math.abs(this.boxL) <= tol &&
                    Math.abs(this.boxT) <= tol;

                if (isFull) {
                    resolve(file);
                    return;
                }

                // Canvas 裁剪
                var scaleX = this.cropNaturalW / this.cropDispW;
                var scaleY = this.cropNaturalH / this.cropDispH;
                var sx = Math.round(this.boxL * scaleX);
                var sy = Math.round(this.boxT * scaleY);
                var sw = Math.round(this.boxW * scaleX);
                var sh = Math.round(this.boxH * scaleY);

                var canvas = document.createElement('canvas');
                canvas.width = sw;
                canvas.height = sh;
                var ctx = canvas.getContext('2d');

                var img = new Image();
                img.onload = function () {
                    ctx.drawImage(img, sx, sy, sw, sh, 0, 0, sw, sh);
                    canvas.toBlob(function (blob) {
                        var name = (file.name || 'image.jpg').replace(/\.[^.]+$/, '.jpg');
                        var cropped = new File([blob], name, { type: 'image/jpeg' });
                        resolve(cropped);
                    }, 'image/jpeg', 0.92);
                };
                img.onerror = function () {
                    // canvas 失败时回退原文件
                    resolve(file);
                };
                img.src = src;
            },

            cancelCrop: function () {
                this.cropModalVisible = false;
                this.cropImageSrc = '';
                if (this.cropResolve) {
                    this.cropResolve(null);
                    this.cropResolve = null;
                }
            },
        },
    };

    // ============ 工具函数 ============
    function clamp(val, min, max) {
        return Math.max(min, Math.min(max, val));
    }

    function selfRef(ctx, refName) {
        var el = ctx.$refs ? ctx.$refs[refName] : null;
        return el && el.$el ? el.$el : el;
    }

    // ============ CSS（可注入 <style>） ============
    ImageCropper.CROP_CSS = [
        '/* 裁剪弹窗 */',
        '.crop-overlay{position:fixed;top:0;left:0;right:0;bottom:0;background:rgba(0,0,0,0.6);display:flex;justify-content:center;align-items:center;z-index:2000;}',
        '.crop-dialog{background:#fff;border-radius:12px;width:90vw;max-width:700px;max-height:90vh;display:flex;flex-direction:column;box-shadow:0 8px 40px rgba(0,0,0,0.3);}',
        '.crop-header{padding:16px 20px;font-size:16px;font-weight:600;border-bottom:1px solid #e2e8f0;text-align:center;}',
        '.crop-body{padding:16px;display:flex;justify-content:center;align-items:center;background:#f8fafc;min-height:300px;overflow:hidden;}',
        '.crop-wrap{position:relative;display:inline-block;max-width:100%;max-height:65vh;overflow:hidden;line-height:0;}',
        '.crop-wrap img{display:block;max-width:100%;max-height:65vh;width:auto;height:auto;}',
        '',
        '/* 裁剪框 + 阴影遮罩 */',
        '.crop-box{position:absolute;box-shadow:0 0 0 9999px rgba(0,0,0,0.55);border:1px solid rgba(255,255,255,0.8);cursor:move;z-index:1;}',
        '',
        '/* 拖拽手柄 */',
        '.crop-handle{position:absolute;width:12px;height:12px;background:#fff;border:2px solid #3182ce;border-radius:2px;z-index:2;}',
        '.crop-handle.nw{top:-6px;left:-6px;cursor:nw-resize;}',
        '.crop-handle.n{top:-6px;left:50%;margin-left:-6px;cursor:n-resize;}',
        '.crop-handle.ne{top:-6px;right:-6px;cursor:ne-resize;}',
        '.crop-handle.e{top:50%;margin-top:-6px;right:-6px;cursor:e-resize;}',
        '.crop-handle.se{bottom:-6px;right:-6px;cursor:se-resize;}',
        '.crop-handle.s{bottom:-6px;left:50%;margin-left:-6px;cursor:s-resize;}',
        '.crop-handle.sw{bottom:-6px;left:-6px;cursor:sw-resize;}',
        '.crop-handle.w{top:50%;margin-top:-6px;left:-6px;cursor:w-resize;}',
        '',
        '/* 弹窗底部 */',
        '.crop-footer{padding:12px 20px;border-top:1px solid #e2e8f0;display:flex;justify-content:flex-end;gap:10px;}',
        '.crop-footer .btn{padding:8px 24px;border:none;border-radius:6px;font-size:14px;cursor:pointer;}',
        '.crop-footer .btn-success{background:#38a169;color:#fff;}',
        '.crop-footer .btn-success:hover{background:#2f855a;}',
        '.crop-footer .btn-default{background:#e2e8f0;color:#4a5568;}',
        '.crop-footer .btn-default:hover{background:#cbd5e0;}',
        '',
        '/* 裁剪信息 */',
        '.crop-info{padding:8px 20px 0;font-size:12px;color:#718096;text-align:center;}',
    ].join('\n');

    // 暴露到全局
    window.ImageCropper = ImageCropper;
})();
