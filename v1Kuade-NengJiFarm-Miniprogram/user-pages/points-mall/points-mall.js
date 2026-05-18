const { api } = require('../../utils/api');

Page({
  data: {
    points: 0,
    earnedPoints: 0,
    spentPoints: 0,
    todayEarned: 0,
    activeTab: 'all',
    tabs: [
      { key: 'all', name: '全部' },
      { key: 'goods', name: '商品' }
    ],
    goodsList: [],
    loading: true,
    // 分页
    currentPage: 1,
    pageSize: 20,
    hasMore: true,
    total: 0
  },

  onLoad() {
    this.loadPointsSummary();
    this.loadGoodsList();
  },

  onShow() {
    // 每次显示刷新积分
    this.loadPointsSummary();
  },

  // 加载积分总览
  loadPointsSummary() {
    api.points.summary({ showLoading: false })
      .then(data => {
        if (data) {
          this.setData({
            points: data.totalPoints || 0,
            earnedPoints: data.earnedPoints || 0,
            spentPoints: data.spentPoints || 0,
            todayEarned: data.todayEarned || 0
          });
        }
      })
      .catch(() => {});
  },

  // 加载积分商品列表
  loadGoodsList(append = false) {
    const page = append ? this.data.currentPage + 1 : 1;

    this.setData({ loading: !append });

    api.points.goods({ page, pageSize: this.data.pageSize })
      .then(data => {
        const list = data.list || [];
        const total = data.total || list.length;
        const hasMore = list.length >= this.data.pageSize;
        const goodsList = list.map(item => ({
          id: item.id,
          name: item.name,
          image: this._processImage(item.image),
          points: item.pointsPrice,
          stock: item.stock,
          desc: item.description || ''
        }));

        this.setData({
          goodsList: append ? [...this.data.goodsList, ...goodsList] : goodsList,
          total,
          currentPage: page,
          hasMore,
          loading: false
        });
      })
      .catch(() => {
        this.setData({ loading: false });
      });
  },

  // 上拉加载更多
  onReachBottom() {
    if (this.data.hasMore && !this.data.loading) {
      this.loadGoodsList(true);
    }
  },

  _processImage(image) {
    if (!image) return '';
    const baseUrl = 'http://192.168.101.75';
    if (image.startsWith('http')) return image;
    if (image.startsWith('/api/')) return baseUrl + image;
    return baseUrl + '/api/file/image/' + image;
  },

  switchTab(e) {
    const tab = e.currentTarget.dataset.tab;
    if (tab === this.data.activeTab) return;
    this.setData({ activeTab: tab });
  },

  goToPointsDetail() {
    wx.navigateTo({
      url: '/user-pages/points-detail/points-detail'
    });
  },

  goToMyExchange() {
    wx.navigateTo({
      url: '/user-pages/my-exchange/my-exchange'
    });
  },

  goToGoodsDetail(e) {
    const id = e.currentTarget.dataset.id;
    wx.navigateTo({
      url: '/user-pages/points-goods-detail/points-goods-detail?id=' + id
    });
  },

  exchangeNow(e) {
    const id = e.currentTarget.dataset.id;
    const goods = this.data.goodsList.find(g => g.id == id);
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
          api.points.exchange({ commodityId: goods.id, quantity: 1 })
            .then(data => {
              wx.hideLoading();
              this.setData({ points: data.pointsRemaining || 0 });
              wx.showToast({ title: '兑换成功', icon: 'success' });
              // 刷新积分和库存
              this.loadPointsSummary();
              this.loadGoodsList();
            })
            .catch(err => {
              wx.hideLoading();
              const msg = (err && err.message) || '兑换失败';
              wx.showToast({ title: msg, icon: 'none' });
            });
        }
      }
    });
  }
});
