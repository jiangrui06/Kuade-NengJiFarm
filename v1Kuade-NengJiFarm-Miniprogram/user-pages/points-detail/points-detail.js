const { api } = require('../../utils/api');

Page({
  data: {
    points: 0,
    todayEarned: 0,
    earnedPoints: 0,
    spentPoints: 0,
    records: [],
    loading: true,
    hasMore: true,
    currentPage: 1,
    pageSize: 20,
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

  onShow() {
    this.loadSummary();
  },

  // 加载积分总览
  loadSummary() {
    api.points.summary({ showLoading: false })
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

    this.setData({ loading: !append });

    const params = { page, pageSize: this.data.pageSize };
    if (this.data.typeFilter) {
      params.type = this.data.typeFilter;
    }

    api.points.records(params)
      .then(data => {
        const list = data.list || [];
        const total = data.total || list.length;
        const records = list.map(item => ({
          id: item.id,
          type: item.type,
          desc: item.desc,
          points: item.type === 'earn' ? '+' + item.points : '-' + item.points,
          balance: item.balance,
          time: item.time
        }));

        this.setData({
          records: append ? [...this.data.records, ...records] : records,
          currentPage: page,
          hasMore: this.data.currentPage * this.data.pageSize < total,
          loading: false
        });
      })
      .catch(() => {
        this.setData({ loading: false });
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

  // 上拉加载更多
  onReachBottom() {
    if (this.data.hasMore && !this.data.loading) {
      this.loadRecords(true);
    }
  }
});
