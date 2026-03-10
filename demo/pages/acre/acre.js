Page({
  data: {
    acreList: []
  },
  
  onLoad: function() {
    // 模拟从后台加载数据
    this.loadAcreData();
  },
  
  loadAcreData: function() {
    // 模拟后台API返回的数据
    const acreData = [
      {
        id: 1,
        name: "xxx田地",
        description: "认购一亩田是新型农场推出的共享农业体验项目，让您体验种植的乐趣，收获自己的劳动成果。",
        price: "99999$",
        image: "https://img.freepik.com/free-photo/yellow-field-with-lines_1127-3388.jpg"
      },
      {
        id: 2,
        name: "xxx田地",
        description: "认购一亩田是新型农场推出的共享农业体验项目，让您体验种植的乐趣，收获自己的劳动成果。",
        price: "99999$",
        image: "https://img.freepik.com/free-photo/agriculture-field-with-growing-crops_23-2148872538.jpg"
      },
      {
        id: 3,
        name: "xxx田地",
        description: "认购一亩田是新型农场推出的共享农业体验项目，让您体验种植的乐趣，收获自己的劳动成果。",
        price: "99999$",
        image: "https://img.freepik.com/free-photo/wheat-field_1127-3185.jpg"
      }
    ];
    
    this.setData({
      acreList: acreData
    });
  },
  
  navigateToDetail: function(e) {
    const id = e.currentTarget.dataset.id;
    wx.navigateTo({
      url: '/pages/acre-detail/acre-detail?id=' + id
    });
  }
});