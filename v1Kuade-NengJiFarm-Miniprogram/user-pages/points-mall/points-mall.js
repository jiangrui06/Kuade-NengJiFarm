// 积分商城 - 使用假数据
Page({
  data: {
    points: 1280,
    activeTab: 'all',
    tabs: [
      { key: 'all', name: '全部' },
      { key: 'goods', name: '商品' }
    ],
    goodsList: [],
    loading: true
  },

  onLoad() {
    this.loadMockData();
  },

  onShow() {
    // 刷新积分
    this.loadUserPoints();
  },

  // 加载假数据
  loadMockData() {
    this.setData({ loading: true });

    // 模拟网络请求
    setTimeout(() => {
      const mockGoods = [
        {
          id: 1,
          name: '农场散养土鸡蛋 10枚装',
          image: 'https://api.nengjifarm.com/api/file/image/farm_0000000000012.jpg',
          points: 500,
          stock: 100,
          desc: '新鲜散养土鸡蛋'
        },
        {
          id: 2,
          name: '有机大米 5kg',
          image: 'https://api.nengjifarm.com/api/file/image/farm_0000000000012.jpg',
          points: 800,
          stock: 50,
          desc: '生态有机种植'
        },
        {
          id: 3,
          name: '农场自制辣椒酱 200g',
          image: 'https://api.nengjifarm.com/api/file/image/farm_0000000000012.jpg',
          points: 200,
          stock: 200,
          desc: '纯手工制作'
        },
        {
          id: 4,
          name: '田园蔬菜礼盒 4kg',
          image: 'https://api.nengjifarm.com/api/file/image/farm_0000000000012.jpg',
          points: 350,
          stock: 30,
          desc: '当季新鲜蔬菜'
        },
        {
          id: 5,
          name: '纯天然蜂蜜 250g',
          image: 'https://api.nengjifarm.com/api/file/image/farm_0000000000012.jpg',
          points: 600,
          stock: 80,
          desc: '农场自产蜂蜜'
        }
      ];

      this.setData({
        goodsList: mockGoods,
        loading: false
      });
    }, 300);
  },

  // 加载用户积分
  loadUserPoints() {
    const cache = wx.getStorageSync('user_points');
    if (cache) {
      this.setData({ points: cache });
    } else {
      // mock: 默认 1280 积分
      this.setData({ points: 1280 });
    }
  },

  // 切换Tab
  switchTab(e) {
    const tab = e.currentTarget.dataset.tab;
    if (tab === this.data.activeTab) return;
    this.setData({ activeTab: tab });
  },

  // 跳转积分明细
  goToPointsDetail() {
    wx.navigateTo({
      url: '/user-pages/points-detail/points-detail'
    });
  },

  // 跳转我的兑换
  goToMyExchange() {
    wx.navigateTo({
      url: '/user-pages/my-exchange/my-exchange'
    });
  },

  // 跳转商品详情
  goToGoodsDetail(e) {
    const id = e.currentTarget.dataset.id;
    wx.navigateTo({
      url: '/user-pages/points-goods-detail/points-goods-detail?id=' + id
    });
  },

  // 立即兑换
  exchangeNow(e) {
    const id = e.currentTarget.dataset.id;
    const goods = this.data.goodsList.find(g => g.id === id);
    if (!goods) return;

    if (this.data.points < goods.points) {
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
            const newPoints = this.data.points - goods.points;
            this.setData({ points: newPoints });
            wx.setStorageSync('user_points', newPoints);
            wx.showToast({ title: '兑换成功', icon: 'success' });
          }, 1000);
        }
      }
    });
  }
});
