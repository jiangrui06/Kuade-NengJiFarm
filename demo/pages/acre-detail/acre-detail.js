Page({
  data: {
    acreDetail: {}
  },
  
  onLoad: function(options) {
    const id = options.id;
    this.loadAcreDetail(id);
  },
  
  loadAcreDetail: function(id) {
    // 模拟后台API返回的数据
    const acreDetails = {
      1: {
        id: 1,
        name: "xxx田地",
        price: "99999元",
        image: "https://img.freepik.com/free-photo/yellow-field-with-lines_1127-3388.jpg",
        description: "本地块为标准型农业用地，实测面积整一亩（666.7平方米）。地形状方正规整，四至清晰，无边角零碎、无低洼坑洼，整体地势平坦开阔，南北通透，采光通风条件绝佳，属于优质良田范畴。"
      },
      2: {
        id: 2,
        name: "xxx田地",
        price: "99999元",
        image: "https://img.freepik.com/free-photo/agriculture-field-with-growing-crops_23-2148872538.jpg",
        description: "本地块为标准型农业用地，实测面积整一亩（666.7平方米）。地形状方正规整，四至清晰，无边角零碎、无低洼坑洼，整体地势平坦开阔，南北通透，采光通风条件绝佳，属于优质良田范畴。"
      },
      3: {
        id: 3,
        name: "xxx田地",
        price: "99999元",
        image: "https://img.freepik.com/free-photo/wheat-field_1127-3185.jpg",
        description: "本地块为标准型农业用地，实测面积整一亩（666.7平方米）。地形状方正规整，四至清晰，无边角零碎、无低洼坑洼，整体地势平坦开阔，南北通透，采光通风条件绝佳，属于优质良田范畴。"
      }
    };
    
    this.setData({
      acreDetail: acreDetails[id] || acreDetails[1]
    });
  },
  
  confirmPurchase: function() {
    wx.showModal({
      title: '确认购买',
      content: '您确定要购买此地块吗？',
      success: function(res) {
        if (res.confirm) {
          wx.showToast({
            title: '购买成功！',
            icon: 'success',
            duration: 2000
          });
          // 这里可以添加跳转到支付页面的逻辑
        }
      }
    });
  }
});