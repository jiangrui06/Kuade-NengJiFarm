const { api } = require('../../utils/api');
const share = require('../../utils/share');

Page({
  data: {
    records: [],
    loading: true,
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

  loadExchangeRecords(append = false) {
    const page = append ? this.data.currentPage + 1 : 1;

    this.setData({ loading: !append });

    Promise.all([
      api.points.exchangeRecords({ page, pageSize: this.data.pageSize }, { showLoading: false }),
      new Promise(resolve => setTimeout(resolve, 1000))
    ])
      .then(([data]) => {
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
          loading: false
        });
      })
      .catch(() => {
        this.setData({ loading: false });
      });
  },

  _processImage(image) {
    if (!image) return '';
    const baseUrl = getApp().globalData.baseUrl || 'https://api.nengjifarm.com';
    if (image.startsWith('http')) return image;
    if (image.startsWith('/api/')) return baseUrl + image;
    return baseUrl + '/api/file/image/' + image;
  },

  // 下拉刷新
  onPullDownRefresh() {
    this.setData({ loading: true, currentPage: 1, hasMore: true, records: [] }, () => {
      this.loadExchangeRecords();
      setTimeout(() => {
        wx.stopPullDownRefresh();
      }, 1000);
    });
  },

  onReachBottom() {
    if (this.data.hasMore && !this.data.loading) {
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
