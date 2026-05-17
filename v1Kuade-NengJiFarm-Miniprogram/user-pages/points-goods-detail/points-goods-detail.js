// 积分商品详情 - 使用假数据
Page({
  data: {
    goods: {
      id: '',
      name: '',
      points: 0,
      image: '',
      detailImage: '',
      description: '',
      weight: '',
      storage: '',
      videoUrl: '',
      stock: 0
    },
    swiperList: [],
    hasVideo: false,
    loading: true,
    userPoints: 1280
  },

  onLoad(options) {
    const id = parseInt(options.id || 0);
    this.loadUserPoints();
    this.loadGoodsDetail(id);
  },

  onShow() {
    this.loadUserPoints();
  },

  loadUserPoints() {
    const cache = wx.getStorageSync('user_points');
    this.setData({ userPoints: cache || 1280 });
  },

  loadGoodsDetail(id) {
    this.setData({ loading: true });
    const baseImage = 'https://api.nengjifarm.com/api/file/image/farm_0000000000012.jpg';
    setTimeout(() => {
      const mockGoods = {
        id: id || 1,
        name: '农场散养土鸡蛋 10枚装',
        points: 500,
        image: baseImage,
        detailImage: baseImage,
        description: '新鲜散养土鸡蛋，农场自产，健康美味。',
        weight: '10枚/盒',
        storage: '阴凉干燥处保存',
        videoUrl: '',
        stock: 100
      };
      const hasVideo = !!mockGoods.videoUrl;
      let swiperList = [];
      if (mockGoods.detailImage) {
        swiperList = [
          { id: 1, image: mockGoods.detailImage },
          { id: 2, image: mockGoods.image }
        ];
      }
      this.setData({
        goods: mockGoods,
        swiperList,
        loading: false,
        hasVideo
      });
    }, 300);
  },

  previewImage(e) {
    const current = e.currentTarget.dataset.url;
    const urls = this.data.swiperList.map(item => item.image);
    wx.previewImage({ current, urls });
  },

  previewDetailImages(e) {
    const current = e.currentTarget.dataset.url;
    const urls = [this.data.goods.detailImage, this.data.goods.image].filter(Boolean);
    wx.previewImage({ current, urls });
  },

  exchangeNow() {
    const goods = this.data.goods;
    if (!goods) return;

    if (this.data.userPoints < goods.points) {
      wx.showToast({ title: '积分不足', icon: 'none' });
      return;
    }

    if (goods.stock <= 0) {
      wx.showToast({ title: '库存不足', icon: 'none' });
      return;
    }

    wx.showModal({
      title: '确认兑换',
      content: `确定要使用 ${goods.points} 积分兑换「${goods.name}」吗？`,
      success: (res) => {
        if (res.confirm) {
          wx.showLoading({ title: '兑换中...' });
          setTimeout(() => {
            wx.hideLoading();
            const newPoints = this.data.userPoints - goods.points;
            this.setData({ userPoints: newPoints });
            wx.setStorageSync('user_points', newPoints);
            wx.showToast({ title: '兑换成功', icon: 'success' });
            setTimeout(() => {
              wx.navigateTo({ url: '/user-pages/my-exchange/my-exchange' });
            }, 1500);
          }, 1000);
        }
      }
    });
  }
});
