const { api } = require('../../utils/api');

Page({
  data: {
    goods: {
      id: '',
      name: '',
      pointsPrice: 0,
      image: '',
      description: '',
      weightText: '',
      storageCondition: '',
      price: 0,
      stock: 0,
      unit: ''
    },
    swiperList: [],
    loading: true,
    userPoints: 0,
    exchanging: false
  },

  onLoad(options) {
    const id = parseInt(options.id || 0);
    if (!id) {
      wx.showToast({ title: '商品不存在', icon: 'none' });
      return;
    }
    this.loadUserPoints();
    this.loadGoodsDetail(id);
  },

  onShow() {
    this.loadUserPoints();
  },

  loadUserPoints() {
    api.points.summary({ showLoading: false })
      .then(data => {
        if (data) {
          this.setData({ userPoints: data.totalPoints || 0 });
        }
      })
      .catch(() => {});
  },

  loadGoodsDetail(id) {
    this.setData({ loading: true });

    api.points.goodsDetail(id)
      .then(data => {
        if (!data) {
          wx.showToast({ title: '商品不存在', icon: 'none' });
          this.setData({ loading: false });
          return;
        }

        const goods = {
          id: data.id,
          name: data.name || '',
          pointsPrice: data.pointsPrice || 0,
          points: data.pointsPrice || 0, // WXML 兼容
          image: this._processImage(data.image),
          detailImage: this._processImage(data.image), // WXML 兼容
          description: data.description || '',
          weightText: data.weightText || '',
          weight: data.weightText || '', // WXML 兼容
          storageCondition: data.storageCondition || '',
          storage: data.storageCondition || '', // WXML 兼容
          price: data.price || 0,
          stock: data.stock || 0,
          unit: data.unit || ''
        };

        const swiperList = goods.image ? [{ id: 1, image: goods.image }] : [];

        this.setData({
          goods,
          swiperList,
          loading: false
        });
      })
      .catch(err => {
        if (err && err.code === 404) {
          wx.showToast({ title: '商品不存在', icon: 'none' });
        } else {
          wx.showToast({ title: '加载失败', icon: 'none' });
        }
        this.setData({ loading: false });
      });
  },

  _processImage(image) {
    if (!image) return '';
    const baseUrl = 'https://api.nengjifarm.com';
    if (image.startsWith('http')) return image;
    if (image.startsWith('/api/')) return baseUrl + image;
    return baseUrl + '/api/file/image/' + image;
  },

  previewImage(e) {
    const current = e.currentTarget.dataset.url;
    const urls = this.data.swiperList.map(item => item.image);
    wx.previewImage({ current, urls });
  },

  exchangeNow() {
    const goods = this.data.goods;
    if (!goods || !goods.id) return;
    if (this.data.exchanging) return;

    if (this.data.userPoints < goods.pointsPrice) {
      wx.showToast({ title: '积分不足', icon: 'none' });
      return;
    }

    if (goods.stock <= 0) {
      wx.showToast({ title: '库存不足', icon: 'none' });
      return;
    }

    wx.showModal({
      title: '确认兑换',
      content: `确定要使用 ${goods.pointsPrice} 积分兑换「${goods.name}」吗？`,
      success: (res) => {
        if (res.confirm) {
          this.setData({ exchanging: true });
          wx.showLoading({ title: '兑换中...' });

          api.points.exchange({ commodityId: goods.id, quantity: 1 })
            .then(data => {
              wx.hideLoading();
              this.setData({
                exchanging: false,
                userPoints: data.pointsRemaining || 0
              });
              wx.showToast({ title: '兑换成功', icon: 'success' });

              setTimeout(() => {
                wx.navigateTo({ url: '/user-pages/my-exchange/my-exchange' });
              }, 1500);
            })
            .catch(err => {
              wx.hideLoading();
              this.setData({ exchanging: false });
              const msg = (err && err.message) || '兑换失败';
              wx.showToast({ title: msg, icon: 'none' });
            });
        }
      }
    });
  }
});
