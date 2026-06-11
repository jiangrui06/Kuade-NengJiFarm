Page({
  data: {},

  goBack() {
    wx.navigateBack();
  },

  onShareAppMessage() {
    return { title: '隐私政策' };
  }
});
