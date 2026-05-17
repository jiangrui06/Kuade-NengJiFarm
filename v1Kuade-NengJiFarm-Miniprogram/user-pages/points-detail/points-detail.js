// 积分明细 - 使用假数据
Page({
  data: {
    points: 1280,
    records: [],
    loading: true,
    hasMore: true
  },

  onLoad() {
    this.loadMockData();
    this.loadPoints();
  },

  loadPoints() {
    const cache = wx.getStorageSync('user_points');
    this.setData({ points: cache || 1280 });
  },

  loadMockData() {
    this.setData({ loading: true });
    setTimeout(() => {
      const mockRecords = [
        { id: 1, type: 'earn', desc: '消费积分奖励（订单 No.20240501001）', points: '+120', time: '2026-05-15 14:30:00', balance: 1280 },
        { id: 2, type: 'spend', desc: '兑换「农场散养土鸡蛋」', points: '-500', time: '2026-05-14 10:00:00', balance: 1160 },
        { id: 3, type: 'earn', desc: '消费积分奖励（订单 No.20240428003）', points: '+80', time: '2026-04-28 16:45:00', balance: 1660 },
        { id: 4, type: 'earn', desc: '消费积分奖励（订单 No.20240415002）', points: '+200', time: '2026-04-15 09:20:00', balance: 1580 },
        { id: 5, type: 'spend', desc: '兑换「有机大米 5kg」', points: '-800', time: '2026-04-10 11:30:00', balance: 1380 },
        { id: 6, type: 'earn', desc: '消费积分奖励（订单 No.20240320001）', points: '+60', time: '2026-03-20 15:00:00', balance: 2180 }
      ];
      this.setData({ records: mockRecords, loading: false });
    }, 300);
  }
});
