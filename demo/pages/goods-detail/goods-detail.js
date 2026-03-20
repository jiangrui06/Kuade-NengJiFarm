const api = require('../../utils/api');

Page({
  data: {
    goods: {
      id: '',
      name: '',
      price: 0,
      image: '',
      detailImage: '',
      description: '',
      weight: '',
      storage: ''
    },
    loading: true
  },

  onLoad(options) {
    const goodsId = options.id;

    if (!goodsId) {
      this.setData({ loading: false });
      wx.showToast({
        title: '缺少商品ID',
        icon: 'none'
      });
      return;
    }

    this.getGoodsDetail(goodsId);
  },

  getGoodsDetail(goodsId) {
    wx.showLoading({ title: '加载中...' });

    api.request({
      url: `/api/DemoApi/goods/${goodsId}`,
      method: 'GET'
    })
      .then((data) => {
        this.setData({
          goods: {
            id: data.id || goodsId,
            name: data.name || '',
            price: Number(data.price || 0),
            image: data.image || '',
            detailImage: data.detailImage || data.image || '',
            description: data.description || '',
            weight: data.weight || '',
            storage: data.storage || ''
          },
          loading: false
        });
      })
      .catch((err) => {
        console.error('获取商品详情失败:', err);
        this.setData({ loading: false });
      })
      .finally(() => {
        wx.hideLoading();
      });
  },

  addToCart() {
    wx.showToast({
      title: '已加入购物车',
      icon: 'success'
    });
  },

  buyNow() {
    wx.showToast({
      title: '购买功能开发中',
      icon: 'none'
    });
  }
});