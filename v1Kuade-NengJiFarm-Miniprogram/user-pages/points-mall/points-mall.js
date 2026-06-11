const { api, get } = require('../../utils/api');
const share = require('../../utils/share');

Page({
  data: {
    points: 0,
    earnedPoints: 0,
    spentPoints: 0,
    todayEarned: 0,
    goodsList: [],
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

  onUnload() {
    this._destroyLoadMoreObserver();
  },

  onShow() {
    this.loadPointsSummary();
  },

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

  loadGoodsList(append = false) {
    const page = append ? this.data.currentPage + 1 : 1;

    this.setData({
      loading: !append,
      loadingMore: append
    });

    get('/api/points/goods', { page, pageSize: this.data.pageSize }, { showLoading: false, skipAuthCheck: true })
      .then((data) => {
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

        this.setData({
          goodsList,
          total,
          currentPage: page,
          hasMore,
          loading: false,
          loadingMore: false
        }, () => {
          // 加载完成后滚动到倒数第 3 条商品附近，给用户留出继续下滑的空间
          if (append && hasMore && newItems.length > 0) {
            wx.createSelectorQuery()
              .selectAll('.goods-card')
              .boundingClientRect((rects) => {
                if (rects && rects.length > 0) {
                  const lastRect = rects[rects.length - 1];
                  const singleHeight = lastRect.height || 120;
                  const scrollBack = singleHeight * 3 + 100;
                  wx.createSelectorQuery()
                    .selectViewport()
                    .scrollOffset((offset) => {
                      const currentScrollTop = offset ? offset.scrollTop : 0;
                      const targetScrollTop = currentScrollTop - scrollBack;
                      wx.pageScrollTo({
                        scrollTop: targetScrollTop > 0 ? targetScrollTop : 0,
                        duration: 300
                      });
                    })
                    .exec();
                }
              })
              .exec();
          }
          // 重新设置 IntersectionObserver
          if (goodsList.length > 0) {
            setTimeout(() => this._setupLoadMoreObserver(), 100);
          }
        });
      })
      .catch(() => {
        this.setData({ loading: false, loadingMore: false });
      });
  },

  // IntersectionObserver 哨兵：监听触底，比 onReachBottom 更可靠
  _setupLoadMoreObserver() {
    this._destroyLoadMoreObserver();
    if (!this.data.hasMore || this.data.goodsList.length === 0) return;

    this._loadMoreObserver = wx.createIntersectionObserver(this, { thresholds: [0] });
    this._loadMoreObserver.relativeToViewport({ bottom: 100 }).observe('.load-more-sentinel', (res) => {
      if (res.intersectionRatio > 0 && this.data.hasMore && !this.data.loading && !this.data.loadingMore) {
        if (this._lastObserverReset && Date.now() - this._lastObserverReset < 600) return;
        this.loadGoodsList(true);
      }
    });
    this._lastObserverReset = Date.now();
  },

  _destroyLoadMoreObserver() {
    if (this._loadMoreObserver) {
      this._loadMoreObserver.disconnect();
      this._loadMoreObserver = null;
    }
  },

  onReachBottom() {
    if (this.data.hasMore && !this.data.loading && !this.data.loadingMore) {
      this.loadGoodsList(true);
    }
  },

  onPullDownRefresh() {
    this.setData({ loading: true });
    Promise.all([
      this.loadPointsSummary(),
      this.setData({ currentPage: 1, hasMore: true, goodsList: [] }, () => {
        this.loadGoodsList();
      })
    ]);
    setTimeout(() => {
      wx.stopPullDownRefresh();
    }, 1000);
  },

  _processImage(image) {
    if (!image) return '';
    const baseUrl = getApp().globalData.baseUrl || 'https://api.nengjifarm.com';
    if (image.startsWith('http')) return image;
    if (image.startsWith('/api/')) return baseUrl + image;
    return baseUrl + '/api/file/image/' + image;
  },

  goToPointsDetail() {
    wx.navigateTo({ url: '/user-pages/points-detail/points-detail' });
  },

  goToMyExchange() {
    wx.navigateTo({ url: '/user-pages/my-exchange/my-exchange' });
  },

  goToGoodsDetail(e) {
    const id = e.currentTarget.dataset.id;
    wx.navigateTo({ url: '/user-pages/points-goods-detail/points-goods-detail?id=' + id });
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
              this.loadPointsSummary();
            })
            .catch(err => {
              this.setData({ loading: false });
              wx.showToast({ title: (err && err.message) || '兑换失败', icon: 'none' });
            });
        }
      }
    });
  },

  onShareAppMessage: share.onShareAppMessage,
  onShareTimeline: share.onShareTimeline,

});
