const { api } = require('../../utils/api');
const share = require('../../utils/share');

Page({
  data: {
    records: [],
    loading: true,
    loadingMore: false,
    hasMore: true,
    currentPage: 1,
    pageSize: 20,
    total: 0
  },

  onLoad() {
    const { checkLogin } = require('../../utils/api');
    if (!checkLogin()) return;
    this.loadExchangeRecords();
  },

  onShow() {
    const { checkLogin } = require('../../utils/api');
    if (!checkLogin()) return;
    this.loadExchangeRecords();
  },

  onUnload() {
    this._destroyLoadMoreObserver();
  },

  loadExchangeRecords(append = false) {
    const page = append ? this.data.currentPage + 1 : 1;

    if (append) {
      this.setData({ loadingMore: true });
    } else {
      this.setData({ loading: true });
    }

    api.points.exchangeRecords({ page, pageSize: this.data.pageSize }, { showLoading: false })
      .then((data) => {
        const list = data.list || [];
        const total = data.total || list.length;

        const records = list.map(item => ({
          id: item.id,
          name: item.name,
          image: this._processImage(item.image),
          points: item.points,
          time: item.time,
          status: item.status,
          orderNo: item.orderNo
        }));

        this.setData({
          records: append ? [...this.data.records, ...records] : records,
          total,
          currentPage: page,
          hasMore: page * this.data.pageSize < total,
          loading: false,
          loadingMore: false
        }, () => {
          // 加载完成后滚动到倒数第 3 条记录附近，给用户留出继续下滑的空间
          if (append && this.data.hasMore && records.length > 0) {
            wx.createSelectorQuery()
              .selectAll('.exchange-card')
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
          if (this.data.records.length > 0) {
            setTimeout(() => this._setupLoadMoreObserver(), 100);
          }
        });
      })
      .catch(() => {
        this.setData({ loading: false, loadingMore: false });
      });
  },

  _processImage(image) {
    if (!image) return '';
    const baseUrl = getApp().globalData.baseUrl || 'https://api.nengjifarm.com';
    if (image.startsWith('http')) return image;
    if (image.startsWith('/api/')) return baseUrl + image;
    return baseUrl + '/api/file/image/' + image;
  },

  onPullDownRefresh() {
    this.setData({ loading: true, currentPage: 1, hasMore: true, records: [] }, () => {
      this.loadExchangeRecords();
      setTimeout(() => {
        wx.stopPullDownRefresh();
      }, 1000);
    });
  },

  // IntersectionObserver 哨兵：监听触底，比 onReachBottom 更可靠
  _setupLoadMoreObserver() {
    this._destroyLoadMoreObserver();
    if (!this.data.hasMore || this.data.records.length === 0) return;

    this._loadMoreObserver = wx.createIntersectionObserver(this, { thresholds: [0] });
    this._loadMoreObserver.relativeToViewport({ bottom: 100 }).observe('.load-more-sentinel', (res) => {
      if (res.intersectionRatio > 0 && this.data.hasMore && !this.data.loading && !this.data.loadingMore) {
        if (this._lastObserverReset && Date.now() - this._lastObserverReset < 600) return;
        this.loadExchangeRecords(true);
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
      this.loadExchangeRecords(true);
    }
  },

  goToDetail(e) {
    const orderNo = e.currentTarget.dataset.orderNo;
    if (!orderNo) return;
    wx.navigateTo({
      url: `/user-pages/exchange-result/exchange-result?orderNo=${orderNo}`
    });
  },

  onShareAppMessage: share.onShareAppMessage,
  onShareTimeline: share.onShareTimeline,

});
