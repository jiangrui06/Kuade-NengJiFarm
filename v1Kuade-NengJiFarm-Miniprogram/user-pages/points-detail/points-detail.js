const { api } = require('../../utils/api');
const share = require('../../utils/share');

Page({
  data: {
    points: 0,
    todayEarned: 0,
    earnedPoints: 0,
    spentPoints: 0,
    records: [],
    loading: true,
    loadingMore: false,
    hasMore: true,
    currentPage: 1,
    pageSize: 10,
    typeFilter: '', // ''=all, 'earn', 'spend'
    typeTabs: [
      { key: '', name: '全部' },
      { key: 'earn', name: '收入' },
      { key: 'spend', name: '支出' }
    ]
  },

  onLoad() {
    this.loadSummary();
    this.loadRecords();
  },

  onUnload() {
    this._destroyLoadMoreObserver();
  },

  onShow() {
    this.loadSummary();
  },

  // 加载积分总览
  loadSummary() {
    api.points.summary({}, { showLoading: false })
      .then(data => {
        if (data) {
          this.setData({
            points: data.totalPoints || 0,
            todayEarned: data.todayEarned || 0,
            earnedPoints: data.earnedPoints || 0,
            spentPoints: data.spentPoints || 0
          });
        }
      })
      .catch(() => {});
  },

  // 加载积分流水
  loadRecords(append = false) {
    const page = append ? this.data.currentPage + 1 : 1;

    if (append) {
      this.setData({ loadingMore: true });
    } else {
      this.setData({ loading: true });
    }

    const params = { page, pageSize: this.data.pageSize };
    if (this.data.typeFilter) {
      params.type = this.data.typeFilter;
    }

    api.points.records(params, { showLoading: false })
      .then((data) => {
        const list = data.list || [];
        const total = data.total !== undefined && data.total !== null ? data.total : 0;
        const records = list.map(item => ({
          id: item.id,
          type: item.type,
          desc: item.desc,
          points: item.type === 'earn' ? '+' + item.points : '-' + item.points,
          time: item.time
        }));

        // 判断是否还有更多数据：
        // 1. 有 total 且 > 0 时用 total 判断
        // 2. 否则，如果当前返回条数达到 pageSize，假设可能还有更多数据
        let hasMore = false;
        if (total > 0) {
          hasMore = page * this.data.pageSize < total;
        }
        if (!hasMore && list.length > 0) {
          hasMore = list.length >= this.data.pageSize;
        }

        this.setData({
          records: append ? [...this.data.records, ...records] : records,
          currentPage: page,
          hasMore,
          loading: false,
          loadingMore: false
        }, () => {
          // 加载完成后滚动到倒数第 3 条记录附近，给用户留出继续下滑的空间
          if (append && hasMore && records.length > 0) {
            wx.createSelectorQuery()
              .selectAll('.record-item')
              .boundingClientRect((rects) => {
                if (rects && rects.length > 0) {
                  // 取最后一个元素计算单条高度，回滚3条记录的高度 + 100px留白
                  const lastRect = rects[rects.length - 1];
                  const singleHeight = lastRect.height || 80;
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
          if (records.length > 0) {
            setTimeout(() => this._setupLoadMoreObserver(), 100);
          }
        });
      })
      .catch(() => {
        this.setData({ loading: false, loadingMore: false });
      });
  },

  // 切换类型筛选
  switchTypeTab(e) {
    const type = e.currentTarget.dataset.type;
    if (type === this.data.typeFilter) return;

    this.setData({
      typeFilter: type,
      currentPage: 1,
      hasMore: true,
      records: []
    }, () => {
      this.loadRecords();
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
        this.loadRecords(true);
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

  // 上拉加载更多（兜底）
  onReachBottom() {
    if (this.data.hasMore && !this.data.loading && !this.data.loadingMore) {
      this.loadRecords(true);
    }
  },

  // 下拉刷新
  onPullDownRefresh() {
    this.loadSummary();
    this.setData({ currentPage: 1, hasMore: true, records: [], loading: true }, () => {
      this.loadRecords();
    });
    setTimeout(() => {
      wx.stopPullDownRefresh();
    }, 1000);
  },

  onShareAppMessage: share.onShareAppMessage,
  onShareTimeline: share.onShareTimeline,

});
