// 我的兑换 - 使用假数据
Page({
  data: {
    records: [],
    loading: true
  },

  onLoad() {
    this.loadMockData();
  },

  loadMockData() {
    this.setData({ loading: true });
    setTimeout(() => {
      const mockRecords = [
        { id: 1, name: '农场散养土鸡蛋 10枚装', image: 'https://api.nengjifarm.com/api/file/image/farm_0000000000012.jpg', points: 500, time: '2026-05-14 10:00', status: '已完成', orderNo: 'EX20260514001' },
        { id: 2, name: '有机大米 5kg', image: 'https://api.nengjifarm.com/api/file/image/farm_0000000000012.jpg', points: 800, time: '2026-04-10 11:30', status: '已完成', orderNo: 'EX20260410002' }
      ];
      this.setData({ records: mockRecords, loading: false });
    }, 300);
  }
});
