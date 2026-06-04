/**
 * 页面分享配置
 * 所有页面统一分享标题和样式
 */
const SHARE_TITLE = '稻田时光农场 · 回归自然 享受生活';

module.exports = {
  onShareAppMessage() {
    return {
      title: SHARE_TITLE,
      path: this.route,
    };
  },

  onShareTimeline() {
    return {
      title: SHARE_TITLE,
    };
  },
};
