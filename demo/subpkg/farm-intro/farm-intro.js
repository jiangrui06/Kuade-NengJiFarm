Page({
  data: {
    farmInfo: null,
    loading: true
  },

  onLoad: function (options) {
    console.log('农场介绍页面加载');
    this.getFarmIntro();
  },

  // 处理图片路径，确保使用正确的基础 URL
  processImageUrl: function (imageUrl) {
    if (!imageUrl) return '';
    
    // 去除反引号和空格
    imageUrl = imageUrl.replace(/[`\s]/g, '');
    
    // 如果是完整的 URL，替换基础 URL
    if (imageUrl.startsWith('http://') || imageUrl.startsWith('https://')) {
      // 替换 127.0.0.1:5000 为 192.168.203.56
      return imageUrl.replace('http://127.0.0.1:5000', 'http://192.168.203.56');
    }
    
    // 如果是相对路径，添加基础 URL
    return 'http://192.168.203.56' + imageUrl;
  },

  // 获取农场介绍信息
  getFarmIntro: function () {
    wx.showLoading({ title: '加载中...' });
    
    const api = require('../../utils/api');
    api.request({
      url: '/api/farm/intro',
      method: 'GET'
    })
    .then(data => {
      // 处理农场介绍图片路径
      const processedFarmInfo = {
        ...data,
        mainImage: this.processImageUrl(data.mainImage),
        images: (data.images || []).map(image => this.processImageUrl(image))
      };
      
      this.setData({
        farmInfo: processedFarmInfo,
        loading: false
      });
      wx.hideLoading();
    })
    .catch(err => {
      console.error('获取农场介绍信息失败:', err);
      wx.hideLoading();
      wx.showToast({ title: '加载失败，请重试', icon: 'none' });
      this.setData({ loading: false });
    });
  }
});