const { api, get } = require('../../utils/api');
const share = require('../../utils/share');

Page({
  data: {
    points: 0,
    earnedPoints: 0,
    spentPoints: 0,
    todayEarned: 0,
    goodsList: [],
    displayList: [],
    displayCount: 4,
    loading: true,
    loadingMore: false,
    // 分页
    currentPage: 1,
    pageSize: 10,
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
    get('/api/points/summary', {}, { showLoading: false, skipAuthCheck: true })
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

    this.setData({
      loading: !append,
      loadingMore: append
    });

    Promise.all([
      get('/api/points/goods', { page, pageSize: this.data.pageSize }, { showLoading: false, skipAuthCheck: true }),
      new Promise(resolve => setTimeout(resolve, 1000))
    ])
      .then(([data]) => {
        const list = data.list || [];
        const total = data.total || list.length;
        const hasMore = list.length >= this.data.pageSize;
        const newItems = list.map(item => ({
          id: item.id,
          name: item.name,
          image: this._processImage(item.image),
          points: item.pointsPrice,
          stock: item.stock,
          desc: item.description || '',
          spec: item.spec || ''
        }));

        const goodsList = append ? [...this.data.goodsList, ...newItems] : newItems;
        const displayCount = append ? Math.min(this.data.displayCount + newItems.length, goodsList.length) : Math.min(this.data.displayCount, goodsList.length);

        this.setData({
          goodsList,
          displayList: goodsList.slice(0, displayCount),
          total,
          currentPage: page,
          hasMore,
          loading: false,
          loadingMore: false
        });
      })
      .catch(() => {
        this.setData({ loading: false, loadingMore: false });
      });
  },

  // 上拉加载更多
  onReachBottom() {
    if (this.data.loading || this.data.loadingMore) return;

    const { displayCount, goodsList, hasMore } = this.data;

    // 先尝试本地增量展示
    if (displayCount < goodsList.length) {
      const newCount = Math.min(displayCount + 4, goodsList.length);
      this.setData({
        displayCount: newCount,
        displayList: goodsList.slice(0, newCount)
      });
      return;
    }

    // 本地数据已全部展示，请求更多
    if (hasMore) {
      this.loadGoodsList(true);
    }
  },

  // 下拉刷新
  onPullDownRefresh() {
    this.setData({ loading: true });
    Promise.all([
      new Promise(resolve => { this.loadPointsSummary(); resolve(); }),
      new Promise(resolve => {
        this.setData({ currentPage: 1, hasMore: true, goodsList: [], displayList: [], displayCount: 4 }, () => {
          this.loadGoodsList();
          resolve();
        });
      })
    ]).then(() => {
      setTimeout(() => {
        wx.stopPullDownRefresh();
      }, 1000);
    });
  },

  _processImage(image) {
    if (!image) return '';
    const baseUrl = getApp().globalData.baseUrl || 'https://api.nengjifarm.com';
    if (image.startsWith('http')) return image;
    if (image.startsWith('/api/')) return baseUrl + image;
    return baseUrl + '/api/file/image/' + image;
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
          this.setData({ loading: true });
          api.points.exchange({ commodityId: goods.id, quantity: 1 }, { showLoading: false })
            .then(data => {
              this.setData({ loading: false, points: data.pointsRemaining || 0 });
              wx.showToast({ title: '兑换成功', icon: 'success' });
              // 刷新积分，保留当前位置
              this.loadPointsSummary();
            })
            .catch(err => {
              this.setData({ loading: false });
              const msg = (err && err.message) || '兑换失败';
              wx.showToast({ title: msg, icon: 'none' });
            });
        }
      }
    });
  },

  onShareAppMessage: share.onShareAppMessage,
  onShareTimeline: share.onShareTimeline,

});
